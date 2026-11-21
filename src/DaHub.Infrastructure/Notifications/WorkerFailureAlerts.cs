using DaHub.Core.Entities;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Persistence;

namespace DaHub.Infrastructure.Notifications;

public sealed class WorkerFailureAlerts(Dictionary<string, string> destinations)
{
    internal const string NotificationType = "WorkerFailure";

    internal bool IsApproved(NotificationMessage message) =>
        destinations.TryGetValue(message.Channel, out var recipient)
        && string.Equals(recipient, message.Recipient, StringComparison.Ordinal);

    internal void AddToQueue(DaHubDbContext database, BackgroundJob job, DateTime now)
    {
        // A failed notification must not generate another notification.
        if (job.JobType == "NotificationRetry") return;
        var body = FormattableString.Invariant($"Background job {job.Id} failed on attempt {job.AttemptCount}. Review the persisted job outcome before retrying.");
        foreach (var (channel, recipient) in destinations)
            database.BackgroundJobs.Add(NotificationService.CreateJob(
                new(channel, recipient, NotificationType, "DaHub worker failure", body, job.RequestId), now));
    }
}
