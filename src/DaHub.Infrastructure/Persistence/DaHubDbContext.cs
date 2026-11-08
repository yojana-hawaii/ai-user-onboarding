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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

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
