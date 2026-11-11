using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AppExternalIdentityConfiguration : IEntityTypeConfiguration<AppExternalIdentity>
{
    public void Configure(EntityTypeBuilder<AppExternalIdentity> entity)
    {
        entity.ToTable("AppExternalIdentities", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_AppExternalIdentities_LinkMethod", "LinkMethod IN ('Admin','Bootstrap')");
            table.HasCheckConstraint("CK_AppExternalIdentities_Validity", "(LinkMethod = 'Admin' AND LinkedByUserId IS NOT NULL) OR LinkMethod = 'Bootstrap'");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.AppUserId).HasColumnType("int").IsRequired();
        entity.Property(x => x.ProviderKey).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Issuer).HasColumnType("nvarchar(400)").HasMaxLength(400).IsRequired().UseCollation("Latin1_General_100_BIN2");
        entity.Property(x => x.Subject).HasColumnType("nvarchar(255)").HasMaxLength(255).IsRequired().UseCollation("Latin1_General_100_BIN2");
        entity.Property(x => x.LinkedByUserId).HasColumnType("int");
        entity.Property(x => x.LinkMethod).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        entity.Property(x => x.LinkedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.IsActive).HasColumnType("bit").IsRequired().HasDefaultValue(true);
        entity.Property(x => x.RevokedAt).HasColumnType("datetime2");
        entity.HasAlternateKey(x => new { x.Issuer, x.Subject }).HasName("UQ_AppExternalIdentities_IssuerSubject");
        entity.HasIndex(x => x.AppUserId, "IX_AppExternalIdentities_AppUserId").HasDatabaseName("IX_AppExternalIdentities_AppUserId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppExternalIdentities_AppUserId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.LinkedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AppExternalIdentities_LinkedByUserId");
    }
}
