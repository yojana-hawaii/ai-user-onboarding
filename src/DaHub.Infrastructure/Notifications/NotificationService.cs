using System.Text.Json;
using DaHub.Core.Entities;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Persistence;

namespace DaHub.Infrastructure.Notifications;

public sealed class NotificationService(DaHubDbContext database, TimeProvider clock) : INotificationService
{
    public async Task<long> EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var job = CreateJob(message, clock.GetUtcNow().UtcDateTime);
        database.BackgroundJobs.Add(job);
        await database.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    internal static BackgroundJob CreateJob(NotificationMessage message, DateTime scheduledAt)
    {
        Validate(message);
        return new BackgroundJob
        {
            JobType = "NotificationRetry",
            RequestId = message.RequestId,
            Payload = JsonSerializer.Serialize(message),
            CreatedAt = scheduledAt,
            ScheduledAt = scheduledAt
        };
    }

    internal static void Validate(NotificationMessage message)
    {
        if (message.Channel is not ("Email" or "Teams" or "Slack" or "SMS") || !Valid(message.Recipient, 200)
            || !Valid(message.NotificationType, 80) || !Valid(message.Subject, 300)
            || string.IsNullOrWhiteSpace(message.Body) || message.Body.Length > 8000
            || message.RequestId is <= 0)
            throw new ArgumentException("Invalid notification.");
    }

    private static bool Valid(string? value, int length) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= length && !value.Any(char.IsControl);
}
