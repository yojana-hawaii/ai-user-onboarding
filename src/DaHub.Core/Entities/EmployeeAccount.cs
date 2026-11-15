namespace DaHub.Core.Entities;

public sealed class EmployeeAccount
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Guid? ADObjectGuid { get; set; }
    public string? EntraObjectId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public DateTime LinkedAt { get; set; }
    public DateTime? RetiredAt { get; set; }
}
