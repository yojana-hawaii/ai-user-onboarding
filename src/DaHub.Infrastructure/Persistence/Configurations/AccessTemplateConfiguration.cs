using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AccessTemplateConfiguration : IEntityTypeConfiguration<AccessTemplate>
{
    public void Configure(EntityTypeBuilder<AccessTemplate> entity)
    {
        entity.ToTable("AccessTemplates", DaHubDbContext.Schema);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.Name).HasColumnType("nvarchar(150)").HasMaxLength(150).IsRequired();
        entity.Property(x => x.Description).HasColumnType("nvarchar(500)").HasMaxLength(500);
        entity.Property(x => x.DepartmentId).HasColumnType("int");
        entity.Property(x => x.LocationId).HasColumnType("int");
        entity.Property(x => x.JobTitleId).HasColumnType("int");
        entity.Property(x => x.SpecialtyId).HasColumnType("int");
        entity.Property(x => x.M365LicenseSku).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
        entity.Property(x => x.EHRAccessRequired).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.Version).HasColumnType("int").IsRequired().HasDefaultValue(1);
        entity.Property(x => x.IsActive).HasColumnType("bit").IsRequired().HasDefaultValue(true);
        entity.Property(x => x.CreatedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.UpdatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.DepartmentId, "IX_AccessTemplates_DeptId").HasDatabaseName("IX_AccessTemplates_DeptId");
        entity.HasIndex(x => x.LocationId, "IX_AccessTemplates_LocationId").HasDatabaseName("IX_AccessTemplates_LocationId");
        entity.HasIndex(x => x.JobTitleId, "IX_AccessTemplates_JobTitleId").HasDatabaseName("IX_AccessTemplates_JobTitleId");
        entity.HasIndex(x => x.SpecialtyId, "IX_AccessTemplates_SpecId").HasDatabaseName("IX_AccessTemplates_SpecId");
        entity.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccessTemplates_DepartmentId");
        entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccessTemplates_LocationId");
        entity.HasOne<JobTitle>().WithMany().HasForeignKey(x => x.JobTitleId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccessTemplates_JobTitleId");
        entity.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccessTemplates_SpecialtyId");
    }
}
