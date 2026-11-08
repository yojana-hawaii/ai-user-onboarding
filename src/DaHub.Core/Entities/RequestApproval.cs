namespace DaHub.Core.Entities;

public sealed class RequestApproval
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int? DelegatedFromId { get; set; }
    public int Revision { get; set; } = 1;
    public int? OriginalManagerEmployeeId { get; set; }
    public int? DelegationId { get; set; }
    public string? ActedByUPN { get; set; }
    public DateTime RoutedAt { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public int ApproverUserId { get; set; }
    public int? ActedByUserId { get; set; }
    public required string ApproverUPN { get; set; }
    public required string ApproverRole { get; set; }
    public int EscalationLevel { get; set; } = 0;
    public string? Decision { get; set; }
    public string? Comments { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int RemindersSent { get; set; } = 0;
    public DateTime? ExpiresAt { get; set; }
}
