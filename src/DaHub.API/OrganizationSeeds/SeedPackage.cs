using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaHub.API.OrganizationSeeds;

public sealed record SeedLocation(string SiteCode, string Name, string? Address = null, string? Phone = null);
public sealed record SeedDepartment(string Name, string OUPath);
public sealed record SeedJobTitle(string Title, bool EHRAccessDefault = false);
public sealed record SeedSpecialty(string Name, string? Description = null);
public sealed record SeedGroup(string GroupType, string GroupName, string GroupIdentifier, bool IsRequired = false);
public sealed record SeedTemplate(string Name, string M365LicenseSku, string? Description = null,
    string? Department = null, string? Location = null, string? JobTitle = null, string? Specialty = null,
    bool EHRAccessRequired = false, SeedGroup[]? Groups = null);

public sealed record SeedPackage
{
    public int SchemaVersion { get; init; }
    public SeedLocation[] Locations { get; init; } = [];
    public SeedDepartment[] Departments { get; init; } = [];
    public SeedJobTitle[] JobTitles { get; init; } = [];
    public SeedSpecialty[] Specialties { get; init; } = [];
    public SeedTemplate[] Templates { get; init; } = [];

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static SeedPackage Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            CheckProperties(document.RootElement);
            var package = JsonSerializer.Deserialize<SeedPackage>(json, Json)
                ?? throw new SeedValidationException("A JSON object is required.");
            package.Validate();
            return package;
        }
        catch (JsonException)
        {
            // Parser messages can echo private values. Return only a fixed safe message.
            throw new SeedValidationException("Invalid JSON, unknown fields, or unsupported field types.");
        }
    }

    private static void CheckProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new SeedValidationException("Duplicate JSON fields are not allowed.");
                CheckProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckProperties(item);
    }

    private void Validate()
    {
        if (SchemaVersion != 1) throw new SeedValidationException("schemaVersion must be 1.");
        if (Locations is null || Departments is null || JobTitles is null || Specialties is null || Templates is null)
            throw new SeedValidationException("Catalog arrays cannot be null.");
        var total = Locations.Length + Departments.Length + JobTitles.Length + Specialties.Length + Templates.Length;
        if (total == 0 || total > 1000) throw new SeedValidationException("Provide between 1 and 1000 catalog rows.");
        Rows(Locations, x => x.SiteCode, "locations", x =>
        {
            Text(x.SiteCode, 20); Text(x.Name, 150); Text(x.Address, 300, true); Text(x.Phone, 30, true);
        });
        Rows(Departments, x => x.Name, "departments", x => { Text(x.Name, 100); Text(x.OUPath, 500); });
        Rows(JobTitles, x => x.Title, "jobTitles", x => Text(x.Title, 150));
        Rows(Specialties, x => x.Name, "specialties", x => { Text(x.Name, 150); Text(x.Description, 500, true); });
        Rows(Templates, x => x.Name, "templates", x =>
        {
            Text(x.Name, 150); Text(x.M365LicenseSku, 100); Text(x.Description, 500, true);
            Text(x.Department, 100, true); Text(x.Location, 20, true);
            Text(x.JobTitle, 150, true); Text(x.Specialty, 150, true);
            if (x.Groups?.Length > 100) throw new SeedValidationException("A template allows at most 100 groups.");
            Rows(x.Groups ?? [], g => g.GroupType + ":" + g.GroupIdentifier, "groups", g =>
            {
                if (g.GroupType is not ("AD_Security" or "AD_Distribution" or "Entra_Group"))
                    throw new SeedValidationException("Unsupported group type.");
                Text(g.GroupName, 300); Text(g.GroupIdentifier, 500);
            });
        });
    }

    private static void Rows<T>(T[] rows, Func<T, string> key, string section, Action<T> validate) where T : class
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Length; index++)
        {
            try
            {
                var row = rows[index] ?? throw new SeedValidationException("Rows cannot be null.");
                validate(row);
                if (!keys.Add(key(row))) throw new SeedValidationException("Duplicate catalog key.");
            }
            catch (SeedValidationException exception)
            {
                throw new SeedValidationException($"{section}[{index}]: {exception.Message}");
            }
        }
    }

    private static void Text(string? value, int max, bool optional = false)
    {
        if (value is null && optional) return;
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value != value.Trim() || value.Any(char.IsControl))
            throw new SeedValidationException($"Text must be nonblank, at most {max} characters, without surrounding whitespace or control characters.");
    }
}

public sealed class SeedValidationException(string message) : Exception(message);
