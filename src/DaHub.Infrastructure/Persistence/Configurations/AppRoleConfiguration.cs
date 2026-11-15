using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> entity)
    {
        entity.ToTable("AppRoles", DaHubDbContext.Schema);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.Name).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Description).HasColumnType("nvarchar(300)").HasMaxLength(300);
        entity.Property(x => x.IsActive).HasColumnType("bit").IsRequired().HasDefaultValue(true);
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.Name, "UX_AppRoles_Name").HasDatabaseName("UX_AppRoles_Name").IsUnique();
    }
}
