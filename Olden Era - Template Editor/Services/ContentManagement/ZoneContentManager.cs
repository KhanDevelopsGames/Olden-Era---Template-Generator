using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Content manager for defining zone-specific content */
public static class ZoneContentManager
{
    /// <summary>
    /// Player spawn zone — guaranteed starter mines anchored near the castle, a full rare mine
    /// set spread along roads, utility/economic buildings, tier-split hiring picks, one hero
    /// stat trainer, guarded resource banks, and starter loot.
    /// Grounded in the consensus across Exodus, Staircase, Kerberos, Blitz, Universe, and
    /// Yin Yang spawn zones from the example template corpus.
    /// </summary>
    public static List<ContentItem> BuildPlayerZoneMandatoryContent(int castleCount, bool spawnFootholds)
    {
        var content = new List<ContentItem>();

        if (spawnFootholds)
            content.Add(ContentPresets.RemoteFoothold(castleCount));

        content.AddRange([
            // ── Basic mines — guarded, anchored near the player castle (every template). ──
            ContentPresets.MineWood_Anchored(),
            ContentPresets.MineOre_Anchored(),
            // ── Gold mine near crossroads (Exodus/Staircase/Yin Yang pattern). ──
            ContentPresets.MineGold_NearCrossroads(),
            // ── Rare mines spread along roads (Exodus/Staircase/Yin Yang pattern). ──
            ContentPresets.MineCrystals_NextToRoad(),
            ContentPresets.MineMercury_NextToRoad(),
            ContentPresets.MineGemstones_NextToRoad(),
            ContentPresets.AlchemyLab_NearRoad(),
            // ── Utility buildings (Blitz/Kerberos/Exodus pattern). ──
            new() { Sid = "watchtower" },
            ContentItemBuilder.Create(ContentIds.Market).Guarded().RoadDistance(DistancePresets.Near).Build(),
            ContentItemBuilder.Create(ContentIds.ManaWell).RoadDistance(DistancePresets.Near).Build(),
            // ── Hero training — tier-2 stat building (fort/university/orb_observatory) ──
            //    + uncommon hero bank (university/wise_owl/tree_of_knowledge) (Blitz/Exodus pattern).
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
            new() { IncludeLists = ["content_list_building_uncommon_hero_banks"] },

            // ── Hiring — low-tier × 2 + high-tier × 1 + full pool × 1 (Kerberos + Universe blend). ──
            new() { IncludeLists = ["content_list_building_random_hires_low_tier"] },
            new() { IncludeLists = ["content_list_building_random_hires_low_tier"] },
            new() { IncludeLists = ["content_list_building_random_hires_high_tier"] },
            new() { IncludeLists = ["basic_content_list_building_random_hires"] },

            // ── Guarded resource banks — tier 1 × 2 + tier 2 × 1 (Exodus pattern). ──
            new() { IncludeLists = ["basic_content_list_building_guarded_resource_banks_tier_1"] },
            new() { IncludeLists = ["basic_content_list_building_guarded_resource_banks_tier_1"] },
            new() { IncludeLists = ["basic_content_list_building_guarded_resource_banks_tier_2"] },

            // ── Loot — epic items + army pandora (Exodus/Blitz pattern). ──
            ContentItemBuilder.Create(ContentIds.RandomItemEpic).SoloEncounter().Build(),
            ContentItemBuilder.Create(ContentIds.PandoraBox).SoloEncounter().Build(),
            new() { IncludeLists = ["content_list_pickup_pandora_box_army_low_tier"] },
        ]);
        return content;
    }

