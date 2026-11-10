using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence;

public sealed class DaHubDbContext(DbContextOptions<DaHubDbContext> options) : DbContext(options)
{
    public const string Schema = "dahub_ita";

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<Specialty> Specialties => Set<Specialty>();

    public DbSet<AccessTemplate> AccessTemplates => Set<AccessTemplate>();
    public DbSet<AccessTemplateGroup> AccessTemplateGroups => Set<AccessTemplateGroup>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeLocation> EmployeeLocations => Set<EmployeeLocation>();
    public DbSet<EmployeeDepartment> EmployeeDepartments => Set<EmployeeDepartment>();
    public DbSet<EmployeeJobTitle> EmployeeJobTitles => Set<EmployeeJobTitle>();
    public DbSet<EmployeeSpecialty> EmployeeSpecialties => Set<EmployeeSpecialty>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<AppPermission> AppPermissions => Set<AppPermission>();
    public DbSet<AppRolePermission> AppRolePermissions => Set<AppRolePermission>();
    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppExternalIdentity> AppExternalIdentities => Set<AppExternalIdentity>();
    public DbSet<EmployeeAccount> EmployeeAccounts => Set<EmployeeAccount>();
    public DbSet<EmploymentPeriod> EmploymentPeriods => Set<EmploymentPeriod>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<WorkflowRequest> Requests => Set<WorkflowRequest>();
    public DbSet<RequestRevision> RequestRevisions => Set<RequestRevision>();
    public DbSet<UrgentExecution> UrgentExecutions => Set<UrgentExecution>();
    public DbSet<RequestAccessTemplate> RequestAccessTemplates => Set<RequestAccessTemplate>();
    public DbSet<RequestApproval> RequestApprovals => Set<RequestApproval>();
    public DbSet<ProvisioningStep> ProvisioningSteps => Set<ProvisioningStep>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<SysAidTicket> SysAidTickets => Set<SysAidTicket>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<AuditLogExport> AuditLogExports => Set<AuditLogExport>();
    public DbSet<NotificationLogEntry> NotificationLog => Set<NotificationLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DaHubDbContext).Assembly);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");
        SchemaSeeds.Configure(modelBuilder);
        // Authorization triggers update versions transactionally, including direct SQL maintenance.
        foreach (var entity in new[] { typeof(AppUser), typeof(AppExternalIdentity), typeof(AppRole),
                     typeof(AppPermission), typeof(AppUserRole), typeof(AppRolePermission) })
            modelBuilder.Entity(entity).ToTable(table => table.UseSqlOutputClause(false));

        var locations = modelBuilder.Entity<Location>();
        ConfigureLookup(locations, "Locations");
        locations.Property(x => x.Name).HasMaxLength(150).IsRequired();
        locations.Property(x => x.Address).HasMaxLength(300);
        locations.Property(x => x.SiteCode).HasMaxLength(20).IsRequired();
        locations.Property(x => x.Phone).HasMaxLength(30);
        locations.HasIndex(x => x.SiteCode).IsUnique();

        var departments = modelBuilder.Entity<Department>();
        ConfigureLookup(departments, "Departments");
        departments.Property(x => x.Name).HasMaxLength(100).IsRequired();
        departments.Property(x => x.OUPath).HasMaxLength(500).IsRequired();

        var jobTitles = modelBuilder.Entity<JobTitle>();
        ConfigureLookup(jobTitles, "JobTitles");
        jobTitles.Property(x => x.Title).HasMaxLength(150).IsRequired();
        jobTitles.Property(x => x.EHRAccessDefault).HasDefaultValue(false);
        jobTitles.HasIndex(x => x.Title).IsUnique();

        var specialties = modelBuilder.Entity<Specialty>();
        ConfigureLookup(specialties, "Specialties");
        specialties.Property(x => x.Name).HasMaxLength(150).IsRequired();
        specialties.Property(x => x.Description).HasMaxLength(500);
        specialties.HasIndex(x => x.Name).IsUnique();
    }

    private static void ConfigureLookup<TEntity>(EntityTypeBuilder<TEntity> entity, string table)
        where TEntity : class
    {
        entity.ToTable(table);
        entity.HasKey("Id");
        entity.Property<int>("Id").UseIdentityColumn();
        entity.Property<bool>("IsActive").HasDefaultValue(true);
        entity.Property<DateTime>("CreatedAt").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property<DateTime>("UpdatedAt").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
