using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmployeeAccountConfiguration : IEntityTypeConfiguration<EmployeeAccount>
{
    public void Configure(EntityTypeBuilder<EmployeeAccount> entity)
    {
        entity.ToTable("EmployeeAccounts", DaHubDbContext.Schema);
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.EmployeeId).HasColumnType("int").IsRequired();
        entity.Property(x => x.ADObjectGuid).HasColumnType("uniqueidentifier");
        entity.Property(x => x.EntraObjectId).HasColumnType("nvarchar(36)").HasMaxLength(36);
        entity.Property(x => x.Username).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Email).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.LinkedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RetiredAt).HasColumnType("datetime2");
        entity.HasIndex(x => x.ADObjectGuid, "UX_EmployeeAccounts_AD").HasDatabaseName("UX_EmployeeAccounts_AD").IsUnique().HasFilter("ADObjectGuid IS NOT NULL");
        entity.HasIndex(x => x.EntraObjectId, "UX_EmployeeAccounts_Entra").HasDatabaseName("UX_EmployeeAccounts_Entra").IsUnique().HasFilter("EntraObjectId IS NOT NULL");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_EmployeeAccounts_EmployeeId");
    }
}
