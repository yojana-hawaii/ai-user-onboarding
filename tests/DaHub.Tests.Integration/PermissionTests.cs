using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using DaHub.API.Authentication;
using DaHub.API.Authorization;
using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class PermissionTests
{
    private sealed class Endpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, proceed) =>
            {
                if (context.Request.Path == "/test/login")
                {
                    var principal = await context.RequestServices.GetRequiredService<LocalIdentity>()
                        .SignInAsync("https://synthetic.okta.com", "subject", context.RequestAborted);
                    await context.SignInAsync(AuthRegistration.CookieScheme, principal!);
                    return;
                }
                await proceed(context);
            });
            next(app);
            app.UseEndpoints(endpoints =>
            {
                // Test-only read-only probes exercise resource policies over the real cookie/API pipeline.
                endpoints.MapGet("/test/authority/{policy}", async (HttpContext context, IAuthorizationService authorization, string policy) =>
                {
                    object? resource = null;
                    if (int.TryParse(context.Request.Query["manager"], out var manager))
                        resource = new DelegateAssignment(manager);
                    if (int.TryParse(context.Request.Query["request"], out var request)
                        && int.TryParse(context.Request.Query["revision"], out var revision))
                        resource = new UrgentAction(request, revision, context.Request.Query["reason"].ToString());
                    return (await authorization.AuthorizeAsync(context.User, resource, policy)).Succeeded
                        ? Results.Ok() : Results.Problem(statusCode: 403, title: "Forbidden", detail: "Authority denied.");
                }).RequireAuthorization();
                foreach (var permission in Keys.Concat(AllPermissions()).Distinct(StringComparer.Ordinal))
                    endpoints.MapGet("/test/" + permission, () => Results.Ok())
                        .WithMetadata(new RequirePermissionAttribute(permission));
            });
        };
    }

    private static readonly string[] Keys = ["requests.submit", "provisioning.execute", "audit.search", "retention.manage", "unknown.permission"];

    private static string[] AllPermissions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "access-seeds.json")));
        return document.RootElement.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()!).ToArray();
    }

    [Theory]
    [InlineData(1, "HR")]
    [InlineData(2, "IT")]
    [InlineData(3, "Manager")]
    [InlineData(4, "Executive")]
    [InlineData(5, "Admin")]
    [InlineData(6, "Compliance")]
    [InlineData(7, "UrgentOperator")]
    public async Task EverySeededPermissionIsEnforcedForEveryRole(int roleId, string role)
    {
        using var fixture = new Fixture(roleId);
        using var login = await fixture.Client.GetAsync("/test/login");
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "access-seeds.json")));
        var expected = document.RootElement.GetProperty("grants").EnumerateArray().Select(value => value.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in AllPermissions())
        {
            using var response = await fixture.Client.GetAsync("/test/" + permission);
            Assert.Equal(expected.Contains(role + ":" + permission) ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var session = await fixture.Client.GetAsync("/api/auth/session");
        using var payload = JsonDocument.Parse(await session.Content.ReadAsStringAsync());
        Assert.Equal(AllPermissions().Where(key => expected.Contains(role + ":" + key)).Order(StringComparer.Ordinal),
            payload.RootElement.GetProperty("permissions").EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal));
    }
    private sealed class Fixture : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }
        public CapturedLogEvents Logs { get; } = new();
        public ReadRace Race { get; } = new();
        public Fixture(params int[] roles) : this(false, roles) { }
        public Fixture(bool simulateRace, params int[] roles)
        {
            var name = Guid.NewGuid().ToString();
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DaHubDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<DaHubDbContext>>();
                services.AddDbContext<DaHubDbContext>(options =>
                {
                    options.UseInMemoryDatabase(name);
                    if (simulateRace) options.AddInterceptors(Race);
                });
                services.AddSingleton<IStartupFilter, Endpoints>();
                services.AddSingleton<Serilog.Core.ILogEventSink>(Logs);
            }));
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            Change(database =>
            {
                database.Database.EnsureCreated();
                database.AppUsers.Add(new AppUser { Id = 1, DisplayName = "Synthetic", UPN = "synthetic@example.invalid", EmployeeId = 12 });
                database.AppExternalIdentities.Add(new AppExternalIdentity { Id = 1, AppUserId = 1, ProviderKey = "Okta", Issuer = "https://synthetic.okta.com", Subject = "subject", LinkMethod = "Bootstrap" });
                foreach (var role in roles)
                    database.AppUserRoles.Add(new AppUserRole { UserId = 1, RoleId = role, AssignedBy = "synthetic" });
                database.Requests.Add(new WorkflowRequest { Id = 1, EmployeeId = 12, RequestType = "Onboarding", SubmittedByUPN = "synthetic@example.invalid", IsUrgent = true });
                database.RequestRevisions.Add(new RequestRevision { RequestId = 1, Revision = 1, SnapshotJson = "{}", CreatedByUPN = "synthetic@example.invalid", ChangeReason = "Synthetic" });
            });
        }
        public void Change(Action<DaHubDbContext> change)
        {
            using var scope = Factory.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<DaHubDbContext>();
            change(database);
            database.SaveChanges();
        }
        public async Task<bool> Allowed(string policy, object? resource)
        {
            using var login = await Client.GetAsync("/test/login");
            var query = resource switch
            {
                UrgentAction urgent => $"?request={urgent.RequestId}&revision={urgent.Revision}&reason={Uri.EscapeDataString(urgent.Justification)}",
                DelegateAssignment delegation => $"?manager={delegation.ManagerEmployeeId}",
                _ => ""
            };
            using var response = await Client.GetAsync("/test/authority/" + policy + query);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden });
            return response.StatusCode == HttpStatusCode.OK;
        }
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }

    private sealed class ReadRace : Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor
    {
        public Action? OnUserRead { get; set; }
        public object InitializedInstance(Microsoft.EntityFrameworkCore.Diagnostics.MaterializationInterceptionData data, object entity)
        {
            if (entity is AppUser && OnUserRead is { } action)
            {
                OnUserRead = null;
                action();
            }
            return entity;
        }
    }

    [Fact]
    public async Task ResolverUsesStableUserAndDistinctActiveLocalRoleUnion()
    {
        using var fixture = new Fixture(1, 2, 3);
        using var scope = fixture.Factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<PermissionResolver>();
        var permissions = await resolver.ResolveAsync(1, default);
        Assert.Contains("requests.submit", permissions);
        Assert.Contains("provisioning.execute", permissions);
        Assert.Contains("approvals.action", permissions);
        Assert.Equal(permissions.Length, permissions.Distinct().Count());
        Assert.Empty(await resolver.ResolveAsync(999, default));
        fixture.Change(database =>
        {
            database.AppRoles.Single(role => role.Id == 2).IsActive = false;
            database.AppPermissions.Single(permission => permission.Name == "requests.submit").IsActive = false;
        });
        var active = await resolver.ResolveAsync(1, default);
        Assert.DoesNotContain("provisioning.execute", active);
        Assert.DoesNotContain("requests.submit", active);
        fixture.Change(database => database.AppUsers.Single().IsActive = false);
        Assert.Empty(await resolver.ResolveAsync(1, default));
    }

    [Fact]
    public async Task RealCookieAndPermissionMetadataReturn401403AndMultiRole200()
    {
        using var fixture = new Fixture(1, 2);
        using var anonymous = await fixture.Client.GetAsync("/test/requests.submit");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var problem = await anonymous.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problem!.Type);
        Assert.NotNull(problem.Detail);
        using var login = await fixture.Client.GetAsync("/test/login");
        foreach (var key in Keys[..4])
        {
            using var response = await fixture.Client.GetAsync("/test/" + key);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var denied = await fixture.Client.GetAsync("/test/unknown.permission");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var forbidden = await denied.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal("Forbidden", forbidden!.Title);
        Assert.NotNull(forbidden.Detail);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("identity")]
    [InlineData("issuer")]
    [InlineData("subject")]
    public async Task ExistingCookieCannotAuthorizeAfterRevocation(string change)
    {
        using var fixture = new Fixture(2);
        using var login = await fixture.Client.GetAsync("/test/login");
        if (change is "issuer" or "subject")
        {
            // EF alternate keys are immutable; replace the synthetic row to simulate rebinding the same link ID.
            fixture.Change(database => database.AppExternalIdentities.RemoveRange(database.AppExternalIdentities));
            fixture.Change(database => database.AppExternalIdentities.Add(new AppExternalIdentity
            {
                Id = 1,
                AppUserId = 1,
                ProviderKey = "Okta",
                LinkMethod = "Bootstrap",
                Issuer = change == "issuer" ? "https://other-synthetic.okta.com" : "https://synthetic.okta.com",
                Subject = change == "subject" ? "other-synthetic-subject" : "subject"
            }));
        }
        fixture.Change(database =>
        {
            if (change == "inactive") database.AppUsers.Single().IsActive = false;
            if (change == "identity") database.AppExternalIdentities.Single().RevokedAt = DateTime.UtcNow;
        });
        using var response = await fixture.Client.GetAsync("/test/provisioning.execute");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExistingCookieRefreshesRemovedAndAddedPermissionsWithoutLogin()
    {
        using var fixture = new Fixture(2);
        using var login = await fixture.Client.GetAsync("/test/login");
        fixture.Change(database =>
        {
            database.AppUserRoles.RemoveRange(database.AppUserRoles);
            database.AppUserRoles.Add(new AppUserRole { UserId = 1, RoleId = 1, AssignedBy = "synthetic" });
            database.AppUsers.Single().AuthorizationVersion++;
        });
        using var removed = await fixture.Client.GetAsync("/test/provisioning.execute");
        Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        using var added = await fixture.Client.GetAsync("/test/requests.submit");
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        using var session = await fixture.Client.GetAsync("/api/auth/session");
        var json = await session.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("requests.submit", json.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain("provisioning.execute", json.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        fixture.Change(database => Assert.Single(database.AuditLog));
        var entry = fixture.Logs.Events.Last();
        Assert.Equal("1", ((Serilog.Events.ScalarValue)entry.Properties["ActorUserId"]).Value);
        Assert.Equal("synthetic@example.invalid", ((Serilog.Events.ScalarValue)entry.Properties["ActorUPN"]).Value);
    }

    [Fact]
    public async Task ConcurrentChangeDuringPermissionRefreshFailsClosed()
    {
        using var fixture = new Fixture(true, 2);
        using var login = await fixture.Client.GetAsync("/test/login");
        fixture.Change(database => database.AppUsers.Single().AuthorizationVersion++);
        fixture.Race.OnUserRead = () => fixture.Change(database =>
        {
            database.AppUserRoles.RemoveRange(database.AppUserRoles);
            database.AppUsers.Single().AuthorizationVersion++;
        });
        using var response = await fixture.Client.GetAsync("/test/provisioning.execute");
        Assert.Null(fixture.Race.OnUserRead);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnavailableAuthorizationStoreCannotUseCachedPermissions()
    {
        using var fixture = new Fixture(true, 2);
        using var login = await fixture.Client.GetAsync("/test/login");
        fixture.Race.OnUserRead = () => throw new InvalidOperationException("synthetic unavailable store");
        using var response = await fixture.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("synthetic unavailable", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("role")]
    [InlineData("permission")]
    [InlineData("grant")]
    public async Task PermissionEditsRefreshWithoutPreservingObsoleteAuthority(string change)
    {
        using var fixture = new Fixture(2);
        using var login = await fixture.Client.GetAsync("/test/login");
        fixture.Change(database =>
        {
            if (change == "role") database.AppRoles.Single(role => role.Id == 2).IsActive = false;
            if (change == "permission") database.AppPermissions.Single(permission => permission.Name == "provisioning.execute").IsActive = false;
            if (change == "grant") database.AppRolePermissions.RemoveRange(database.AppRolePermissions.Where(grant => grant.RoleId == 2));
            database.AppUsers.Single().AuthorizationVersion++;
        });
        using var removed = await fixture.Client.GetAsync("/test/provisioning.execute");
        Assert.Equal(HttpStatusCode.Forbidden, removed.StatusCode);
        using var activeSession = await fixture.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, activeSession.StatusCode);
    }

    [Theory]
    [InlineData(1, false, false, true, false)]
    [InlineData(2, false, true, false, false)]
    [InlineData(3, false, false, false, true)]
    [InlineData(5, false, false, true, false)]
    [InlineData(7, false, false, false, false)]
    public async Task DefaultRolesHaveSeparateUrgencyRetentionAndDelegationAuthority(int role, bool urgent, bool retention, bool backup, bool own)
    {
        using var fixture = new Fixture(role);
        Assert.Equal(urgent, await fixture.Allowed("Urgent", new UrgentAction(1, 1, "Synthetic reason")));
        Assert.Equal(retention, await fixture.Allowed("Retention", null));
        Assert.Equal(backup || own, await fixture.Allowed("Delegation", new DelegateAssignment(12)));
        Assert.Equal(backup, await fixture.Allowed("Delegation", new DelegateAssignment(99)));
    }

    [Fact]
    public async Task DesignatedItNeedsCurrentRevisionAndReasonAndDoesNotApproveRequest()
    {
        using var fixture = new Fixture(2, 7);
        Assert.True(await fixture.Allowed("Urgent", new UrgentAction(1, 1, "Synthetic reason")));
        Assert.False(await fixture.Allowed("Urgent", new UrgentAction(1, 2, "Synthetic reason")));
        Assert.False(await fixture.Allowed("Urgent", new UrgentAction(1, 1, "  ")));
        Assert.False(await fixture.Allowed("Urgent", null));
        Assert.False(await fixture.Allowed("Delegation", null));
        fixture.Change(database =>
        {
            Assert.Equal("Pending", database.Requests.Single().ApprovalStatus);
            database.Requests.Single().Status = "Cancelled";
        });
        Assert.False(await fixture.Allowed("Urgent", new UrgentAction(1, 1, "Synthetic reason")));
    }
}
