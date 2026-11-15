namespace DaHub.Core.Notifications;

// Body is plain text. Callers must supply only approved notification content, never
// credentials, patient data, raw provider errors or actionable approval authority.
public sealed record NotificationMessage(string Channel, string Recipient, string NotificationType,
    string Subject, string Body, int? RequestId = null);

public interface INotificationService
{
    // Persist intent before sending. Call within the caller's workflow transaction when
    // notification intent must commit atomically with a state transition.
    Task<long> EnqueueAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public enum DeliveryOutcome { Accepted, TransientFailure, PermanentFailure, Uncertain }
public sealed record DeliveryResult(DeliveryOutcome Outcome, DateTime? RetryAtUtc = null);

public interface INotificationTransport
{
    string Channel { get; }
    Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}
