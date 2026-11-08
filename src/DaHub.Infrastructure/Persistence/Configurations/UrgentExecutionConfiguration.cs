using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class UrgentExecutionConfiguration : IEntityTypeConfiguration<UrgentExecution>
{
    public void Configure(EntityTypeBuilder<UrgentExecution> entity)
    {
        entity.ToTable("UrgentExecutions", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_UrgentExecutions_Justification", "LEN(LTRIM(RTRIM(Justification))) > 0");
            table.HasCheckConstraint("CK_UrgentExecutions_ActionsJson", "ISJSON(ActionsJson) = 1");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestId).HasColumnType("int").IsRequired();
        entity.Property(x => x.Revision).HasColumnType("int").IsRequired();
        entity.Property(x => x.ActorUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Justification).HasColumnType("nvarchar(1000)").HasMaxLength(1000).IsRequired();
        entity.Property(x => x.ActionsJson).HasColumnType("nvarchar(max)").IsRequired();
        entity.Property(x => x.ExecutedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UrgentExecutions_RequestId");
        entity.HasOne<RequestRevision>().WithMany().HasForeignKey(x => new { x.RequestId, x.Revision }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UrgentExecutions_Revision");
    }
}
