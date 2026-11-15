using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkerAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MaxAttempts",
                schema: "dahub_ita",
                table: "BackgroundJobs",
                type: "int",
                nullable: false,
                defaultValue: 4,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 3);

            migrationBuilder.CreateTable(
                name: "BackgroundJobAttempts",
                schema: "dahub_ita",
                columns: table => new
                {
                    JobId = table.Column<long>(type: "bigint", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobAttempts", x => new { x.JobId, x.Attempt });
                    table.ForeignKey(
                        name: "FK_BackgroundJobAttempts_BackgroundJobs_JobId",
                        column: x => x.JobId,
                        principalSchema: "dahub_ita",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id");
                });
            // Retain attempts for operations; the runtime cannot delete this evidence.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dahub_ita].[BackgroundJobAttempts] TO [dahub_app_runtime];
                DENY DELETE ON OBJECT::[dahub_ita].[BackgroundJobAttempts] TO [dahub_app_runtime];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundJobAttempts",
                schema: "dahub_ita");

            migrationBuilder.AlterColumn<int>(
                name: "MaxAttempts",
                schema: "dahub_ita",
                table: "BackgroundJobs",
                type: "int",
                nullable: false,
                defaultValue: 3,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 4);
        }
    }
}


