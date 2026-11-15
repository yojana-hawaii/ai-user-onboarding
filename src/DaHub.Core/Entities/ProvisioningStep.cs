namespace DaHub.Core.Entities;

public sealed class ProvisioningStep
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int? TemplateId { get; set; }
    public int StepOrder { get; set; }
    public required string StepName { get; set; }
    public required string TargetSystem { get; set; }
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; } = 0;
    public string? ResultDetail { get; set; }
    public string? ExecutedByUPN { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