    /// <summary>
    /// Low-quality neutral zone — t2 pools, intentionally lean mandatory content.
    /// The t2 content pools supply most of the variety; mandatory content only guarantees
    /// the essential items that every low zone must have: a few rare mines, basic utility
    /// buildings, and a handful of random hires. Modelled after Universe side zones,
    /// Kerberos connector zones, and Madness side zones from the template corpus.
    /// No high-end encounters (no dragon utopias, research labs, unstable ruins).
    /// </summary>
    public static List<ContentItem> BuildLowNeutralMandatoryContent(int castleCount, bool spawnFootholds)
    {
        var content = new List<ContentItem>();

        if (spawnFootholds)
            content.Add(ContentPresets.RemoteFoothold(castleCount));

        content.AddRange([
            // Mines — biome-based rare mine + one fixed rare mine (Blitz Side-1 pattern).
            new() { Name = "name_mine_by_biome_1", IncludeLists = ["basic_content_list_rare_mines_by_biome"], IsMine = true },
            new() { IncludeLists = ["basic_content_list_rare_mines"], IsMine = true },
            // Utility — one guarded market near crossroads, one vision building.
            ContentItemBuilder.Create(ContentIds.Market).Guarded().AddRule(RulePresets.CrossroadsDistance(DistancePresets.Near)).Build(),
            new() { IncludeLists = ["basic_content_list_vision_buildings_tier_1"] },
            // Buff buildings — picked from the real hero-buff pool (mana_well, fountain, stables, etc.).
            new() { IncludeLists = ["basic_content_list_building_hero_buff_tier_1"] },
            new() { IncludeLists = ["basic_content_list_building_hero_buff_tier_1"] },
            // Common hero stat building (stinging_sword, armory_automaton, magic_wheel, knowledge_garden).
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_1"] },
            // Hiring — low-tier random hires (hires 1–4, confirmed content_list name).
            new() { IncludeLists = ["content_list_building_random_hires_low_tier"] },
            new() { IncludeLists = ["content_list_building_random_hires_low_tier"] },
            // Loot — solo pandora box + low-tier army pandora (variants 8–11).
            ContentItemBuilder.Create(ContentIds.PandoraBox).SoloEncounter().Build(),
            new() { IncludeLists = ["content_list_pickup_pandora_box_army_low_tier"] },
            // Pickup — random item + common magic pickup.
            new() { IncludeLists = ["basic_content_list_pickup_random_items"] },
            new() { IncludeLists = ["basic_content_list_building_magic_tier_1"] },
        ]);

        return content;
    }

    /// <summary>
    /// Medium-quality neutral zone — t3 pools, tier-3 resource banks, medium hires, stat buildings,
    /// epic+legendary loot, pandora boxes, gold and rare mines.
    /// Based on t3-pool side/treasure zones found in Staircase, Shamrock, Blitz, Kerberos, and similar templates.
    /// No high-end encounters (no dragon utopias, research labs, unstable ruins).
    /// </summary>
    public static List<ContentItem> BuildMediumNeutralMandatoryContent(int castleCount, bool spawnFootholds)
    {
        var content = new List<ContentItem>();

        if (spawnFootholds)
            content.Add(ContentPresets.RemoteFoothold(castleCount));

        content.AddRange([
            // Mines — full rare set + gold + alchemy lab (Yin Yang/Staircase t3 pattern).
            ContentPresets.MineCrystals_NextToRoad(),
            ContentPresets.MineMercury_NextToRoad(),
            ContentPresets.MineGemstones_NextToRoad(),
            ContentPresets.AlchemyLab_NearRoad(),
            ContentItemBuilder.Create(ContentIds.MineGold).Mine().RoadDistance(DistancePresets.Near).Build(),
            // Utility — watchtower (guarded) + vision building (tier 1 only: flattering_mirror/watchtower).
            // wind_rose (full map reveal) lives in tier_2 and belongs exclusively in high zones.
            ContentItemBuilder.Create(ContentIds.Watchtower).Guarded().Build(),
            new() { IncludeLists = ["basic_content_list_vision_buildings_tier_1"] },
            // Buff buildings.
            new() { IncludeLists = ["basic_content_list_building_hero_buff_tier_1"] },
            // Hero stats — tier 1 common + tier 2 uncommon picks.
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_1"] },
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
            // Magic buildings — tier 1 + tier 2.
            new() { IncludeLists = ["basic_content_list_building_magic_tier_1"] },
            new() { IncludeLists = ["basic_content_list_building_magic_tier_2"] },
            // Hiring — low-tier + high-tier random hires (confirmed generator list names).
            new() { IncludeLists = ["content_list_building_random_hires_low_tier"] },
            new() { IncludeLists = ["content_list_building_random_hires_high_tier"] },
            // Unit banks — biome-restricted (Blitz Treasure-2 pattern).
            new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_only_biome_restriction"] },
            // Guarded resource banks — tier 2 (no black tower / tier-1 banks).
            new() { IncludeLists = ["basic_content_list_building_guarded_resource_banks_tier_2"] },
            // Loot — epic items + pandora boxes with army variants.
            ContentItemBuilder.Create(ContentIds.RandomItemEpic).SoloEncounter().Build(),
            ContentItemBuilder.Create(ContentIds.RandomItemEpic).Build(),
            ContentItemBuilder.Create(ContentIds.PandoraBox).SoloEncounter().Build(),
            ContentItemBuilder.Create(ContentIds.PandoraBox).Build(),
            new() { IncludeLists = ["content_list_pickup_pandora_box_army_low_tier"] },
        ]);

        return content;
    }

