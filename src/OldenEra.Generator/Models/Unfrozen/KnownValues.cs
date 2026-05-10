namespace OldenEra.Generator.Models.Unfrozen
{
    /// <summary>
    /// Known string constants collected from all example templates.
    /// Use these lists to populate dropdowns and provide validation in the editor.
    /// </summary>
    public static class KnownValues
    {
        // ── Template root ────────────────────────────────────────────────────────

        public static readonly string[] GameModes =
        [
            "Classic",
            "SingleHero",
        ];

        /// <summary>Official/example-backed map sizes (sizeX = sizeZ).</summary>
        public static readonly int[] MapSizes =
        [
            64, 80, 96, 112, 128, 144, 160, 176, 192, 208, 240
        ];

        /// <summary>Experimental map sizes above the largest official/example-backed size.</summary>
        public static readonly int[] ExperimentalMapSizes =
        [
            256, 272, 288, 304, 320, 336, 352, 368, 384,
            400, 416, 432, 448, 464, 480, 496, 512
        ];

        public static readonly int[] AllMapSizes = [.. MapSizes, .. ExperimentalMapSizes];

        public static int MaxOfficialMapSize => MapSizes[^1];

        public static bool IsExperimentalMapSize(int size) =>
            Array.IndexOf(ExperimentalMapSizes, size) >= 0;

        /// <summary>Returns a short size label (S, M, L, XL, H, G, C) for a given map size.</summary>
        public static string MapSizeLabel(int size) => size switch
        {
            64        => "S",
            80 or 96  => "M",
            112 or 128 => "L",
            144 or 160 => "XL",
            176 or 192 => "H",
            >= 208 and <= 256 => "G",
            _ => "C",
        };

        /// <summary>
        /// Human-readable labels for each displayWinCondition ID.
        /// Index-aligned with <see cref="VictoryConditionIds"/>.
        /// </summary>
        public static readonly string[] VictoryConditionLabels =
        [
            "Standard",
            "Lost Starting City",
            "Gladiator Arena",
            "Hold City",
            "Tournament",
        ];

        /// <summary>
        /// displayWinCondition JSON values, aligned with <see cref="VictoryConditionLabels"/>.
        /// win_condition_4 ("Gladiator Arena") is required because shipped templates
        /// (Helltide, Symmetry) use it; without it the dropdown can't represent loaded
        /// settings. The "Gladiator Arena" name is inferred from MainWindow code paths
        /// that gate gladiator-arena rules on this id; verify against in-game text.
        /// </summary>
        public static readonly string[] VictoryConditionIds =
        [
            "win_condition_1",
            "win_condition_3",
            "win_condition_4",
            "win_condition_5",
            "win_condition_6",
        ];

        // ── Game rules ───────────────────────────────────────────────────────────

        /// <summary>Known values for Bonus.ReceiverFilter.</summary>
        public static readonly string[] BonusReceiverFilters =
        [
            "all_heroes",
            "start_hero",
        ];

        /// <summary>Known values for Bonus.Sid (start-game bonuses).</summary>
        public static readonly string[] BonusSids =
        [
            "add_bonus_hero_item",
            "add_bonus_hero_spell",
            "add_bonus_hero_stat",
            "add_bonus_hero_unit_multipler",
            "add_bonus_res",
        ];

        /// <summary>Known values for WinConditions.ChampionSelectRule.</summary>
        public static readonly string[] ChampionSelectRules =
        [
            "StartHero",
        ];

        // ── Value overrides ──────────────────────────────────────────────────────

        /// <summary>
        /// Known object / encounter SIDs used in ValueOverride and as mandatory
        /// content / content pool references across example templates.
        /// </summary>
        public static readonly string[] ObjectSids =
        [
            "alchemy_lab",
            "arena",
            "beer_fountain",
            "boreal_call",
            "celestial_sphere",
            "chimerologist",
            "circus",
            "college_of_wonder",
            "crystal_trail",
            "dragon_utopia",
            "eternal_dragon",
            "fickle_shrine",
            "flattering_mirror",
            "forge",
            "fort",
            "fountain",
            "fountain_2",
            "huntsmans_camp",
            "infernal_cirque",
            "insaras_eye",
            "jousting_range",
            "mana_well",
            "market",
            "mine_crystals",
            "mine_gemstones",
            "mine_gold",
            "mine_mercury",
            "mine_ore",
            "mine_wood",
            "mirage",
            "monty_hall",
            "mysterious_stone",
            "mystical_tower",
            "mythic_scroll_box",
            "orb_observatory",
            "pandora_box",
            "petrified_memorial",
            "pile_of_books",
            "point_of_balance",
            "prison",
            "quixs_path",
            "random_hire_1",
            "random_hire_2",
            "random_hire_3",
            "random_hire_4",
            "random_hire_5",
            "random_hire_6",
            "random_hire_7",
            "random_item_common",
            "random_item_epic",
            "random_item_legendary",
            "random_item_rare",
            "remote_foothold",
            "research_laboratory",
            "ritual_pyre",
            "sacrificial_shrine",
            "shady_den",
            "stables",
            "tavern",
            "tear_of_truth",
            "the_gorge",
            "town_gate",
            "tree_of_abundance",
            "troglodyte_throne",
            "unforgotten_grave",
            "university",
            "unstable_ruins",
            "watchtower",
            "wind_rose",
            "wise_owl",
        ];

        // ── Variant / orientation ────────────────────────────────────────────────

        /// <summary>Known values for Orientation.Mode.</summary>
        public static readonly string[] OrientationModes =
        [
            "BoundingCircle",
            "MinimalBoundingSquare",
        ];

        // ── Border ───────────────────────────────────────────────────────────────

        /// <summary>Known values for Border.WaterType.</summary>
        public static readonly string[] WaterTypes =
        [
            "water grass",
        ];

        // ── Zone ─────────────────────────────────────────────────────────────────

        /// <summary>Known values for Zone.Layout.</summary>
        public static readonly string[] ZoneLayouts =
        [
            "zone_layout_ai_spawn",
            "zone_layout_back",
            "zone_layout_center",
            "zone_layout_center_zone",
            "zone_layout_leaf",
            "zone_layout_player_spawn",
            "zone_layout_second_spawn",
            "zone_layout_side_spawn_zone",
            "zone_layout_side_zone",
            "zone_layout_sides",
            "zone_layout_spawn",
            "zone_layout_spawns",
            "zone_layout_start_zone",
            "zone_layout_supertreasure_zone",
            "zone_layout_treasure",
            "zone_layout_treasure_zone",
            "zone_layout_treasures",
            "zone_layout_wincondition_zone",
        ];

        // ── Main object ──────────────────────────────────────────────────────────

        /// <summary>Known values for MainObject.Type.</summary>
        public static readonly string[] MainObjectTypes =
        [
            "AbandonedOutpost",
            "City",
            "GladiatorArena",
            "Spawn",
        ];

        /// <summary>Known values for MainObject.Spawn (player slots).</summary>
        public static readonly string[] SpawnPlayers =
        [
            "Player1",
            "Player2",
            "Player3",
            "Player4",
            "Player5",
            "Player6",
            "Player7",
            "Player8",
        ];

        /// <summary>Known values for MainObject.Placement.</summary>
        public static readonly string[] MainObjectPlacements =
        [
            "Center",
            "Connection",
            "NearZone",
            "Uniform",
        ];

        /// <summary>Known values for MainObject.BuildingsConstructionSid.</summary>
        public static readonly string[] BuildingsConstructionSids =
        [
            "arcade_buildings_construction",
            "army_buildings_construction",
            "chosen_one_buildings_construction",
            "chosen_one_buildings_construction_up_1",
            "chosen_one_buildings_construction_up_2",
            "chosen_one_buildings_construction_up_3",
            "default_buildings_construction",
            "extra_poor_buildings_construction",
            "extra_rich_buildings_construction",
            "full_buildings_construction",
            "massacre_buildings_construction",
            "massacre_buildings_construction_up_1",
            "massacre_buildings_construction_up_2",
            "massacre_buildings_construction_up_3",
            "medium_buildings_construction",
            "poor_buildings_construction",
            "rich_buildings_construction",
            "siege_buildings_construction",
            "ultra_rich_buildings_construction",
        ];

        // ── Biome selectors ──────────────────────────────────────────────────────

        /// <summary>
        /// Known values for BiomeSelector.Type / TypedSelector.Type
        /// (used for zoneBiome, contentBiome, metaObjectsBiome, faction).
        /// </summary>
        public static readonly string[] SelectorTypes =
        [
            "FromList",
            "Match",
            "MatchMainObject",
            "MatchZone",
        ];

        // ── Roads ────────────────────────────────────────────────────────────────

        /// <summary>Known values for Road.Type.</summary>
        public static readonly string[] RoadTypes =
        [
            "Dirt",
            "Stone",
        ];

        /// <summary>Known values for RoadEndpoint.Type.</summary>
        public static readonly string[] RoadEndpointTypes =
        [
            "Connection",
            "MainObject",
            "MandatoryContent",
        ];

        // ── Connections ──────────────────────────────────────────────────────────

        /// <summary>Known values for Connection.ConnectionType.</summary>
        public static readonly string[] ConnectionTypes =
        [
            "Default",
            "Direct",
            "GladiatorArena",
            "Portal",
            "Proximity",
        ];

        /// <summary>Known values for Connection.GatePlacement.</summary>
        public static readonly string[] GatePlacements =
        [
            "Center",
        ];
    }
}
