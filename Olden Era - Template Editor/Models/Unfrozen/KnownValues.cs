namespace OldenEraTemplateEditor.Models
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
            //"Gladiator Arena",
            "Hold City",
            "Tournament",
        ];

        /// <summary>
        /// displayWinCondition JSON values, aligned with <see cref="VictoryConditionLabels"/>.
        /// </summary>
        public static readonly string[] VictoryConditionIds =
        [
            "win_condition_1",
            "win_condition_3",
            //"win_condition_4",
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

        // ── Bannable items catalog ────────────────────────────────────────────────

        /// <summary>
        /// Record for an artifact that can appear in globalBans.items.
        /// Category is one of: Movement, Diplomacy, Combat.
        /// Extend this list as new artifacts are added to the game.
        /// </summary>
        public record BannableItem(string Id, string DisplayName, string Category);

        /// <summary>
        /// Record for a spell that can appear in globalBans.magics.
        /// Extend this list as new spells are added to the game.
        /// </summary>
        public record BannableMagic(string Id, string DisplayName);

        /// <summary>Known bannable artifact IDs sourced from official game templates and test_content_lists.</summary>
        public static readonly BannableItem[] BannableItems =
        [
            // ── Movement ──────────────────────────────────────────────────────────
            new("pole_star_artifact",                                              "Pole Star",                                        "Movement"),
            new("seven_league_boots_artifact",                                     "Seven League Boots",                               "Movement"),
            new("swamp_boots_artifact",                                            "Swamp Boots",                                      "Movement"),
            new("warlord_boots_artifact",                                          "Warlord Boots",                                    "Movement"),
            new("magic_key_ring_artifact",                                         "Magic Key Ring",                                   "Movement"),
            new("legions_step_artifact",                                           "Legion's Step",                                    "Movement"),
            new("fallen_angel_wings_artifact",                                     "Fallen Angel Wings",                               "Movement"),
            new("banner_of_four_winds_artifact",                                   "Banner of Four Winds",                             "Movement"),
            new("spyglass_artifact",                                               "Spyglass",                                         "Movement"),

            // ── Diplomacy ─────────────────────────────────────────────────────────
            new("voodoosh_doll_artifact",                                          "Voodoosh Doll",                                    "Diplomacy"),
            new("flag_of_truce_artifact",                                          "Flag of Truce",                                    "Diplomacy"),
            new("ring_of_neutrality_artifact",                                     "Ring of Neutrality",                               "Diplomacy"),

            // ── Combat ───────────────────────────────────────────────────────────
            new("shackles_of_war_artifact",                                        "Shackles of War",                                  "Combat"),
            new("ogres_club_of_havoc_artifact",                                    "Ogre's Club of Havoc",                             "Combat"),
            new("tarq_of_the_rampaging_ogre_artifact",                             "Tarq of the Rampaging Ogre",                       "Combat"),
            new("tunic_of_the_cyclops_king_artifact",                              "Tunic of the Cyclops King",                        "Combat"),
            new("garotte_artifact",                                                "Garotte",                                          "Combat"),
            new("hourglass_of_protection_artifact",                                "Hourglass of Protection",                          "Combat"),
            new("shoddy_shield_artifact",                                          "Shoddy Shield",                                    "Combat"),
            new("eagle_armor_artifact",                                            "Eagle Armor",                                      "Combat"),
            new("chain_mail_artifact",                                             "Chain Mail",                                       "Combat"),
            new("head_torch_artifact",                                             "Head Torch",                                       "Combat"),
            new("fine_wand_artifact",                                              "Fine Wand",                                        "Combat"),
            new("lords_ring_artifact",                                             "Lord's Ring",                                      "Combat"),

            // ── Magic ────────────────────────────────────────────────────────────
            new("catechism_of_night_magic_artifact",                               "Catechism of Night Magic",                         "Magic"),
            new("catechism_of_daylight_magic_artifact",                            "Catechism of Daylight Magic",                      "Magic"),
            new("catechism_of_spacetime_magic_artifact",                           "Catechism of Spacetime Magic",                     "Magic"),
            new("catechism_of_primal_magic_artifact",                              "Catechism of Primal Magic",                        "Magic"),
            new("spellbinders_hat_artifact",                                       "Spellbinder's Hat",                                "Magic"),
            new("spells_in_a_bottle_artifact",                                     "Spells in a Bottle",                               "Magic"),
            new("orb_of_inhibition_artifact",                                      "Orb of Inhibition",                                "Magic"),
            new("orb_of_destruction_artifact",                                     "Orb of Destruction",                               "Magic"),
            new("seal_of_silence_artifact",                                        "Seal of Silence",                                  "Magic"),
            new("crown_of_the_supreme_magi_artifact",                              "Crown of the Supreme Magi",                        "Magic"),
            new("clothes_of_enlightenment_artifact",                               "Clothes of Enlightenment",                         "Magic"),
            new("cards_deck_artifact",                                             "Cards Deck",                                       "Magic"),
            new("runestone_shards_artifact",                                       "Runestone Shards",                                 "Magic"),

            // ── Misc (standalone) ────────────────────────────────────────────────
            new("golden_goose_egg_artifact",                                       "Golden Goose Egg",                                 "Misc"),
            new("tactical_guide_artifact",                                         "Tactical Guide",                                   "Misc"),
            new("endless_bag_artifact",                                            "Endless Bag",                                      "Misc"),
            new("soulless_sash_artifact",                                          "Soulless Sash",                                    "Misc"),
            new("monster_head_artifact",                                           "Monster Head",                                     "Misc"),
            new("omencaller_artifact",                                             "Omencaller",                                       "Misc"),
            new("sixth_finger_artifact",                                           "Sixth Finger",                                     "Misc"),
            new("soulscaller_ring_artifact",                                       "Soulscaller Ring",                                 "Misc"),
            new("chain_link_artifact",                                             "Chain Link",                                       "Misc"),
            new("demonic_heart_artifact",                                          "Demonic Heart",                                    "Misc"),
            new("two_faced_mask_artifact",                                         "Two-Faced Mask",                                   "Misc"),
            new("ancient_idol_artifact",                                           "Ancient Idol",                                     "Misc"),
            new("excalibur_artifact",                                              "Excalibur",                                        "Misc"),
            new("caduceus_artifact",                                               "Caduceus",                                         "Misc"),

            // ── Set: Resonant Sphere ──────────────────────────────────────────────
            new("resonant_sphere_orb_of_twilight_artifact",                        "Resonant Sphere: Orb of Twilight",                 "Set"),
            new("resonant_sphere_orb_of_daylight_artifact",                        "Resonant Sphere: Orb of Daylight",                 "Set"),
            new("resonant_sphere_orb_of_eternity_artifact",                        "Resonant Sphere: Orb of Eternity",                 "Set"),
            new("resonant_sphere_primal_orb_artifact",                             "Resonant Sphere: Primal Orb",                      "Set"),

            // ── Set: Tranquility ──────────────────────────────────────────────────
            new("tranquility_brightmind_tiara_artifact",                           "Tranquility: Brightmind Tiara",                    "Set"),
            new("tranquility_magic_mirror_artifact",                               "Tranquility: Magic Mirror",                        "Set"),
            new("tranquility_ring_of_serenity_artifact",                           "Tranquility: Ring of Serenity",                    "Set"),

            // ── Set: Shamaniac Soul ───────────────────────────────────────────────
            new("shamaniac_soul_shaman_staff_artifact",                            "Shamaniac Soul: Shaman Staff",                     "Set"),
            new("shamaniac_soul_iridescent_cloak_artifact",                        "Shamaniac Soul: Iridescent Cloak",                 "Set"),
            new("shamaniac_soul_gemwood_mask_artifact",                            "Shamaniac Soul: Gemwood Mask",                     "Set"),
            new("shamaniac_soul_clutching_ring_artifact",                          "Shamaniac Soul: Clutching Ring",                   "Set"),

            // ── Set: Knight's Honor ───────────────────────────────────────────────
            new("knights_honor_drums_of_war_artifact",                             "Knight's Honor: Drums of War",                     "Set"),
            new("knights_honor_lance_artifact",                                    "Knight's Honor: Lance",                            "Set"),
            new("knights_honor_misericorde_artifact",                              "Knight's Honor: Misericorde",                      "Set"),
            new("knights_honor_plate_armor_artifact",                              "Knight's Honor: Plate Armor",                      "Set"),
            new("knights_honor_armet_artifact",                                    "Knight's Honor: Armet",                            "Set"),

            // ── Set: Ukhtabar Seal ────────────────────────────────────────────────
            new("ukhtabar_seal_ukh_seal_artifact",                                 "Ukhtabar Seal: Ukh Seal",                          "Set"),
            new("ukhtabar_seal_tabar_seal_artifact",                               "Ukhtabar Seal: Tabar Seal",                        "Set"),

            // ── Set: Milo's Curse ─────────────────────────────────────────────────
            new("milos_curse_golden_pig_artifact",                                 "Milo's Curse: Golden Pig",                         "Set"),
            new("milos_curse_golden_moth_artifact",                                "Milo's Curse: Golden Moth",                        "Set"),
            new("milos_curse_skull_of_milos_artifact",                             "Milo's Curse: Skull of Milos",                     "Set"),

            // ── Set: Pauper's Glory ───────────────────────────────────────────────
            new("paupers_glory_wooden_ring_artifact",                              "Pauper's Glory: Wooden Ring",                      "Set"),
            new("paupers_glory_straw_hat_artifact",                                "Pauper's Glory: Straw Hat",                        "Set"),
            new("paupers_glory_rope_belt_artifact",                                "Pauper's Glory: Rope Belt",                        "Set"),
            new("paupers_glory_rags_artifact",                                     "Pauper's Glory: Rags",                             "Set"),
            new("paupers_glory_dumb_club_artifact",                                "Pauper's Glory: Dumb Club",                        "Set"),
            new("paupers_glory_last_coin_artifact",                                "Pauper's Glory: Last Coin",                        "Set"),

            // ── Set: Angelic Alliance ─────────────────────────────────────────────
            new("angelic_alliance_sword_of_judgement_artifact",                    "Angelic Alliance: Sword of Judgement",             "Set"),
            new("angelic_alliance_celestial_sash_of_bliss_artifact",               "Angelic Alliance: Celestial Sash of Bliss",        "Set"),
            new("angelic_alliance_lions_shield_of_courage_artifact",               "Angelic Alliance: Lion's Shield of Courage",       "Set"),
            new("angelic_alliance_armor_of_wonder_artifact",                       "Angelic Alliance: Armor of Wonder",                "Set"),
            new("angelic_alliance_helm_of_heavenly_enlightenment_artifact",        "Angelic Alliance: Helm of Heavenly Enlightenment", "Set"),
            new("angelic_alliance_sandals_of_the_saint_artifact",                  "Angelic Alliance: Sandals of the Saint",           "Set"),

            // ── Set: Gifts of Dwarven Lords ───────────────────────────────────────
            new("gifts_of_dwarven_lords_automated_antimagic_shield_artifact",      "Dwarven Gifts: Automated Antimagic Shield",        "Set"),
            new("gifts_of_dwarven_lords_automated_antimagic_shield_artifact_alt",  "Dwarven Gifts: Automated Antimagic Shield (Alt)",  "Set"),
            new("gifts_of_dwarven_lords_protective_belt_artifact",                 "Dwarven Gifts: Protective Belt",                   "Set"),
            new("gifts_of_dwarven_lords_protective_belt_artifact_alt",             "Dwarven Gifts: Protective Belt (Alt)",             "Set"),
            new("gifts_of_dwarven_lords_crimson_resonance_controller_artifact",    "Dwarven Gifts: Crimson Resonance Controller",      "Set"),
            new("gifts_of_dwarven_lords_crimson_resonance_controller_artifact_alt","Dwarven Gifts: Crimson Resonance Controller (Alt)","Set"),
            new("gifts_of_dwarven_lords_emerald_resonance_controller_artifact",    "Dwarven Gifts: Emerald Resonance Controller",      "Set"),
            new("gifts_of_dwarven_lords_emerald_resonance_controller_artifact_alt","Dwarven Gifts: Emerald Resonance Controller (Alt)","Set"),

            // ── Set: Elixir of Life ───────────────────────────────────────────────
            new("elixir_of_life_flask_of_oblivion_artifact",                       "Elixir of Life: Flask of Oblivion",                "Set"),
            new("elixir_of_life_lifeblood_fairy_artifact",                         "Elixir of Life: Lifeblood Fairy",                  "Set"),
            new("elixir_of_life_ring_of_life_artifact",                            "Elixir of Life: Ring of Life",                     "Set"),

            // ── Set: Shadow of Death ──────────────────────────────────────────────
            new("shadow_of_death_cursed_armor_artifact",                           "Shadow of Death: Cursed Armor",                    "Set"),
            new("shadow_of_death_bone_boots_artifact",                             "Shadow of Death: Bone Boots",                      "Set"),
            new("shadow_of_death_second_shade_artifact",                           "Shadow of Death: Second Shade",                    "Set"),
            new("shadow_of_death_dark_hatchet_artifact",                           "Shadow of Death: Dark Hatchet",                    "Set"),

            // ── Set: Wanderer's Way ───────────────────────────────────────────────
            new("wanderers_way_boots_of_travel_artifact",                          "Wanderer's Way: Boots of Travel",                  "Set"),
            new("wanderers_way_backpack_artifact",                                 "Wanderer's Way: Backpack",                         "Set"),

            // ── Set: Living Arrows ────────────────────────────────────────────────
            new("living_arrows_shroomwood_bow_artifact",                           "Living Arrows: Shroomwood Bow",                    "Set"),
            new("living_arrows_light_and_shade_cloak_artifact",                    "Living Arrows: Light and Shade Cloak",             "Set"),
            new("living_arrows_quivering_quiver_artifact",                         "Living Arrows: Quivering Quiver",                  "Set"),

            // ── Set: Duelist's Pride ──────────────────────────────────────────────
            new("duelists_pride_rapier_artifact",                                  "Duelist's Pride: Rapier",                          "Set"),
            new("duelists_pride_buckler_artifact",                                 "Duelist's Pride: Buckler",                         "Set"),
            new("duelists_pride_brass_knuckles_artifact",                          "Duelist's Pride: Brass Knuckles",                  "Set"),

            // ── Set: Ethereal Knowledge ───────────────────────────────────────────
            new("ethereal_knowledge_glass_dagger_artifact",                        "Ethereal Knowledge: Glass Dagger",                 "Set"),
            new("ethereal_knowledge_mirror_shoes_artifact",                        "Ethereal Knowledge: Mirror Shoes",                 "Set"),
            new("ethereal_knowledge_vortex_dress_artifact",                        "Ethereal Knowledge: Vortex Dress",                 "Set"),
            new("ethereal_knowledge_third_eye_artifact",                           "Ethereal Knowledge: Third Eye",                    "Set"),

            // ── Set: Inner Song ───────────────────────────────────────────────────
            new("inner_song_music_sheet_artifact",                                 "Inner Song: Music Sheet",                          "Set"),
            new("inner_song_singing_pan_pipe_artifact",                            "Inner Song: Singing Pan Pipe",                     "Set"),
            new("inner_song_fancy_mask_artifact",                                  "Inner Song: Fancy Mask",                           "Set"),

            // ── Set: Power of the Dragon Father ──────────────────────────────────
            new("power_of_the_dragon_father_red_dragon_flame_tongue_artifact",     "Dragon Father: Red Dragon Flame Tongue",           "Set"),
            new("power_of_the_dragon_father_dragon_scale_shield_artifact",         "Dragon Father: Dragon Scale Shield",               "Set"),
            new("power_of_the_dragon_father_dragon_scale_armor_artifact",          "Dragon Father: Dragon Scale Armor",                "Set"),
            new("power_of_the_dragon_father_dragon_crest_artifact",                "Dragon Father: Dragon Crest",                      "Set"),
            new("power_of_the_dragon_father_dragonbone_greaves_artifact",          "Dragon Father: Dragonbone Greaves",                "Set"),
            new("power_of_the_dragon_father_slithering_sash_artifact",             "Dragon Father: Slithering Sash",                   "Set"),
            new("power_of_the_dragon_father_dragon_wing_artifact",                 "Dragon Father: Dragon Wing",                       "Set"),
            new("power_of_the_dragon_father_piercing_eye_of_a_dragon_artifact",    "Dragon Father: Piercing Eye of a Dragon",          "Set"),

            // ── Set: Beelzebub's Blessing ─────────────────────────────────────────
            new("beelzebubs_blessing_demon_claw_artifact",                         "Beelzebub's Blessing: Demon Claw",                 "Set"),
            new("beelzebubs_blessing_chitinous_shield_artifact",                   "Beelzebub's Blessing: Chitinous Shield",           "Set"),
            new("beelzebubs_blessing_heartbeat_artifact",                          "Beelzebub's Blessing: Heartbeat",                  "Set"),
            new("beelzebubs_blessing_demon_crest_artifact",                        "Beelzebub's Blessing: Demon Crest",                "Set"),

            // ── Set: Boreolos ─────────────────────────────────────────────────────
            new("boreolos_hand_artifact",                                          "Boreolos: Hand",                                   "Set"),
            new("boreolos_foot_artifact",                                          "Boreolos: Foot",                                   "Set"),
            new("boreolos_heart_artifact",                                         "Boreolos: Heart",                                  "Set"),
            new("boreolos_head_artifact",                                          "Boreolos: Head",                                   "Set"),

            // ── Set: Holy Sigils ──────────────────────────────────────────────────
            new("holy_sigil_of_roph_artifact",                                     "Holy Sigil of Roph",                               "Set"),
            new("holy_sigil_of_eridore_artifact",                                  "Holy Sigil of Eridore",                            "Set"),
            new("holy_sigil_of_mearea_artifact",                                   "Holy Sigil of Mearea",                             "Set"),
            new("holy_sigil_of_insara_artifact",                                   "Holy Sigil of Insara",                             "Set"),
            new("holy_sigil_of_quix_artifact",                                     "Holy Sigil of Quix",                               "Set"),
            new("holy_sigil_of_the_seven_magi_artifact",                           "Holy Sigil of the Seven Magi",                     "Set"),
            new("holy_sigil_of_the_second_man_artifact",                           "Holy Sigil of the Second Man",                     "Set"),
            new("holy_sigil_of_uurdt_artifact",                                    "Holy Sigil of Uurdt",                              "Set"),

            // ── Set: Rule of Shadow ───────────────────────────────────────────────
            new("rule_of_shadow_liquid_silence_artifact",                          "Rule of Shadow: Liquid Silence",                   "Set"),
            new("rule_of_shadow_the_truthmaker_artifact",                          "Rule of Shadow: The Truthmaker",                   "Set"),
            new("rule_of_shadow_the_truthseeker_artifact",                         "Rule of Shadow: The Truthseeker",                  "Set"),
            new("rule_of_shadow_nostrias_gaze_artifact",                           "Rule of Shadow: Nostria's Gaze",                   "Set"),

            // ── Set: Ambassador's Word ────────────────────────────────────────────
            new("ambassadors_word_diplomatic_gifts_artifact",                      "Ambassador's Word: Diplomatic Gifts",              "Set"),
            new("ambassadors_word_ambassadors_sash_artifact",                      "Ambassador's Word: Ambassador's Sash",             "Set"),

            // ── Set: Warrior's Strength ───────────────────────────────────────────
            new("warriors_strength_warriors_belt_artifact",                        "Warrior's Strength: Warrior's Belt",               "Set"),
            new("warriors_strength_warriors_oberegus_artifact",                    "Warrior's Strength: Warrior's Oberegus",           "Set"),

            // ── Set: Keeper's Fortitude ───────────────────────────────────────────
            new("keepers_fortitude_keepers_ring_artifact",                         "Keeper's Fortitude: Keeper's Ring",                "Set"),
            new("keepers_fortitude_keepers_oberegus_artifact",                     "Keeper's Fortitude: Keeper's Oberegus",            "Set"),

            // ── Set: Wizard's Might ───────────────────────────────────────────────
            new("wizards_might_wizards_cloak_artifact",                            "Wizard's Might: Wizard's Cloak",                   "Set"),
            new("wizards_might_wizards_oberegus_artifact",                         "Wizard's Might: Wizard's Oberegus",                "Set"),

            // ── Set: Scholar's Wisdom ─────────────────────────────────────────────
            new("scholars_wisdom_scholars_tiara_artifact",                         "Scholar's Wisdom: Scholar's Tiara",                "Set"),
            new("scholars_wisdom_scholars_oberegus_artifact",                      "Scholar's Wisdom: Scholar's Oberegus",             "Set"),
        ];

        /// <summary>Known bannable spell IDs sourced from official game templates.</summary>
        public static readonly BannableMagic[] BannableMagics =
        [
            new("neutral_magic_pocket_dimension", "Pocket Dimension"),
            new("neutral_magic_light_gate",       "Light Gate"),
            new("neutral_magic_town_portal",      "Town Portal"),
            new("neutral_magic_dimension_door",   "Dimension Door"),
            new("neutral_magic_shadow_form",      "Shadow Form"),
        ];

        /// <summary>A single learnable spell from the game's spell library.</summary>
        public record SpellEntry(string Id, string Name, string School, int Tier);

        /// <summary>All learnable spells grouped by school, sorted by tier within each school.</summary>
        public static readonly SpellEntry[] KnownSpells =
        [
            // ── Neutral ─────────────────────────────────────────────────────────
            new("neutral_magic_pocket_dimension", "Pocket Dimension", "neutral", 2),
            new("neutral_magic_second_sight",     "Second Sight",     "neutral", 2),
            new("neutral_magic_shadow_form",      "Shadowflight",     "neutral", 3),
            new("neutral_magic_town_portal",      "Town Portal",      "neutral", 3),
            new("neutral_magic_dimension_door",   "Dimension Door",   "neutral", 4),
            new("neutral_magic_light_gate",       "Gate of Light",    "neutral", 4),

            // ── Day ─────────────────────────────────────────────────────────────
            new("day_2_magic_sharp_edge",       "Blessing",          "day", 1),
            new("day_3_magic_haste",            "Haste",             "day", 1),
            new("day_1_magic_healing_water",    "Healing Water",     "day", 1),
            new("day_5_magic_shorten_shadow",   "Shorten Shadow",    "day", 1),
            new("day_4_magic_favorable_wind",   "Favorable Wind",    "day", 2),
            new("day_17_magic_clear_view",      "From a Bird's Eye", "day", 2),
            new("day_7_magic_inner_light",      "Inner Light",       "day", 2),
            new("day_6_magic_cleansing_ray",    "Weakening Ray",     "day", 2),
            new("day_9_magic_arinas_hymn",      "Arina's Touch",     "day", 3),
            new("day_11_magic_masterful_parry", "Riposte",           "day", 3),
            new("day_10_magic_second_song",     "Song of Power",     "day", 3),
            new("day_8_magic_taunt",            "Taunt",             "day", 3),
            new("day_18_magic_farsight",        "Clear Fog",         "day", 4),
            new("day_13_magic_holy_arms",       "Heavenly Blades",   "day", 4),
            new("day_12_magic_radiant_armor",   "Radiant Armor",     "day", 4),
            new("day_14_magic_vengeance",       "Vengeance",         "day", 4),
            new("day_16_magic_arinas_chosen",   "Arina's Chosen",    "day", 5),
            new("day_15_magic_judgement",       "Judgement",         "day", 5),

            // ── Night ───────────────────────────────────────────────────────────
            new("night_4_magic_despair",           "Despair",           "night", 1),
            new("night_3_magic_enlarge_shadow",    "Enlarge Shadow",    "night", 1),
            new("night_7_magic_fatal_decay",       "Fatal Decay",       "night", 1),
            new("night_1_magic_unnatural_calm",    "Unnatural Calm",    "night", 1),
            new("night_17_magic_read_minds",       "Read Minds",        "night", 2),
            new("night_5_magic_shade_cloak",       "Shade Cloak",       "night", 2),
            new("night_6_magic_deaths_grip",       "Umbral Grip",       "night", 2),
            new("night_2_magic_web",               "Web",               "night", 2),
            new("night_18_magic_nairas_veil",      "Naira's Veil",      "night", 3),
            new("night_10_magic_silence",          "Silence",           "night", 3),
            new("night_8_magic_sleep",             "Sleep",             "night", 3),
            new("night_9_magic_twilight",          "Twilight",          "night", 3),
            new("night_13_magic_berserker",        "Berserk",           "night", 4),
            new("night_12_magic_summon_starchild", "Summon Starchild",  "night", 4),
            new("night_11_magic_vulnerability",    "Vulnerability",     "night", 4),
            new("night_15_magic_deaths_call",      "Coup de Grace",     "night", 5),
            new("night_14_magic_nairas_kiss",      "Naira's Kiss",      "night", 5),
            new("night_16_magic_shadow_army",      "Shadow Army",       "night", 5),

            // ── Primal ──────────────────────────────────────────────────────────
            new("primal_17_magic_groundsight",              "Groundsight",           "primal", 1),
            new("primal_1_magic_thunderbolt",               "Lightning Bolt",        "primal", 1),
            new("primal_2_magic_thick_hide",                "Thick Hide",            "primal", 1),
            new("primal_5_magic_crystal_crown",             "Crystal Crown",         "primal", 2),
            new("primal_4_magic_fire_globe",                "Fireball",              "primal", 2),
            new("primal_6_magic_ice_bolt",                  "Ice Bolt",              "primal", 2),
            new("primal_3_magic_wean",                      "Wean",                  "primal", 2),
            new("primal_8_magic_cave_in",                   "Cave In",               "primal", 3),
            new("primal_9_magic_earths_rage",               "Earth's Rage",          "primal", 3),
            new("primal_7_magic_wall_of_flame",             "Firewall",              "primal", 3),
            new("primal_16_magic_stone_fangs",              "Stone Fangs",           "primal", 3),
            new("primal_10_magic_primordial_purity",        "Anti-Magic",            "primal", 4),
            new("primal_12_magic_chain_lightning",          "Chain Lightning",       "primal", 4),
            new("primal_13_magic_avalanche",                "Circle of Winter",      "primal", 4),
            new("primal_18_magic_primordial_chaos",         "Primordial Chaos",      "primal", 4),
            new("primal_11_magic_armageddon",               "Armageddon",            "primal", 5),
            new("primal_14_magic_hksmillas_rampage",        "Hksmilla's Rampage",    "primal", 5),
            new("primal_15_magic_summon_primal_remnant",    "Summon Primal Remnant", "primal", 5),

            // ── Space ───────────────────────────────────────────────────────────
            new("space_1_magic_early_start",          "Early Start",         "space", 1),
            new("space_3_magic_energyze",             "Energize",            "space", 1),
            new("space_11_magic_decimate",            "Guillotine",          "space", 1),
            new("space_4_magic_optical_illusion",     "Optical Illusion",    "space", 1),
            new("space_6_magic_blink",                "Blink",               "space", 2),
            new("space_8_magic_carapace",             "Carapace",            "space", 2),
            new("space_2_magic_energy_explosion",     "Energy Explosion",    "space", 2),
            new("space_17_magic_reinforcements",      "Reinforcements",      "space", 2),
            new("space_18_magic_assemble",            "Assemble!",           "space", 3),
            new("space_9_magic_impending_fate",       "Impending Fate",      "space", 3),
            new("space_7_magic_shackles",             "Shackles",            "space", 3),
            new("space_5_magic_trap_jaws",            "Temporal Spheres",    "space", 3),
            new("space_10_magic_mirror_copy",         "Mirror Copy",         "space", 4),
            new("space_12_magic_rewind",              "Rewind Life",         "space", 4),
            new("space_15_magic_trap_snare",          "Spatial Snare",       "space", 4),
            new("space_13_magic_black_hole",          "Black Hole",          "space", 5),
            new("space_14_magic_doreaths_tide",       "Doreath's Tide",      "space", 5),
            new("space_16_magic_reality_distortion",  "Reality Distortion",  "space", 5),
        ];

        /// <summary>
        /// Converts a snake_case SID (with optional _artifact suffix) to a Title Case display name.
        /// Used as fallback for IDs not in the catalog.
        /// </summary>
        public static string SidToDisplayName(string sid)
        {
            var s = sid.Replace("_artifact", "").Replace('_', ' ');
            if (s.Length == 0) return sid;
            return char.ToUpper(s[0]) + s[1..];
        }
    }
}
