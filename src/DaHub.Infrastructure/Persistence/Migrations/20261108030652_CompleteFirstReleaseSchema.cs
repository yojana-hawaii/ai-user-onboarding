using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DaHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFirstReleaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessTemplates",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    JobTitleId = table.Column<int>(type: "int", nullable: true),
                    SpecialtyId = table.Column<int>(type: "int", nullable: true),
                    M365LicenseSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EHRAccessRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessTemplates_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "dahub_ita",
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccessTemplates_JobTitleId",
                        column: x => x.JobTitleId,
                        principalSchema: "dahub_ita",
                        principalTable: "JobTitles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccessTemplates_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dahub_ita",
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccessTemplates_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalSchema: "dahub_ita",
                        principalTable: "Specialties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppPermissions",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppRoles",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogExports",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExportedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExporterRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FilterDateFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilterDateTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FilterActorUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FilterTargetUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FilterActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    RowCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ExportedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogExports", x => x.Id);
                    table.CheckConstraint("CK_AuditLogExports_ExporterRole", "ExporterRole IN ('IT','Compliance')");
                    table.CheckConstraint("CK_AuditLogExports_Format", "Format IN ('CSV','PDF')");
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LegalFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LegalLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreferredName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ManagerId = table.Column<int>(type: "int", nullable: true),
                    EmploymentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "PendingHR"),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ADObjectGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntraObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "PendingOnboard"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.CheckConstraint("CK_Employees_EmploymentType", "EmploymentType IN ('FT','PT','Contractor','Volunteer','Student','Temporary')");
                    table.CheckConstraint("CK_Employees_Status", "Status IN ('Active','PendingOnboard','Offboarded','Disabled','OnLeave')");
                    table.CheckConstraint("CK_Employees_VerificationStatus", "VerificationStatus IN ('PendingHR','Verified')");
                    table.ForeignKey(
                        name: "FK_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccessTemplateGroups",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    GroupType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    GroupIdentifier = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessTemplateGroups", x => x.Id);
                    table.CheckConstraint("CK_AccessTemplateGroups_GroupType", "GroupType IN ('AD_Security','AD_Distribution','Entra_Group')");
                    table.ForeignKey(
                        name: "FK_AccessTemplateGroups_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "dahub_ita",
                        principalTable: "AccessTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppRolePermissions",
                schema: "dahub_ita",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    GrantedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_AppRolePermissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppPermissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppRolePermissions_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppRoles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuthorizationVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.CheckConstraint("CK_AppUsers_AppRole", "AppRole IN ('HR','IT','Manager','Executive','Admin','Compliance')");
                    table.CheckConstraint("CK_AppUsers_AuthorizationVersion", "AuthorizationVersion > 0");
                    table.ForeignKey(
                        name: "FK_AppUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAccounts",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ADObjectGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntraObjectId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAccounts", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeAccounts_Id_EmployeeId", x => new { x.Id, x.EmployeeId });
                    table.ForeignKey(
                        name: "FK_EmployeeAccounts_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDepartments",
                schema: "dahub_ita",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDepartments", x => new { x.EmployeeId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_EmployeeDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "dahub_ita",
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeDepartments_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeJobTitles",
                schema: "dahub_ita",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    JobTitleId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeJobTitles", x => new { x.EmployeeId, x.JobTitleId });
                    table.ForeignKey(
                        name: "FK_EmployeeJobTitles_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeJobTitles_JobTitleId",
                        column: x => x.JobTitleId,
                        principalSchema: "dahub_ita",
                        principalTable: "JobTitles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLocations",
                schema: "dahub_ita",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLocations", x => new { x.EmployeeId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_EmployeeLocations_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dahub_ita",
                        principalTable: "Locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSpecialties",
                schema: "dahub_ita",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SpecialtyId = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSpecialties", x => new { x.EmployeeId, x.SpecialtyId });
                    table.ForeignKey(
                        name: "FK_EmployeeSpecialties_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeSpecialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalSchema: "dahub_ita",
                        principalTable: "Specialties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppExternalIdentities",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppUserId = table.Column<int>(type: "int", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Subject = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    LinkedByUserId = table.Column<int>(type: "int", nullable: true),
                    LinkMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExternalIdentities", x => x.Id);
                    table.UniqueConstraint("UQ_AppExternalIdentities_IssuerSubject", x => new { x.Issuer, x.Subject });
                    table.CheckConstraint("CK_AppExternalIdentities_LinkMethod", "LinkMethod IN ('Admin','Bootstrap')");
                    table.CheckConstraint("CK_AppExternalIdentities_Validity", "(LinkMethod = 'Admin' AND LinkedByUserId IS NOT NULL) OR LinkMethod = 'Bootstrap'");
                    table.ForeignKey(
                        name: "FK_AppExternalIdentities_AppUserId",
                        column: x => x.AppUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppExternalIdentities_LinkedByUserId",
                        column: x => x.LinkedByUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagerEmployeeId = table.Column<int>(type: "int", nullable: false),
                    DelegateUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => x.Id);
                    table.CheckConstraint("CK_ApprovalDelegations_Validity", "EndsAt > StartsAt");
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_ManagerEmployeeId",
                        column: x => x.ManagerEmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppUserRoles",
                schema: "dahub_ita",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AssignedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AppUserRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppUserRoles_UserId",
                        column: x => x.UserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmploymentPeriods",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    EmploymentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndApprovedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EndApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AccountDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AccountDecisionByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountDecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmploymentPeriods", x => x.Id);
                    table.CheckConstraint("CK_EmploymentPeriods_AccountDecision", "AccountDecision IN ('Reuse','Replace') OR AccountDecision IS NULL");
                    table.CheckConstraint("CK_EmploymentPeriods_EmploymentType", "EmploymentType IN ('FT','PT','Contractor','Volunteer','Student','Temporary')");
                    table.ForeignKey(
                        name: "FK_EmploymentPeriods_AccountId",
                        columns: x => new { x.AccountId, x.EmployeeId },
                        principalSchema: "dahub_ita",
                        principalTable: "EmployeeAccounts",
                        principalColumns: new[] { "Id", "EmployeeId" });
                    table.ForeignKey(
                        name: "FK_EmploymentPeriods_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssignedToUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Submitted"),
                    EmploymentPeriodId = table.Column<int>(type: "int", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ExecutionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "NotStarted"),
                    ResolutionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "None"),
                    IsUrgent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EffectiveAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OneDriveRetentionAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RetentionRecipientUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MailboxRetentionDays = table.Column<int>(type: "int", nullable: true),
                    OneDriveRetentionDays = table.Column<int>(type: "int", nullable: true),
                    RetentionAuthorizedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RetentionAuthorizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreservationConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EHRRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EHRVerifiedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EHRVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EHRVerificationEvidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MailboxRetentionAction = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ITNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                    table.CheckConstraint("CK_Requests_ApprovalStatus", "ApprovalStatus IN ('Pending','Approved','Rejected','Preauthorized')");
                    table.CheckConstraint("CK_Requests_ExecutionStatus", "ExecutionStatus IN ('NotStarted','Scheduled','InProgress','Completed','Failed')");
                    table.CheckConstraint("CK_Requests_MailboxRetentionAction", "MailboxRetentionAction IN ('ForwardToManager','SharedMailbox','Delete') OR MailboxRetentionAction IS NULL");
                    table.CheckConstraint("CK_Requests_MailboxRetentionDays", "MailboxRetentionDays >= 0");
                    table.CheckConstraint("CK_Requests_OneDriveRetentionAction", "OneDriveRetentionAction IN ('AssignToManager','Retain','Delete') OR OneDriveRetentionAction IS NULL");
                    table.CheckConstraint("CK_Requests_OneDriveRetentionDays", "OneDriveRetentionDays >= 0");
                    table.CheckConstraint("CK_Requests_RequestType", "RequestType IN ('Onboarding','Offboarding','Transfer', 'NameUpdate','AccessRequest')");
                    table.CheckConstraint("CK_Requests_ResolutionStatus", "ResolutionStatus IN ('None','Required','Resolved')");
                    table.CheckConstraint("CK_Requests_Revision", "Revision > 0");
                    table.CheckConstraint("CK_Requests_Status", "Status IN ('Draft','Submitted','PendingApproval','Approved', 'Rejected','InProgress','Completed','Failed','Cancelled','NeedsResolution')");
                    table.CheckConstraint("CK_Requests_TerminationType", "TerminationType IN ('Voluntary','Involuntary','Retirement','Deceased') OR TerminationType IS NULL");
                    table.ForeignKey(
                        name: "FK_Requests_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Requests_EmploymentPeriodId",
                        column: x => x.EmploymentPeriodId,
                        principalSchema: "dahub_ita",
                        principalTable: "EmploymentPeriods",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationLog",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Sent"),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ErrorDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLog", x => x.Id);
                    table.CheckConstraint("CK_NotificationLog_Channel", "Channel IN ('Email','Teams')");
                    table.CheckConstraint("CK_NotificationLog_Status", "Status IN ('Sent','Failed','Retrying')");
                    table.ForeignKey(
                        name: "FK_NotificationLog_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProvisioningSteps",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: true),
                    StepOrder = table.Column<int>(type: "int", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ResultDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningSteps", x => x.Id);
                    table.CheckConstraint("CK_ProvisioningSteps_Status", "Status IN ('Pending','InProgress','Completed','Failed','Skipped')");
                    table.CheckConstraint("CK_ProvisioningSteps_TargetSystem", "TargetSystem IN ('AD','EntraID','M365','Exchange','SysAid','Manual')");
                    table.ForeignKey(
                        name: "FK_ProvisioningSteps_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProvisioningSteps_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "dahub_ita",
                        principalTable: "AccessTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RequestAccessTemplates",
                schema: "dahub_ita",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    MatchedOn = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ITApproved = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestAccessTemplates", x => new { x.RequestId, x.TemplateId });
                    table.ForeignKey(
                        name: "FK_RequestAccessTemplates_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestAccessTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "dahub_ita",
                        principalTable: "AccessTemplates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RequestRevisions",
                schema: "dahub_ita",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ChangeReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestRevisions", x => new { x.RequestId, x.Revision });
                    table.CheckConstraint("CK_RequestRevisions_Revision", "Revision > 0");
                    table.CheckConstraint("CK_RequestRevisions_SnapshotJson", "ISJSON(SnapshotJson) = 1");
                    table.ForeignKey(
                        name: "FK_RequestRevisions_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SysAidTickets",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    SysAidSrId = table.Column<int>(type: "int", nullable: false),
                    TicketType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedToUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Open"),
                    SysAidUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SysAidTickets", x => x.Id);
                    table.CheckConstraint("CK_SysAidTickets_Status", "Status IN ('Open','InProgress','Resolved','Cancelled')");
                    table.CheckConstraint("CK_SysAidTickets_TicketType", "TicketType IN ('EHRProvision','EHRDeprovision','EHRSiteUpdate')");
                    table.ForeignKey(
                        name: "FK_SysAidTickets_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    ProvisioningStepId = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TargetEmployeeUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AffectedSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OldValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    TimestampUTC = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                    table.CheckConstraint("CK_AuditLog_AffectedSystem", "AffectedSystem IN ('AD','EntraID','M365','Exchange','SysAid','App') OR AffectedSystem IS NULL");
                    table.CheckConstraint("CK_AuditLog_Result", "Result IN ('Success','Failure','Info')");
                    table.ForeignKey(
                        name: "FK_AuditLog_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLog_ProvisioningStepId",
                        column: x => x.ProvisioningStepId,
                        principalSchema: "dahub_ita",
                        principalTable: "ProvisioningSteps",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLog_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BackgroundJobs",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    ProvisioningStepId = table.Column<int>(type: "int", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 3),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundJobs", x => x.Id);
                    table.CheckConstraint("CK_BackgroundJobs_JobType", "JobType IN ('ProvisioningRetry','ApprovalReminder','ApprovalEscalation', 'SysAidSync','NotificationRetry','ScheduledTermination','WorkerExpiry','WorkerExpiryNotice')");
                    table.CheckConstraint("CK_BackgroundJobs_Status", "Status IN ('Queued','InProgress','Completed','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_ProvisioningStepId",
                        column: x => x.ProvisioningStepId,
                        principalSchema: "dahub_ita",
                        principalTable: "ProvisioningSteps",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BackgroundJobs_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RequestApprovals",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    DelegatedFromId = table.Column<int>(type: "int", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    OriginalManagerEmployeeId = table.Column<int>(type: "int", nullable: true),
                    DelegationId = table.Column<int>(type: "int", nullable: true),
                    ActedByUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoutedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ReminderAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApproverUserId = table.Column<int>(type: "int", nullable: false),
                    ActedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApproverUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemindersSent = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestApprovals", x => x.Id);
                    table.CheckConstraint("CK_RequestApprovals_ApproverRole", "ApproverRole IN ('Manager','Delegate')");
                    table.CheckConstraint("CK_RequestApprovals_Decision", "Decision IN ('Approved','Rejected') OR Decision IS NULL");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_ActedByUserId",
                        column: x => x.ActedByUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalSchema: "dahub_ita",
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_DelegatedFromId",
                        column: x => x.DelegatedFromId,
                        principalSchema: "dahub_ita",
                        principalTable: "RequestApprovals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_DelegationId",
                        column: x => x.DelegationId,
                        principalSchema: "dahub_ita",
                        principalTable: "ApprovalDelegations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_OriginalManagerEmployeeId",
                        column: x => x.OriginalManagerEmployeeId,
                        principalSchema: "dahub_ita",
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RequestApprovals_Revision",
                        columns: x => new { x.RequestId, x.Revision },
                        principalSchema: "dahub_ita",
                        principalTable: "RequestRevisions",
                        principalColumns: new[] { "RequestId", "Revision" });
                });

            migrationBuilder.CreateTable(
                name: "UrgentExecutions",
                schema: "dahub_ita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ActorUPN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrgentExecutions", x => x.Id);
                    table.CheckConstraint("CK_UrgentExecutions_ActionsJson", "ISJSON(ActionsJson) = 1");
                    table.CheckConstraint("CK_UrgentExecutions_Justification", "LEN(LTRIM(RTRIM(Justification))) > 0");
                    table.ForeignKey(
                        name: "FK_UrgentExecutions_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dahub_ita",
                        principalTable: "Requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UrgentExecutions_Revision",
                        columns: x => new { x.RequestId, x.Revision },
                        principalSchema: "dahub_ita",
                        principalTable: "RequestRevisions",
                        principalColumns: new[] { "RequestId", "Revision" });
                });

            migrationBuilder.InsertData(
                schema: "dahub_ita",
                table: "AppPermissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "Requests", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Submit new onboarding, offboarding, transfer, name change, and access requests", true, "requests.submit" },
                    { 2, "Requests", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "View status of own submitted requests", true, "requests.view.own" },
                    { 3, "Requests", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "View all requests regardless of submitter", true, "requests.view.all" },
                    { 4, "Requests", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Cancel a request before execution begins", true, "requests.cancel" },
                    { 5, "Provisioning", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Execute provisioning steps on approved requests", true, "provisioning.execute" },
                    { 6, "Provisioning", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Retry a failed provisioning step", true, "provisioning.retry" },
                    { 7, "Provisioning", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Mark a provisioning step as Skipped with reason", true, "provisioning.skip" },
                    { 8, "Provisioning", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Mark a provisioning step as Completed via Manual Override", true, "provisioning.override" },
                    { 9, "Approvals", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Approve or reject requests assigned to this user", true, "approvals.action" },
                    { 10, "Provisioning", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Execute urgent onboarding/offboarding with justification; requires provisioning.execute", true, "provisioning.urgent" },
                    { 11, "Approvals", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Manage dated delegation of own manager approvals", true, "approvals.delegate.own" },
                    { 12, "Approvals", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "HR/Admin backup assignment of dated delegates", true, "approvals.delegate.manage" },
                    { 13, "Provisioning", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "IT authorization of mailbox and OneDrive actions", true, "retention.manage" },
                    { 14, "Templates", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "View access templates and their group assignments", true, "templates.view" },
                    { 15, "Templates", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Create, edit, clone, and deactivate access templates", true, "templates.manage" },
                    { 16, "Audit", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Search and filter the audit log", true, "audit.search" },
                    { 17, "Audit", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Export filtered audit log records to CSV or PDF", true, "audit.export" },
                    { 18, "Users", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Create, edit, and deactivate app users and role assignments", true, "users.manage" },
                    { 19, "Dashboard", new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "View executive dashboards, metrics, and compliance reports", true, "dashboard.view" }
                });

            migrationBuilder.InsertData(
                schema: "dahub_ita",
                table: "AppRoles",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Submit onboarding, offboarding, transfer, name change, and access requests", true, "HR" },
                    { 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Execute provisioning tasks, retry failed steps, view all requests and full audit logs", true, "IT" },
                    { 3, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Approve or reject requests for direct reports; requests escalate up if unresponsive", true, "Manager" },
                    { 4, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Read-only access to dashboards, metrics, and compliance reports", true, "Executive" },
                    { 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Template management, user administration, integration settings; urgency requires separate designation", true, "Admin" },
                    { 6, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Read-only audit log search and export for HIPAA/HRSA reviews", true, "Compliance" },
                    { 7, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "Supplemental permission for designated IT staff; requires IT execution permission", true, "UrgentOperator" }
                });

            migrationBuilder.InsertData(
                schema: "dahub_ita",
                table: "AppRolePermissions",
                columns: new[] { "PermissionId", "RoleId", "GrantedAt", "GrantedBy" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 2, 1, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 4, 1, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 12, 1, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 2, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 3, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 4, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 5, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 6, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 7, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 8, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 13, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 14, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 16, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 17, 2, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 1, 3, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 2, 3, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 9, 3, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 11, 3, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 19, 4, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 1, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 2, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 3, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 4, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 5, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 6, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 7, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 8, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 9, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 12, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 14, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 15, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 16, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 17, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 18, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 19, 5, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 16, 6, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 17, 6, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" },
                    { 10, 7, new DateTime(2026, 11, 7, 0, 0, 0, 0, DateTimeKind.Utc), "system:seed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ATGroups_TemplateId",
                schema: "dahub_ita",
                table: "AccessTemplateGroups",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTemplates_DeptId",
                schema: "dahub_ita",
                table: "AccessTemplates",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTemplates_JobTitleId",
                schema: "dahub_ita",
                table: "AccessTemplates",
                column: "JobTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTemplates_LocationId",
                schema: "dahub_ita",
                table: "AccessTemplates",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTemplates_SpecId",
                schema: "dahub_ita",
                table: "AccessTemplates",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExternalIdentities_AppUserId",
                schema: "dahub_ita",
                table: "AppExternalIdentities",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExternalIdentities_LinkedByUserId",
                schema: "dahub_ita",
                table: "AppExternalIdentities",
                column: "LinkedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_AppPermissions_Name",
                schema: "dahub_ita",
                table: "AppPermissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRolePerms_PermissionId",
                schema: "dahub_ita",
                table: "AppRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_AppRoles_Name",
                schema: "dahub_ita",
                table: "AppRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegateUserId",
                schema: "dahub_ita",
                table: "ApprovalDelegations",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_ManagerEmployeeId",
                schema: "dahub_ita",
                table: "ApprovalDelegations",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUserRoles_RoleId",
                schema: "dahub_ita",
                table: "AppUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_EmployeeId",
                schema: "dahub_ita",
                table: "AppUsers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_AppUsers_EmployeeId",
                schema: "dahub_ita",
                table: "AppUsers",
                column: "EmployeeId",
                unique: true,
                filter: "EmployeeId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorUPN",
                schema: "dahub_ita",
                table: "AuditLog",
                column: "ActorUPN");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorUserId",
                schema: "dahub_ita",
                table: "AuditLog",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ProvisioningStepId",
                schema: "dahub_ita",
                table: "AuditLog",
                column: "ProvisioningStepId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_RequestId",
                schema: "dahub_ita",
                table: "AuditLog",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TargetEmpUPN",
                schema: "dahub_ita",
                table: "AuditLog",
                column: "TargetEmployeeUPN");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                schema: "dahub_ita",
                table: "AuditLog",
                column: "TimestampUTC");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogExports_ExportedAt",
                schema: "dahub_ita",
                table: "AuditLogExports",
                column: "ExportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogExports_ExportedByUPN",
                schema: "dahub_ita",
                table: "AuditLogExports",
                column: "ExportedByUPN");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ProvisioningStepId",
                schema: "dahub_ita",
                table: "BackgroundJobs",
                column: "ProvisioningStepId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_RequestId",
                schema: "dahub_ita",
                table: "BackgroundJobs",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_ScheduledAt",
                schema: "dahub_ita",
                table: "BackgroundJobs",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobs_Status",
                schema: "dahub_ita",
                table: "BackgroundJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAccounts_EmployeeId",
                schema: "dahub_ita",
                table: "EmployeeAccounts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "UX_EmployeeAccounts_AD",
                schema: "dahub_ita",
                table: "EmployeeAccounts",
                column: "ADObjectGuid",
                unique: true,
                filter: "ADObjectGuid IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_EmployeeAccounts_Entra",
                schema: "dahub_ita",
                table: "EmployeeAccounts",
                column: "EntraObjectId",
                unique: true,
                filter: "EntraObjectId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmpDept_DepartmentId",
                schema: "dahub_ita",
                table: "EmployeeDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "UX_EmpDept_OnePrimary",
                schema: "dahub_ita",
                table: "EmployeeDepartments",
                column: "EmployeeId",
                unique: true,
                filter: "IsPrimary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmpJobTitles_JobTitleId",
                schema: "dahub_ita",
                table: "EmployeeJobTitles",
                column: "JobTitleId");

            migrationBuilder.CreateIndex(
                name: "UX_EmpJobTitles_OnePrimary",
                schema: "dahub_ita",
                table: "EmployeeJobTitles",
                column: "EmployeeId",
                unique: true,
                filter: "IsPrimary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmpLoc_LocationId",
                schema: "dahub_ita",
                table: "EmployeeLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "UX_EmpLoc_OnePrimary",
                schema: "dahub_ita",
                table: "EmployeeLocations",
                column: "EmployeeId",
                unique: true,
                filter: "IsPrimary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                schema: "dahub_ita",
                table: "Employees",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                schema: "dahub_ita",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Username",
                schema: "dahub_ita",
                table: "Employees",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "UX_Employees_Email",
                schema: "dahub_ita",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Employees_Username",
                schema: "dahub_ita",
                table: "Employees",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmpSpec_SpecialtyId",
                schema: "dahub_ita",
                table: "EmployeeSpecialties",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "UX_EmpSpec_OnePrimary",
                schema: "dahub_ita",
                table: "EmployeeSpecialties",
                column: "EmployeeId",
                unique: true,
                filter: "IsPrimary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentPeriods_AccountId_EmployeeId",
                schema: "dahub_ita",
                table: "EmploymentPeriods",
                columns: new[] { "AccountId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentPeriods_EmployeeId",
                schema: "dahub_ita",
                table: "EmploymentPeriods",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_NotifLog_Recipient",
                schema: "dahub_ita",
                table: "NotificationLog",
                column: "Recipient");

            migrationBuilder.CreateIndex(
                name: "IX_NotifLog_RequestId",
                schema: "dahub_ita",
                table: "NotificationLog",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_NotifLog_SentAt",
                schema: "dahub_ita",
                table: "NotificationLog",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotifLog_Status",
                schema: "dahub_ita",
                table: "NotificationLog",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Steps_RequestId",
                schema: "dahub_ita",
                table: "ProvisioningSteps",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Steps_Status",
                schema: "dahub_ita",
                table: "ProvisioningSteps",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Steps_TemplateId",
                schema: "dahub_ita",
                table: "ProvisioningSteps",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ReqAccessTemplates_TemplateId",
                schema: "dahub_ita",
                table: "RequestAccessTemplates",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_RequestId",
                schema: "dahub_ita",
                table: "RequestApprovals",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestApprovals_ActedByUserId",
                schema: "dahub_ita",
                table: "RequestApprovals",
                column: "ActedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestApprovals_ApproverUserId",
                schema: "dahub_ita",
                table: "RequestApprovals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestApprovals_DelegatedFromId",
                schema: "dahub_ita",
                table: "RequestApprovals",
                column: "DelegatedFromId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestApprovals_DelegationId",
                schema: "dahub_ita",
                table: "RequestApprovals",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestApprovals_OriginalManagerEmployeeId",
                schema: "dahub_ita",
                table: "RequestApprovals",
                column: "OriginalManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestApprovals_RequestId_Revision",
                schema: "dahub_ita",
                table: "RequestApprovals",
                columns: new[] { "RequestId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_EmployeeId",
                schema: "dahub_ita",
                table: "Requests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_EmploymentPeriodId",
                schema: "dahub_ita",
                table: "Requests",
                column: "EmploymentPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status",
                schema: "dahub_ita",
                table: "Requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_SubmittedAt",
                schema: "dahub_ita",
                table: "Requests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Type",
                schema: "dahub_ita",
                table: "Requests",
                column: "RequestType");

            migrationBuilder.CreateIndex(
                name: "IX_SysAidTickets_RequestId",
                schema: "dahub_ita",
                table: "SysAidTickets",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_UrgentExecutions_RequestId_Revision",
                schema: "dahub_ita",
                table: "UrgentExecutions",
                columns: new[] { "RequestId", "Revision" });
            // Insert missing sites by natural key; preserve existing lookup IDs and edits.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'MAIN')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'915 N King St — Main', N'915 N King St, Honolulu, HI 96817', N'MAIN');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'710NK')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'710 N King St', N'710 N King St, Honolulu, HI 96817', N'710NK');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'DT')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'89 S King St — Downtown', N'89 S King St, Honolulu, HI 96813', N'DT');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'952NK')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'952 N King St', N'952 N King St, Honolulu, HI 96817', N'952NK');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'KAAAHI')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'Kaaahi Clinic', N'Honolulu, HI', N'KAAAHI');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'KOHOU')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'Kohou Clinic', N'Honolulu, HI', N'KOHOU');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'VIRTUAL')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'Virtual / All Sites', NULL, N'VIRTUAL');
                IF NOT EXISTS (SELECT 1 FROM [dahub_ita].[Locations] WHERE [SiteCode] = N'ADMIN')
                    INSERT [dahub_ita].[Locations] ([Name], [Address], [SiteCode]) VALUES (N'Admin — Off-Site', NULL, N'ADMIN');
                """);

            migrationBuilder.Sql("""
                CREATE ROLE [dahub_app_runtime] AUTHORIZATION [dbo];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[Locations] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[Departments] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[JobTitles] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[Specialties] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AccessTemplates] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AccessTemplateGroups] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[Employees] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[EmployeeLocations] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[EmployeeDepartments] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[EmployeeJobTitles] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[EmployeeSpecialties] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AppRoles] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AppPermissions] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AppRolePermissions] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AppUserRoles] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AppUsers] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AppExternalIdentities] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[EmployeeAccounts] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[EmploymentPeriods] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[ApprovalDelegations] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[Requests] TO [dahub_app_runtime];
                GRANT SELECT, INSERT ON OBJECT::[dahub_ita].[RequestRevisions] TO [dahub_app_runtime];
                DENY UPDATE, DELETE ON OBJECT::[dahub_ita].[RequestRevisions] TO [dahub_app_runtime];
                GRANT SELECT, INSERT ON OBJECT::[dahub_ita].[UrgentExecutions] TO [dahub_app_runtime];
                DENY UPDATE, DELETE ON OBJECT::[dahub_ita].[UrgentExecutions] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[RequestAccessTemplates] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[RequestApprovals] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[ProvisioningSteps] TO [dahub_app_runtime];
                GRANT SELECT, INSERT ON OBJECT::[dahub_ita].[AuditLog] TO [dahub_app_runtime];
                DENY UPDATE, DELETE ON OBJECT::[dahub_ita].[AuditLog] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[SysAidTickets] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[BackgroundJobs] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[AuditLogExports] TO [dahub_app_runtime];
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dahub_ita].[NotificationLog] TO [dahub_app_runtime];
                GRANT SELECT ON OBJECT::[dahub_ita].[__EFMigrationsHistory] TO [dahub_app_runtime];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @members nvarchar(max);
                SELECT @members = STRING_AGG(CONVERT(nvarchar(max),
                    N'ALTER ROLE [dahub_app_runtime] DROP MEMBER ' + QUOTENAME(member.name) + N';'), NCHAR(10))
                FROM sys.database_role_members membership
                JOIN sys.database_principals role ON role.principal_id = membership.role_principal_id
                JOIN sys.database_principals member ON member.principal_id = membership.member_principal_id
                WHERE role.name = N'dahub_app_runtime';
                IF @members IS NOT NULL EXEC sys.sp_executesql @members;
                DROP ROLE [dahub_app_runtime];
                """);

            migrationBuilder.DropTable(
                name: "AccessTemplateGroups",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AppExternalIdentities",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AppRolePermissions",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AppUserRoles",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AuditLogExports",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "BackgroundJobs",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "EmployeeDepartments",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "EmployeeJobTitles",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "EmployeeLocations",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "EmployeeSpecialties",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "NotificationLog",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "RequestAccessTemplates",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "RequestApprovals",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "SysAidTickets",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "UrgentExecutions",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AppPermissions",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AppRoles",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "ProvisioningSteps",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "ApprovalDelegations",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "RequestRevisions",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AccessTemplates",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "AppUsers",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "Requests",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "EmploymentPeriods",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "EmployeeAccounts",
                schema: "dahub_ita");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "dahub_ita");
        }
    }
}
