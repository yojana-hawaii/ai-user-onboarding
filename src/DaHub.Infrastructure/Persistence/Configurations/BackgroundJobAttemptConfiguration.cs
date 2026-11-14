using DaHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DaHub.Infrastructure.Persistence.Configurations;

public sealed class BackgroundJobAttemptConfiguration : IEntityTypeConfiguration<BackgroundJobAttempt>
{
    public void Configure(EntityTypeBuilder<BackgroundJobAttempt> entity)
    {
        entity.ToTable("BackgroundJobAttempts");
        entity.HasKey(x => new { x.JobId, x.Attempt });
        entity.Property(x => x.Outcome).HasMaxLength(20);
        entity.Property(x => x.ErrorCode).HasMaxLength(50);
        entity.HasOne<BackgroundJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.NoAction);
    }
}
