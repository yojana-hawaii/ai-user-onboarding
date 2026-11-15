using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.OrganizationSeeds;

public sealed record SeedSummary(int Added, int Unchanged);
public sealed record SeedPreview(SeedSummary Summary, string PreviewToken);

public sealed class SeedConflictException : Exception;

public sealed class SeedImporter(DaHubDbContext database, IDataProtectionProvider protection, TimeProvider clock)
{
    private sealed record Receipt(int Actor, string Package, string State, DateTimeOffset Expires);
    private sealed record Catalogs(Location[] Locations, Department[] Departments, JobTitle[] JobTitles,
        Specialty[] Specialties, AccessTemplate[] Templates, AccessTemplateGroup[] Groups);
    private sealed record Plan(Catalogs State, SeedSummary Summary);
    private readonly IDataProtector protector = protection.CreateProtector("DaHub.OrganizationSeedPreview.v1");

    public async Task<SeedPreview> PreviewAsync(SeedPackage package, int actor, CancellationToken cancellationToken)
    {
        var plan = await PlanAsync(package, cancellationToken);
        var receipt = new Receipt(actor, Hash(package), Hash(plan.State), clock.GetUtcNow().AddMinutes(10));
        return new SeedPreview(plan.Summary, protector.Protect(JsonSerializer.Serialize(receipt)));
    }

    public async Task<SeedSummary> ApplyAsync(SeedPackage package, string previewToken, int actor,
        Func<Task<bool>> authorized, CancellationToken cancellationToken)
    {
        Receipt? receipt;
        try { receipt = JsonSerializer.Deserialize<Receipt>(protector.Unprotect(previewToken)); }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException)
        {
            throw new SeedConflictException();
        }
        if (receipt is null || receipt.Actor != actor || receipt.Package != Hash(package) || receipt.Expires <= clock.GetUtcNow())
            throw new SeedConflictException();

