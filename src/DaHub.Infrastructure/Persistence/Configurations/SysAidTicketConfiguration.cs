using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class SysAidTicketConfiguration : IEntityTypeConfiguration<SysAidTicket>
{
    public void Configure(EntityTypeBuilder<SysAidTicket> entity)
    {
        entity.ToTable("SysAidTickets", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_SysAidTickets_TicketType", "TicketType IN ('EHRProvision','EHRDeprovision','EHRSiteUpdate')");
            table.HasCheckConstraint("CK_SysAidTickets_Status", "Status IN ('Open','InProgress','Resolved','Cancelled')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.RequestId).HasColumnType("int").IsRequired();
        entity.Property(x => x.SysAidSrId).HasColumnType("int").IsRequired();
        entity.Property(x => x.TicketType).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
        entity.Property(x => x.AssignedToUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.Status).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired().HasDefaultValue("Open");
        entity.Property(x => x.SysAidUrl).HasColumnType("nvarchar(500)").HasMaxLength(500);
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.ResolvedAt).HasColumnType("datetime2");
        entity.HasOne<WorkflowRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_SysAidTickets_RequestId");
    }
}
