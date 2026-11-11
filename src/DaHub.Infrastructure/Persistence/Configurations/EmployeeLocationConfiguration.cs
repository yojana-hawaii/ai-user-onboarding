using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmployeeLocationConfiguration : IEntityTypeConfiguration<EmployeeLocation>
{
    public void Configure(EntityTypeBuilder<EmployeeLocation> entity)
    {
        entity.ToTable("EmployeeLocations", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.EmployeeId, x.LocationId });
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.LocationId).HasColumnType("int").IsRequired();
        entity.Property(x => x.IsPrimary).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.LocationId, "IX_EmpLoc_LocationId").HasDatabaseName("IX_EmpLoc_LocationId");
        entity.HasIndex(x => x.EmployeeId, "UX_EmpLoc_OnePrimary").HasDatabaseName("UX_EmpLoc_OnePrimary").IsUnique().HasFilter("IsPrimary = 1");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeLocations_EmployeeId");
        entity.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeLocations_LocationId");
    }
}
