using DaHub.Core.Jobs;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Jobs;
using DaHub.Infrastructure.Notifications;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class NotificationSqlAssertions
{
    public static async Task VerifyAsync(string connection)
    {
        await using var database = new DaHubDbContext(new DbContextOptionsBuilder<DaHubDbContext>().UseSqlServer(connection).Options);
        foreach (var channel in new[] { "Email", "Teams", "Slack", "SMS" })
        {
            // Roll back synthetic notification evidence before the broader migration rollback.
            await using var transaction = await database.Database.BeginTransactionAsync();
            var id = await new NotificationService(database, TimeProvider.System).EnqueueAsync(
                new(channel, "Synthetic", "SyntheticDelivery", "Subject", "Body"), CancellationToken.None);
            var job = await database.BackgroundJobs.SingleAsync(x => x.Id == id);
            job.AttemptCount = 1;
            var handler = new NotificationRetryHandler(database, [new Sender(channel)], TimeProvider.System);
            for (var attempt = 1; attempt <= 4; attempt++)
            {
                job.AttemptCount = attempt;
                Assert.Equal(attempt < 4 ? JobOutcome.Retry : JobOutcome.Failed,
                    (await handler.ExecuteAsync(job, CancellationToken.None)).Outcome);
            }
            var logs = await database.NotificationLog.Where(x => x.NotificationType == "SyntheticDelivery").OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(new[] { 1, 2, 3, 4 }, logs.Select(x => x.AttemptCount));
            Assert.Equal(new[] { "Retrying", "Retrying", "Retrying", "Failed" }, logs.Select(x => x.Status));
            Assert.All(logs, x => Assert.Null(x.DeliveredAt));
            await transaction.RollbackAsync();
            database.ChangeTracker.Clear();
        }
        // Exercise persisted Retry-After through actual atomic claims and completion.
        var queue = new NotificationService(database, TimeProvider.System);
        var retryId = await queue.EnqueueAsync(new("SMS", "Synthetic", "SyntheticRetryAfter", "S", "B"), CancellationToken.None);
        var store = new SqlJobStore(database);
        var claim = (await store.ClaimAsync(CancellationToken.None))!;
        Assert.Equal(retryId, claim.Id);
        var retryAt = DateTime.UtcNow.AddHours(1);
        await store.FinishAsync(claim, new(JobOutcome.Retry, "TransientFailure", retryAt), CancellationToken.None);
        Assert.Equal(retryAt, (await database.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == retryId)).ScheduledAt);
        await database.BackgroundJobs.Where(x => x.Id == retryId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Cancelled"));
    }

    private sealed class Sender(string channel) : INotificationTransport
    {
        public string Channel => channel;
        public Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken) => Task.FromResult(new DeliveryResult(DeliveryOutcome.TransientFailure));
    }
}
