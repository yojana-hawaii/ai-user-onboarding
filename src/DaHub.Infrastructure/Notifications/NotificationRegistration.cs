using System.Net.Mail;
using DaHub.Core.Jobs;
using DaHub.Core.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DaHub.Infrastructure.Notifications;

public static class NotificationRegistration
{
    public static IServiceCollection AddNotificationQueue(this IServiceCollection services) => services
        .AddSingleton(TimeProvider.System).AddScoped<INotificationService, NotificationService>();

    public static IServiceCollection AddNotificationDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotificationQueue();
        services.AddScoped<IJobHandler, NotificationRetryHandler>();
        foreach (var channel in new[] { "Teams", "Slack", "Email", "SMS" })
        {
            var section = configuration.GetSection("Notifications:" + channel);
            var mode = section["Mode"] ?? "Mock";
            if (mode == "Mock") { services.AddSingleton<INotificationTransport>(new MockNotificationTransport(channel)); continue; }
            if (mode != "Live" || channel == "SMS") throw new InvalidOperationException("Unsupported notification mode.");
            if (channel == "Email")
            {
                var host = section["Host"];
                if (Uri.CheckHostName(host) != UriHostNameType.Dns || !int.TryParse(section["Port"] ?? "25", out var port) || port is < 1 or > 65535)
                    throw new InvalidOperationException("Invalid SMTP relay configuration.");
                var from = Address(section["From"]);
                var recipients = section.GetSection("Recipients").GetChildren().Select(x => Address(x.Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (recipients.Count == 0) throw new InvalidOperationException("Approved email recipients are required.");
                services.AddSingleton<INotificationTransport>(new SmtpTransport(new SmtpRelayDelivery(host!, port), from, recipients));
                continue;
            }
            var destinations = new Dictionary<string, Uri>(StringComparer.Ordinal);
            foreach (var destination in section.GetSection("Destinations").GetChildren())
            {
                var path = destination["UrlFile"];
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Webhook secret file is required.");
                var value = File.ReadAllText(path).Trim();
                if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"
                    || endpoint.Port != 443 || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Fragment)
                    || !ApprovedHost(channel, endpoint.Host)) throw new InvalidOperationException("Invalid webhook endpoint.");
                destinations.Add(destination.Key, endpoint);
            }
            if (destinations.Count == 0) throw new InvalidOperationException("Approved webhook destinations are required.");
            // Construct directly to exclude request/URI logging handlers from HttpClientFactory.
            services.AddSingleton<INotificationTransport>(_ => new WebhookTransport(channel,
                new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                { Timeout = TimeSpan.FromSeconds(60) }, destinations, TimeProvider.System));
        }
        return services;
    }

    private static bool ApprovedHost(string channel, string host) => channel == "Slack"
        ? host == "hooks.slack.com"
        : host.EndsWith(".logic.azure.com", StringComparison.OrdinalIgnoreCase)
          || host.EndsWith(".environment.api.powerplatform.com", StringComparison.OrdinalIgnoreCase);

    private static string Address(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)
            || !MailAddress.TryCreate(value, out var address) || address.Address != value || value.Length > 200)
            throw new InvalidOperationException("Invalid notification email address.");
        return value;
    }
}
