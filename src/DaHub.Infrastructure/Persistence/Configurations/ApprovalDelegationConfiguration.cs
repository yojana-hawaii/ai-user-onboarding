using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class ApprovalDelegationConfiguration : IEntityTypeConfiguration<ApprovalDelegation>
{
    public void Configure(EntityTypeBuilder<ApprovalDelegation> entity)
    {
        entity.ToTable("ApprovalDelegations", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_ApprovalDelegations_Validity", "EndsAt > StartsAt");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.ManagerEmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.DelegateUserId).HasColumnType("int").IsRequired();
        entity.Property(x => x.AssignedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.StartsAt).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.EndsAt).HasColumnType("datetime2").IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RevokedAt).HasColumnType("datetime2");
        entity.Property(x => x.RevokedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerEmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ApprovalDelegations_ManagerEmployeeId");
        entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DelegateUserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ApprovalDelegations_DelegateUserId");
    }
}
