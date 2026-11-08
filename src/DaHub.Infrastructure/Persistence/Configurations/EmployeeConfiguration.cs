using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> entity)
    {
        entity.ToTable("Employees", DaHubDbContext.Schema, table =>
        {
            table.HasCheckConstraint("CK_Employees_EmploymentType", "EmploymentType IN ('FT','PT','Contractor','Volunteer','Student','Temporary')");
            table.HasCheckConstraint("CK_Employees_VerificationStatus", "VerificationStatus IN ('PendingHR','Verified')");
            table.HasCheckConstraint("CK_Employees_Status", "Status IN ('Active','PendingOnboard','Offboarded','Disabled','OnLeave')");
        });
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnType("int").IsRequired().UseIdentityColumn();
        entity.Property(x => x.LegalFirstName).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
        entity.Property(x => x.LegalLastName).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
        entity.Property(x => x.PreferredName).HasColumnType("nvarchar(100)").HasMaxLength(100);
        entity.Property(x => x.Username).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
        entity.Property(x => x.Email).HasColumnType("nvarchar(200)").HasMaxLength(200).IsRequired();
        entity.Property(x => x.ManagerId).HasColumnType("int");
        entity.Property(x => x.EmploymentType).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
        entity.Property(x => x.StartDate).HasColumnType("date");
        entity.Property(x => x.EndDate).HasColumnType("date");
        entity.Property(x => x.VerificationStatus).HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("PendingHR");
        entity.Property(x => x.ImportedAt).HasColumnType("datetime2");
        entity.Property(x => x.VerifiedByUPN).HasColumnType("nvarchar(200)").HasMaxLength(200);
        entity.Property(x => x.VerifiedAt).HasColumnType("datetime2");
        entity.Property(x => x.ADObjectGuid).HasColumnType("uniqueidentifier");
        entity.Property(x => x.EntraObjectId).HasColumnType("nvarchar(36)").HasMaxLength(36);
        entity.Property(x => x.Status).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired().HasDefaultValue("PendingOnboard");
        entity.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.UpdatedAt).HasColumnType("datetime2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.Username, "IX_Employees_Username").HasDatabaseName("IX_Employees_Username");
        entity.HasIndex(x => x.Email, "IX_Employees_Email").HasDatabaseName("IX_Employees_Email");
        entity.HasIndex(x => x.ManagerId, "IX_Employees_ManagerId").HasDatabaseName("IX_Employees_ManagerId");
        entity.HasIndex(x => x.Username, "UX_Employees_Username").HasDatabaseName("UX_Employees_Username").IsUnique();
        entity.HasIndex(x => x.Email, "UX_Employees_Email").HasDatabaseName("UX_Employees_Email").IsUnique();
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Employees_ManagerId");
    }
}
