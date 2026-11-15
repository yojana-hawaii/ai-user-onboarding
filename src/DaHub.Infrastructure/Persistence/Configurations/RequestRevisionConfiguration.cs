using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class RequestRevisionConfiguration : IEntityTypeConfiguration<RequestRevision>
{
    public void Configure(EntityTypeBuilder<RequestRevision> entity)
    {
        entity.ToTable("RequestRevisions", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_RequestRevisions_Revision", "Revision > 0");
            table.HasCheckConstraint("CK_RequestRevisions_SnapshotJson", "ISJSON(SnapshotJson) = 1");
        });
        entity.HasKey(x => new { x.RequestId, x.Revision });
        entity.Property(x => x.RequestId).HasColumnType("int").IsRequired();
        entity.Property(x => x.Revision).HasColumnType("int").IsRequired();
        entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.CreatedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.ChangeReason).HasColumnType("nvarchar(1000)").HasMaxLength(1000).IsRequired();
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestRevisions_RequestId");
    }
}
