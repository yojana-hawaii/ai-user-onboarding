using DaHub.Core.Entities;

namespace DaHub.Core.Jobs;

public static class JobTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "ProvisioningRetry", "ApprovalReminder", "ApprovalEscalation", "SysAidSync",
        "NotificationRetry", "ScheduledTermination", "WorkerExpiry", "WorkerExpiryNotice"
    };
}

// Persist this identity in the payload when enqueueing workflow work. An action key must
// remain identical across retries and replacement jobs for the same logical action.
public sealed record JobExecutionIdentity(string ActionKey, int RequestId, int Revision,
    int EmploymentPeriodId, DateTime? ApprovedEndAtUtc);

public enum JobOutcome { Completed, Retry, Failed, Cancelled }

public sealed record JobResult(JobOutcome Outcome, string? Code = null, DateTime? RetryAtUtc = null)
{
    public static JobResult Success { get; } = new(JobOutcome.Completed);
}

public interface IJobHandler
{
    string JobType { get; }
    // Implementations must recheck current authority/revision/period immediately before
    // EVERY side effect, reconcile uncertain external results, and reuse the action key.
    // Return only allowlisted diagnostic codes; never return provider responses or secrets.
    Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken);
}

public interface IJobStore
{
    Task<BackgroundJob?> ClaimAsync(CancellationToken cancellationToken);
    Task FinishAsync(BackgroundJob job, JobResult result, CancellationToken cancellationToken);
    Task<int> RecoverAsync(CancellationToken cancellationToken);
}

public static class JobRetryPolicy
{
    public const int MaxAttempts = 4;
    public static TimeSpan Delay(int attempt) => attempt switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(4),
        3 => TimeSpan.FromMinutes(16),
        _ => throw new ArgumentOutOfRangeException(nameof(attempt))
    };

    public static string? SafeCode(string? code) => code is
        "TransientFailure" or "PermanentFailure" or "StaleExecution" or "ReconciliationRequired"
        or "HandlerUnavailable" or "HandlerFailure" or "InvalidResult" ? code : null;
}

// Handlers must fail closed when authority cannot be established. A rejected revision,
// cancelled schedule, changed approved end time or superseded employment period is stale.
public interface IJobExecutionAuthority
{
    Task<bool> IsCurrentAsync(JobExecutionIdentity identity, CancellationToken cancellationToken);
}

public enum ExternalResultState { Absent, Confirmed, Uncertain }

// Persist only stable identifiers and approved evidence references, never raw responses.
public sealed record JobExternalResult(string ActionKey, ExternalResultState State,
    string? ExternalId, string? EvidenceReference);

public interface IJobResultReconciler
{
    Task<JobExternalResult> ReconcileAsync(JobExecutionIdentity identity, CancellationToken cancellationToken);
}
