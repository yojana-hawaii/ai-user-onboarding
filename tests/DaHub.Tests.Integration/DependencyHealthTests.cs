using DaHub.API;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class DependencyHealthTests
{
    private sealed class Probe(bool available, bool throws = false) : IDirectoryConnectivity
    {
        public Task<bool> CheckAsync(bool ad, CancellationToken cancellationToken) => throws
            ? Task.FromException<bool>(new InvalidOperationException("synthetic private server detail")) : Task.FromResult(available);
    }

    private sealed class WaitingProbe : IDirectoryConnectivity
    {
        public async Task<bool> CheckAsync(bool ad, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return true;
        }
    }

    [Fact]
    public async Task ProbesBoundTheirDeadlineAndHonorCallerCancellation()
    {
        var check = new DirectoryHealthCheck(new DirectoryHealthSettings { Mode = "Live" }, new WaitingProbe(), true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var timedOut = await check.CheckHealthAsync(new HealthCheckContext(), deadline.Token);
        Assert.Equal(HealthStatus.Unhealthy, timedOut.Status);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.CheckHealthAsync(new HealthCheckContext(), cancelled.Token));
    }

    [Theory]
    [InlineData("Mock", true, false, HealthStatus.Healthy)]
    [InlineData("Unconfigured", true, false, HealthStatus.Unhealthy)]
    [InlineData("Live", true, false, HealthStatus.Healthy)]
    [InlineData("Live", false, false, HealthStatus.Unhealthy)]
    [InlineData("Live", false, true, HealthStatus.Unhealthy)]
    public async Task DirectoryHealthDistinguishesMockMissingHealthyAndUnavailable(string mode, bool available, bool throws, HealthStatus expected)
    {
        foreach (var ad in new[] { true, false })
        {
            var check = new DirectoryHealthCheck(new DirectoryHealthSettings { Mode = mode }, new Probe(available, throws), ad);
            var result = await check.CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(expected, result.Status);
            Assert.Null(result.Exception);
            Assert.DoesNotContain("private", result.Description ?? "");
        }
    }

    [Theory]
    [InlineData("Mock", false, "", "")]
    [InlineData("Invalid", true, "", "")]
    [InlineData("Live", true, "", "")]
    [InlineData("Live", true, "host.invalid", "not-a-guid")]
    public void InvalidDirectoryConfigurationFailsStartup(string mode, bool development, string host, string tenant)
    {
        Assert.Throws<InvalidOperationException>(() => new DirectoryHealthSettings
        { Mode = mode, AdHost = host, EntraTenantId = tenant }.Validate(development));
    }

    [Fact]
    public void ValidDirectorySettingsAndUnconfiguredProductionAreAccepted()
    {
        new DirectoryHealthSettings().Validate(false);
        new DirectoryHealthSettings { Mode = "Mock" }.Validate(true);
        new DirectoryHealthSettings { Mode = "Live", AdHost = "host.invalid", EntraTenantId = Guid.NewGuid().ToString() }.Validate(false);
    }

    [Theory]
    [InlineData(true, true, true, 200)]
    [InlineData(false, true, true, 503)]
    [InlineData(true, false, true, 503)]
    [InlineData(true, true, false, 503)]
    public async Task AggregateHealthRequiresEveryDependency(bool sql, bool ad, bool entra, int status)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                foreach (var pair in new[] { ("sql", sql), ("ad", ad), ("entra", entra) })
                    options.Registrations.Add(new HealthCheckRegistration(pair.Item1,
                        new StubCheck(pair.Item2), HealthStatus.Unhealthy, []));
            })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var path in new[] { "/health", "/api/health" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(status, (int)response.StatusCode);
        }
    }

    private sealed class StubCheck(bool healthy) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
    }

    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.Equal("login.microsoftonline.com", request.RequestUri.Host);
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task EntraProbeUsesCredentialFreeMetadataRequest(HttpStatusCode status, bool expected)
    {
        using var handler = new Handler(status);
        var probe = new DirectoryConnectivity(new DirectoryHealthSettings { EntraTenantId = Guid.NewGuid().ToString() }, handler);
        Assert.Equal(expected, await probe.CheckAsync(false, default));
    }
}
