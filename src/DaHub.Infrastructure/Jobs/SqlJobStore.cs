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
                SELECT TOP (1) j.* FROM dahub_ita.BackgroundJobs j WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE j.Status = N'Queued' AND j.ScheduledAt <= SYSUTCDATETIME()
                    AND j.AttemptCount < j.MaxAttempts AND j.MaxAttempts BETWEEN 1 AND 4
                    AND NOT EXISTS (SELECT 1 FROM dahub_ita.ProvisioningActions a
                        WHERE a.StepId = j.ProvisioningStepId AND j.JobType = N'ProvisioningRetry'
                        AND a.PrerequisiteStepId IS NOT NULL AND NOT EXISTS (
                            SELECT 1 FROM dahub_ita.ProvisioningActions p
                            WHERE p.StepId = a.PrerequisiteStepId AND p.RequestId = a.RequestId
                                AND p.Revision = a.Revision AND p.State = N'Completed'))
                ORDER BY j.ScheduledAt, j.Id
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
        await new SqlProvisioningActions(database).ProjectAsync(job, status, code, now, result.ProvisioningReceipt, cancellationToken);
        if (status == "Failed" && failureAlerts is not null)
        {
            failureAlerts.AddToQueue(database, job, now);
        }
        await database.SaveChangesAsync(cancellationToken);
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
            await new SqlProvisioningActions(database).ProjectAsync(job, "Failed", "ReconciliationRequired",
                job.CompletedAt!.Value, null, cancellationToken);
        }
        if (jobs.Count > 0)
            await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return jobs.Count;
    }
}
