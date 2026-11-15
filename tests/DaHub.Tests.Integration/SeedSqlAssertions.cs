using DaHub.API.OrganizationSeeds;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class SeedSqlAssertions
{
    public static async Task VerifyAsync(string connection)
    {
        var protection = new EphemeralDataProtectionProvider();
        DaHubDbContext Open(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<DaHubDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new DaHubDbContext(options.Options);
        }
        SeedImporter Importer(DaHubDbContext db) => new(db, protection, TimeProvider.System);
        await using var database = Open();
        var actor = new AppUser { DisplayName = "Synthetic seed actor" };
        database.AppUsers.Add(actor);
        await database.SaveChangesAsync();
        var package = SeedPackage.Parse(OrganizationSeedTests.Package.Replace("Synthetic", "Imported", StringComparison.Ordinal));
        var importer = Importer(database);
        var preview = await importer.PreviewAsync(package, actor.Id, default);
        Assert.Equal(new SeedSummary(5, 0), preview.Summary);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => importer.ApplyAsync(package, preview.PreviewToken, actor.Id, () => Task.FromResult(false), default));
        Assert.False(await database.Locations.AnyAsync(x => x.SiteCode == "SYNTHETIC"));
        Assert.Equal(new SeedSummary(5, 0), await importer.ApplyAsync(package, preview.PreviewToken, actor.Id, () => Task.FromResult(true), default));
        database.ChangeTracker.Clear();
        var audit = await database.AuditLog.SingleAsync(x => x.ActionType == "OrganizationSeedsImported");
        Assert.Equal(actor.Id, audit.ActorUserId);
        Assert.DoesNotContain("OU=", audit.NewValueJson);
        Assert.DoesNotContain("example-group", audit.NewValueJson);
        var template = await database.AccessTemplates.SingleAsync(x => x.Name == "Imported template");
        Assert.Equal((await database.Locations.SingleAsync(x => x.SiteCode == "SYNTHETIC")).Id, template.LocationId);
        Assert.Equal("example-group", (await database.AccessTemplateGroups.SingleAsync(x => x.TemplateId == template.Id)).GroupIdentifier);
        await Assert.ThrowsAsync<SeedConflictException>(() => importer.ApplyAsync(package, preview.PreviewToken, actor.Id, () => Task.FromResult(true), default));
        var repeat = await importer.PreviewAsync(package, actor.Id, default);
        Assert.Equal(new SeedSummary(0, 5), await importer.ApplyAsync(package, repeat.PreviewToken, actor.Id, () => Task.FromResult(true), default));
        Assert.Single(await database.AuditLog.Where(x => x.ActionType == "OrganizationSeedsImported").ToArrayAsync());
        Assert.Empty(await database.AppExternalIdentities.Where(x => x.AppUserId == actor.Id).ToArrayAsync());
        Assert.Empty(await database.AppUserRoles.Where(x => x.UserId == actor.Id).ToArrayAsync());

        // A failed final save must undo the earlier lookup/template writes too.
        var failingPackage = SeedPackage.Parse("{\"schemaVersion\":1,\"locations\":[{\"siteCode\":\"ROLLBACK\",\"name\":\"Synthetic rollback\"}]}");
        await using (var failing = Open(new RejectSeedAudit()))
        {
            var failingImporter = Importer(failing);
            var receipt = await failingImporter.PreviewAsync(failingPackage, actor.Id, default);
            await Assert.ThrowsAsync<DbUpdateException>(() => failingImporter.ApplyAsync(failingPackage, receipt.PreviewToken, actor.Id, () => Task.FromResult(true), default));
        }
        Assert.False(await database.Locations.AnyAsync(x => x.SiteCode == "ROLLBACK"));

        var stale = await importer.PreviewAsync(package, actor.Id, default);
        database.JobTitles.Add(new JobTitle { Title = "Concurrent catalog change" });
        await database.SaveChangesAsync();
        await Assert.ThrowsAsync<SeedConflictException>(() => importer.ApplyAsync(package, stale.PreviewToken, actor.Id, () => Task.FromResult(true), default));

        var concurrent = SeedPackage.Parse("{\"schemaVersion\":1,\"locations\":[{\"siteCode\":\"CONCURRENT\",\"name\":\"Synthetic concurrent\"}]}");
        var shared = await importer.PreviewAsync(concurrent, actor.Id, default);
        async Task<bool> Attempt()
        {
            await using var db = Open();
            try
            {
                await Importer(db).ApplyAsync(concurrent, shared.PreviewToken, actor.Id, () => Task.FromResult(true), default);
                return true;
            }
            catch (SeedConflictException) { return false; }
            catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 1205 }) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, x => x);
        Assert.Equal(1, await database.Locations.CountAsync(x => x.SiteCode == "CONCURRENT"));

        // Explicit synthetic test enrollment, separate from the importer. Exercise actual HTTP apply,
        // cookie/CSRF checks and both current-authority queries using the restricted SQL identity.
        database.AppExternalIdentities.Add(new AppExternalIdentity
        {
            AppUserId = actor.Id,
            ProviderKey = "Okta",
            Issuer = "https://synthetic.okta.com",
            Subject = "seed-subject",
            LinkMethod = "Bootstrap"
        });
        database.AppUserRoles.Add(new AppUserRole { UserId = actor.Id, RoleId = 5, AssignedBy = "system:test" });
        await database.SaveChangesAsync();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DaHub"] = connection, ["Database:ConnectionStringFile"] = "" }));
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, OrganizationSeedTests.Login>());
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await client.GetAsync("/test/seed-login");
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        const string httpPackage = "{\"schemaVersion\":1,\"locations\":[{\"siteCode\":\"HTTP\",\"name\":\"Synthetic HTTP\"}]}";
        using var httpPreview = await client.PostAsync("/api/admin/organization-seeds/preview", new StringContent(httpPackage, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, httpPreview.StatusCode);
        var httpReceipt = await httpPreview.Content.ReadFromJsonAsync<SeedPreview>();
        client.DefaultRequestHeaders.Add("X-Seed-Preview", httpReceipt!.PreviewToken);
        using var applied = await client.PostAsync("/api/admin/organization-seeds/apply", new StringContent(httpPackage, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal(new SeedSummary(1, 0), await applied.Content.ReadFromJsonAsync<SeedSummary>());
        Assert.Equal(1, await database.Locations.CountAsync(x => x.SiteCode == "HTTP"));
    }

    private sealed class RejectSeedAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLogEntry>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Synthetic audit storage failure");
            return ValueTask.FromResult(result);
        }
    }
}
