namespace DaHub.Core.Lookups;

public sealed class Department
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string OUPath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
