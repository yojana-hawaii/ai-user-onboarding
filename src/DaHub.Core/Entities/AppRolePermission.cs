namespace DaHub.Core.Entities;

public sealed class AppRolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public DateTime GrantedAt { get; set; }
    public required string GrantedBy { get; set; }
}
