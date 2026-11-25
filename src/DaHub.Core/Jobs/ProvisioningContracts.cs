using DaHub.Core.Entities;

namespace DaHub.Core.Jobs;

public sealed record ProvisioningReceipt(string ExternalId, string EvidenceReference);
public sealed record ProvisioningResult(ExternalResultState State, ProvisioningReceipt? Receipt = null,
    bool Retryable = false);

// Deliberately mock-only. No adapter or authority resolver is registered by an application host.
// A future live contract must establish provider-specific idempotency and reconciliation first.
public interface IMockProvisioningProvider
{
    Task<ProvisioningResult> ReconcileAsync(ProvisioningAction action, CancellationToken cancellationToken);
    Task<ProvisioningResult> ExecuteAsync(ProvisioningAction action, CancellationToken cancellationToken);
}

public interface IProvisioningActionStore
{
    Task<ProvisioningAction?> FindAsync(BackgroundJob job, CancellationToken cancellationToken);
    Task<bool> TryOwnAsync(BackgroundJob job, CancellationToken cancellationToken);
}
