namespace DaHub.Core.Entities;

public sealed class AppUser
{
    public int Id { get; set; }
    public string? UPN { get; set; }
    public int AuthorizationVersion { get; set; } = 1;
    public required string DisplayName { get; set; }
    public string? AppRole { get; set; }
    public int? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
