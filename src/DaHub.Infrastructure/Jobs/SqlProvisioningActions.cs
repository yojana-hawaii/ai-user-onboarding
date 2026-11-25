using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DaHub.Infrastructure.Jobs;

public sealed class SqlProvisioningActions(DaHubDbContext database) : IProvisioningActionStore
{
    public Task<ProvisioningAction?> FindAsync(BackgroundJob job, CancellationToken cancellationToken) =>
        database.ProvisioningActions.AsNoTracking().SingleOrDefaultAsync(x =>
            job.JobType == "ProvisioningRetry" && x.StepId == job.ProvisioningStepId && x.RequestId == job.RequestId, cancellationToken);

    public async Task<bool> TryOwnAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Persist intent BEFORE contacting even a mock provider. A competing/replacement job
        // cannot steal an in-flight effect; only the matching job may resume a known-safe retry.
        var changed = await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE a WITH (UPDLOCK, ROWLOCK)
            SET State = N'InProgress', OwnerJobId = {job.Id}, OwnerAttempt = {job.AttemptCount}
            FROM dahub_ita.ProvisioningActions a
            JOIN dahub_ita.ProvisioningSteps s ON s.Id = a.StepId AND s.RequestId = a.RequestId
            JOIN dahub_ita.Requests r ON r.Id = a.RequestId AND r.Revision = a.Revision AND r.EmploymentPeriodId = a.EmploymentPeriodId
            JOIN dahub_ita.EmploymentPeriods e ON e.Id = a.EmploymentPeriodId AND e.EmployeeId = r.EmployeeId
            JOIN dahub_ita.BackgroundJobs j ON j.Id = {job.Id}
            WHERE a.StepId = {job.ProvisioningStepId} AND a.RequestId = {job.RequestId}
              AND j.JobType = N'ProvisioningRetry' AND j.RequestId = a.RequestId AND j.ProvisioningStepId = a.StepId
              AND j.Status = N'InProgress' AND j.AttemptCount = {job.AttemptCount} AND j.StartedAt = {job.StartedAt}
              AND (a.State = N'Pending' OR (a.State = N'Retry' AND a.OwnerJobId = j.Id) OR a.State = N'ReconciliationRequired')
              AND (a.PrerequisiteStepId IS NULL OR EXISTS (
                  SELECT 1 FROM dahub_ita.ProvisioningActions p WHERE p.StepId = a.PrerequisiteStepId
                  AND p.RequestId = a.RequestId AND p.Revision = a.Revision AND p.State = N'Completed'));
            """, cancellationToken);
        if (changed == 1)
            await database.ProvisioningSteps.Where(x => x.Id == job.ProvisioningStepId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "InProgress")
                    .SetProperty(x => x.StartedAt, job.StartedAt).SetProperty(x => x.CompletedAt, (DateTime?)null), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed == 1;
    }

    // The Sprint 3 caller supplies a step from its authorized immutable snapshot. This bounded
    // seam creates ONE mock action and its job together; it is not a workflow/approval engine.
    public async Task<BackgroundJob> EnqueueAsync(ProvisioningStep step, ProvisioningAction action,
        IJobExecutionAuthority authority, CancellationToken cancellationToken)
    {
        if (action.ActionKey.Length is 0 or > 100 || action.ActionKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')
            || action.Operation is not ("MockDirectory" or "MockHelpdesk") || step.TargetSystem != "Manual"
            || step.RequestId != action.RequestId || action.State != "Pending" || action.OwnerJobId is not null
            || action.OwnerAttempt is not null || action.ExternalId is not null || action.EvidenceReference is not null
            || !await authority.IsCurrentAsync(new(action.ActionKey, action.RequestId, action.Revision,
                action.EmploymentPeriodId, action.ApprovedEndAtUtc), cancellationToken))
            throw new InvalidOperationException("Mock provisioning action is not authorized.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await database.Requests.AnyAsync(x => x.Id == action.RequestId && x.Revision == action.Revision
                && x.EmploymentPeriodId == action.EmploymentPeriodId && database.EmploymentPeriods.Any(e =>
                    e.Id == action.EmploymentPeriodId && e.EmployeeId == x.EmployeeId), cancellationToken)
            || action.PrerequisiteStepId is { } prerequisite && !await database.ProvisioningActions.AnyAsync(x =>
                x.StepId == prerequisite && x.RequestId == action.RequestId && x.Revision == action.Revision, cancellationToken))
            throw new InvalidOperationException("Mock provisioning snapshot is stale.");
        database.ProvisioningSteps.Add(step);
        await database.SaveChangesAsync(cancellationToken);
        action.StepId = step.Id;
        database.ProvisioningActions.Add(action);
        var job = new BackgroundJob
        {
            JobType = "ProvisioningRetry",
            RequestId = action.RequestId,
            ProvisioningStepId = step.Id,
            ScheduledAt = DateTime.UtcNow
        };
        database.BackgroundJobs.Add(job);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.ChangeTracker.Clear();
        return job;
    }

    // Called ONLY inside SqlJobStore's fenced outcome/recovery transaction. Never SaveChanges
    // or commit here: audit, step, job, attempt, deadline and alert must roll back together.
    public async Task ProjectAsync(BackgroundJob job, string status, string? code, DateTime now,
        ProvisioningReceipt? receipt, CancellationToken cancellationToken)
    {
        if (job.JobType != "ProvisioningRetry") return;
        var action = await database.ProvisioningActions.SingleOrDefaultAsync(x => x.StepId == job.ProvisioningStepId
            && x.OwnerJobId == job.Id && x.OwnerAttempt == job.AttemptCount && x.State == "InProgress", cancellationToken);
        if (action is null) return; // A rejected/duplicate job never owns the step.
        if (status == "Completed" && !ProvisioningStepHandler.Safe(receipt))
            throw new InvalidOperationException("Provisioning completion requires safe evidence.");
        action.State = status == "Queued" ? "Retry" : code == "ReconciliationRequired" ? "ReconciliationRequired"
            : status == "Completed" ? "Completed" : "Failed";
        if (receipt is not null && status == "Completed")
        {
            action.ExternalId = receipt.ExternalId;
            action.EvidenceReference = receipt.EvidenceReference;
        }
        var step = await database.ProvisioningSteps.SingleAsync(x => x.Id == action.StepId, cancellationToken);
        step.Status = status == "Queued" ? "Pending" : status == "Completed" ? "Completed" : "Failed";
        step.RetryCount = Math.Max(step.RetryCount, job.AttemptCount - 1);
        step.StartedAt ??= job.StartedAt;
        step.CompletedAt = status == "Queued" ? null : now;
        step.ResultDetail = code;
        database.AuditLog.Add(new AuditLogEntry
        {
            RequestId = action.RequestId,
            ProvisioningStepId = action.StepId,
            ActorUPN = "DaHub.Worker",
            ActionType = "MockProvisioningOutcome",
            AffectedSystem = "App",
            Result = status == "Completed" ? "Success" : status == "Queued" ? "Info" : "Failure",
            Detail = $"InitiatedByUserId={action.InitiatedByUserId};JobId={job.Id};Attempt={job.AttemptCount};Code={code}",
            TimestampUTC = now
        });
    }
}
