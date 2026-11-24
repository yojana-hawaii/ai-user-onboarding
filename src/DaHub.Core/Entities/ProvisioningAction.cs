namespace DaHub.Core.Entities;

// One immutable logical effect per step. Job replacements retain this identity and evidence.
public sealed class ProvisioningAction
{
    public int StepId { get; set; }
    public required string ActionKey { get; set; }
    public int RequestId { get; set; }
    public int Revision { get; set; }
    public int EmploymentPeriodId { get; set; }
    public DateTime? ApprovedEndAtUtc { get; set; }
    public int InitiatedByUserId { get; set; }
    public int? PrerequisiteStepId { get; set; }
    public required string Operation { get; set; }
    public string State { get; set; } = "Pending";
    public long? OwnerJobId { get; set; }
    public int? OwnerAttempt { get; set; }
    public string? ExternalId { get; set; }
    public string? EvidenceReference { get; set; }
}
