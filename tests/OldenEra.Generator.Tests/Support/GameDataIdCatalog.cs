using System.Text.Json;

namespace OldenEra.Generator.Tests.Support;

public enum IdCategory
{
    ContentPool,
    ContentList,
    ZoneLayout,
    EncounterTemplate
}

public sealed class GameDataIdCatalog
{
    private readonly Dictionary<IdCategory, HashSet<string>> _byCategory = new()
    {
        [IdCategory.ContentPool] = new(StringComparer.Ordinal),
        [IdCategory.ContentList] = new(StringComparer.Ordinal),
        [IdCategory.ZoneLayout] = new(StringComparer.Ordinal),
        [IdCategory.EncounterTemplate] = new(StringComparer.Ordinal),
    };

    public IReadOnlySet<string> Pools => _byCategory[IdCategory.ContentPool];
    public IReadOnlySet<string> ContentLists => _byCategory[IdCategory.ContentList];
    public IReadOnlySet<string> ZoneLayouts => _byCategory[IdCategory.ZoneLayout];
    public IReadOnlySet<string> EncounterTemplates => _byCategory[IdCategory.EncounterTemplate];

    public bool Contains(IdCategory cat, string id) => _byCategory[cat].Contains(id);

    public static GameDataIdCatalog LoadFromRepo()
    {
        string root = FindGeneratorDataDirectory();
        var catalog = new GameDataIdCatalog();

        var folderToCategory = new Dictionary<string, IdCategory>(StringComparer.Ordinal)
        {
            ["content_pools"] = IdCategory.ContentPool,
            ["content_lists"] = IdCategory.ContentList,
            ["zone_layouts"] = IdCategory.ZoneLayout,
            ["encounter_templates"] = IdCategory.EncounterTemplate,
        };

        foreach (var (folder, category) in folderToCategory)
        {
            string dir = Path.Combine(root, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                HarvestNamesFromArray(file, catalog._byCategory[category]);
            }
        }

        return catalog;
    }

    private static void HarvestNamesFromArray(string file, HashSet<string> sink)
    {
        using var stream = File.OpenRead(file);
        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        using var doc = JsonDocument.Parse(stream, options);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            if (element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                string? id = nameProp.GetString();
                if (!string.IsNullOrEmpty(id)) sink.Add(id);
            }
        }
    }

    private static string FindGeneratorDataDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "src", "OldenEra.TemplateEditor", "GameData", "GeneratorData");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "Could not locate src/OldenEra.TemplateEditor/GameData/GeneratorData by walking up from AppContext.BaseDirectory.");
    }
}
