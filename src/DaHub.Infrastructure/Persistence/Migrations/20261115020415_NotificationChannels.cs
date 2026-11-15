using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationLog_Channel",
                schema: "dahub_ita",
                table: "NotificationLog");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationLog_Channel",
                schema: "dahub_ita",
                table: "NotificationLog",
                sql: "Channel IN ('Email','Teams','Slack','SMS')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationLog_Channel",
                schema: "dahub_ita",
                table: "NotificationLog");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationLog_Channel",
                schema: "dahub_ita",
                table: "NotificationLog",
                sql: "Channel IN ('Email','Teams')");
        }
    }
}
