using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class ApiFoundationTests
{
    private sealed class Endpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/error", () => Task.FromException(new InvalidOperationException("sensitive synthetic detail"))).AllowAnonymous();
                endpoints.MapGet("/test/validation", () => Task.FromException(new FluentValidation.ValidationException("sensitive synthetic input"))).AllowAnonymous();
                endpoints.MapGet("/test/binding", (int count) => Results.Ok(count)).AllowAnonymous();
                endpoints.MapGet("/test/empty/{status:int}", (int status) => Results.StatusCode(status)).AllowAnonymous();
            });
        };
    }

    [Theory]
    [InlineData("/missing", 404)]
    [InlineData("/api/auth/login?returnUrl=https://untrusted.invalid", 400)]
    [InlineData("/api/auth/session", 401)]
    [InlineData("/test/error", 500)]
    [InlineData("/test/validation", 400)]
    [InlineData("/test/binding?count=invalid", 400)]
    [InlineData("/test/empty/403", 403)]
    [InlineData("/test/empty/429", 429)]
    public async Task ErrorsHaveSafeConsistentProblemDetails(string path, int expected)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, Endpoints>());
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Correlation-ID", "synthetic-correlation");
        request.Headers.Add("Accept", "text/html");
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, json.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("detail").GetString()));
        Assert.Equal("synthetic-correlation", json.GetProperty("correlationId").GetString());
        Assert.DoesNotContain("sensitive synthetic", json.ToString());
        Assert.Equal("synthetic-correlation", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Contains("max-age=31536000", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")));
    }

    [Fact]
    public async Task HttpRedirectsBeforeServingAnyEndpoint()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/health/live?probe=1");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("https://localhost/api/health/live?probe=1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task AggregateHealthReportsDependenciesWithoutSensitiveDetails()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", json.GetProperty("checks").GetProperty("sql").GetString());
        Assert.True(json.GetProperty("checks").TryGetProperty("ad", out _));
        Assert.True(json.GetProperty("checks").TryGetProperty("entra", out _));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("invalid correlation")]
    [InlineData("synthetic-valid-correlation")]
    public async Task StructuredLogsIncludeSafeContextAndRollingFileOutput(string correlation)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dahub-log-test-" + Guid.NewGuid());
        var sink = new CapturedLogEvents();
        try
        {
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Logging:Directory", directory);
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ILogEventSink>(sink);
                    services.AddSingleton<IStartupFilter, Endpoints>();
                });
            }))
            {
                using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
                using var request = new HttpRequestMessage(HttpMethod.Get, "/test/error?code=synthetic-sensitive-query");
                request.Headers.Add("X-Correlation-ID", correlation);
                request.Headers.Add("Authorization", "Bearer synthetic-sensitive-header");
                using var response = await client.SendAsync(request);
                var returned = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
                if (correlation.Contains(' ')) Assert.NotEqual(correlation, returned);
                else Assert.Equal(correlation, returned);
                var entry = Assert.Single(sink.Events);
                Assert.Equal(returned, ((ScalarValue)entry.Properties["CorrelationId"]).Value);
                Assert.Equal("anonymous", ((ScalarValue)entry.Properties["ActorUserId"]).Value);
                Assert.Equal("anonymous", ((ScalarValue)entry.Properties["ActorUPN"]).Value);
                Assert.Equal("/test/error", ((ScalarValue)entry.Properties["RequestPath"]).Value);
                Assert.Null(entry.Exception);
            }
            var output = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "*.jsonl")));
            Assert.Contains("CorrelationId", output);
            Assert.DoesNotContain("synthetic-sensitive", output);
            Assert.DoesNotContain("sensitive synthetic detail", output);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}

internal sealed class CapturedLogEvents : ILogEventSink
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();
    public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
}
