using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AuditLogExportConfiguration : IEntityTypeConfiguration<AuditLogExport>
{
    public void Configure(EntityTypeBuilder<AuditLogExport> entity)
    {
        entity.ToTable("AuditLogExports", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_AuditLogExports_ExporterRole", "ExporterRole IN ('IT','Compliance')");
            table.HasCheckConstraint("CK_AuditLogExports_Format", "Format IN ('CSV','PDF')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.ExportedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.ExporterRole).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
        entity.Property(x => x.Format).HasColumnType("nvarchar(10)").HasMaxLength(10).IsRequired();
        entity.Property(x => x.FilterDateFrom).HasColumnType("datetime2");
        entity.Property(x => x.FilterDateTo).HasColumnType("datetime2");
        entity.Property(x => x.FilterActorUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.FilterTargetUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.FilterActionType).HasColumnType("nvarchar(80)").HasMaxLength(80);
        entity.Property(x => x.RowCount).HasColumnType("int").IsRequired().HasDefaultValue(0);
        entity.Property(x => x.IPAddress).HasColumnType("nvarchar(45)").HasMaxLength(45);
        entity.Property(x => x.ExportedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.ExportedByUPN, "IX_AuditLogExports_ExportedByUPN").HasDatabaseName("IX_AuditLogExports_ExportedByUPN");
        entity.HasIndex(x => x.ExportedAt, "IX_AuditLogExports_ExportedAt").HasDatabaseName("IX_AuditLogExports_ExportedAt");
    }
}
