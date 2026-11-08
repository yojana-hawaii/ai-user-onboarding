namespace DaHub.Core.Entities;

public sealed class ApprovalDelegation
{
    public int Id { get; set; }
    public int ManagerEmployeeId { get; set; }
    public int DelegateUserId { get; set; }
    public required string AssignedByUPN { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByUPN { get; set; }
}
