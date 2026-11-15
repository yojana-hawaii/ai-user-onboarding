using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class DatabaseReadinessTests
{
    [Fact]
    public async Task MissingDatabaseConfigurationFailsReadinessWithoutLeakingDetails()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DaHub"] = "",
                    ["Database:ConnectionStringFile"] = ""
                })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var live = await client.GetAsync("/api/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.True(live.Headers.CacheControl?.NoStore);
    }
}
