using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthenticationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task SessionRequiresAuthenticationWithoutRedirectingApiCalls()
    {
        using var response = await _client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("https://attacker.invalid/")]
    [InlineData("//attacker.invalid/")]
    [InlineData("/\\attacker.invalid/")]
    public async Task LoginRejectsExternalReturns(string returnUrl)
    {
        using var response = await _client.GetAsync("/api/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnconfiguredLoginFailsClosed()
    {
        using var response = await _client.GetAsync("/api/auth/login");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task LogoutCannotBeTriggeredByGet()
    {
        using var response = await _client.GetAsync("/api/auth/logout");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
