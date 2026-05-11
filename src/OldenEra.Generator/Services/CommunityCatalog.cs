using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Reference data sourced from the alcaras/homm-olden community datamine.
    /// Held in memory after first access; no network or disk I/O at runtime.
    /// </summary>
    /// <remarks>
    /// The JSON files live alongside this assembly as embedded resources
    /// (see <c>OldenEra.Generator.csproj</c>). Re-fetch via the script at
    /// <c>src/OldenEra.Generator/CommunityData/scripts/fetch-from-alcaras.py</c>.
    /// </remarks>
    public sealed class CommunityCatalog
    {
        public IReadOnlyList<HeroEntry> Heroes { get; }
        public IReadOnlyList<UnitEntry> Units { get; }
        public IReadOnlyList<SpellEntry> Spells { get; }
        public IReadOnlyList<SkillEntry> Skills { get; }
        public IReadOnlyList<SubclassEntry> Subclasses { get; }
        public IReadOnlyList<FactionEntry> Factions { get; }

        private CommunityCatalog(
            IReadOnlyList<HeroEntry> heroes,
            IReadOnlyList<UnitEntry> units,
            IReadOnlyList<SpellEntry> spells,
            IReadOnlyList<SkillEntry> skills,
            IReadOnlyList<SubclassEntry> subclasses,
            IReadOnlyList<FactionEntry> factions)
        {
            Heroes = heroes;
            Units = units;
            Spells = spells;
            Skills = skills;
            Subclasses = subclasses;
            Factions = factions;

            _spellSchools = new Lazy<IReadOnlyList<string>>(() =>
                Spells.Select(s => s.School ?? "")
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(SpellSchoolOrder)
                      .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                      .ToList());
        }

        private static readonly Lazy<CommunityCatalog> _instance = new(LoadFromEmbedded);

        public static CommunityCatalog Default => _instance.Value;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static CommunityCatalog LoadFromEmbedded()
        {
            return new CommunityCatalog(
                heroes: LoadArray<HeroEntry>("heroes.json"),
                units: LoadArray<UnitEntry>("units.json"),
                spells: LoadArray<SpellEntry>("spells.json"),
                skills: LoadArray<SkillEntry>("skills.json"),
                subclasses: LoadArray<SubclassEntry>("subclasses.json"),
                factions: LoadArray<FactionEntry>("factions.json"));
        }

        private static IReadOnlyList<T> LoadArray<T>(string fileName)
        {
            var asm = typeof(CommunityCatalog).Assembly;
            string resourceName = $"OldenEra.Generator.CommunityData.{fileName}";
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. "
                    + "Verify the .csproj <EmbeddedResource Include=\"CommunityData\\*.json\" /> entry.");
            return JsonSerializer.Deserialize<List<T>>(stream, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize {fileName}.");
        }

        // ── Filtering helpers ────────────────────────────────────────────────

        public IEnumerable<HeroEntry> HeroesByFaction(string factionId) =>
            Heroes.Where(h => string.Equals(h.Faction, factionId, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<SpellEntry> SpellsBySchool(string school) =>
            Spells.Where(s => string.Equals(s.School, school, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<UnitEntry> UnitsByFaction(string factionId) =>
            Units.Where(u => string.Equals(u.Faction, factionId, StringComparison.OrdinalIgnoreCase));

        // ── Spell school taxonomy ────────────────────────────────────────────

        /// <summary>
        /// Distinct spell schools in canonical display order
        /// (Day, Night, Arcane, Primal, then any others alphabetically).
        /// </summary>
        public IReadOnlyList<string> SpellSchools => _spellSchools.Value;
        private readonly Lazy<IReadOnlyList<string>> _spellSchools;

        public static int SpellSchoolOrder(string? school) => school?.ToLowerInvariant() switch
        {
            "day" => 0,
            "night" => 1,
            "arcane" => 2,
            "primal" => 3,
            _ => 99,
        };

        public static string FriendlySpellSchool(string? school) => school?.ToLowerInvariant() switch
        {
            "day" => "Day",
            "night" => "Night",
            "arcane" => "Arcane",
            "primal" => "Primal",
            _ => string.IsNullOrEmpty(school) ? "Other" : char.ToUpper(school[0]) + school[1..],
        };
    }

    public sealed record FactionEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("unitKey")] string UnitKey,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("skill")] string Skill,
        [property: JsonPropertyName("might")] string MightClass,
        [property: JsonPropertyName("magic")] string MagicClass);

    public sealed record HeroEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("faction")] string Faction,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("specialty")] string? Specialty,
        [property: JsonPropertyName("specDesc")] string? SpecialtyDescription);

    public sealed record UnitEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("faction")] string Faction,
        [property: JsonPropertyName("tier")] int Tier,
        [property: JsonPropertyName("variant")] string? Variant);

    public sealed record SpellEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("school")] string School,
        [property: JsonPropertyName("tier")] int Tier,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("desc")] string? Description);

    public sealed record SkillEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("group")] string Group,
        [property: JsonPropertyName("skillType")] string SkillType,
        [property: JsonPropertyName("factionId")] string? FactionId);

    public sealed record SubclassEntry(
        [property: JsonPropertyName("faction")] string Faction,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("class")] string Class,
        [property: JsonPropertyName("skills")] IReadOnlyList<string> Skills,
        [property: JsonPropertyName("effect")] string Effect);
}
