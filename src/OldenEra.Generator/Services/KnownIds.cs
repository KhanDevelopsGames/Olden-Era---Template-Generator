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
        /// Content list IDs referenced by the generator's mandatoryContent groups
        /// via <c>IncludeLists = ["..."]</c>. All values must exist in shipped
        /// <c>GameData/GeneratorData/content_lists/</c>.
        /// </summary>
        public static class ContentLists
        {
            public const string BuildingGuardedResourceBanksTier1 = "basic_content_list_building_guarded_resource_banks_tier_1";
            public const string BuildingGuardedResourceBanksTier2 = "basic_content_list_building_guarded_resource_banks_tier_2";
            public const string BuildingGuardedResourceBanksTier3 = "basic_content_list_building_guarded_resource_banks_tier_3";
            public const string BuildingGuardedUnitsBanksNoBiome = "basic_content_list_building_guarded_units_banks_no_biome_restriction";
            public const string BuildingGuardedUnitsBanksOnlyBiome = "basic_content_list_building_guarded_units_banks_only_biome_restriction";
            public const string BuildingHeroBuffTier1 = "basic_content_list_building_hero_buff_tier_1";
            public const string BuildingHeroStatsAndSkillsTier1 = "basic_content_list_building_hero_stats_and_skills_tier_1";
            public const string BuildingHeroStatsAndSkillsTier2 = "basic_content_list_building_hero_stats_and_skills_tier_2";
            public const string BuildingHeroStatsAndSkillsTier3 = "basic_content_list_building_hero_stats_and_skills_tier_3";
            public const string BuildingMagicTier1 = "basic_content_list_building_magic_tier_1";
            public const string BuildingMagicTier2 = "basic_content_list_building_magic_tier_2";
            public const string BuildingRandomHires = "basic_content_list_building_random_hires";
            public const string PickupMythicScrollBox = "basic_content_list_pickup_mythic_scroll_box";
            public const string PickupRandomItems = "basic_content_list_pickup_random_items";
            public const string RareMinesByBiome = "basic_content_list_rare_mines_by_biome";
            public const string RareMines = "basic_content_list_rare_mines";
            public const string VisionBuildingsTier1 = "basic_content_list_vision_buildings_tier_1";
            public const string BuildingEpicGuardedResourceBanks = "content_list_building_epic_guarded_resource_banks";
            public const string BuildingRandomHiresHighTier = "content_list_building_random_hires_high_tier";
            public const string BuildingRandomHiresLowTier = "content_list_building_random_hires_low_tier";
            public const string BuildingUncommonHeroBanks = "content_list_building_uncommon_hero_banks";
            public const string BuildingUtopia = "content_list_building_utopia";
            public const string PickupPandoraBoxArmyHighTier = "content_list_pickup_pandora_box_army_high_tier";
            public const string PickupPandoraBoxArmyLowTier = "content_list_pickup_pandora_box_army_low_tier";

            public static IEnumerable<string> All()
            {
                yield return BuildingGuardedResourceBanksTier1;
                yield return BuildingGuardedResourceBanksTier2;
                yield return BuildingGuardedResourceBanksTier3;
                yield return BuildingGuardedUnitsBanksNoBiome;
                yield return BuildingGuardedUnitsBanksOnlyBiome;
                yield return BuildingHeroBuffTier1;
                yield return BuildingHeroStatsAndSkillsTier1;
                yield return BuildingHeroStatsAndSkillsTier2;
                yield return BuildingHeroStatsAndSkillsTier3;
                yield return BuildingMagicTier1;
                yield return BuildingMagicTier2;
                yield return BuildingRandomHires;
                yield return PickupMythicScrollBox;
                yield return PickupRandomItems;
                yield return RareMinesByBiome;
                yield return RareMines;
                yield return VisionBuildingsTier1;
                yield return BuildingEpicGuardedResourceBanks;
                yield return BuildingRandomHiresHighTier;
                yield return BuildingRandomHiresLowTier;
                yield return BuildingUncommonHeroBanks;
                yield return BuildingUtopia;
                yield return PickupPandoraBoxArmyHighTier;
                yield return PickupPandoraBoxArmyLowTier;
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
            foreach (string id in ContentLists.All())
            {
                if (!catalog.Contains(GameDataCategory.ContentList, id))
                {
                    missing.Add($"{nameof(GameDataCategory.ContentList)}:{id}");
                }
            }
            // Inline zone layouts (Spawn/Sides/TreasureZone/Center) intentionally not
            // checked: they are template-local, not shipped.
            return missing;
        }
    }
}
