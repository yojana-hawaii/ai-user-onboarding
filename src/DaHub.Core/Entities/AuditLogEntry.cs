namespace DaHub.Core.Entities;

public sealed class AuditLogEntry
{
    public long Id { get; set; }
    public int? RequestId { get; set; }
    public int? ProvisioningStepId { get; set; }
    public int? ActorUserId { get; set; }
    public required string ActorUPN { get; set; }
    public required string ActionType { get; set; }
    public string? TargetEmployeeUPN { get; set; }
    public string? AffectedSystem { get; set; }
    public required string Result { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? Detail { get; set; }
    public string? IPAddress { get; set; }
    public DateTime TimestampUTC { get; set; }
}
