using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AppRolePermissionConfiguration : IEntityTypeConfiguration<AppRolePermission>
{
    public void Configure(EntityTypeBuilder<AppRolePermission> entity)
    {
        entity.ToTable("AppRolePermissions", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.RoleId, x.PermissionId });
        entity.Property(x => x.RoleId).HasColumnType("int").IsRequired();
        entity.Property(x => x.PermissionId).HasColumnType("int").IsRequired();
        entity.Property(x => x.GrantedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.GrantedBy).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.HasIndex(x => x.PermissionId, "IX_AppRolePerms_PermissionId").HasDatabaseName("IX_AppRolePerms_PermissionId");
        entity.HasOne<AppRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppRolePermissions_RoleId");
        entity.HasOne<AppPermission>().WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppRolePermissions_PermissionId");
    }
}
