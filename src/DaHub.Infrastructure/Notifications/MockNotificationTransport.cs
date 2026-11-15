using DaHub.Core.Notifications;

namespace DaHub.Infrastructure.Notifications;

// Deliberately never reports delivery. Tests inject synthetic outcomes explicitly.
// SMS retains this provider-neutral boundary until a provider is selected.
public sealed class MockNotificationTransport(string channel) : INotificationTransport
{
    public string Channel => channel;
    public Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken) =>
        Task.FromResult(new DeliveryResult(DeliveryOutcome.PermanentFailure));
}
