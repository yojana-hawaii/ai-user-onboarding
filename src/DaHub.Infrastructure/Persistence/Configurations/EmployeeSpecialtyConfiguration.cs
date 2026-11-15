using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmployeeSpecialtyConfiguration : IEntityTypeConfiguration<EmployeeSpecialty>
{
    public void Configure(EntityTypeBuilder<EmployeeSpecialty> entity)
    {
        entity.ToTable("EmployeeSpecialties", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.EmployeeId, x.SpecialtyId });
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.SpecialtyId).HasColumnType("int").IsRequired();
        entity.Property(x => x.IsPrimary).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.AssignedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.AssignedBy).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.HasIndex(x => x.SpecialtyId, "IX_EmpSpec_SpecialtyId").HasDatabaseName("IX_EmpSpec_SpecialtyId");
        entity.HasIndex(x => x.EmployeeId, "UX_EmpSpec_OnePrimary").HasDatabaseName("UX_EmpSpec_OnePrimary").IsUnique().HasFilter("IsPrimary = 1");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeSpecialties_EmployeeId");
        entity.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeSpecialties_SpecialtyId");
    }
}
