using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaHub.API;

public sealed class DirectoryHealthSettings
{
    public string Mode { get; set; } = "Unconfigured";
    public string AdHost { get; set; } = "";
    public int AdPort { get; set; } = 636;
    public string EntraTenantId { get; set; } = "";

    public void Validate(bool development)
    {
        if (Mode is not ("Unconfigured" or "Mock" or "Live") || (!development && Mode == "Mock"))
            throw new InvalidOperationException("DirectoryHealth:Mode must be Live or Unconfigured outside Development.");
        if (Mode == "Live" && (Uri.CheckHostName(AdHost) == UriHostNameType.Unknown
            || AdPort is < 1 or > 65535 || !Guid.TryParse(EntraTenantId, out _)))
            throw new InvalidOperationException("Live directory health requires a valid AD host/port and Entra tenant ID.");
    }
}

// Read-only transport probes, separate from the privileged provisioning worker.
public interface IDirectoryConnectivity
{
    Task<bool> CheckAsync(bool ad, CancellationToken cancellationToken);
}

public sealed class DirectoryConnectivity(DirectoryHealthSettings settings, IHttpClientFactory clients) : IDirectoryConnectivity
{
    public async Task<bool> CheckAsync(bool ad, CancellationToken cancellationToken)
    {
        if (!ad)
        {
            using var client = clients.CreateClient("directory-health");
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://login.microsoftonline.com/{Guid.Parse(settings.EntraTenantId):D}/v2.0/.well-known/openid-configuration");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        using var connection = new TcpClient();
        await connection.ConnectAsync(settings.AdHost, settings.AdPort, cancellationToken);
        using var tls = new SslStream(connection.GetStream());
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = settings.AdHost,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
        }, cancellationToken);
        return tls.IsAuthenticated;
    }
}

public sealed class DirectoryHealthCheck(DirectoryHealthSettings settings, IDirectoryConnectivity connectivity, bool ad) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (settings.Mode == "Mock") return HealthCheckResult.Healthy("Mock");
        if (settings.Mode != "Live") return HealthCheckResult.Unhealthy("Unconfigured");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            return await connectivity.CheckAsync(ad, timeout.Token)
                ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Unavailable");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return HealthCheckResult.Unhealthy("Unavailable"); }
    }
}

public static class DependencyHealth
{
    public static void AddDependencyHealth(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("DirectoryHealth").Get<DirectoryHealthSettings>() ?? new();
        settings.Validate(builder.Environment.IsDevelopment());
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<IDirectoryConnectivity, DirectoryConnectivity>();
        builder.Services.AddHttpClient("directory-health").ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("sql", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
        foreach (var ad in new[] { true, false })
            builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(ad ? "ad" : "entra",
                services => new DirectoryHealthCheck(settings, services.GetRequiredService<IDirectoryConnectivity>(), ad),
                HealthStatus.Unhealthy, ["directory"]));
    }

    public static async Task<IResult> AggregateAsync(HttpContext context, HealthCheckService checks, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache";
        var report = await checks.CheckHealthAsync(cancellationToken);
        var statuses = report.Entries.ToDictionary(entry => entry.Key,
            entry => entry.Value.Description == "Mock" ? "Mock" : entry.Value.Status.ToString());
        return report.Status == HealthStatus.Healthy
            ? Results.Ok(new { status = "Healthy", checks = statuses })
            : Results.Problem(statusCode: 503, title: "Service Unavailable", detail: "One or more dependencies are unavailable.",
                extensions: new Dictionary<string, object?> { ["healthStatus"] = "Degraded", ["checks"] = statuses });
    }
}
