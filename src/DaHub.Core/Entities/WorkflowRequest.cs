namespace DaHub.Core.Entities;

public sealed class WorkflowRequest
{
    public int Id { get; set; }
    public required string RequestType { get; set; }
    public int EmployeeId { get; set; }
    public required string SubmittedByUPN { get; set; }
    public string? AssignedToUPN { get; set; }
    public string Status { get; set; } = "Submitted";
    public int? EmploymentPeriodId { get; set; }
    public int Revision { get; set; } = 1;
    public string ApprovalStatus { get; set; } = "Pending";
    public string ExecutionStatus { get; set; } = "NotStarted";
    public string ResolutionStatus { get; set; } = "None";
    public bool IsUrgent { get; set; } = false;
    public DateTime? EffectiveAt { get; set; }
    public string? ScheduledByUPN { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? OneDriveRetentionAction { get; set; }
    public string? RetentionRecipientUPN { get; set; }
    public int? MailboxRetentionDays { get; set; }
    public int? OneDriveRetentionDays { get; set; }
    public string? RetentionAuthorizedByUPN { get; set; }
    public DateTime? RetentionAuthorizedAt { get; set; }
    public DateTime? PreservationConfirmedAt { get; set; }
    public bool EHRRequired { get; set; } = false;
    public string? EHRVerifiedByUPN { get; set; }
    public DateTime? EHRVerifiedAt { get; set; }
    public string? EHRVerificationEvidence { get; set; }
    public string? ResolvedByUPN { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionDetail { get; set; }
    public string? SnapshotJson { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string? TerminationType { get; set; }
    public string? MailboxRetentionAction { get; set; }
    public string? Notes { get; set; }
    public string? ITNotes { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
