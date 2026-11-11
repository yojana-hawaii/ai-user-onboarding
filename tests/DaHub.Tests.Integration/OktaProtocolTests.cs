using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using DaHub.API.Authentication;
using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class OktaProtocolTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Provider : HttpMessageHandler
    {
        public const string Issuer = "https://example.okta.com";
        public readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "synthetic-key" };
        public string Nonce { get; set; } = "";
        public string Failure { get; set; } = "";
        public string Subject { get; set; } = "enrolled-subject";
        public bool PkceReceived { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            PkceReceived = body.ContainsKey("code_verifier");
            var now = DateTime.UtcNow;
            var key = Failure == "signature" ? new RsaSecurityKey(RSA.Create(2048)) { KeyId = Key.KeyId } : Key;
            var token = new JwtSecurityToken(
                Failure == "issuer" ? "https://untrusted.invalid" : Issuer,
                Failure == "audience" ? "wrong-client" : "synthetic-client",
                [new Claim("sub", Subject), new Claim("iat", EpochTime.GetIntDate(now).ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
                    new Claim("nonce", Failure == "nonce" ? "wrong-nonce" : Nonce),
                    new Claim("email", "same-email@example.invalid"), new Claim("groups", "Admin")],
                now.AddHours(-2), Failure == "expiry" ? now.AddHours(-1) : now.AddMinutes(5),
                new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id_token = new JwtSecurityTokenHandler().WriteToken(token), access_token = "unused", token_type = "Bearer" })
            };
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Provider Provider = new();
        public readonly Clock Clock = new();
        public readonly WebApplicationFactory<Program> Factory;
        public readonly HttpClient Client;
        private readonly string _secret = Path.GetTempFileName();
        private readonly string _database = Guid.NewGuid().ToString();

        public Fixture()
        {
            File.WriteAllText(_secret, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                foreach (var setting in new Dictionary<string, string>
                {
                    ["Okta:Enabled"] = "true",
                    ["Okta:Issuer"] = Provider.Issuer,
                    ["Okta:ClientId"] = "synthetic-client",
                    ["Okta:ClientSecretFile"] = _secret,
                    ["Okta:PublicOrigin"] = "https://localhost"
                })
                    builder.UseSetting(setting.Key, setting.Value);
                builder.ConfigureServices(services =>
                {
                    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
                    services.RemoveAll<DbContextOptions<DaHubDbContext>>();
                    services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<DaHubDbContext>>();
                    services.AddDbContext<DaHubDbContext>(options => options.UseInMemoryDatabase(_database));
                    services.PostConfigure<OpenIdConnectOptions>(AuthRegistration.OktaScheme, options =>
                    {
                        var configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = Provider.Issuer,
                            AuthorizationEndpoint = Provider.Issuer + "/authorize",
                            TokenEndpoint = Provider.Issuer + "/token",
                            EndSessionEndpoint = Provider.Issuer + "/logout"
                        };
                        configuration.SigningKeys.Add(Provider.Key);
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                        options.Backchannel = new HttpClient(Provider);
                        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                    });
                });
            });
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            using var scope = Factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
            database.AppUsers.Add(new AppUser { Id = 1, DisplayName = "Synthetic User", UPN = "same-email@example.invalid" });
            database.AppExternalIdentities.Add(new AppExternalIdentity
            {
                Id = 1,
                AppUserId = 1,
                ProviderKey = "Okta",
                Issuer = Provider.Issuer,
                Subject = "enrolled-subject",
                LinkMethod = "Bootstrap"
            });
            database.SaveChanges();
        }

        public async Task<HttpResponseMessage> LoginAsync(bool omitCookies = false)
        {
            using var login = await Client.GetAsync("/api/auth/login?returnUrl=%2Frequested-route");
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
            Assert.Equal("code", query["response_type"]);
            Assert.Equal("S256", query["code_challenge_method"]);
            Assert.Equal("https://localhost/api/auth/callback", query["redirect_uri"]);
            Provider.Nonce = query["nonce"].ToString();
            var state = Provider.Failure == "state" ? "tampered" : query["state"].ToString();
            if (omitCookies)
            {
                using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });
                return await client.PostAsync("/api/auth/callback", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = "synthetic-code",
                    ["state"] = state
                }));
            }
            return await Client.PostAsync("/api/auth/callback", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "synthetic-code",
                ["state"] = state
            }));
        }

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
            Provider.Key.Rsa.Dispose();
            File.Delete(_secret);
        }
    }

    [Fact]
    public async Task ValidCodeCreatesOpaqueSessionAndCsrfProtectedLogoutRevokesIt()
    {
        using var fixture = new Fixture();
        using var callback = await fixture.LoginAsync();
        Assert.Equal("https://localhost/requested-route", callback.Headers.Location?.ToString());
        Assert.True(fixture.Provider.PkceReceived);
        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-DaHub.Session=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic", cookie);
        using var session = await fixture.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(1, (await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appUserId").GetInt32());
        using var deniedLogout = await fixture.Client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, deniedLogout.StatusCode);
        using var forgedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        forgedRequest.Headers.Add("X-XSRF-TOKEN", "invalid-proof");
        using var forgedLogout = await fixture.Client.SendAsync(forgedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, forgedLogout.StatusCode);
        var csrf = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        using var logout = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.StartsWith(Provider.Issuer + "/logout?", logout.Headers.Location!.ToString());
        var query = QueryHelpers.ParseQuery(logout.Headers.Location.Query);
        Assert.False(string.IsNullOrEmpty(query["id_token_hint"]));
        Assert.Equal("https://localhost/api/auth/signed-out", query["post_logout_redirect_uri"]);
        using var signedOut = await fixture.Client.GetAsync("/api/auth/signed-out?state=" + Uri.EscapeDataString(query["state"].ToString()));
        Assert.Equal("https://localhost/", signedOut.Headers.Location?.ToString());
        using var unsolicited = await fixture.Client.GetAsync("/api/auth/signed-out");
        Assert.Equal(HttpStatusCode.BadRequest, unsolicited.StatusCode);
        using var replayClient = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        replayClient.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replayClient.GetAsync("/api/auth/session")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<DaHubDbContext>().AuditLog.SingleAsync();
        Assert.Equal(1, audit.ActorUserId);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("expiry")]
    [InlineData("nonce")]
    [InlineData("state")]
    public async Task InvalidProtocolResponseNeverCreatesSession(string failure)
    {
        using var fixture = new Fixture();
        fixture.Provider.Failure = failure;
        using var callback = await fixture.LoginAsync();
        Assert.Equal("/?authError=sign-in-failed", callback.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DaHubDbContext>().AuditLog.ToListAsync());
    }

    [Theory]
    [InlineData("unknown-subject")]
    [InlineData("ENROLLED-SUBJECT")]
    [InlineData("enrolled-subject ")]
    public async Task MatchingEmailAndProviderGroupsCannotEnrollUnknownIdentity(string subject)
    {
        using var fixture = new Fixture();
        fixture.Provider.Subject = subject;
        using var callback = await fixture.LoginAsync();
        Assert.Equal("/?authError=sign-in-failed", callback.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task MissingCorrelationCookieDeniesCallback()
    {
        using var fixture = new Fixture();
        using var callback = await fixture.LoginAsync(omitCookies: true);
        Assert.Equal("/?authError=sign-in-failed", callback.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task ApiActivitySlidesSessionUntilThirtyMinutesIdle()
    {
        using var fixture = new Fixture();
        using var callback = await fixture.LoginAsync();
        fixture.Clock.Now += TimeSpan.FromMinutes(29);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
        fixture.Clock.Now += TimeSpan.FromMinutes(29);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
        fixture.Clock.Now += TimeSpan.FromMinutes(30);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("identity")]
    [InlineData("revokedAt")]
    public async Task LocalRevocationDeniesTheNextRequest(string change)
    {
        using var fixture = new Fixture();
        using var callback = await fixture.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
            if (change == "identity") (await database.AppExternalIdentities.SingleAsync()).IsActive = false;
            else if (change == "revokedAt") (await database.AppExternalIdentities.SingleAsync()).RevokedAt = DateTime.UtcNow;
            else if (change == "user") (await database.AppUsers.SingleAsync()).IsActive = false;
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/session")).StatusCode);
    }
}
