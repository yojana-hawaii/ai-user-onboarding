namespace DaHub.Core.Entities;

public sealed class EmployeeDepartment
{
    public int EmployeeId { get; set; }
    public int DepartmentId { get; set; }
    public bool IsPrimary { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}
