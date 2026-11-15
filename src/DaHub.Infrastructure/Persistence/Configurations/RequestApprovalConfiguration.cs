using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class RequestApprovalConfiguration : IEntityTypeConfiguration<RequestApproval>
{
    public void Configure(EntityTypeBuilder<RequestApproval> entity)
    {
        entity.ToTable("RequestApprovals", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_RequestApprovals_ApproverRole", "ApproverRole IN ('Manager','Delegate')");
            table.HasCheckConstraint("CK_RequestApprovals_Decision", "Decision IN ('Approved','Rejected') OR Decision IS NULL");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestId).HasColumnType("int").IsRequired();
        entity.Property(x => x.DelegatedFromId).HasColumnType("int");
        entity.Property(x => x.Revision).HasColumnType("int").IsRequired().HasDefaultValue(1);
        entity.Property(x => x.OriginalManagerEmployeeId).HasColumnType("int");
        entity.Property(x => x.DelegationId).HasColumnType("int");
        entity.Property(x => x.ActedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.RoutedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.ReminderAt).HasColumnType("datetime2");
        entity.Property(x => x.SupersededAt).HasColumnType("datetime2");
        entity.Property(x => x.ApproverUserId).HasColumnType("int").IsRequired();
        entity.Property(x => x.ActedByUserId).HasColumnType("int");
        entity.Property(x => x.ApproverUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.ApproverRole).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
        entity.Property(x => x.EscalationLevel).HasColumnType("int").IsRequired().HasDefaultValue(0);
        entity.Property(x => x.Decision).HasColumnType("nvarchar(20)").HasMaxLength(20);
        entity.Property(x => x.Comments).HasColumnType("nvarchar(1000)").HasMaxLength(1000);
        entity.Property(x => x.DecidedAt).HasColumnType("datetime2");
        entity.Property(x => x.RemindersSent).HasColumnType("int").IsRequired().HasDefaultValue(0);
        entity.Property(x => x.ExpiresAt).HasColumnType("datetime2");
        entity.HasIndex(x => x.RequestId, "IX_Approvals_RequestId").HasDatabaseName("IX_Approvals_RequestId");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_RequestId");
        entity.HasOne<RequestApproval>().WithMany().HasForeignKey(x => x.DelegatedFromId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_DelegatedFromId");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.OriginalManagerEmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_OriginalManagerEmployeeId");
        entity.HasOne<ApprovalDelegation>().WithMany().HasForeignKey(x => x.DelegationId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_DelegationId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_ApproverUserId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActedByUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_ActedByUserId");
        entity.HasOne<RequestRevision>().WithMany().HasForeignKey(x => new { x.RequestId, x.Revision }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RequestApprovals_Revision");
    }
}
