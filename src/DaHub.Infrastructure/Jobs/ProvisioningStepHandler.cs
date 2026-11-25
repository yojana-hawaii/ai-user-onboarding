using DaHub.Core.Entities;
using DaHub.Core.Jobs;

namespace DaHub.Infrastructure.Jobs;

// Explicit test-harness registration only; payloads do not contain authority or provider responses.
public sealed class ProvisioningStepHandler(IProvisioningActionStore actions,
    IJobExecutionAuthority authority, IMockProvisioningProvider provider) : IJobHandler
{
    public string JobType => "ProvisioningRetry";

    public async Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        var action = await actions.FindAsync(job, cancellationToken);
        if (action is null || !await CurrentAsync(action, cancellationToken))
            return new(JobOutcome.Cancelled, "StaleExecution");
        if (!await actions.TryOwnAsync(job, cancellationToken))
            return new(JobOutcome.Cancelled, "StaleExecution");

        // Every attempt first asks the independent provider ledger. Uncertain outcomes never replay.
        var result = await provider.ReconcileAsync(action, cancellationToken);
        if (!await CurrentAsync(action, cancellationToken)) return new(JobOutcome.Cancelled, "StaleExecution");
        if (result.State == ExternalResultState.Absent && action.State != "ReconciliationRequired")
            result = await provider.ExecuteAsync(action, cancellationToken);
        return result.State switch
        {
            ExternalResultState.Confirmed when Safe(result.Receipt) =>
                new(JobOutcome.Completed) { ProvisioningReceipt = result.Receipt },
            ExternalResultState.Absent when result.Retryable && action.State != "ReconciliationRequired" =>
                new(JobOutcome.Retry, "TransientFailure"),
            ExternalResultState.Absent when action.State != "ReconciliationRequired" =>
                new(JobOutcome.Failed, "PermanentFailure"),
            _ => new(JobOutcome.Failed, "ReconciliationRequired")
        };
    }

    private Task<bool> CurrentAsync(ProvisioningAction action, CancellationToken cancellationToken) =>
        authority.IsCurrentAsync(new(action.ActionKey, action.RequestId, action.Revision,
            action.EmploymentPeriodId, action.ApprovedEndAtUtc), cancellationToken);

    internal static bool Safe(ProvisioningReceipt? receipt) => receipt is not null
        && SafeReference(receipt.ExternalId) && SafeReference(receipt.EvidenceReference);

    private static bool SafeReference(string value) => value.Length is > 0 and <= 128
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
