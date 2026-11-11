using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class WorkflowRequestConfiguration : IEntityTypeConfiguration<WorkflowRequest>
{
    public void Configure(EntityTypeBuilder<WorkflowRequest> entity)
    {
        entity.ToTable("Requests", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_Requests_RequestType", "RequestType IN ('Onboarding','Offboarding','Transfer', 'NameUpdate','AccessRequest')");
            table.HasCheckConstraint("CK_Requests_Status", "Status IN ('Draft','Submitted','PendingApproval','Approved', 'Rejected','InProgress','Completed','Failed','Cancelled','NeedsResolution')");
            table.HasCheckConstraint("CK_Requests_Revision", "Revision > 0");
            table.HasCheckConstraint("CK_Requests_ApprovalStatus", "ApprovalStatus IN ('Pending','Approved','Rejected','Preauthorized')");
            table.HasCheckConstraint("CK_Requests_ExecutionStatus", "ExecutionStatus IN ('NotStarted','Scheduled','InProgress','Completed','Failed')");
            table.HasCheckConstraint("CK_Requests_ResolutionStatus", "ResolutionStatus IN ('None','Required','Resolved')");
            table.HasCheckConstraint("CK_Requests_OneDriveRetentionAction", "OneDriveRetentionAction IN ('AssignToManager','Retain','Delete') OR OneDriveRetentionAction IS NULL");
            table.HasCheckConstraint("CK_Requests_MailboxRetentionDays", "MailboxRetentionDays >= 0");
            table.HasCheckConstraint("CK_Requests_OneDriveRetentionDays", "OneDriveRetentionDays >= 0");
            table.HasCheckConstraint("CK_Requests_TerminationType", "TerminationType IN ('Voluntary','Involuntary','Retirement','Deceased') OR TerminationType IS NULL");
            table.HasCheckConstraint("CK_Requests_MailboxRetentionAction", "MailboxRetentionAction IN ('ForwardToManager','SharedMailbox','Delete') OR MailboxRetentionAction IS NULL");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestType).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.SubmittedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.AssignedToUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.Status).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired().HasDefaultValue("Submitted");
        entity.Property(x => x.EmploymentPeriodId).HasColumnType("int");
        entity.Property(x => x.Revision).HasColumnType("int").IsRequired().HasDefaultValue(1);
        entity.Property(x => x.ApprovalStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
        entity.Property(x => x.ExecutionStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("NotStarted");
        entity.Property(x => x.ResolutionStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("None");
        entity.Property(x => x.IsUrgent).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.EffectiveAt).HasColumnType("datetime2");
        entity.Property(x => x.ScheduledByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.ScheduledAt).HasColumnType("datetime2");
        entity.Property(x => x.OneDriveRetentionAction).HasColumnType("nvarchar(30)").HasMaxLength(30);
        entity.Property(x => x.RetentionRecipientUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.MailboxRetentionDays).HasColumnType("int");
        entity.Property(x => x.OneDriveRetentionDays).HasColumnType("int");
        entity.Property(x => x.RetentionAuthorizedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.RetentionAuthorizedAt).HasColumnType("datetime2");
        entity.Property(x => x.PreservationConfirmedAt).HasColumnType("datetime2");
        entity.Property(x => x.EHRRequired).HasColumnType("bit").IsRequired().HasDefaultValue(false);
        entity.Property(x => x.EHRVerifiedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.EHRVerifiedAt).HasColumnType("datetime2");
        entity.Property(x => x.EHRVerificationEvidence).HasColumnType("nvarchar(max)");
        entity.Property(x => x.ResolvedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.ResolvedAt).HasColumnType("datetime2");
        entity.Property(x => x.ResolutionDetail).HasColumnType("nvarchar(max)");
        entity.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        entity.Property(x => x.EffectiveDate).HasColumnType("date");
        entity.Property(x => x.TerminationType).HasColumnType("nvarchar(30)").HasMaxLength(30);
        entity.Property(x => x.MailboxRetentionAction).HasColumnType("nvarchar(30)").HasMaxLength(30);
        entity.Property(x => x.Notes).HasColumnType("nvarchar(max)");
        entity.Property(x => x.ITNotes).HasColumnType("nvarchar(max)");
        entity.Property(x => x.SubmittedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.CompletedAt).HasColumnType("datetime2");
        entity.Property(x => x.UpdatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.EmployeeId, "IX_Requests_EmployeeId").HasDatabaseName("IX_Requests_EmployeeId");
        entity.HasIndex(x => x.Status, "IX_Requests_Status").HasDatabaseName("IX_Requests_Status");
        entity.HasIndex(x => x.RequestType, "IX_Requests_Type").HasDatabaseName("IX_Requests_Type");
        entity.HasIndex(x => x.SubmittedAt, "IX_Requests_SubmittedAt").HasDatabaseName("IX_Requests_SubmittedAt");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Requests_EmployeeId");
        entity.HasOne<EmploymentPeriod>().WithMany().HasForeignKey(x => x.EmploymentPeriodId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Requests_EmploymentPeriodId");
    }
}
