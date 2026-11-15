using System.Security.Claims;
using DaHub.API.Authentication;
using Microsoft.AspNetCore.Authentication;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class SessionStoreTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task ActivitySlidesExpiryAndThirtyMinutesIdleExpires()
    {
        var clock = new Clock();
        using var store = new ServerTicketStore(clock);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), "test");
        var key = await store.StoreAsync(ticket);
        clock.Now += TimeSpan.FromMinutes(29);
        Assert.NotNull(await store.RetrieveAsync(key));
        clock.Now += TimeSpan.FromMinutes(29);
        Assert.NotNull(await store.RetrieveAsync(key));
        clock.Now += TimeSpan.FromMinutes(30);
        Assert.Null(await store.RetrieveAsync(key));
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
    }

    [Fact]
    public async Task RemovedSessionsCannotBeReplayedOrResurrectedByRenewal()
    {
        using var store = new ServerTicketStore(TimeProvider.System);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), "test");
        var key = await store.StoreAsync(ticket);
        await store.RenewAsync(key, ticket);
        Assert.NotNull(await store.RetrieveAsync(key));
        await store.RemoveAsync(key);
        await store.RenewAsync(key, ticket);
        Assert.Null(await store.RetrieveAsync(key));
        Assert.Null(await store.RetrieveAsync("unknown"));
    }

    [Theory]
    [InlineData("http://example.okta.com", "https://localhost:4200")]
    [InlineData("https://example.okta.com/", "https://localhost:4200")]
    [InlineData("https://example.okta.com", "http://localhost:4200")]
    [InlineData("https://example.okta.com", "https://localhost:4200/path")]
    [InlineData("https://example.okta.com", "https://user@localhost:4200")]
    public void RejectsUnsafeConfiguration(string issuer, string origin)
    {
        var settings = new OktaSettings { Issuer = issuer, PublicOrigin = origin, ClientId = "test", ClientSecretFile = "test" };
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }
}
