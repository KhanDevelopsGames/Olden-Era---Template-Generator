using System.Text.Json;

namespace OldenEra.Generator.Services
{
    public enum GameDataCategory
    {
        ContentPool,
        ContentList,
        ZoneLayout,
        EncounterTemplate,
    }

    /// <summary>
    /// In-memory index of every IDable entry shipped under
    /// <c>GameData/GeneratorData/</c>. One entry per <c>name</c> field across the
    /// content_pools, content_lists, zone_layouts, and encounter_templates folders.
    /// </summary>
    /// <remarks>
    /// Read-only after construction. Hosts and tests can use this to validate that
    /// IDs emitted by the generator (or chosen by the user via UI) actually exist
    /// in the data the game ships with.
    /// </remarks>
    public sealed class GameDataCatalog
    {
        private readonly Dictionary<GameDataCategory, HashSet<string>> _byCategory;

        private GameDataCatalog(Dictionary<GameDataCategory, HashSet<string>> byCategory)
        {
            _byCategory = byCategory;
        }

        public IReadOnlySet<string> Pools => _byCategory[GameDataCategory.ContentPool];
        public IReadOnlySet<string> ContentLists => _byCategory[GameDataCategory.ContentList];
        public IReadOnlySet<string> ZoneLayouts => _byCategory[GameDataCategory.ZoneLayout];
        public IReadOnlySet<string> EncounterTemplates => _byCategory[GameDataCategory.EncounterTemplate];

        public bool Contains(GameDataCategory category, string id) =>
            _byCategory[category].Contains(id);

        /// <summary>
        /// Loads from a directory tree containing <c>content_pools/</c>,
        /// <c>content_lists/</c>, <c>zone_layouts/</c>, and
        /// <c>encounter_templates/</c> subfolders. Missing subfolders are tolerated.
        /// </summary>
        public static GameDataCatalog LoadFromDirectory(string generatorDataRoot)
        {
            if (!Directory.Exists(generatorDataRoot))
            {
                throw new DirectoryNotFoundException(
                    $"GameData root not found: {generatorDataRoot}");
            }

            var byCategory = NewByCategory();
            foreach (var (folder, category) in FolderToCategory)
            {
                string dir = Path.Combine(generatorDataRoot, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
                {
                    using var stream = File.OpenRead(file);
                    HarvestNamesFromArray(stream, byCategory[category]);
                }
            }
            return new GameDataCatalog(byCategory);
        }

        /// <summary>
        /// Loads from a sequence of pre-opened JSON streams, each tagged with the
        /// category it represents. Useful for the Blazor host, which fetches files
        /// over HTTP rather than from disk.
        /// </summary>
        public static GameDataCatalog LoadFromStreams(IEnumerable<(GameDataCategory Category, Stream Json)> sources)
        {
            var byCategory = NewByCategory();
            foreach (var (category, json) in sources)
            {
                HarvestNamesFromArray(json, byCategory[category]);
            }
            return new GameDataCatalog(byCategory);
        }

        private static readonly IReadOnlyDictionary<string, GameDataCategory> FolderToCategory =
            new Dictionary<string, GameDataCategory>(StringComparer.Ordinal)
            {
                ["content_pools"] = GameDataCategory.ContentPool,
                ["content_lists"] = GameDataCategory.ContentList,
                ["zone_layouts"] = GameDataCategory.ZoneLayout,
                ["encounter_templates"] = GameDataCategory.EncounterTemplate,
            };

        private static Dictionary<GameDataCategory, HashSet<string>> NewByCategory() => new()
        {
            [GameDataCategory.ContentPool] = new(StringComparer.Ordinal),
            [GameDataCategory.ContentList] = new(StringComparer.Ordinal),
            [GameDataCategory.ZoneLayout] = new(StringComparer.Ordinal),
            [GameDataCategory.EncounterTemplate] = new(StringComparer.Ordinal),
        };

        private static void HarvestNamesFromArray(Stream stream, HashSet<string> sink)
        {
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
                if (element.TryGetProperty("name", out var nameProp)
                    && nameProp.ValueKind == JsonValueKind.String)
                {
                    string? id = nameProp.GetString();
                    if (!string.IsNullOrEmpty(id)) sink.Add(id);
                }
            }
        }
    }
}
