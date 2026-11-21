using System.Text.Json;
using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Persistence;

namespace DaHub.Infrastructure.Notifications;

public sealed class NotificationRetryHandler(DaHubDbContext database,
    IEnumerable<INotificationTransport> transports, TimeProvider clock, WorkerFailureAlerts? failureAlerts = null) : IJobHandler
{
    public string JobType => "NotificationRetry";

    public async Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        NotificationMessage message;
        try
        {
            message = JsonSerializer.Deserialize<NotificationMessage>(job.Payload ?? "")!;
            if (message is null) return new(JobOutcome.Failed, "PermanentFailure");
            NotificationService.Validate(message);
            if (message.RequestId != job.RequestId || job.AttemptCount is < 1 or > 4 || job.MaxAttempts is < 1 or > 4)
                return new(JobOutcome.Failed, "PermanentFailure");
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            return new(JobOutcome.Failed, "PermanentFailure");
        }
        // Commit evidence BEFORE the external operation. An interruption leaves a visible
        // uncertain attempt; the worker never blindly replays an abandoned claim.
        var log = new NotificationLogEntry
        {
            RequestId = message.RequestId,
            Channel = message.Channel,
            Recipient = message.Recipient,
            Subject = message.Subject,
            NotificationType = message.NotificationType,
            AttemptCount = job.AttemptCount,
            Status = "Failed",
            ErrorDetail = "ReconciliationRequired",
            SentAt = clock.GetUtcNow().UtcDateTime
        };
        database.NotificationLog.Add(log);
        await database.SaveChangesAsync(cancellationToken);
        DeliveryResult delivery;
        try
        {
            var senders = transports.Where(x => x.Channel == message.Channel).ToArray();
            var approved = message.NotificationType != WorkerFailureAlerts.NotificationType
                || failureAlerts?.IsApproved(message) == true;
            delivery = approved && senders.Length == 1
                ? await senders[0].SendAsync(message, cancellationToken) : new(DeliveryOutcome.PermanentFailure);
        }
        catch (Exception) { delivery = new(DeliveryOutcome.Uncertain); }
        var outcome = delivery.Outcome;
        var retry = outcome == DeliveryOutcome.TransientFailure && job.AttemptCount < job.MaxAttempts;
        log.Status = outcome == DeliveryOutcome.Accepted ? "Sent" : retry ? "Retrying" : "Failed";
        log.ErrorDetail = outcome switch
        {
            DeliveryOutcome.Accepted => null,
            DeliveryOutcome.TransientFailure => "TransientFailure",
            DeliveryOutcome.PermanentFailure => "PermanentFailure",
            _ => "ReconciliationRequired"
        };
        if (outcome == DeliveryOutcome.Accepted) log.DeliveredAt = clock.GetUtcNow().UtcDateTime;
        using var persistence = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await database.SaveChangesAsync(persistence.Token);
        return new(outcome == DeliveryOutcome.Accepted ? JobOutcome.Completed : retry ? JobOutcome.Retry : JobOutcome.Failed, log.ErrorDetail, retry ? delivery.RetryAtUtc : null);
    }
}
