using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DaHub.API.Authentication;
using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class EnrollmentTests
{
    internal static readonly OktaSettings Settings = new() { Enabled = true, Issuer = "https://synthetic.okta.com" };
    internal static EnrollIdentity Input(string subject = "new-subject", int? user = null) =>
        new(user, "Synthetic user", "same@example.invalid", Settings.Issuer, subject, "verification:test-001");

    private sealed class Fixture : IDisposable
    {
        public readonly WebApplicationFactory<Program> Factory;
        public readonly HttpClient Client;
        public Fixture(int role = 5)
        {
            var name = Guid.NewGuid().ToString();
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DaHubDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<DaHubDbContext>>();
                services.AddDbContext<DaHubDbContext>(options => options.UseInMemoryDatabase(name)
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
                services.Replace(ServiceDescriptor.Singleton(Settings));
                services.AddSingleton<IStartupFilter, OrganizationSeedTests.Login>();
            }));
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var scope = Factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
            database.Database.EnsureCreated();
            database.AppUsers.Add(new AppUser { Id = 1, DisplayName = "Synthetic administrator" });
            database.AppExternalIdentities.Add(new AppExternalIdentity
            { AppUserId = 1, ProviderKey = "Okta", Issuer = Settings.Issuer, Subject = "seed-subject", LinkMethod = "Bootstrap" });
            database.AppUserRoles.Add(new AppUserRole { UserId = 1, RoleId = role, AssignedBy = "system:test" });
            database.SaveChanges();
        }
        public async Task LoginAsync()
        {
            using var login = await Client.GetAsync("/test/seed-login");
            var csrf = await Client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
            Client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        }
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }

    [Fact]
    public async Task EnrollmentRequiresCurrentPermissionAndCsrf()
    {
        using var fixture = new Fixture();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/admin/identities/", Input())).StatusCode);
        using var login = await fixture.Client.GetAsync("/test/seed-login");
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/admin/identities/", Input())).StatusCode);
        await fixture.LoginAsync();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PostAsJsonAsync("/api/admin/identities/", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync("/api/admin/identities/", Input())).StatusCode);
        using var denied = new Fixture(1);
        await denied.LoginAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.Client.PostAsJsonAsync("/api/admin/identities/", Input())).StatusCode);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("subject")]
    [InlineData("evidence")]
    [InlineData("name")]
    [InlineData("upn")]
    public async Task InvalidEnrollmentFailsWithoutCreatingUsers(string field)
    {
        using var fixture = new Fixture();
        await fixture.LoginAsync();
        var input = Input();
        input = field switch
        {
            "issuer" => input with { Issuer = "https://untrusted.invalid" },
            "subject" => input with { Subject = "padded " },
            "evidence" => input with { VerificationReference = "" },
            "name" => input with { DisplayName = new string('x', 201) },
            _ => input with { UPN = "bad\ncontact" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/admin/identities/", input)).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DaHubDbContext>().AppUsers.CountAsync());
    }

    [Fact]
    public async Task ExplicitLinksPreserveRolesContactAndStableLoginHistory()
    {
        using var fixture = new Fixture();
        await fixture.LoginAsync();
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
        var identities = scope.ServiceProvider.GetRequiredService<LocalIdentity>();
        var actor = (await identities.SignInAsync(Settings.Issuer, "seed-subject", default))!;
        var service = scope.ServiceProvider.GetRequiredService<EnrollmentService>();
        var userId = await service.EnrollAsync(Input(), actor, default);
        await service.EnrollAsync(Input("second-subject", userId), actor, default);
        await service.EnrollAsync(Input("NEW-SUBJECT"), actor, default);
        var user = await database.AppUsers.SingleAsync(x => x.Id == userId);
        user.UPN = "changed@example.invalid";
        await database.SaveChangesAsync();
        var first = await identities.SignInAsync(Settings.Issuer, "new-subject", default);
        var second = await identities.SignInAsync(Settings.Issuer, "second-subject", default);
        Assert.Equal(first!.FindFirstValue(ClaimTypes.NameIdentifier), second!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("changed@example.invalid", second!.FindFirstValue(ClaimTypes.Upn));
        Assert.Equal(3, await database.AppUsers.CountAsync());
        Assert.Empty(await database.AppUserRoles.Where(x => x.UserId == userId).ToArrayAsync());
        Assert.Equal(2, await database.AuditLog.CountAsync(x => x.ActionType == "Login" && x.ActorUserId == userId));
        Assert.NotNull(user.LastLoginAt);
        Assert.Null(await identities.SignInAsync(Settings.Issuer, "unknown-subject", default));
        Assert.Equal(3, await database.AppUsers.CountAsync());
        Assert.All(await database.AppExternalIdentities.Where(x => x.AppUserId == userId).ToArrayAsync(), x => Assert.Equal(1, x.LinkedByUserId));
        Assert.Equal(3, await database.AuditLog.CountAsync(x => x.ActionType == "IdentityEnrolled" && x.ActorUserId == 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnrollAsync(Input("unauthorized"), new ClaimsPrincipal(), default));
        await Assert.ThrowsAsync<EnrollmentConflictException>(() => service.EnrollAsync(Input("missing-user", 999), actor, default));
        await Assert.ThrowsAsync<EnrollmentConflictException>(() => service.LinkEmployeeAsync(userId, new(999, "verification:test"), actor, default));
        await service.LinkEmployeeAsync(userId, new(null, "verification:test"), actor, default);
        var link = await database.AppExternalIdentities.SingleAsync(x => x.Subject == "new-subject");
        await service.RevokeAsync(link.Id, actor, default);
        Assert.Null(await identities.SignInAsync(Settings.Issuer, "new-subject", default));
        Assert.NotNull(await identities.SignInAsync(Settings.Issuer, "second-subject", default));
        await Assert.ThrowsAsync<EnrollmentConflictException>(() => service.EnrollAsync(Input(), actor, default));
        await Assert.ThrowsAsync<EnrollmentConflictException>(() => service.RevokeAsync(999, actor, default));
    }
}
