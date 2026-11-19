using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Infrastructure.Notifications;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DaHub.Infrastructure.Jobs;

public sealed class SqlJobStore(DaHubDbContext database, WorkerFailureAlerts? failureAlerts = null) : IJobStore
{
    public async Task<BackgroundJob?> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // UPDATE OUTPUT and the attempt insertion commit together. READPAST skips competing
        // row locks; UPDLOCK holds the selected row through commit. No SELECT-then-UPDATE race.
        var jobs = await database.BackgroundJobs.FromSqlRaw("""
            ;WITH candidate AS (
                SELECT TOP (1) * FROM dahub_ita.BackgroundJobs WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE Status = N'Queued' AND ScheduledAt <= SYSUTCDATETIME()
                    AND AttemptCount < MaxAttempts AND MaxAttempts BETWEEN 1 AND 4
                ORDER BY ScheduledAt, Id
            )
            UPDATE candidate SET Status = N'InProgress', StartedAt = SYSUTCDATETIME(),
                CompletedAt = NULL, AttemptCount = AttemptCount + 1
            OUTPUT inserted.*;
            """).AsNoTracking().ToListAsync(cancellationToken);
        var job = jobs.SingleOrDefault();
        if (job is not null)
        {
            database.BackgroundJobAttempts.Add(new BackgroundJobAttempt
            {
                JobId = job.Id,
                Attempt = job.AttemptCount,
                StartedAt = job.StartedAt!.Value,
                Outcome = "InProgress"
            });
            await database.SaveChangesAsync(cancellationToken);
            database.ChangeTracker.Clear();
        }
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    public async Task FinishAsync(BackgroundJob job, JobResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = (await database.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").ToListAsync(cancellationToken)).Single();
        var retry = result.Outcome == JobOutcome.Retry && job.AttemptCount < job.MaxAttempts;
        var status = retry ? "Queued" : result.Outcome switch
        {
            JobOutcome.Completed => "Completed",
            JobOutcome.Cancelled => "Cancelled",
            _ => "Failed"
        };
        var code = status == "Completed" ? null : JobRetryPolicy.SafeCode(result.Code) ?? "PermanentFailure";
        var deadline = retry ? now + JobRetryPolicy.Delay(job.AttemptCount) : job.ScheduledAt;
        if (retry && result.RetryAtUtc is { Kind: DateTimeKind.Utc } retryAt && retryAt > deadline)
            deadline = retryAt;
        // The attempt number fences late completions after recovery or manual requeue.
        var changed = await database.BackgroundJobs
            .Where(x => x.Id == job.Id && x.Status == "InProgress" && x.AttemptCount == job.AttemptCount && x.StartedAt == job.StartedAt)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, status)
                .SetProperty(x => x.CompletedAt, retry ? (DateTime?)null : now)
                .SetProperty(x => x.ScheduledAt, deadline).SetProperty(x => x.LastError, code), cancellationToken);
        if (changed != 1) throw new InvalidOperationException("Job claim is no longer current.");
        await database.BackgroundJobAttempts.Where(x => x.JobId == job.Id && x.Attempt == job.AttemptCount && x.Outcome == "InProgress")
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Outcome, retry ? "Retry" : status)
                .SetProperty(x => x.CompletedAt, now).SetProperty(x => x.RetryAt, retry ? deadline : (DateTime?)null)
                .SetProperty(x => x.ErrorCode, code), cancellationToken);
        if (status == "Failed" && failureAlerts is not null)
        {
            failureAlerts.AddToQueue(database, job, now);
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> RecoverAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // An abandoned external operation has an uncertain result. Retain it for IT reconciliation,
        // rather than automatically reissuing a directory/account/ticket write.
        var jobs = await database.BackgroundJobs.FromSqlRaw("""
            UPDATE dahub_ita.BackgroundJobs WITH (ROWLOCK)
            SET Status = N'Failed', CompletedAt = SYSUTCDATETIME(), LastError = N'ReconciliationRequired'
            OUTPUT inserted.*
            WHERE Status = N'InProgress' AND StartedAt < DATEADD(minute, -5, SYSUTCDATETIME());
            """).AsNoTracking().ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            await database.BackgroundJobAttempts.Where(x => x.JobId == job.Id && x.Attempt == job.AttemptCount && x.Outcome == "InProgress")
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Outcome, "Failed")
                    .SetProperty(x => x.CompletedAt, job.CompletedAt).SetProperty(x => x.ErrorCode, "ReconciliationRequired"), cancellationToken);
            failureAlerts?.AddToQueue(database, job, job.CompletedAt!.Value);
        }
        if (jobs.Count > 0 && failureAlerts is not null)
            await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return jobs.Count;
    }
}
