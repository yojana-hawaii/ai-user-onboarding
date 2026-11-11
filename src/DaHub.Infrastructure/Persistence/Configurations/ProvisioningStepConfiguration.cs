using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class ProvisioningStepConfiguration : IEntityTypeConfiguration<ProvisioningStep>
{
    public void Configure(EntityTypeBuilder<ProvisioningStep> entity)
    {
        entity.ToTable("ProvisioningSteps", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_ProvisioningSteps_TargetSystem", "TargetSystem IN ('AD','EntraID','M365','Exchange','SysAid','Manual')");
            table.HasCheckConstraint("CK_ProvisioningSteps_Status", "Status IN ('Pending','InProgress','Completed','Failed','Skipped')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestId).HasColumnType("int").IsRequired();
        entity.Property(x => x.TemplateId).HasColumnType("int");
        entity.Property(x => x.StepOrder).HasColumnType("int").IsRequired();
        entity.Property(x => x.StepName).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
        entity.Property(x => x.TargetSystem).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
        entity.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
        entity.Property(x => x.RetryCount).HasColumnType("int").IsRequired().HasDefaultValue(0);
        entity.Property(x => x.ResultDetail).HasColumnType("nvarchar(max)");
        entity.Property(x => x.ExecutedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.StartedAt).HasColumnType("datetime2");
        entity.Property(x => x.CompletedAt).HasColumnType("datetime2");
        entity.HasIndex(x => x.RequestId, "IX_Steps_RequestId").HasDatabaseName("IX_Steps_RequestId");
        entity.HasIndex(x => x.Status, "IX_Steps_Status").HasDatabaseName("IX_Steps_Status");
        entity.HasIndex(x => x.TemplateId, "IX_Steps_TemplateId").HasDatabaseName("IX_Steps_TemplateId");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ProvisioningSteps_RequestId");
        entity.HasOne<AccessTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ProvisioningSteps_TemplateId");
    }
}
