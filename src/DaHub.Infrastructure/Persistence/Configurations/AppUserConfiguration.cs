using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> entity)
    {
        entity.ToTable("AppUsers", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_AppUsers_AuthorizationVersion", "AuthorizationVersion > 0");
            table.HasCheckConstraint("CK_AppUsers_AppRole", "AppRole IN ('HR','IT','Manager','Executive','Admin','Compliance')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.UPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.AuthorizationVersion).HasColumnType("int").IsRequired().HasDefaultValue(1);
        entity.Property(x => x.DisplayName).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.AppRole).HasColumnType("nvarchar(30)").HasMaxLength(30);
        entity.Property(x => x.EmployeeId).HasColumnType("int");
        entity.Property(x => x.IsActive).HasColumnType("bit").IsRequired().HasDefaultValue(true);
        entity.Property(x => x.LastLoginAt).HasColumnType("datetime2");
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.UpdatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.EmployeeId, "IX_AppUsers_EmployeeId").HasDatabaseName("IX_AppUsers_EmployeeId");
        entity.HasIndex(x => x.EmployeeId, "UX_AppUsers_EmployeeId").HasDatabaseName("UX_AppUsers_EmployeeId").IsUnique().HasFilter("EmployeeId IS NOT NULL");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppUsers_EmployeeId");
    }
}