    /// <summary>
    /// High-quality neutral zone — t4/t5 pools, highest-challenge encounters: dragon utopias,
    /// unstable ruins, research labs, mythic scroll boxes, tier-3 hero stats, many unit banks
    /// (including biome-restricted), legendary loot, many pandora boxes, gold-heavy mines.
    /// Based on t4/t5-pool treasure zones found in Staircase, Symphony, Blitz, Crossroads, and
    /// high-zone mandatory content across the example template corpus.
    /// Only this tier spawns dragon utopias, unstable ruins, and research laboratories.
    /// </summary>
    public static List<ContentItem> BuildHighNeutralMandatoryContent(int castleCount, bool spawnFootholds)
    {
        var content = new List<ContentItem>();

        if (spawnFootholds)
            content.Add(ContentPresets.RemoteFoothold(castleCount));

        content.AddRange([
            // Epic encounters — exclusive to high zones.
            new() { IncludeLists = ["content_list_building_utopia"] },
            new() { IncludeLists = ["content_list_building_utopia"] },
            new() { IncludeLists = ["content_list_building_epic_guarded_resource_banks"] },
            new() { IncludeLists = ["content_list_building_epic_guarded_resource_banks"] },
            // Utility — vision + buff buildings.
            new() { IncludeLists = ["basic_content_list_vision_buildings_tier_1"] },
            new() { IncludeLists = ["basic_content_list_building_hero_buff_tier_1"] },
            // Hero stats — tier 2 + tier 3 (fort/university/maze/college_of_wonder).
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_3"] },
            new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_3"] },
            // Magic buildings — tier 2 (magic amplifiers).
            new() { IncludeLists = ["basic_content_list_building_magic_tier_2"] },
            new() { IncludeLists = ["basic_content_list_building_magic_tier_2"] },
            // Hiring — high-tier hires (hires 5–7) + all hires pool.
            new() { IncludeLists = ["content_list_building_random_hires_high_tier"] },
            new() { IncludeLists = ["content_list_building_random_hires_high_tier"] },
            new() { IncludeLists = ["basic_content_list_building_random_hires"] },
            // Unit banks — biome-restricted + no-restriction variants.
            new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_only_biome_restriction"] },
            new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"] },
            new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"] },
            // Guarded resource banks — tier 2 + tier 3.
            new() { IncludeLists = ["basic_content_list_building_guarded_resource_banks_tier_2"] },
            new() { IncludeLists = ["basic_content_list_building_guarded_resource_banks_tier_3"] },
            // Loot — mythic scrolls, legendary items, pandora boxes with high-tier armies.
            new() { IncludeLists = ["basic_content_list_pickup_mythic_scroll_box"] },
            new() { IncludeLists = ["basic_content_list_pickup_mythic_scroll_box"] },
            ContentItemBuilder.Create(ContentIds.RandomItemLegendary).SoloEncounter().Build(),
            ContentItemBuilder.Create(ContentIds.RandomItemLegendary).Build(),
            ContentItemBuilder.Create(ContentIds.RandomItemEpic).Build(),
            ContentItemBuilder.Create(ContentIds.PandoraBox).SoloEncounter().Build(),
            ContentItemBuilder.Create(ContentIds.PandoraBox).Build(),
            ContentItemBuilder.Create(ContentIds.PandoraBox).Build(),
            new() { IncludeLists = ["content_list_pickup_pandora_box_army_high_tier"] },
            new() { IncludeLists = ["content_list_pickup_pandora_box_army_high_tier"] },
            // Mines — gold-heavy with full rare set.
            ContentPresets.MineGold_NearCrossroads(),
            ContentItemBuilder.Create(ContentIds.MineGold).Mine().Build(),
            ContentItemBuilder.Create(ContentIds.MineGold).Mine().Build(),
            ContentItemBuilder.Create(ContentIds.MineCrystals).Mine().Build(),
            ContentItemBuilder.Create(ContentIds.MineMercury).Mine().Build(),
            ContentItemBuilder.Create(ContentIds.MineGemstones).Mine().Build(),
            ContentItemBuilder.Create(ContentIds.AlchemyLab).Mine().Build(),
            ContentItemBuilder.Create(ContentIds.AlchemyLab).Mine().Build(),
        ]);

        return content;
    }
}
}
