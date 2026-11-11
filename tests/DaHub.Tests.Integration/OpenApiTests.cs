using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class OpenApiTests
{
    [Fact]
    public async Task DevelopmentPublishesDocumentAndBundledSwaggerUi()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/health/live").GetProperty("get");
        Assert.Equal("Liveness", operation.GetProperty("operationId").GetString());
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(document.RootElement.GetProperty("paths").GetProperty("/api/health/ready")
            .GetProperty("get").GetProperty("responses").TryGetProperty("503", out _));
        Assert.Contains("Swagger UI", await client.GetStringAsync("/swagger/index.html"));
        Assert.Contains("/openapi/v1.json", await client.GetStringAsync("/swagger/index.js"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task DocumentationIsNotPublishedOutsideDevelopment(string environment)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.UseEnvironment(environment));
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/openapi/v1.json", "/swagger/index.html", "/swagger/swagger-ui-bundle.js" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
