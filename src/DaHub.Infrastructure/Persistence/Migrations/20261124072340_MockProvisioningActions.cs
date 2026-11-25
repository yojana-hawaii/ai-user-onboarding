using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MockProvisioningActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProvisioningActions",
                schema: "dahub_ita",
                columns: table => new
                {
                    StepId = table.Column<int>(type: "int", nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    EmploymentPeriodId = table.Column<int>(type: "int", nullable: false),
                    ApprovedEndAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitiatedByUserId = table.Column<int>(type: "int", nullable: false),
                    PrerequisiteStepId = table.Column<int>(type: "int", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    State = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OwnerJobId = table.Column<long>(type: "bigint", nullable: true),
                    OwnerAttempt = table.Column<int>(type: "int", nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EvidenceReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningActions", x => x.StepId);
                    table.CheckConstraint("CK_ProvisioningActions_Operation", "Operation IN ('MockDirectory','MockHelpdesk')");
                    table.CheckConstraint("CK_ProvisioningActions_Prerequisite", "PrerequisiteStepId IS NULL OR PrerequisiteStepId <> StepId");
                    table.CheckConstraint("CK_ProvisioningActions_State", "State IN ('Pending','InProgress','Retry','Completed','Failed','ReconciliationRequired')");
                    table.ForeignKey(
                        name: "FK_ProvisioningActions_AppUsers_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProvisioningActions_BackgroundJobs_OwnerJobId",
                        column: x => x.OwnerJobId,
                        principalSchema: "dahub_ita",
                        principalTable: "BackgroundJobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProvisioningActions_EmploymentPeriods_EmploymentPeriodId",
                        column: x => x.EmploymentPeriodId,
                        principalSchema: "dahub_ita",
                        principalTable: "EmploymentPeriods",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProvisioningActions_ProvisioningActions_PrerequisiteStepId",
                        column: x => x.PrerequisiteStepId,
                        principalSchema: "dahub_ita",
                        principalTable: "ProvisioningActions",
                        principalColumn: "StepId");
                    table.ForeignKey(
                        name: "FK_ProvisioningActions_ProvisioningSteps_StepId",
                        column: x => x.StepId,
                        principalSchema: "dahub_ita",
                        principalTable: "ProvisioningSteps",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProvisioningActions_RequestRevisions_RequestId_Revision",
                        columns: x => new { x.RequestId, x.Revision },
                        principalSchema: "dahub_ita",
                        principalTable: "RequestRevisions",
                        principalColumns: new[] { "RequestId", "Revision" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningActions_ActionKey",
                schema: "dahub_ita",
                table: "ProvisioningActions",
                column: "ActionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningActions_EmploymentPeriodId",
                schema: "dahub_ita",
                table: "ProvisioningActions",
                column: "EmploymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningActions_InitiatedByUserId",
                schema: "dahub_ita",
                table: "ProvisioningActions",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningActions_OwnerJobId",
                schema: "dahub_ita",
                table: "ProvisioningActions",
                column: "OwnerJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningActions_PrerequisiteStepId",
                schema: "dahub_ita",
                table: "ProvisioningActions",
                column: "PrerequisiteStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningActions_RequestId_Revision",
                schema: "dahub_ita",
                table: "ProvisioningActions",
                columns: new[] { "RequestId", "Revision" });
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE ON OBJECT::[dahub_ita].[ProvisioningActions] TO [dahub_app_runtime]; DENY DELETE ON OBJECT::[dahub_ita].[ProvisioningActions] TO [dahub_app_runtime];");
            migrationBuilder.Sql("""
                CREATE TRIGGER [dahub_ita].[TR_ProvisioningActions_ImmutableIdentity]
                ON [dahub_ita].[ProvisioningActions] AFTER UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT StepId, ActionKey, DATALENGTH(ActionKey), RequestId, Revision, EmploymentPeriodId, ApprovedEndAtUtc,
                            InitiatedByUserId, PrerequisiteStepId, Operation COLLATE Latin1_General_100_BIN2, DATALENGTH(Operation) FROM deleted
                        EXCEPT
                        SELECT StepId, ActionKey, DATALENGTH(ActionKey), RequestId, Revision, EmploymentPeriodId, ApprovedEndAtUtc,
                            InitiatedByUserId, PrerequisiteStepId, Operation COLLATE Latin1_General_100_BIN2, DATALENGTH(Operation) FROM inserted)
                        THROW 51000, 'Provisioning action identity is immutable.', 1;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProvisioningActions",
                schema: "dahub_ita");
        }
    }
}
