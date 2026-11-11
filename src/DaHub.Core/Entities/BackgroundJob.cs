namespace DaHub.Core.Entities;

public sealed class BackgroundJob
{
    public long Id { get; set; }
    public required string JobType { get; set; }
    public int? RequestId { get; set; }
    public int? ProvisioningStepId { get; set; }
    public string? Payload { get; set; }
    public string Status { get; set; } = "Queued";
    public DateTime ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AttemptCount { get; set; } = 0;
    public int MaxAttempts { get; set; } = 3;
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}
