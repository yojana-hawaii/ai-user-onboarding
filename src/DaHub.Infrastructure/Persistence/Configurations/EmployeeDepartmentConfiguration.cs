using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDepartmentConfiguration : IEntityTypeConfiguration<EmployeeDepartment>
{
    public void Configure(EntityTypeBuilder<EmployeeDepartment> entity)
    {
        entity.ToTable("EmployeeDepartments", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.EmployeeId, x.DepartmentId });
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.DepartmentId).HasColumnType("int").IsRequired();
        entity.Property(x => x.IsPrimary).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.DepartmentId, "IX_EmpDept_DepartmentId").HasDatabaseName("IX_EmpDept_DepartmentId");
        entity.HasIndex(x => x.EmployeeId, "UX_EmpDept_OnePrimary").HasDatabaseName("UX_EmpDept_OnePrimary").IsUnique().HasFilter("IsPrimary = 1");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeDepartments_EmployeeId");
        entity.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeDepartments_DepartmentId");
    }
}
