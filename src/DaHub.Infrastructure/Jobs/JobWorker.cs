using DaHub.Core.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DaHub.Infrastructure.Jobs;

public sealed class JobWorker(IServiceScopeFactory scopes, ILogger<JobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOnceAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            {
                // Never log SQL/handler exception objects, payloads, or provider responses.
                logger.LogError("Worker storage unavailable; polling will resume");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
        var recovered = await store.RecoverAsync(cancellationToken);
        if (recovered > 0) logger.LogCritical("IT attention required: {Count} interrupted jobs need reconciliation", recovered);
        var job = await store.ClaimAsync(cancellationToken);
        if (job is null) return false;
        JobResult result;
        try
        {
            var handlers = scope.ServiceProvider.GetServices<IJobHandler>()
                .Where(handler => string.Equals(handler.JobType, job.JobType, StringComparison.Ordinal)).ToArray();
            if (!JobTypes.All.Contains(job.JobType) || handlers.Length != 1)
                result = new(JobOutcome.Failed, "HandlerUnavailable");
            else
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                result = await handlers[0].ExecuteAsync(job, timeout.Token).WaitAsync(timeout.Token);
                if (!Enum.IsDefined(result.Outcome)) result = new(JobOutcome.Failed, "InvalidResult");
                result = result with { Code = JobRetryPolicy.SafeCode(result.Code) };
            }
        }
        catch (Exception)
        {
            // An exception/cancellation may follow a successful external write. Never replay blindly.
            result = new(JobOutcome.Failed, "ReconciliationRequired");
        }
        // A shutdown must not discard the current outcome. A bounded independent token permits
        // persistence; if storage fails, the durable InProgress attempt is recovered later.
        using var persistence = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await store.FinishAsync(job, result, persistence.Token);
        if (result.Outcome == JobOutcome.Failed || result.Outcome == JobOutcome.Retry && job.AttemptCount >= job.MaxAttempts)
            logger.LogCritical("IT attention required: job {JobId} attempt {Attempt} failed", job.Id, job.AttemptCount);
        return true;
    }
}
