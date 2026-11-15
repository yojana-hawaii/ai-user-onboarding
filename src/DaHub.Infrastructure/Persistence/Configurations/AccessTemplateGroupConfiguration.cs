using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AccessTemplateGroupConfiguration : IEntityTypeConfiguration<AccessTemplateGroup>
{
    public void Configure(EntityTypeBuilder<AccessTemplateGroup> entity)
    {
        entity.ToTable("AccessTemplateGroups", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_AccessTemplateGroups_GroupType", "GroupType IN ('AD_Security','AD_Distribution','Entra_Group')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.TemplateId).HasColumnType("int").IsRequired();
        entity.Property(x => x.GroupType).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        entity.Property(x => x.GroupName).HasColumnType("nvarchar(300)").HasMaxLength(300).IsRequired();
        entity.Property(x => x.GroupIdentifier).HasColumnType("nvarchar(500)").HasMaxLength(500).IsRequired();
        entity.Property(x => x.IsRequired).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.TemplateId, "IX_ATGroups_TemplateId").HasDatabaseName("IX_ATGroups_TemplateId");
        entity.HasOne<AccessTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AccessTemplateGroups_TemplateId");
    }
}
