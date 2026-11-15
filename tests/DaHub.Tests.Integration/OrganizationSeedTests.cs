using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DaHub.API.Authentication;
using DaHub.API.OrganizationSeeds;
using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class OrganizationSeedTests
{
    internal const string Package = """
        {"schemaVersion":1,
         "locations":[{"siteCode":"SYNTHETIC","name":"Synthetic site","address":"Example address","phone":"000"}],
         "departments":[{"name":"Synthetic department","ouPath":"OU=Example,DC=example,DC=test"}],
         "jobTitles":[{"title":"Synthetic title","ehrAccessDefault":true}],
         "specialties":[{"name":"Synthetic specialty","description":"Example"}],
         "templates":[{"name":"Synthetic template","m365LicenseSku":"example-sku","description":"Example",
          "department":"Synthetic department","location":"SYNTHETIC","jobTitle":"Synthetic title","specialty":"Synthetic specialty",
          "ehrAccessRequired":true,"groups":[{"groupType":"AD_Security","groupName":"Example","groupIdentifier":"example-group","isRequired":true}]}]}
        """;

    public static TheoryData<string> InvalidPackages => new()
    {
        "null", "[]", "{", "{\"schemaVersion\":2}", "{\"schemaVersion\":1}",
        "{\"schemaVersion\":1,\"locations\":null}",
        "{\"schemaVersion\":1,\"users\":[]}",
        "{\"schemaVersion\":1,\"SchemaVersion\":1}",
        "{\"schemaVersion\":1,\"locations\":[null]}",
        "{\"schemaVersion\":1,\"locations\":[{\"siteCode\":\" A\",\"name\":\"Name\"}]}",
        "{\"schemaVersion\":1,\"locations\":[{\"siteCode\":\"A\",\"name\":\"Name\",\"secret\":\"private-marker\"}]}",
        "{\"schemaVersion\":1,\"locations\":[{\"siteCode\":\"A\",\"name\":\"Name\"},{\"siteCode\":\"a\",\"name\":\"Name\"}]}",
        "{\"schemaVersion\":1,\"departments\":[{\"name\":\"Dept\"}]}",
        "{\"schemaVersion\":1,\"templates\":[{\"name\":\"Template\",\"m365LicenseSku\":\"sku\",\"groups\":[{\"groupType\":\"Unknown\"}]}]}"
    };

    [Theory]
    [MemberData(nameof(InvalidPackages))]
    public void InvalidPackagesNeverEchoValues(string json)
    {
        var exception = Assert.Throws<SeedValidationException>(() => SeedPackage.Parse(json));
        Assert.DoesNotContain("private-marker", exception.Message);
    }

    [Fact]
    public void EnforcesRowGroupLengthAndControlCharacterLimits()
    {
        var many = "{\"schemaVersion\":1,\"locations\":" + JsonSerializer.Serialize(Enumerable.Range(0, 1001)
            .Select(i => new { siteCode = i.ToString(), name = "Example" })) + "}";
        Assert.Throws<SeedValidationException>(() => SeedPackage.Parse(many));
        var groups = "{\"schemaVersion\":1,\"templates\":[{\"name\":\"T\",\"m365LicenseSku\":\"sku\",\"groups\":"
            + JsonSerializer.Serialize(Enumerable.Range(0, 101).Select(i => new { groupType = "AD_Security", groupName = "Example", groupIdentifier = i.ToString() })) + "}]}";
        Assert.Throws<SeedValidationException>(() => SeedPackage.Parse(groups));
        Assert.Throws<SeedValidationException>(() => SeedPackage.Parse(Package.Replace("SYNTHETIC", new string('x', 21), StringComparison.Ordinal)));
        Assert.Throws<SeedValidationException>(() => SeedPackage.Parse(Package.Replace("Example address", "Bad\\nAddress", StringComparison.Ordinal)));
        Assert.Throws<SeedValidationException>(() => SeedPackage.Parse(Package.Replace("\"groups\":[{", "\"groups\":[null,{", StringComparison.Ordinal)));
    }

    internal sealed class Login : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, proceed) =>
            {
                if (context.Request.Path == "/test/seed-login")
                {
                    var principal = await context.RequestServices.GetRequiredService<LocalIdentity>()
                        .SignInAsync("https://synthetic.okta.com", "seed-subject", context.RequestAborted);
                    await context.SignInAsync(AuthRegistration.CookieScheme, principal!);
                    return;
                }
                await proceed(context);
            });
            next(app);
        };
    }

    private sealed class Fixture : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }
        public Fixture(int role = 5)
        {
            var name = Guid.NewGuid().ToString();
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DaHubDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<DaHubDbContext>>();
                services.AddDbContext<DaHubDbContext>(options => options.UseInMemoryDatabase(name));
                services.AddSingleton<IStartupFilter, Login>();
            }));
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            Change(database =>
            {
                database.Database.EnsureCreated();
                database.AppUsers.Add(new AppUser { Id = 1, DisplayName = "Synthetic seed admin" });
                database.AppExternalIdentities.Add(new AppExternalIdentity
                {
                    Id = 1,
                    AppUserId = 1,
                    ProviderKey = "Okta",
                    Issuer = "https://synthetic.okta.com",
                    Subject = "seed-subject",
                    LinkMethod = "Bootstrap"
                });
                database.AppUserRoles.Add(new AppUserRole { UserId = 1, RoleId = role, AssignedBy = "system:test" });
            });
        }
        public void Change(Action<DaHubDbContext> action)
        {
            using var scope = Factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
            action(database);
            database.SaveChanges();
        }
        public async Task SignIn() { using var login = await Client.GetAsync("/test/seed-login"); }
        public async Task<HttpResponseMessage> Post(string route, string content = Package, string? preview = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/organization-seeds/" + route);
            request.Content = new StringContent(content, Encoding.UTF8, "application/json");
            if (csrf)
            {
                var token = await Client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
                request.Headers.Add("X-XSRF-TOKEN", token.GetProperty("requestToken").GetString());
            }
            if (preview is not null) request.Headers.Add("X-Seed-Preview", preview);
            return await Client.SendAsync(request);
        }
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }

    [Fact]
    public async Task RequiresSessionBothPermissionsAndAntiforgery()
    {
        using var anonymous = new Fixture();
        using var denied = await anonymous.Post("preview", csrf: false);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        foreach (var role in new[] { 1, 2, 3, 4, 6, 7 })
        {
            using var fixture = new Fixture(role);
            await fixture.SignIn();
            using var forbidden = await fixture.Post("preview");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            using var apply = await fixture.Post("apply");
            Assert.Equal(HttpStatusCode.Forbidden, apply.StatusCode);
        }
        using var admin = new Fixture();
        await admin.SignIn();
        using var noCsrf = await admin.Post("preview", csrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var preview = await admin.Post("preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var payload = await preview.Content.ReadFromJsonAsync<SeedPreview>();
        Assert.Equal(new SeedSummary(5, 0), payload!.Summary);
        Assert.Equal("no-store", preview.Headers.CacheControl!.ToString());
        admin.Change(database => Assert.Empty(database.Locations));
        using var missing = await admin.Post("apply");
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        using var tampered = await admin.Post("apply", preview: "tampered");
        Assert.Equal(HttpStatusCode.Conflict, tampered.StatusCode);
        using var changed = await admin.Post("apply", Package.Replace("Example address", "Changed", StringComparison.Ordinal), payload.PreviewToken);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task RejectsInvalidOversizedAndNonJsonUploads()
    {
        using var fixture = new Fixture();
        await fixture.SignIn();
        using var invalid = await fixture.Post("preview", "{}");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var oversized = await fixture.Post("preview", new string(' ', SeedEndpoints.MaxBytes + 1));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        var token = await fixture.Client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
        fixture.Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token.GetProperty("requestToken").GetString());
        using var nonJson = await fixture.Client.PostAsync("/api/admin/organization-seeds/preview", new StringContent(Package));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, nonJson.StatusCode);
        using var badUtf8 = new ByteArrayContent([0xff]);
        badUtf8.Headers.ContentType = new("application/json");
        using var utf8 = await fixture.Client.PostAsync("/api/admin/organization-seeds/preview", badUtf8);
        Assert.Equal(HttpStatusCode.BadRequest, utf8.StatusCode);
    }

    [Fact]
    public async Task PreviewRejectsConflictsAmbiguityAndMissingReferences()
    {
        using var fixture = new Fixture();
        await fixture.SignIn();
        using var missing = await fixture.Post("preview", Package.Replace("\"department\":\"Synthetic department\"", "\"department\":\"Unknown\"", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        fixture.Change(database => database.Locations.Add(new Location { SiteCode = "SYNTHETIC", Name = "Edited" }));
        using var conflict = await fixture.Post("preview");
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        fixture.Change(database => database.Departments.AddRange(
            new Department { Name = "Ambiguous", OUPath = "OU=A" }, new Department { Name = "ambiguous", OUPath = "OU=B" }));
        using var ambiguous = await fixture.Post("preview", "{\"schemaVersion\":1,\"jobTitles\":[{\"title\":\"Example\"}]}");
        Assert.Equal(HttpStatusCode.BadRequest, ambiguous.StatusCode);
    }

    [Fact]
    public async Task ReceiptIsBoundToActorAndExpiry()
    {
        using var fixture = new Fixture();
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
        var clock = new SeedClock();
        var importer = new SeedImporter(database, new EphemeralDataProtectionProvider(), clock);
        var package = SeedPackage.Parse(Package);
        var preview = await importer.PreviewAsync(package, 1, default);
        await Assert.ThrowsAsync<SeedConflictException>(() => importer.ApplyAsync(package, preview.PreviewToken, 2, () => Task.FromResult(true), default));
        clock.Now = clock.Now.AddMinutes(11);
        await Assert.ThrowsAsync<SeedConflictException>(() => importer.ApplyAsync(package, preview.PreviewToken, 1, () => Task.FromResult(true), default));
    }

    private sealed class SeedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
