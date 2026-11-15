using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class RequestAccessTemplateConfiguration : IEntityTypeConfiguration<RequestAccessTemplate>
{
    public void Configure(EntityTypeBuilder<RequestAccessTemplate> entity)
    {
        entity.ToTable("RequestAccessTemplates", DaHubDbContext.Schema);
        entity.HasKey(x => new { x.RequestId, x.TemplateId });
        entity.Property(x => x.RequestId).HasColumnType("int").IsRequired();
        entity.Property(x => x.TemplateId).HasColumnType("int").IsRequired();
        entity.Property(x => x.MatchedOn).HasColumnType("nvarchar(300)").HasMaxLength(300).IsRequired();
        entity.Property(x => x.ITApproved).HasColumnType("bit").IsRequired().HasDefaultValue(true);
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.TemplateId, "IX_ReqAccessTemplates_TemplateId").HasDatabaseName("IX_ReqAccessTemplates_TemplateId");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestAccessTemplates_RequestId");
        entity.HasOne<AccessTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestAccessTemplates_TemplateId");
    }
}
