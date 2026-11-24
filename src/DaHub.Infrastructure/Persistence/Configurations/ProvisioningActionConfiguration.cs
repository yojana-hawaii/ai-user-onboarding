using DaHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class ProvisioningActionConfiguration : IEntityTypeConfiguration<ProvisioningAction>
{
    public void Configure(EntityTypeBuilder<ProvisioningAction> entity)
    {
        entity.ToTable("ProvisioningActions", DaHubDbContext.Schema, table =>
        {
            table.UseSqlOutputClause(false);
            table.HasCheckConstraint("CK_ProvisioningActions_State", "State IN ('Pending','InProgress','Retry','Completed','Failed','ReconciliationRequired')");
            table.HasCheckConstraint("CK_ProvisioningActions_Operation", "Operation IN ('MockDirectory','MockHelpdesk')");
            table.HasCheckConstraint("CK_ProvisioningActions_Prerequisite", "PrerequisiteStepId IS NULL OR PrerequisiteStepId <> StepId");
        });
        entity.HasKey(x => x.StepId);
        entity.Property(x => x.StepId).ValueGeneratedNever();
        entity.Property(x => x.ActionKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        entity.HasIndex(x => x.ActionKey).IsUnique();
        entity.Property(x => x.Operation).HasMaxLength(30);
        entity.Property(x => x.State).HasMaxLength(30);
        entity.Property(x => x.ExternalId).HasMaxLength(128);
        entity.Property(x => x.EvidenceReference).HasMaxLength(128);
        entity.Property(x => x.ApprovedEndAtUtc).HasColumnType("datetime2");
        entity.HasOne<ProvisioningStep>().WithOne().HasForeignKey<ProvisioningAction>(x => x.StepId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<ProvisioningAction>().WithMany().HasForeignKey(x => x.PrerequisiteStepId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<RequestRevision>().WithMany().HasForeignKey(x => new { x.RequestId, x.Revision }).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<EmploymentPeriod>().WithMany().HasForeignKey(x => x.EmploymentPeriodId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InitiatedByUserId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.OwnerJobId).OnDelete(DeleteBehavior.NoAction);
    }
}
