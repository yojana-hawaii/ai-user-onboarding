using System.Net;
using System.Net.Mail;
using System.Text.Json;
using DaHub.Core.Notifications;
using DaHub.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class NotificationTransportTests
{
    [Theory]
    [InlineData("Teams", 200, DeliveryOutcome.Accepted)]
    [InlineData("Teams", 202, DeliveryOutcome.Accepted)]
    [InlineData("Slack", 200, DeliveryOutcome.Accepted)]
    [InlineData("Slack", 429, DeliveryOutcome.TransientFailure)]
    [InlineData("Teams", 503, DeliveryOutcome.TransientFailure)]
    [InlineData("Teams", 400, DeliveryOutcome.PermanentFailure)]
    [InlineData("Slack", 403, DeliveryOutcome.PermanentFailure)]
    [InlineData("Slack", 302, DeliveryOutcome.PermanentFailure)]
    [InlineData("Teams", 500, DeliveryOutcome.Uncertain)]
    [InlineData("Teams", 408, DeliveryOutcome.Uncertain)]
    public async Task WebhookPayloadAndResponseClassification(string channel, int status, DeliveryOutcome expected)
    {
        var http = new Http(status, "ok");
        using var client = new HttpClient(http);
        var transport = new WebhookTransport(channel, client, new Dictionary<string, Uri> { ["IT"] = new("https://example.invalid/synthetic") }, TimeProvider.System);
        var result = await transport.SendAsync(new(channel, "IT", "Synthetic", "Subject", "Body"), CancellationToken.None);
        Assert.Equal(expected, result.Outcome);
        using var json = JsonDocument.Parse(http.Payload!);
        if (channel == "Teams")
        {
            var card = json.RootElement.GetProperty("attachments")[0];
            Assert.Equal("application/vnd.microsoft.card.adaptive", card.GetProperty("contentType").GetString());
            Assert.Equal("Body", card.GetProperty("content").GetProperty("body")[1].GetProperty("text").GetString());
        }
        else Assert.Equal("Subject\nBody", json.RootElement.GetProperty("text").GetString());
        if (expected == DeliveryOutcome.TransientFailure) Assert.True(result.RetryAtUtc > DateTime.UtcNow.AddMinutes(9));
        Assert.Equal(DeliveryOutcome.PermanentFailure, (await transport.SendAsync(new(channel, "Unknown", "Synthetic", "S", "B"), CancellationToken.None)).Outcome);
        Assert.Equal(1, http.Calls);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("ok but not confirmed")]
    public async Task SlackRequiresExactAcknowledgement(string body)
    {
        using var client = new HttpClient(new Http(200, body));
        var sender = new WebhookTransport("Slack", client, new Dictionary<string, Uri> { ["IT"] = new("https://example.invalid/") }, TimeProvider.System);
        Assert.Equal(DeliveryOutcome.Uncertain, (await sender.SendAsync(new("Slack", "IT", "Synthetic", "S", "B"), CancellationToken.None)).Outcome);
    }

    [Theory]
    [InlineData(0, DeliveryOutcome.Accepted)]
    [InlineData(450, DeliveryOutcome.TransientFailure)]
    [InlineData(550, DeliveryOutcome.PermanentFailure)]
    public async Task SmtpUsesSingleApprovedRecipientAndEscapedHtml(int status, DeliveryOutcome expected)
    {
        var smtp = new Smtp(status);
        var sender = new SmtpTransport(smtp, "sender@example.invalid", new HashSet<string> { "synthetic@example.invalid" });
        Assert.Equal(expected, (await sender.SendAsync(new("Email", "synthetic@example.invalid", "Synthetic", "Subject", "<script>&"), CancellationToken.None)).Outcome);
        Assert.Equal(DeliveryOutcome.PermanentFailure, (await sender.SendAsync(new("Email", "other@example.invalid", "Synthetic", "S", "B"), CancellationToken.None)).Outcome);
        Assert.Equal(1, smtp.Calls);
    }

    [Fact]
    public async Task MockChannelsNeverClaimDelivery()
    {
        await using var services = new ServiceCollection().AddNotificationDelivery(new ConfigurationBuilder().Build()).BuildServiceProvider();
        foreach (var transport in services.GetServices<INotificationTransport>())
            Assert.Equal(DeliveryOutcome.PermanentFailure, (await transport.SendAsync(new(transport.Channel, "IT", "Synthetic", "S", "B"), CancellationToken.None)).Outcome);
        Assert.Equal(4, services.GetServices<INotificationTransport>().Count());
    }

    [Theory]
    [InlineData("SMS", "Live")]
    [InlineData("Teams", "Unknown")]
    [InlineData("Teams", "Live")]
    [InlineData("Email", "Live")]
    public void InvalidLiveConfigurationFailsStartup(string channel, string mode)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"Notifications:{channel}:Mode"] = mode }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddNotificationDelivery(config));
    }

    [Theory]
    [InlineData("Teams", "https://synthetic.logic.azure.com/synthetic")]
    [InlineData("Teams", "https://synthetic.environment.api.powerplatform.com/synthetic")]
    [InlineData("Slack", "https://hooks.slack.com/synthetic")]
    public void WebhookConfigurationResolvesSecretFileAndRejectsUnsafeEndpoints(string channel, string endpoint)
    {
        var path = Path.GetTempFileName();
        try
        {
            var values = new Dictionary<string, string?> { [$"Notifications:{channel}:Mode"] = "Live", [$"Notifications:{channel}:Destinations:IT:UrlFile"] = path };
            var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            File.WriteAllText(path, endpoint);
            using var services = new ServiceCollection().AddNotificationDelivery(config).BuildServiceProvider();
            Assert.Contains(services.GetServices<INotificationTransport>(), x => x.Channel == channel && x is WebhookTransport);
            foreach (var invalid in new[] { "http://hooks.slack.com/", "https://example.invalid/", "https://hooks.slack.com:8443/", "https://user@hooks.slack.com/", "https://hooks.slack.com/#secret" })
            {
                File.WriteAllText(path, invalid);
                Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddNotificationDelivery(config));
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SmtpConfigurationRequiresExplicitRelayAndRecipients()
    {
        var values = new Dictionary<string, string?> { ["Notifications:Email:Mode"] = "Live", ["Notifications:Email:Host"] = "relay.example.invalid", ["Notifications:Email:From"] = "sender@example.invalid", ["Notifications:Email:Recipients:0"] = "synthetic@example.invalid" };
        using var services = new ServiceCollection().AddNotificationDelivery(new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
        Assert.Contains(services.GetServices<INotificationTransport>(), x => x is SmtpTransport);
        values["Notifications:Email:From"] = "bad\r\nheader";
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddNotificationDelivery(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    private sealed class Http(int status, string body) : HttpMessageHandler
    {
        public string? Payload { get; private set; }
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(10));
            return response;
        }
    }

    private sealed class Smtp(int status) : ISmtpDelivery
    {
        public int Calls { get; private set; }
        public Task SendAsync(MailMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("synthetic@example.invalid", Assert.Single(message.To).Address);
            Assert.Equal("Subject", message.Subject);
            Assert.Contains("&lt;script&gt;&amp;", message.Body);
            Assert.True(message.IsBodyHtml);
            if (status != 0) throw new SmtpException((SmtpStatusCode)status, "sensitive provider response");
            return Task.CompletedTask;
        }
    }
}
