namespace DaHub.Core.Entities;

public sealed class Employee
{
    public int Id { get; set; }
    public required string LegalFirstName { get; set; }
    public required string LegalLastName { get; set; }
    public string? PreferredName { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public int? ManagerId { get; set; }
    public required string EmploymentType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string VerificationStatus { get; set; } = "PendingHR";
    public DateTime? ImportedAt { get; set; }
    public string? VerifiedByUPN { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? ADObjectGuid { get; set; }
    public string? EntraObjectId { get; set; }
    public string Status { get; set; } = "PendingOnboard";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
