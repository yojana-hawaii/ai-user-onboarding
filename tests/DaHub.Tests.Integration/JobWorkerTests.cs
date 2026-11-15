using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class JobWorkerTests
{
    [Theory]
    [InlineData("ProvisioningRetry")]
    [InlineData("ApprovalReminder")]
    [InlineData("ApprovalEscalation")]
    [InlineData("SysAidSync")]
    [InlineData("NotificationRetry")]
    [InlineData("ScheduledTermination")]
    [InlineData("WorkerExpiry")]
    [InlineData("WorkerExpiryNotice")]
    public async Task DispatcherRunsRegisteredHandler(string type)
    {
        var handler = new Handler(type, JobResult.Success);
        var store = new Store(new BackgroundJob { Id = 1, JobType = type, AttemptCount = 1 });
        await RunAsync(store, handler);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(JobOutcome.Completed, Assert.Single(store.Results).Outcome);
    }

    [Fact]
    public async Task MissingUnknownAndDuplicateHandlersFailVisibly()
    {
        foreach (var type in new[] { "Unknown", "WorkerExpiry" })
        {
            var store = new Store(new BackgroundJob { JobType = type });
            await RunAsync(store);
            Assert.Equal(new JobResult(JobOutcome.Failed, "HandlerUnavailable"), Assert.Single(store.Results));
        }
        var duplicate = new Store(new BackgroundJob { JobType = "WorkerExpiry" });
        await RunAsync(duplicate, new Handler("WorkerExpiry", JobResult.Success), new Handler("WorkerExpiry", JobResult.Success));
        Assert.Equal(JobOutcome.Failed, Assert.Single(duplicate.Results).Outcome);
    }

    [Fact]
    public async Task FailureDoesNotStopIndependentWorkAndDoesNotPersistExceptionText()
    {
        var failed = new Handler("SysAidSync", null);
        var success = new Handler("ProvisioningRetry", JobResult.Success);
        var store = new Store(new BackgroundJob { JobType = failed.JobType }, new BackgroundJob { JobType = success.JobType });
        await RunAsync(store, failed, success);
        Assert.Equal(new JobResult(JobOutcome.Failed, "ReconciliationRequired"), store.Results[0]);
        Assert.Equal(JobOutcome.Completed, store.Results[1].Outcome);
    }

    [Fact]
    public async Task ExplicitTransientAndStaleResultsAndUntrustedCodesAreHandled()
    {
        foreach (var result in new[] { new JobResult(JobOutcome.Retry, "TransientFailure"),
            new JobResult(JobOutcome.Cancelled, "StaleExecution"), new JobResult(JobOutcome.Failed, "secret"),
            new JobResult((JobOutcome)999, "secret") })
        {
            var store = new Store(new BackgroundJob { JobType = "WorkerExpiry" });
            await RunAsync(store, new Handler("WorkerExpiry", result));
            Assert.DoesNotContain("secret", Assert.Single(store.Results).ToString());
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(3, 16)]
    public void RetryDelaysAreBounded(int attempt, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), JobRetryPolicy.Delay(attempt));
        Assert.Equal(4, new BackgroundJob { JobType = "SysAidSync" }.MaxAttempts);
    }

    [Fact]
    public void ExecutionIdentityAndExternalEvidenceRoundTripWithoutNormalizingActionKeys()
    {
        var identity = new JobExecutionIdentity("Synthetic-CaseSensitive-Action ", 10, 3, 20,
            new DateTime(2026, 11, 14, 20, 0, 0, DateTimeKind.Utc));
        var json = System.Text.Json.JsonSerializer.Serialize(identity);
        Assert.Equal(identity, System.Text.Json.JsonSerializer.Deserialize<JobExecutionIdentity>(json));
        foreach (var state in Enum.GetValues<ExternalResultState>())
        {
            var result = new JobExternalResult(identity.ActionKey, state, "synthetic-id", "evidence/synthetic");
            Assert.Equal(result, System.Text.Json.JsonSerializer.Deserialize<JobExternalResult>(System.Text.Json.JsonSerializer.Serialize(result)));
        }
    }
    [Fact]
    public void InvalidRetryNumberIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JobRetryPolicy.Delay(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => JobRetryPolicy.Delay(4));
    }

    [Fact]
    public void WorkerHostRegistersSeparateWorkerAndRejectsMissingDatabaseConfiguration()
    {
        using var host = DaHub.API.WorkerHost.Build([
            "--environment", "Development", "--Database:ConnectionStringFile", "",
            "--ConnectionStrings:DaHub", "Server=localhost;Database=Synthetic;User Id=synthetic;Password=synthetic;Encrypt=True;TrustServerCertificate=True"]);
        Assert.Single(host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>());
        using var scope = host.Services.CreateScope();
        Assert.IsType<SqlJobStore>(scope.ServiceProvider.GetRequiredService<IJobStore>());
        Assert.IsType<DaHub.Infrastructure.Notifications.NotificationRetryHandler>(Assert.Single(scope.ServiceProvider.GetServices<IJobHandler>()));
        Assert.Throws<InvalidOperationException>(() => DaHub.API.WorkerHost.Build([
            "--Database:ConnectionStringFile", "", "--ConnectionStrings:DaHub", ""]));
    }

    [Fact]
    public async Task ShutdownAfterHandlerStartsPersistsUncertainResultWithoutReplaying()
    {
        var handler = new CancellableHandler();
        var store = new Store(new BackgroundJob { JobType = handler.JobType });
        await using var provider = new ServiceCollection().AddSingleton<IJobStore>(store)
            .AddSingleton<IJobHandler>(handler).BuildServiceProvider();
        using var worker = new JobWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(new JobResult(JobOutcome.Failed, "ReconciliationRequired"), Assert.Single(store.Results));
    }

    private sealed class CancellableHandler : IJobHandler
    {
        public string JobType => "ScheduledTermination";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return JobResult.Success;
        }
    }
    private static async Task RunAsync(Store store, params IJobHandler[] handlers)
    {
        var services = new ServiceCollection().AddSingleton<IJobStore>(store);
        foreach (var handler in handlers) services.AddSingleton(handler);
        await using var provider = services.BuildServiceProvider();
        var worker = new JobWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        while (await worker.RunOnceAsync(CancellationToken.None)) { }
        Assert.True(store.Recovered);
    }

    private sealed class Handler(string type, JobResult? result) : IJobHandler
    {
        public string JobType => type;
        public int Calls { get; private set; }
        public Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken)
        {
            Calls++;
            return result is null ? throw new InvalidOperationException("sensitive external response") : Task.FromResult(result);
        }
    }

    private sealed class Store(params BackgroundJob[] jobs) : IJobStore
    {
        private readonly Queue<BackgroundJob> queue = new(jobs);
        public bool Recovered { get; private set; }
        public List<JobResult> Results { get; } = [];
        public Task<BackgroundJob?> ClaimAsync(CancellationToken cancellationToken) => Task.FromResult(queue.TryDequeue(out var job) ? job : null);
        public Task FinishAsync(BackgroundJob job, JobResult result, CancellationToken cancellationToken)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }
        public Task<int> RecoverAsync(CancellationToken cancellationToken)
        {
            Recovered = true;
            return Task.FromResult(0);
        }
    }
}


