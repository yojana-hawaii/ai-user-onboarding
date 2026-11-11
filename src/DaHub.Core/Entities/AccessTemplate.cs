namespace DaHub.Core.Entities;

public sealed class AccessTemplate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public int? LocationId { get; set; }
    public int? JobTitleId { get; set; }
    public int? SpecialtyId { get; set; }
    public required string M365LicenseSku { get; set; }
    public bool EHRAccessRequired { get; set; } = false;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public required string CreatedByUPN { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
