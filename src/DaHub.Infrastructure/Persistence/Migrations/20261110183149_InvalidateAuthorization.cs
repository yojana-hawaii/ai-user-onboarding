using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaHub.Infrastructure.Persistence.Migrations;

public partial class InvalidateAuthorization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Trigger(migrationBuilder, "AppUserRoles", "INSERT, UPDATE, DELETE", "SELECT UserId FROM inserted UNION SELECT UserId FROM deleted");
        Trigger(migrationBuilder, "AppRolePermissions", "INSERT, UPDATE, DELETE",
            "SELECT ur.UserId FROM dahub_ita.AppUserRoles ur JOIN (SELECT RoleId FROM inserted UNION SELECT RoleId FROM deleted) r ON r.RoleId=ur.RoleId");
        Trigger(migrationBuilder, "AppRoles", "UPDATE",
            "SELECT ur.UserId FROM dahub_ita.AppUserRoles ur JOIN inserted i ON i.Id=ur.RoleId JOIN deleted d ON d.Id=i.Id WHERE i.IsActive<>d.IsActive");
        Trigger(migrationBuilder, "AppPermissions", "UPDATE",
            "SELECT ur.UserId FROM dahub_ita.AppUserRoles ur JOIN dahub_ita.AppRolePermissions rp ON rp.RoleId=ur.RoleId JOIN inserted i ON i.Id=rp.PermissionId JOIN deleted d ON d.Id=i.Id WHERE i.IsActive<>d.IsActive OR i.Name COLLATE Latin1_General_100_BIN2<>d.Name COLLATE Latin1_General_100_BIN2");
        Trigger(migrationBuilder, "AppUsers", "UPDATE",
            "SELECT i.Id AS UserId FROM inserted i JOIN deleted d ON d.Id=i.Id WHERE i.IsActive<>d.IsActive OR ISNULL(i.EmployeeId,0)<>ISNULL(d.EmployeeId,0)");
        Trigger(migrationBuilder, "AppExternalIdentities", "UPDATE, DELETE",
            "SELECT AppUserId AS UserId FROM deleted UNION SELECT AppUserId AS UserId FROM inserted");
    }

    private static void Trigger(MigrationBuilder migrationBuilder, string table, string events, string users) =>
        migrationBuilder.Sql($"""
            CREATE TRIGGER dahub_ita.TR_{table}_AuthorizationVersion
            ON dahub_ita.{table} AFTER {events} AS
            BEGIN
                IF ROWCOUNT_BIG() = 0 RETURN;
                SET NOCOUNT ON;
                UPDATE u SET AuthorizationVersion = u.AuthorizationVersion + 1
                FROM dahub_ita.AppUsers u WHERE u.Id IN ({users});
            END
            """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "AppExternalIdentities", "AppUsers", "AppPermissions", "AppRoles", "AppRolePermissions", "AppUserRoles" })
            migrationBuilder.Sql($"DROP TRIGGER dahub_ita.TR_{table}_AuthorizationVersion");
    }
}
