using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> entity)
    {
        entity.ToTable("BackgroundJobs", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_BackgroundJobs_JobType", "JobType IN ('ProvisioningRetry','ApprovalReminder','ApprovalEscalation', 'SysAidSync','NotificationRetry','ScheduledTermination','WorkerExpiry','WorkerExpiryNotice')");
            table.HasCheckConstraint("CK_BackgroundJobs_Status", "Status IN ('Queued','InProgress','Completed','Failed','Cancelled')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("bigint").IsRequired().UseIdentityColumn();
        entity.Property(x => x.JobType).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
        entity.Property(x => x.RequestId).HasColumnType("int");
        entity.Property(x => x.ProvisioningStepId).HasColumnType("int");
        entity.Property(x => x.Payload).HasColumnType("nvarchar(max)");
        entity.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("Queued");
        entity.Property(x => x.ScheduledAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.StartedAt).HasColumnType("datetime2");
        entity.Property(x => x.CompletedAt).HasColumnType("datetime2");
        entity.Property(x => x.AttemptCount).HasColumnType("int").IsRequired().HasDefaultValue(0);
        entity.Property(x => x.MaxAttempts).HasColumnType("int").IsRequired().HasDefaultValue(3);
        entity.Property(x => x.LastError).HasColumnType("nvarchar(max)");
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.Status, "IX_BackgroundJobs_Status").HasDatabaseName("IX_BackgroundJobs_Status");
        entity.HasIndex(x => x.ScheduledAt, "IX_BackgroundJobs_ScheduledAt").HasDatabaseName("IX_BackgroundJobs_ScheduledAt");
        entity.HasIndex(x => x.RequestId, "IX_BackgroundJobs_RequestId").HasDatabaseName("IX_BackgroundJobs_RequestId");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_BackgroundJobs_RequestId");
        entity.HasOne<ProvisioningStep>().WithMany().HasForeignKey(x => x.ProvisioningStepId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_BackgroundJobs_ProvisioningStepId");
    }
}
