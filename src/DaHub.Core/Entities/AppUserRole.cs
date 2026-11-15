namespace DaHub.Core.Entities;

public sealed class AppUserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; }
    public required string AssignedBy { get; set; }
}
