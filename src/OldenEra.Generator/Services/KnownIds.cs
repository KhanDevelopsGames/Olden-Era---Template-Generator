namespace OldenEra.Generator.Services
{
    /// <summary>
    /// IDs that the generator references by string literal. The values here are the
    /// authoritative source for these names; any literal in the generator that
    /// matches one of these constants should be considered a candidate for
    /// migration to the constant.
    /// </summary>
    /// <remarks>
    /// At construction time, hosts can call <see cref="ValidateAgainst(GameDataCatalog)"/>
    /// to fail fast if a shipped ID has been renamed or removed.
    /// </remarks>
    public static class KnownIds
    {
        public static class ZoneLayouts
        {
            // The generator builds these inline on each template (see BuildZoneLayouts).
            // They are NOT shipped in GameData/zone_layouts/, so they are excluded from
            // the catalog validation. Listed here so the generator can reference one source.
            public const string Spawn = "zone_layout_spawns";
            public const string Sides = "zone_layout_sides";
            public const string TreasureZone = "zone_layout_treasure_zone";
            public const string Center = "zone_layout_center";
        }

        public static class GeneralResourcePools
        {
            public const string StartZonePoor = "content_pool_general_resources_start_zone_poor";
            public const string StartZoneMedium = "content_pool_general_resources_start_zone_medium";
            public const string StartZoneRich = "content_pool_general_resources_start_zone_rich";

            public static IEnumerable<string> All()
            {
                yield return StartZonePoor;
                yield return StartZoneMedium;
                yield return StartZoneRich;
            }
        }

        /// <summary>
        /// Verifies that every <see cref="KnownIds"/> entry in a category that ships
        /// in GameData is actually present in the supplied catalog. Returns the list
        /// of missing IDs; empty list = all good.
        /// </summary>
        public static IReadOnlyList<string> ValidateAgainst(GameDataCatalog catalog)
        {
            var missing = new List<string>();
            foreach (string id in GeneralResourcePools.All())
            {
                if (!catalog.Contains(GameDataCategory.ContentPool, id))
                {
                    missing.Add($"{nameof(GameDataCategory.ContentPool)}:{id}");
                }
            }
            // Inline zone layouts (Spawn/Sides/TreasureZone/Center) intentionally not
            // checked: they are template-local, not shipped.
            return missing;
        }
    }
}
