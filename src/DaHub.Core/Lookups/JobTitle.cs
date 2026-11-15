namespace DaHub.Core.Lookups;

public sealed class JobTitle
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public bool EHRAccessDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