        // Hold catalog and authority reads through commit. A deadlock/SQL failure rolls back the whole import.
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var plan = await PlanAsync(package, cancellationToken);
        if (receipt.State != Hash(plan.State)) throw new SeedConflictException();
        if (!await authorized()) throw new UnauthorizedAccessException();
        if (plan.Summary.Added > 0)
        {
            var state = plan.State;
            var locations = state.Locations.ToDictionary(x => x.SiteCode, StringComparer.OrdinalIgnoreCase);
            var departments = state.Departments.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            var titles = state.JobTitles.ToDictionary(x => x.Title, StringComparer.OrdinalIgnoreCase);
            var specialties = state.Specialties.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var item in package.Locations)
                if (!locations.ContainsKey(item.SiteCode))
                {
                    var entity = new Location { SiteCode = item.SiteCode, Name = item.Name, Address = item.Address, Phone = item.Phone };
                    database.Locations.Add(entity); locations.Add(item.SiteCode, entity);
                }
            foreach (var item in package.Departments)
                if (!departments.ContainsKey(item.Name))
                {
                    var entity = new Department { Name = item.Name, OUPath = item.OUPath };
                    database.Departments.Add(entity); departments.Add(item.Name, entity);
                }
            foreach (var item in package.JobTitles)
                if (!titles.ContainsKey(item.Title))
                {
                    var entity = new JobTitle { Title = item.Title, EHRAccessDefault = item.EHRAccessDefault };
                    database.JobTitles.Add(entity); titles.Add(item.Title, entity);
                }
            foreach (var item in package.Specialties)
                if (!specialties.ContainsKey(item.Name))
                {
                    var entity = new Specialty { Name = item.Name, Description = item.Description };
                    database.Specialties.Add(entity); specialties.Add(item.Name, entity);
                }
            await database.SaveChangesAsync(cancellationToken);
            var templateNames = state.Templates.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var item in package.Templates.Where(x => !templateNames.Contains(x.Name)))
            {
                var template = new AccessTemplate
                {
                    Name = item.Name,
                    Description = item.Description,
                    M365LicenseSku = item.M365LicenseSku,
                    EHRAccessRequired = item.EHRAccessRequired,
                    CreatedByUPN = $"AppUser:{actor}",
                    DepartmentId = item.Department is null ? null : departments[item.Department].Id,
                    LocationId = item.Location is null ? null : locations[item.Location].Id,
                    JobTitleId = item.JobTitle is null ? null : titles[item.JobTitle].Id,
                    SpecialtyId = item.Specialty is null ? null : specialties[item.Specialty].Id
                };
                database.AccessTemplates.Add(template);
                await database.SaveChangesAsync(cancellationToken);
                database.AccessTemplateGroups.AddRange((item.Groups ?? []).Select(group => new AccessTemplateGroup
                {
                    TemplateId = template.Id,
                    GroupType = group.GroupType,
                    GroupName = group.GroupName,
                    GroupIdentifier = group.GroupIdentifier,
                    IsRequired = group.IsRequired
                }));
            }
            database.AuditLog.Add(new AuditLogEntry
            {
                ActorUserId = actor,
                ActorUPN = $"AppUser:{actor}",
                ActionType = "OrganizationSeedsImported",
                AffectedSystem = "App",
                Result = "Success",
                TimestampUTC = clock.GetUtcNow().UtcDateTime,
                // Store a digest and counts, never the uploaded directory topology or raw file.
                NewValueJson = JsonSerializer.Serialize(new { PackageHash = Hash(package), plan.Summary })
            });
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return plan.Summary;
    }

    private async Task<Plan> PlanAsync(SeedPackage package, CancellationToken cancellationToken)
    {
        var state = new Catalogs(
            await database.Locations.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(cancellationToken),
            await database.Departments.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(cancellationToken),
            await database.JobTitles.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(cancellationToken),
            await database.Specialties.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(cancellationToken),
            await database.AccessTemplates.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(cancellationToken),
            await database.AccessTemplateGroups.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(cancellationToken));
        var added = 0;
        var unchanged = 0;
        Compare("locations", package.Locations, state.Locations, x => x.SiteCode, x => x.SiteCode,
            (seed, row) => row.IsActive && seed == new SeedLocation(row.SiteCode, row.Name, row.Address, row.Phone));
        Compare("departments", package.Departments, state.Departments, x => x.Name, x => x.Name,
            (seed, row) => row.IsActive && seed == new SeedDepartment(row.Name, row.OUPath));
        Compare("jobTitles", package.JobTitles, state.JobTitles, x => x.Title, x => x.Title,
            (seed, row) => row.IsActive && seed == new SeedJobTitle(row.Title, row.EHRAccessDefault));
        Compare("specialties", package.Specialties, state.Specialties, x => x.Name, x => x.Name,
            (seed, row) => row.IsActive && seed == new SeedSpecialty(row.Name, row.Description));
        var departments = References(state.Departments.Where(x => x.IsActive).Select(x => (x.Id, x.Name)), package.Departments.Select(x => x.Name));
        var locations = References(state.Locations.Where(x => x.IsActive).Select(x => (x.Id, x.SiteCode)), package.Locations.Select(x => x.SiteCode));
        var titles = References(state.JobTitles.Where(x => x.IsActive).Select(x => (x.Id, x.Title)), package.JobTitles.Select(x => x.Title));
        var specialties = References(state.Specialties.Where(x => x.IsActive).Select(x => (x.Id, x.Name)), package.Specialties.Select(x => x.Name));
        foreach (var item in package.Templates)
        {
            Resolve(departments, item.Department); Resolve(locations, item.Location);
            Resolve(titles, item.JobTitle); Resolve(specialties, item.Specialty);
        }
        Compare("templates", package.Templates, state.Templates, x => x.Name, x => x.Name, (seed, row) =>
            row.IsActive && seed.Name == row.Name && seed.Description == row.Description
            && seed.M365LicenseSku == row.M365LicenseSku && seed.EHRAccessRequired == row.EHRAccessRequired
            && Resolve(departments, seed.Department) == row.DepartmentId
            && Resolve(locations, seed.Location) == row.LocationId && Resolve(titles, seed.JobTitle) == row.JobTitleId
            && Resolve(specialties, seed.Specialty) == row.SpecialtyId
            && Groups(seed.Groups ?? []) == Groups(state.Groups.Where(x => x.TemplateId == row.Id)
                .Select(x => new SeedGroup(x.GroupType, x.GroupName, x.GroupIdentifier, x.IsRequired))));
        return new Plan(state, new SeedSummary(added, unchanged));

        void Compare<TSeed, TEntity>(string section, TSeed[] seeds, TEntity[] rows, Func<TSeed, string> seedKey,
            Func<TEntity, string> rowKey, Func<TSeed, TEntity, bool> equal)
        {
            var grouped = rows.GroupBy(rowKey, StringComparer.OrdinalIgnoreCase).ToArray();
            // Ambiguous legacy catalogs must be reconciled explicitly, never silently merged.
            if (grouped.Any(x => x.Count() != 1)) throw new SeedValidationException($"{section}: existing catalog contains ambiguous keys; reconcile it before importing.");
            var existing = grouped.ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < seeds.Length; index++)
            {
                var seed = seeds[index];
                if (!existing.TryGetValue(seedKey(seed), out var row)) added++;
                else if (equal(seed, row)) unchanged++;
                else throw new SeedValidationException($"{section}[{index}]: conflicts with existing data. No edits were applied.");
            }
        }
    }

    private static Dictionary<string, int> References(IEnumerable<(int Id, string Key)> existing, IEnumerable<string> incoming)
    {
        var result = existing.ToDictionary(x => x.Key, x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var key in incoming) result.TryAdd(key, 0);
        return result;
    }

    private static int? Resolve(Dictionary<string, int> references, string? key) => key is null ? null
        : references.TryGetValue(key, out var id) ? id : throw new SeedValidationException("Template references a missing catalog key.");

    private static string Groups(IEnumerable<SeedGroup> groups) => JsonSerializer.Serialize(groups
        .OrderBy(x => x.GroupType, StringComparer.Ordinal).ThenBy(x => x.GroupIdentifier, StringComparer.Ordinal));
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
