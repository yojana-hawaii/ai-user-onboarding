namespace DaHub.Core.Entities;

public sealed class EmployeeLocation
{
    public int EmployeeId { get; set; }
    public int LocationId { get; set; }
    public bool IsPrimary { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}
