using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmploymentPeriodConfiguration : IEntityTypeConfiguration<EmploymentPeriod>
{
    public void Configure(EntityTypeBuilder<EmploymentPeriod> entity)
    {
        entity.ToTable("EmploymentPeriods", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_EmploymentPeriods_EmploymentType", "EmploymentType IN ('FT','PT','Contractor','Volunteer','Student','Temporary')");
            table.HasCheckConstraint("CK_EmploymentPeriods_AccountDecision", "AccountDecision IN ('Reuse','Replace') OR AccountDecision IS NULL");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.AccountId).HasColumnType("int");
        entity.Property(x => x.EmploymentType).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
        entity.Property(x => x.StartDate).HasColumnType("date");
        entity.Property(x => x.EndAt).HasColumnType("datetime2");
        entity.Property(x => x.ApprovedEndAt).HasColumnType("datetime2");
        entity.Property(x => x.EndApprovedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.EndApprovedAt).HasColumnType("datetime2");
        entity.Property(x => x.AccountDecision).HasColumnType("nvarchar(20)").HasMaxLength(20);
        entity.Property(x => x.AccountDecisionByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.AccountDecisionAt).HasColumnType("datetime2");
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmploymentPeriods_EmployeeId");
        entity.HasOne<EmployeeAccount>().WithMany().HasForeignKey(x => new { x.AccountId, x.EmployeeId }).HasPrincipalKey(x => new { x.Id, x.EmployeeId }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmploymentPeriods_AccountId");
    }
}
