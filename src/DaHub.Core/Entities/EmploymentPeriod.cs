namespace DaHub.Core.Entities;

public sealed class EmploymentPeriod
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int? AccountId { get; set; }
    public required string EmploymentType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateTime? EndAt { get; set; }
    public DateTime? ApprovedEndAt { get; set; }
    public string? EndApprovedByUPN { get; set; }
    public DateTime? EndApprovedAt { get; set; }
    public string? AccountDecision { get; set; }
    public string? AccountDecisionByUPN { get; set; }
    public DateTime? AccountDecisionAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
