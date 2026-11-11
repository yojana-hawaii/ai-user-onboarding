using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class NotificationLogEntryConfiguration : IEntityTypeConfiguration<NotificationLogEntry>
{
    public void Configure(EntityTypeBuilder<NotificationLogEntry> entity)
    {
        entity.ToTable("NotificationLog", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_NotificationLog_Channel", "Channel IN ('Email','Teams')");
            table.HasCheckConstraint("CK_NotificationLog_Status", "Status IN ('Sent','Failed','Retrying')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("bigint").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestId).HasColumnType("int");
        entity.Property(x => x.Channel).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        entity.Property(x => x.Recipient).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Subject).HasColumnType("nvarchar(300)").HasMaxLength(300);
        entity.Property(x => x.NotificationType).HasColumnType("nvarchar(80)").HasMaxLength(80).IsRequired();
        entity.Property(x => x.Status).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("Sent");
        entity.Property(x => x.AttemptCount).HasColumnType("int").IsRequired().HasDefaultValue(1);
        entity.Property(x => x.ErrorDetail).HasColumnType("nvarchar(max)");
        entity.Property(x => x.SentAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.DeliveredAt).HasColumnType("datetime2");
        entity.HasIndex(x => x.RequestId, "IX_NotifLog_RequestId").HasDatabaseName("IX_NotifLog_RequestId");
        entity.HasIndex(x => x.Recipient, "IX_NotifLog_Recipient").HasDatabaseName("IX_NotifLog_Recipient");
        entity.HasIndex(x => x.SentAt, "IX_NotifLog_SentAt").HasDatabaseName("IX_NotifLog_SentAt");
        entity.HasIndex(x => x.Status, "IX_NotifLog_Status").HasDatabaseName("IX_NotifLog_Status");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_NotificationLog_RequestId");
    }
}
