namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Olden Era faction metadata + the hero SID pattern.
    /// </summary>
    /// <remarks>
    /// Hero <em>names</em> are not bundled here. The display strings live in the
    /// game's localization files and are subject to the publisher's copyright;
    /// shipping a name list would require an explicit licence from Unfrozen
    /// Studios or the dervied-data maintainer (e.g. alcaras/homm-olden, which
    /// at the time of writing has no LICENSE file).
    ///
    /// What is safe to ship: the faction list (game facts), the canonical SID
    /// prefix per faction, and the per-faction hero count + might/magic split.
    /// A future <c>heroes.json</c> bundle keyed by SID can layer display names
    /// on top of this without changing the catalog API.
    /// </remarks>
    public static class HeroCatalog
    {
        /// <summary>Display name vs internal SID prefix vs class names.</summary>
        public sealed record FactionEntry(
            string DisplayName,
            string SidPrefix,
            string MightClass,
            string MagicClass);

        /// <summary>
        /// The six factions in Olden Era as of the 2026-05 Early Access build.
        /// Display names match the in-game UI; SidPrefix matches the
        /// <c>unitKey</c> used in the game's data files (note legacy mismatches:
        /// Temple/human, Necropolis/necro, Schism/unfrozen).
        /// </summary>
        public static readonly IReadOnlyList<FactionEntry> Factions = new FactionEntry[]
        {
            new("Temple",     "human",    "Knight",       "Cleric"),
            new("Necropolis", "necro",    "Death Knight", "Necromancer"),
            new("Grove",      "nature",   "Warden",       "Druid"),
            new("Hive",       "demon",    "Enforcer",     "Herald"),
            new("Schism",     "unfrozen", "Oathkeeper",   "Riftspeaker"),
            new("Dungeon",    "dungeon",  "Overlord",     "Warlock"),
        };

        /// <summary>Heroes per faction in the stock 2026-05 build.</summary>
        public const int HeroesPerFaction = 18;

        /// <summary>Indices 1..MightHeroLastIndex are might heroes; the rest are magic.</summary>
        public const int MightHeroLastIndex = 9;

        /// <summary>
        /// Builds the SID for a hero. Game format is <c>{sidPrefix}_hero_{index}</c>,
        /// e.g. <c>human_hero_1</c>, <c>necro_hero_12</c>, <c>unfrozen_hero_5</c>.
        /// Index is 1-based and not zero-padded. Throws when the faction is
        /// unknown or the index falls outside <c>1..HeroesPerFaction</c>.
        /// </summary>
        public static string BuildSid(string factionDisplayName, int heroIndex)
        {
            var faction = Factions.FirstOrDefault(f =>
                string.Equals(f.DisplayName, factionDisplayName, StringComparison.OrdinalIgnoreCase));
            if (faction is null)
            {
                throw new ArgumentException(
                    $"Unknown faction '{factionDisplayName}'. Expected one of: "
                    + string.Join(", ", Factions.Select(f => f.DisplayName)),
                    nameof(factionDisplayName));
            }
            if (heroIndex < 1 || heroIndex > HeroesPerFaction)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(heroIndex),
                    heroIndex,
                    $"Hero index must be between 1 and {HeroesPerFaction} inclusive.");
            }
            return $"{faction.SidPrefix}_hero_{heroIndex}";
        }

        public static bool IsMightHero(int heroIndex) => heroIndex is >= 1 and <= MightHeroLastIndex;
        public static bool IsMagicHero(int heroIndex) => heroIndex is > MightHeroLastIndex and <= HeroesPerFaction;
    }
}
