using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Notifications;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class NotificationTests
{
    [Theory]
    [InlineData(DeliveryOutcome.Accepted, 1, "Sent", JobOutcome.Completed)]
    [InlineData(DeliveryOutcome.TransientFailure, 1, "Retrying", JobOutcome.Retry)]
    [InlineData(DeliveryOutcome.TransientFailure, 4, "Failed", JobOutcome.Failed)]
    [InlineData(DeliveryOutcome.PermanentFailure, 1, "Failed", JobOutcome.Failed)]
    [InlineData(DeliveryOutcome.Uncertain, 1, "Failed", JobOutcome.Failed)]
    public async Task EveryAttemptHasSafeEvidence(DeliveryOutcome outcome, int attempt, string status, JobOutcome expected)
    {
        await using var database = Database();
        var transport = new Transport(outcome);
        var service = new NotificationService(database, TimeProvider.System);
        var id = await service.EnqueueAsync(Message(), CancellationToken.None);
        var job = await database.BackgroundJobs.SingleAsync(x => x.Id == id);
        job.AttemptCount = attempt;
        var handler = new NotificationRetryHandler(database, [transport], TimeProvider.System);
        Assert.Equal(expected, (await handler.ExecuteAsync(job, CancellationToken.None)).Outcome);
        var log = await database.NotificationLog.SingleAsync();
        Assert.Equal(status, log.Status);
        Assert.Equal(attempt, log.AttemptCount);
        Assert.Equal(outcome == DeliveryOutcome.Accepted, log.DeliveredAt.HasValue);
        Assert.Equal("synthetic@example.invalid", log.Recipient);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task InterruptedDeliveryRetainsUncertainAttemptAndDoesNotLeakException()
    {
        await using var database = Database();
        await new NotificationService(database, TimeProvider.System).EnqueueAsync(Message(), CancellationToken.None);
        var job = await database.BackgroundJobs.SingleAsync();
        job.AttemptCount = 1;
        var handler = new NotificationRetryHandler(database, [new Transport(null)], TimeProvider.System);
        Assert.Equal("ReconciliationRequired", (await handler.ExecuteAsync(job, CancellationToken.None)).Code);
        var log = await database.NotificationLog.SingleAsync();
        Assert.Equal("Failed", log.Status);
        Assert.Equal("ReconciliationRequired", log.ErrorDetail);
        Assert.Null(log.DeliveredAt);
    }

    [Fact]
    public async Task QueuesDurablyBeforeDeliveryAndRejectsInvalidInput()
    {
        await using var database = Database();
        var service = new NotificationService(database, TimeProvider.System);
        await service.EnqueueAsync(Message(), CancellationToken.None);
        var job = await database.BackgroundJobs.SingleAsync();
        Assert.Equal("NotificationRetry", job.JobType);
        Assert.Equal(4, job.MaxAttempts);
        Assert.Empty(database.NotificationLog);
        foreach (var message in new[] { Message() with { Channel = "Other" }, Message() with { Subject = "bad\r\nheader" }, Message() with { Body = "" } })
            await Assert.ThrowsAsync<ArgumentException>(() => service.EnqueueAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task MalformedPayloadAndUnavailableTransportDoNotSend()
    {
        await using var database = Database();
        var handler = new NotificationRetryHandler(database, [], TimeProvider.System);
        Assert.Equal(JobOutcome.Failed, (await handler.ExecuteAsync(new BackgroundJob { JobType = "NotificationRetry", Payload = "{" }, CancellationToken.None)).Outcome);
        await new NotificationService(database, TimeProvider.System).EnqueueAsync(Message(), CancellationToken.None);
        var job = await database.BackgroundJobs.SingleAsync();
        job.AttemptCount = 1;
        Assert.Equal(JobOutcome.Failed, (await handler.ExecuteAsync(job, CancellationToken.None)).Outcome);
        Assert.Equal("Failed", (await database.NotificationLog.SingleAsync()).Status);
    }

    [Fact]
    public async Task SuccessfulRetryPreservesEarlierFailureEvidence()
    {
        await using var database = Database();
        await new NotificationService(database, TimeProvider.System).EnqueueAsync(Message(), CancellationToken.None);
        var job = await database.BackgroundJobs.SingleAsync();
        job.AttemptCount = 1;
        await new NotificationRetryHandler(database, [new Transport(DeliveryOutcome.TransientFailure)], TimeProvider.System)
            .ExecuteAsync(job, CancellationToken.None);
        job.AttemptCount = 2;
        Assert.Equal(JobOutcome.Completed, (await new NotificationRetryHandler(database, [new Transport(DeliveryOutcome.Accepted)], TimeProvider.System)
            .ExecuteAsync(job, CancellationToken.None)).Outcome);
        var logs = await database.NotificationLog.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Equal("Retrying", logs[0].Status);
        Assert.Null(logs[0].DeliveredAt);
        Assert.Equal("Sent", logs[1].Status);
        Assert.NotNull(logs[1].DeliveredAt);
    }

    [Fact]
    public async Task FailedEvidenceWritePreventsExternalSend()
    {
        await using var database = new DaHubDbContext(new DbContextOptionsBuilder<DaHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(new FailedWrite()).Options);
        var transport = new Transport(DeliveryOutcome.Accepted);
        var job = new BackgroundJob
        {
            JobType = "NotificationRetry",
            AttemptCount = 1,
            Payload = System.Text.Json.JsonSerializer.Serialize(Message())
        };
        await Assert.ThrowsAsync<DbUpdateException>(() => new NotificationRetryHandler(database, [transport], TimeProvider.System)
            .ExecuteAsync(job, CancellationToken.None));
        Assert.Equal(0, transport.Calls);
    }

    private sealed class FailedWrite : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Synthetic storage outage");
    }

    private static NotificationMessage Message() => new("Email", "synthetic@example.invalid", "Synthetic", "Synthetic status", "Synthetic body");
    private static DaHubDbContext Database() => new(new DbContextOptionsBuilder<DaHubDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class Transport(DeliveryOutcome? outcome) : INotificationTransport
    {
        public string Channel => "Email";
        public int Calls { get; private set; }
        public Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            return outcome.HasValue ? Task.FromResult(new DeliveryResult(outcome.Value)) : throw new InvalidOperationException("sensitive provider response");
        }
    }
}
