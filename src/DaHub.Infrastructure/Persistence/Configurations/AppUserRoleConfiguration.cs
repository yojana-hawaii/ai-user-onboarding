using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
{
    public void Configure(EntityTypeBuilder<AppUserRole> entity)
    {
        entity.ToTable("AppUserRoles", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.UserId, x.RoleId });
        entity.Property(x => x.UserId).HasColumnType("int").IsRequired();
        entity.Property(x => x.RoleId).HasColumnType("int").IsRequired();
        entity.Property(x => x.AssignedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.AssignedBy).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.HasIndex(x => x.RoleId, "IX_AppUserRoles_RoleId").HasDatabaseName("IX_AppUserRoles_RoleId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppUserRoles_UserId");
        entity.HasOne<AppRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppUserRoles_RoleId");
    }
}
