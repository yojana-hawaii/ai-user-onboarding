namespace DaHub.Core.Lookups;

public sealed class Location
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
    public required string SiteCode { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
