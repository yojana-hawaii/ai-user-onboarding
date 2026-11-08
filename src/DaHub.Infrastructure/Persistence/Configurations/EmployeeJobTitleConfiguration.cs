using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmployeeJobTitleConfiguration : IEntityTypeConfiguration<EmployeeJobTitle>
{
    public void Configure(EntityTypeBuilder<EmployeeJobTitle> entity)
    {
        entity.ToTable("EmployeeJobTitles", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.EmployeeId, x.JobTitleId });
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.JobTitleId).HasColumnType("int").IsRequired();
        entity.Property(x => x.IsPrimary).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.AssignedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.AssignedBy).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.HasIndex(x => x.JobTitleId, "IX_EmpJobTitles_JobTitleId").HasDatabaseName("IX_EmpJobTitles_JobTitleId");
        entity.HasIndex(x => x.EmployeeId, "UX_EmpJobTitles_OnePrimary").HasDatabaseName("UX_EmpJobTitles_OnePrimary").IsUnique().HasFilter("IsPrimary = 1");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeJobTitles_EmployeeId");
        entity.HasOne<JobTitle>().WithMany().HasForeignKey(x => x.JobTitleId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeJobTitles_JobTitleId");
    }
}
