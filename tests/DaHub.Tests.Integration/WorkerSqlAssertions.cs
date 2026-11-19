using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Jobs;
using DaHub.Infrastructure.Notifications;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class WorkerSqlAssertions
{
    public static async Task VerifyAsync(string connectionString)
    {
        DbContextOptions<DaHubDbContext> Options() => new DbContextOptionsBuilder<DaHubDbContext>().UseSqlServer(connectionString).Options;
        await using var seed = new DaHubDbContext(Options());
        // Other schema assertions may have queued fixture jobs. Isolate this probe without deleting evidence.
        await seed.BackgroundJobs.Where(x => x.Status == "Queued").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Cancelled"));
        var parent = await seed.Requests.AsNoTracking().FirstAsync();
        var originalParent = System.Text.Json.JsonSerializer.Serialize(parent);
        var job = new BackgroundJob { JobType = "ProvisioningRetry", RequestId = parent.Id, ScheduledAt = DateTime.UtcNow.AddMinutes(-1) };
        var future = new BackgroundJob { JobType = "WorkerExpiry", ScheduledAt = DateTime.UtcNow.AddDays(1) };
        seed.AddRange(job, future);
        await seed.SaveChangesAsync();
        async Task<BackgroundJob?> ClaimAsync()
        {
            await using var db = new DaHubDbContext(Options());
            return await new SqlJobStore(db).ClaimAsync(CancellationToken.None);
        }
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ClaimAsync()));
        var claim = Assert.Single(claims.OfType<BackgroundJob>());
        Assert.Equal(job.Id, claim.Id);
        Assert.Equal(1, claim.AttemptCount);
        Assert.Equal(4, claim.MaxAttempts);
        Assert.Single(await seed.BackgroundJobAttempts.Where(x => x.JobId == job.Id).ToListAsync());
        var services = new ServiceCollection();
        services.AddDbContext<DaHubDbContext>(builder => builder.UseSqlServer(connectionString));
        services.AddScoped<IJobStore, SqlJobStore>();
        services.AddNotificationDelivery(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Notifications:WorkerFailure:Teams"] = "SyntheticIT" }).Build());
        var transport = new TestTransport();
        services.RemoveAll<INotificationTransport>();
        services.AddSingleton<INotificationTransport>(transport);
        await using var provider = services.BuildServiceProvider();
        using var worker = new JobWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        var store = new SqlJobStore(seed, provider.GetRequiredService<WorkerFailureAlerts>());
        async Task VerifyAlertAsync(long failedId, int? requestId, bool accepted = false)
        {
            var alert = await seed.BackgroundJobs.AsNoTracking()
                .SingleAsync(x => x.JobType == "NotificationRetry" && x.Status == "Queued");
            Assert.Equal(requestId, alert.RequestId);
            var message = System.Text.Json.JsonSerializer.Deserialize<NotificationMessage>(alert.Payload!);
            Assert.NotNull(message);
            Assert.Equal("SyntheticIT", message.Recipient);
            Assert.Equal("WorkerFailure", message.NotificationType);
            Assert.Contains($"Background job {failedId} failed", message.Body);
            transport.Outcome = accepted ? DeliveryOutcome.Accepted : DeliveryOutcome.PermanentFailure;
            transport.LastMessage = null;
            Assert.True(await worker.RunOnceAsync(CancellationToken.None));
            Assert.Equal(message, transport.LastMessage);
            var saved = await seed.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == alert.Id);
            Assert.Equal(accepted ? "Completed" : "Failed", saved.Status);
            var delivery = await seed.NotificationLog.AsNoTracking().Where(x => x.NotificationType == "WorkerFailure").OrderByDescending(x => x.Id).FirstAsync();
            Assert.Equal(accepted ? "Sent" : "Failed", delivery.Status);
            Assert.Equal(accepted, delivery.DeliveredAt.HasValue);
            Assert.False(await worker.RunOnceAsync(CancellationToken.None));
        }
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await store.FinishAsync(claim, new JobResult(JobOutcome.Retry, "TransientFailure"), CancellationToken.None);
            var persisted = await seed.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id);
            var evidence = await seed.BackgroundJobAttempts.AsNoTracking().SingleAsync(x => x.JobId == job.Id && x.Attempt == attempt);
            Assert.Equal(attempt, persisted.AttemptCount);
            Assert.NotNull(evidence.CompletedAt);
            if (attempt < 4)
            {
                Assert.Equal("Queued", persisted.Status);
                Assert.Equal("Retry", evidence.Outcome);
                Assert.Equal(persisted.ScheduledAt, evidence.RetryAt);
                Assert.InRange(persisted.ScheduledAt - evidence.CompletedAt!.Value,
                    JobRetryPolicy.Delay(attempt) - TimeSpan.FromSeconds(1), JobRetryPolicy.Delay(attempt) + TimeSpan.FromSeconds(1));
                Assert.Null(await ClaimAsync());
                await seed.BackgroundJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ScheduledAt, DateTime.UtcNow.AddSeconds(-1)));
                claim = (await ClaimAsync())!;
            }
            else
            {
                Assert.Equal("Failed", persisted.Status);
                Assert.Equal("Failed", evidence.Outcome);
                Assert.Null(evidence.RetryAt);
                await Assert.ThrowsAsync<InvalidOperationException>(() => store.FinishAsync(claim, new(JobOutcome.Failed), CancellationToken.None));
                await VerifyAlertAsync(job.Id, parent.Id, accepted: true);
            }
        }
        Assert.Equal(4, await seed.BackgroundJobAttempts.CountAsync(x => x.JobId == job.Id));
        Assert.Equal(originalParent, System.Text.Json.JsonSerializer.Serialize(await seed.Requests.AsNoTracking().SingleAsync(x => x.Id == parent.Id)));
        var interrupted = new BackgroundJob { JobType = "SysAidSync", ScheduledAt = DateTime.UtcNow.AddMinutes(-1) };
        seed.Add(interrupted);
        await seed.SaveChangesAsync();
        var abandoned = (await ClaimAsync())!;
        Assert.Equal(0, await store.RecoverAsync(CancellationToken.None));
        await seed.BackgroundJobs.Where(x => x.Id == abandoned.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.StartedAt, DateTime.UtcNow.AddMinutes(-6)));
        Assert.Equal(1, await store.RecoverAsync(CancellationToken.None));
        Assert.Equal(0, await store.RecoverAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.FinishAsync(abandoned, JobResult.Success, CancellationToken.None));
        Assert.Equal("ReconciliationRequired", (await seed.BackgroundJobAttempts.AsNoTracking().SingleAsync(x => x.JobId == abandoned.Id)).ErrorCode);
        await VerifyAlertAsync(abandoned.Id, null);
        foreach (var outcome in new[] { JobOutcome.Completed, JobOutcome.Cancelled, JobOutcome.Failed })
        {
            var terminal = new BackgroundJob { JobType = "ApprovalReminder", ScheduledAt = DateTime.UtcNow.AddMinutes(-1) };
            seed.Add(terminal);
            await seed.SaveChangesAsync();
            var current = (await ClaimAsync())!;
            await store.FinishAsync(current, new JobResult(outcome, "sensitive value"), CancellationToken.None);
            var saved = await seed.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == terminal.Id);
            Assert.Equal(outcome.ToString(), saved.Status);
            Assert.DoesNotContain("sensitive", saved.LastError ?? "");
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.FinishAsync(current, JobResult.Success, CancellationToken.None));
            if (outcome == JobOutcome.Failed) await VerifyAlertAsync(terminal.Id, null);
            else Assert.Null(await ClaimAsync());
        }
    }

    private sealed class TestTransport : INotificationTransport
    {
        public string Channel => "Teams";
        public DeliveryOutcome Outcome { get; set; }
        public NotificationMessage? LastMessage { get; set; }

        public Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            LastMessage = message;
            return Task.FromResult(new DeliveryResult(Outcome));
        }
    }
}

