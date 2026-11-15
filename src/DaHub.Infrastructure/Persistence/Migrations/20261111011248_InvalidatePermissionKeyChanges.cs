using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvalidatePermissionKeyChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TRIGGER dahub_ita.TR_AppPermissions_AuthorizationVersion
                ON dahub_ita.AppPermissions AFTER UPDATE AS
                BEGIN
                    IF ROWCOUNT_BIG() = 0 RETURN;
                    SET NOCOUNT ON;
                    UPDATE u SET AuthorizationVersion = u.AuthorizationVersion + 1
                    FROM dahub_ita.AppUsers u
                    WHERE u.Id IN (
                        SELECT ur.UserId FROM dahub_ita.AppUserRoles ur
                        JOIN dahub_ita.AppRolePermissions rp ON rp.RoleId=ur.RoleId
                        JOIN inserted i ON i.Id=rp.PermissionId JOIN deleted d ON d.Id=i.Id
                        WHERE i.IsActive<>d.IsActive
                            OR i.Name COLLATE Latin1_General_100_BIN2<>d.Name COLLATE Latin1_General_100_BIN2
                            OR DATALENGTH(i.Name)<>DATALENGTH(d.Name));
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TRIGGER dahub_ita.TR_AppPermissions_AuthorizationVersion
                ON dahub_ita.AppPermissions AFTER UPDATE AS
                BEGIN
                    IF ROWCOUNT_BIG() = 0 RETURN;
                    SET NOCOUNT ON;
                    UPDATE u SET AuthorizationVersion = u.AuthorizationVersion + 1
                    FROM dahub_ita.AppUsers u
                    WHERE u.Id IN (
                        SELECT ur.UserId FROM dahub_ita.AppUserRoles ur
                        JOIN dahub_ita.AppRolePermissions rp ON rp.RoleId=ur.RoleId
                        JOIN inserted i ON i.Id=rp.PermissionId JOIN deleted d ON d.Id=i.Id
                        WHERE i.IsActive<>d.IsActive
                            OR i.Name COLLATE Latin1_General_100_BIN2<>d.Name COLLATE Latin1_General_100_BIN2);
                END
                """);
        }
    }
}
