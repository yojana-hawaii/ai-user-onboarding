using System.Net;
using System.Net.Http.Json;
using DaHub.Core.Notifications;

namespace DaHub.Infrastructure.Notifications;

// URLs are resolved from protected configuration by destination alias. Never persist
// webhook URLs in jobs/logs, follow redirects, or enable HttpClient request logging.
public sealed class WebhookTransport(string channel, HttpClient client,
    IReadOnlyDictionary<string, Uri> destinations, TimeProvider clock) : INotificationTransport
{
    public string Channel => channel;

    public async Task<DeliveryResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (!destinations.TryGetValue(message.Recipient, out var endpoint)) return new(DeliveryOutcome.PermanentFailure);
        object payload = Channel == "Teams" ? new
        {
            type = "message",
            attachments = new[] { new { contentType = "application/vnd.microsoft.card.adaptive", contentUrl = (string?)null,
                content = new { type = "AdaptiveCard", version = "1.2", body = new[] {
                    new { type = "TextBlock", text = message.Subject, wrap = true },
                    new { type = "TextBlock", text = message.Body, wrap = true } } } } }
        } : new { text = message.Subject + "\n" + message.Body, mrkdwn = false, unfurl_links = false, unfurl_media = false };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(payload) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (Channel == "Teams" && response.IsSuccessStatusCode) return new(DeliveryOutcome.Accepted);
        if (Channel == "Slack" && response.StatusCode == HttpStatusCode.OK)
        {
            // Slack confirms acceptance with "ok". Do not persist the response body.
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var bytes = new byte[3];
            var count = await stream.ReadAtLeastAsync(bytes, 3, throwOnEndOfStream: false, cancellationToken);
            return new(count == 2 && bytes[0] == (byte)'o' && bytes[1] == (byte)'k'
                ? DeliveryOutcome.Accepted : DeliveryOutcome.Uncertain);
        }
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            var header = response.Headers.RetryAfter;
            var retryAt = header?.Date?.UtcDateTime;
            if (header?.Delta is { } delta) retryAt = clock.GetUtcNow().UtcDateTime + delta;
            return new(DeliveryOutcome.TransientFailure, retryAt);
        }
        return new((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout
            ? DeliveryOutcome.Uncertain : DeliveryOutcome.PermanentFailure);
    }
}
