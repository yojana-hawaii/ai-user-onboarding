using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> entity)
    {
        entity.ToTable("AuditLog", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_AuditLog_AffectedSystem", "AffectedSystem IN ('AD','EntraID','M365','Exchange','SysAid','App') OR AffectedSystem IS NULL");
            table.HasCheckConstraint("CK_AuditLog_Result", "Result IN ('Success','Failure','Info')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("bigint").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestId).HasColumnType("int");
        entity.Property(x => x.ProvisioningStepId).HasColumnType("int");
        entity.Property(x => x.ActorUserId).HasColumnType("int");
        entity.Property(x => x.ActorUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.ActionType).HasColumnType("nvarchar(80)").HasMaxLength(80).IsRequired();
        entity.Property(x => x.TargetEmployeeUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.AffectedSystem).HasColumnType("nvarchar(50)").HasMaxLength(50);
        entity.Property(x => x.Result).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        entity.Property(x => x.OldValueJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.NewValueJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.Detail).HasColumnType("nvarchar(max)");
        entity.Property(x => x.IPAddress).HasColumnType("nvarchar(45)").HasMaxLength(45);
        entity.Property(x => x.TimestampUTC).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.RequestId, "IX_AuditLog_RequestId").HasDatabaseName("IX_AuditLog_RequestId");
        entity.HasIndex(x => x.TimestampUTC, "IX_AuditLog_Timestamp").HasDatabaseName("IX_AuditLog_Timestamp");
        entity.HasIndex(x => x.ActorUPN, "IX_AuditLog_ActorUPN").HasDatabaseName("IX_AuditLog_ActorUPN");
        entity.HasIndex(x => x.TargetEmployeeUPN, "IX_AuditLog_TargetEmpUPN").HasDatabaseName("IX_AuditLog_TargetEmpUPN");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AuditLog_RequestId");
        entity.HasOne<ProvisioningStep>().WithMany().HasForeignKey(x => x.ProvisioningStepId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AuditLog_ProvisioningStepId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AuditLog_ActorUserId");
    }
}
