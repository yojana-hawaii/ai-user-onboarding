namespace DaHub.Core.Entities;

public sealed class EmployeeSpecialty
{
    public int EmployeeId { get; set; }
    public int SpecialtyId { get; set; }
    public bool IsPrimary { get; set; } = false;
    public DateTime AssignedAt { get; set; }
    public required string AssignedBy { get; set; }
}
