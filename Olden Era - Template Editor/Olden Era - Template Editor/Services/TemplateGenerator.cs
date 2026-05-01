using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Generates an RmgTemplate based on <see cref="GeneratorSettings"/>,
    /// using Jebus Cross Classic as a structural base.
    /// </summary>
    public static class TemplateGenerator
    {
        // Each player zone gets 5 guard connections to the center (same as JCC).
        private const int ConnectionsPerZone = 5;

        // Letters used to name zones: A, B, C … up to 8 players + 8 neutral zones = 16 max.
        private static readonly string[] ZoneLetters =
            ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P"];

        public static RmgTemplate Generate(GeneratorSettings settings)
        {
            var playerLetters = ZoneLetters.Take(settings.PlayerCount).ToList();
            var neutralLetters = ZoneLetters.Skip(settings.PlayerCount).Take(settings.NeutralZoneCount).ToList();

            var template = new RmgTemplate
            {
                Name = settings.TemplateName,
                GameMode = settings.GameMode,
                Description = "custom_generated_template",
                DisplayWinCondition = settings.VictoryCondition,
                SizeX = settings.MapSize,
                SizeZ = settings.MapSize,
                GameRules = BuildGameRules(settings),
                Variants = [BuildVariant(settings, playerLetters, neutralLetters)],
                ZoneLayouts = BuildZoneLayouts(),
                MandatoryContent = BuildAllMandatoryContent(playerLetters, neutralLetters),
                ContentCountLimits = BuildAllContentCountLimits(),
                ContentPools = [],
                ContentLists = []
            };

            return template;
        }

        // ── Game rules ───────────────────────────────────────────────────────────

        private static GameRules BuildGameRules(GeneratorSettings settings) => new()
        {
            HeroCountMin = settings.HeroCountMin,
            HeroCountMax = settings.HeroCountMax,
            HeroCountIncrement = 1,
            HeroHireBan = false,
            EncounterHoles = false,
            FactionLawsExpModifier = 1.0,
            AstrologyExpModifier = 1.0,
            Bonuses =
            [
                new Bonus
                {
                    Sid = "add_bonus_hero_stat",
                    ReceiverSide = -1,
                    ReceiverFilter = "all_heroes",
                    Parameters = ["movementBonus", "0"]
                }
            ],
            WinConditions = new WinConditions
            {
                Classic = true,
                Desertion = true,
                DesertionDay = 3,
                DesertionValue = 3000,
                HeroLighting = true,
                HeroLightingDay = 1,
                LostStartCity = false,
                LostStartCityDay = 3,
                LostStartHero = false,
                CityHold = true,
                CityHoldDays = 6
            }
        };

        // ── Variant ──────────────────────────────────────────────────────────────

        private static Variant BuildVariant(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            int totalZones = settings.PlayerCount + settings.NeutralZoneCount;

            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Center zone
            zones.Add(BuildCenterZone(playerLetters, neutralLetters));

            // Player spawn zones
            for (int i = 0; i < playerLetters.Count; i++)
            {
                string letter = playerLetters[i];
                string player = $"Player{i + 1}";
                string connName = $"Center-{letter}-Main";
                zones.Add(BuildSpawnZone(letter, player, connName));
                connections.AddRange(BuildZoneConnections(letter, "Spawn", connName, 66666));
            }

            // Neutral zones
            for (int i = 0; i < neutralLetters.Count; i++)
            {
                string letter = neutralLetters[i];
                string connName = $"Center-{letter}-Main";
                zones.Add(BuildNeutralZone(letter, connName));
                connections.AddRange(BuildZoneConnections(letter, "Neutral", connName, 55555));
            }

            // Proximity (pseudo) connections between adjacent outer zones to
            // keep the same "no direct border" layout feel as JCC.
            connections.AddRange(BuildProximityConnections(playerLetters, neutralLetters));

            return new Variant
            {
                Orientation = new Orientation
                {
                    ZeroAngleZone = playerLetters.Count > 0 ? $"Spawn-{playerLetters[0]}" : "Center",
                    BaseAngleMin = 45,
                    BaseAngleMax = 45,
                    RandomAngleAmplitude = 360,
                    RandomAngleStep = 360.0 / totalZones
                },
                Border = new Border
                {
                    CornerRadius = 0.0,
                    ObstaclesWidth = 3,
                    ObstaclesNoise = [new NoiseEntry { Amp = 1, Freq = 12 }],
                    WaterWidth = 0,
                    WaterNoise = [new NoiseEntry { Amp = 1, Freq = 12 }],
                    WaterType = "water grass"
                },
                Zones = zones,
                Connections = connections
            };
        }

        // ── Center zone ──────────────────────────────────────────────────────────

        private static Zone BuildCenterZone(List<string> playerLetters, List<string> neutralLetters)
        {
            // The center city's additional connection-placed cities reference each arm connection.
            var centerCityObjects = new List<MainObject>
            {
                new()
                {
                    Type = "City",
                    GuardValue = 25000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "ultra_rich_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Center",
                    HoldCityWinCon = true
                }
            };

            // One extra city per player arm (differentFrom player faction).
            foreach (var letter in playerLetters)
            {
                centerCityObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 0.5,
                    GuardValue = 10000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "rich_buildings_construction",
                    Placement = "Connection",
                    PlacementArgs = [$"Center-{letter}-Main"],
                    Faction = new TypedSelector
                    {
                        Type = "FromList",
                        Args = [$"differentFrom: 0 Spawn-{letter}"]
                    }
                });
            }

            // One neutral city per neutral arm (no faction constraint).
            foreach (var letter in neutralLetters)
            {
                centerCityObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 0.5,
                    GuardValue = 10000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "rich_buildings_construction",
                    Placement = "Connection",
                    PlacementArgs = [$"Center-{letter}-Main"],
                    Faction = new TypedSelector { Type = "FromList", Args = [] }
                });
            }

            // Roads from center city (index 0) to each arm connection.
            var allLetters = playerLetters.Concat(neutralLetters).ToList();
            var roads = new List<Road>();
            foreach (var letter in allLetters)
            {
                roads.Add(StoneRoad(MainObjectEndpoint("0"), ConnectionEndpoint($"Center-{letter}-Main")));
            }

            // Roads from each arm city to its connection.
            for (int i = 0; i < allLetters.Count; i++)
            {
                roads.Add(StoneRoad(MainObjectEndpoint($"{i + 1}"), ConnectionEndpoint($"Center-{allLetters[i]}-Main")));
            }

            // Dirt roads from center city to remote footholds (one per player arm).
            for (int i = 1; i <= playerLetters.Count; i++)
            {
                roads.Add(DirtRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint($"name_remote_foothold_{i}")));
            }

            // Mandatory content limits per player count.
            var countLimits = BuildCenterContentLimits(playerLetters.Count);

            return new Zone
            {
                Name = "Center",
                Size = 1.35,
                Layout = "zone_layout_center",
                GuardCutoffValue = 3000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.6,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [40, 20, 10, 5, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_center_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_center_zone"],
                ResourcesContentPool = ["content_pool_general_resources_treasure_zone_rich_no_scrolls"],
                MandatoryContent = ["mandatory_content_center"],
                ContentCountLimits = countLimits,
                GuardedContentValue = 2150000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 125000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 0,
                ResourcesValuePerArea = 0,
                MainObjects = centerCityObjects,
                ZoneBiome = new BiomeSelector { Type = "FromList", Args = ["Sand"] },
                ContentBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = roads
            };
        }

        // ── Spawn zone ───────────────────────────────────────────────────────────

        private static Zone BuildSpawnZone(string letter, string player, string mainConnName)
        {
            return new Zone
            {
                Name = $"Spawn-{letter}",
                Size = 1.0,
                Layout = "zone_layout_spawns",
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.0,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_start_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_start_zone"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
                MandatoryContent = [$"mandatory_content_side_{letter}"],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = 500000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 125000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 200000,
                ResourcesValuePerArea = 0,
                MainObjects =
                [
                    new()
                    {
                        Type = "Spawn",
                        Spawn = player,
                        RemoveGuardIfHasOwner = true,
                        GuardChance = 0.5,
                        GuardValue = 5000,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "default_buildings_construction",
                        Placement = "Uniform",
                        PlacementArgs = ["true", "0.7", "0"]
                    },
                    new()
                    {
                        Type = "City",
                        Faction = new TypedSelector { Type = "Match", Args = ["0"] },
                        GuardChance = 0.5,
                        GuardValue = 2500,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "poor_buildings_construction",
                        Placement = "Uniform",
                        PlacementArgs = ["false", "-0.8", "3"]
                    },
                    new()
                    {
                        Type = "City",
                        Faction = new TypedSelector { Type = "Match", Args = ["0"] },
                        GuardChance = 0.5,
                        GuardValue = 2500,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "poor_buildings_construction",
                        Placement = "Uniform",
                        PlacementArgs = ["false", "-0.8", "3"]
                    }
                ],
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads =
                [
                    PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(mainConnName)),
                    PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint("1")),
                    PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint("2")),
                    PlainRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint("name_remote_foothold_1"))
                ]
            };
        }

        // ── Neutral zone ─────────────────────────────────────────────────────────

        private static Zone BuildNeutralZone(string letter, string mainConnName)
        {
            return new Zone
            {
                Name = $"Neutral-{letter}",
                Size = 1.0,
                Layout = "zone_layout_spawns",
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.3,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [0, 10, 10, 10, 10, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_side_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_side_zone"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
                MandatoryContent = [$"mandatory_content_neutral_{letter}"],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = 650000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 100000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 150000,
                ResourcesValuePerArea = 0,
                MainObjects =
                [
                    new()
                    {
                        Type = "City",
                        GuardChance = 0.5,
                        GuardValue = 10000,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "rich_buildings_construction",
                        Faction = new TypedSelector { Type = "FromList", Args = [] },
                        Placement = "Uniform",
                        PlacementArgs = ["true", "0.8", "2"]
                    },
                    new()
                    {
                        Type = "City",
                        Faction = new TypedSelector { Type = "Match", Args = ["0"] },
                        GuardChance = 0.5,
                        GuardValue = 2500,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "extra_rich_buildings_construction",
                        Placement = "Uniform",
                        PlacementArgs = ["false", "-0.8", "4"]
                    },
                    new()
                    {
                        Type = "City",
                        Faction = new TypedSelector { Type = "Match", Args = ["0"] },
                        GuardChance = 0.5,
                        GuardValue = 2500,
                        GuardWeeklyIncrement = 0.10,
                        BuildingsConstructionSid = "rich_buildings_construction",
                        Placement = "Uniform",
                        PlacementArgs = ["false", "-0.8", "4"]
                    }
                ],
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads =
                [
                    PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(mainConnName)),
                    PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint("1")),
                    PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint("2")),
                    PlainRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint("name_remote_foothold_1"))
                ]
            };
        }

        // ── Connections ──────────────────────────────────────────────────────────

        private static IEnumerable<Connection> BuildZoneConnections(
            string letter, string zonePrefix, string mainConnName, int guardValue)
        {
            string zoneName = zonePrefix == "Spawn" ? $"Spawn-{letter}" : $"Neutral-{letter}";
            string matchGroupBase = zonePrefix == "Spawn" ? "spawn_main_guard" : "spawn_side_guard";

            // Named main connection (gate at center).
            yield return new Connection
            {
                Name = mainConnName,
                From = "Center",
                To = zoneName,
                ConnectionType = "Direct",
                GuardZone = "Center",
                GuardEscape = false,
                SimTurnSquad = true,
                GuardValue = guardValue,
                GuardWeeklyIncrement = 0.20,
                GuardMatchGroup = matchGroupBase,
                GatePlacement = "Center"
            };

            // Four additional unnamed guard connections (same as JCC).
            for (int i = 1; i <= 4; i++)
            {
                yield return new Connection
                {
                    From = "Center",
                    To = zoneName,
                    ConnectionType = "Direct",
                    GuardZone = "Center",
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = guardValue,
                    GuardWeeklyIncrement = 0.20,
                    GuardMatchGroup = $"{matchGroupBase}_{i}"
                };
            }
        }

        /// <summary>
        /// Adds Proximity (pseudo) connections between every pair of adjacent outer zones
        /// in a ring. Each pair gets one connection (no duplicates in both directions).
        /// </summary>
        private static IEnumerable<Connection> BuildProximityConnections(
            List<string> playerLetters, List<string> neutralLetters)
        {
            var allLetters = playerLetters.Concat(neutralLetters).ToList();
            int count = allLetters.Count;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                // Skip the last→first wrap when there are only 2 zones (they already share
                // the ring via i=0→1, so i=1→0 would be a duplicate).
                if (next == 0 && i != 0 && count == 2) continue;

                string fromLetter = allLetters[i];
                string toLetter = allLetters[next];

                string fromZone = playerLetters.Contains(fromLetter)
                    ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone = playerLetters.Contains(toLetter)
                    ? $"Spawn-{toLetter}" : $"Neutral-{toLetter}";

                yield return new Connection
                {
                    Name = $"Pseudo-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Proximity",
                    Length = 0.5
                };
            }
        }

        // ── Content limit helpers ────────────────────────────────────────────────

        private static List<string> BuildCenterContentLimits(int playerCount)
        {
            // JCC has limits for each player-count variant from 1..6.
            return Enumerable.Range(1, playerCount)
                .Select(n => $"content_limits_center_{n}")
                .ToList();
        }

        private static List<string> BuildSideContentLimits()
        {
            // Identical list used by every outer zone in JCC.
            var limits = new List<string>();
            for (int a = 1; a <= 5; a++)
                for (int b = a + 1; b <= 6; b++)
                    limits.Add($"content_limits_side_{a}_{b}");
            return limits;
        }

        // ── Road / endpoint factories ────────────────────────────────────────────

        private static Road StoneRoad(RoadEndpoint from, RoadEndpoint to) =>
            new() { Type = "Stone", From = from, To = to };

        private static Road DirtRoad(RoadEndpoint from, RoadEndpoint to) =>
            new() { Type = "Dirt", From = from, To = to };

        private static Road PlainRoad(RoadEndpoint from, RoadEndpoint to) =>
            new() { From = from, To = to };

        private static RoadEndpoint MainObjectEndpoint(string index) =>
            new() { Type = "MainObject", Args = [index] };

        private static RoadEndpoint ConnectionEndpoint(string name) =>
            new() { Type = "Connection", Args = [name] };

        private static RoadEndpoint MandatoryContentEndpoint(string name) =>
            new() { Type = "MandatoryContent", Args = [name] };

        // ── Zone layouts ─────────────────────────────────────────────────────────

        private static List<ZoneLayout> BuildZoneLayouts() =>
        [
            new ZoneLayout
            {
                Name = "zone_layout_center",
                ObstaclesFill = 0.36,
                ObstaclesFillVoid = 0.55,
                LakesFill = 0.3,
                MinLakeArea = 16,
                ElevationClusterScale = 0.16,
                ElevationModes =
                [
                    new ElevationMode { Weight = 2, MinElevatedFraction = 0.2, MaxElevatedFraction = 0.4 },
                    new ElevationMode { Weight = 1, MinElevatedFraction = 0.6, MaxElevatedFraction = 0.8 }
                ],
                RoadClusterArea = 160,
                GuardedEncounterResourceFractions = new GuardedEncounterResourceFractions
                {
                    CountBounds = [],
                    Fractions = [0.50]
                },
                AmbientPickupDistribution = new AmbientPickupDistribution
                {
                    Repulsion = 1.0,
                    Noise = 0.3,
                    RoadAttraction = -0.30,
                    ObstacleAttraction = 0.0,
                    GroupSizeWeights = [20, 2, 1]
                }
            },
            new ZoneLayout
            {
                Name = "zone_layout_spawns",
                ObstaclesFill = 0.24,
                ObstaclesFillVoid = 0.48,
                LakesFill = 0.3,
                MinLakeArea = 16,
                ElevationClusterScale = 0.16,
                ElevationModes =
                [
                    new ElevationMode { Weight = 2, MinElevatedFraction = 0.2, MaxElevatedFraction = 0.4 },
                    new ElevationMode { Weight = 1, MinElevatedFraction = 0.6, MaxElevatedFraction = 0.8 }
                ],
                RoadClusterArea = 160,
                GuardedEncounterResourceFractions = new GuardedEncounterResourceFractions
                {
                    CountBounds = [],
                    Fractions = [0.66]
                },
                AmbientPickupDistribution = new AmbientPickupDistribution
                {
                    Repulsion = 1.0,
                    Noise = 0.4,
                    RoadAttraction = -0.30,
                    ObstacleAttraction = 0.0,
                    GroupSizeWeights = [20, 2, 1]
                }
            }
        ];

        // ── Mandatory content ────────────────────────────────────────────────────

        private static List<MandatoryContentGroup> BuildAllMandatoryContent(
            List<string> playerLetters, List<string> neutralLetters)
        {
            var groups = new List<MandatoryContentGroup> { BuildCenterMandatoryContent(playerLetters.Count) };

            foreach (var letter in playerLetters)
                groups.Add(BuildSpawnMandatoryContent(letter, $"Center-{letter}-Main"));

            foreach (var letter in neutralLetters)
                groups.Add(BuildNeutralMandatoryContent(letter, $"Center-{letter}-Main"));

            return groups;
        }

        private static MandatoryContentGroup BuildCenterMandatoryContent(int playerCount)
        {
            var content = new List<ContentItem>();

            // High-value guarded objects
            foreach (var sid in new[] { "unstable_ruins", "dragon_utopia", "research_laboratory" })
                for (int v = 1; v <= 3; v++)
                    content.Add(new ContentItem
                    {
                        Sid = sid, Variant = v,
                        Rules = [new ContentPlacementRule { Type = "Sid", Args = [sid], TargetMin = 0.5, TargetMax = 0.5, Weight = 1 }]
                    });

            // Remote footholds — one per player arm
            for (int i = 1; i <= playerCount; i++)
                content.Add(new ContentItem
                {
                    Name = $"name_remote_foothold_{i}",
                    Sid = "remote_foothold",
                    IsGuarded = false,
                    Rules =
                    [
                        new ContentPlacementRule { Type = "Sid", Args = ["remote_foothold"], TargetMin = 0.5, TargetMax = 0.5, Weight = 1 },
                        new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 1 }
                    ]
                });

            // Ambient objects
            for (int i = 0; i < 4; i++) content.Add(new ContentItem { Sid = "mystical_tower" });
            for (int i = 0; i < 6; i++) content.Add(new ContentItem { Sid = "pandora_box" });
            for (int i = 0; i < 2; i++) content.Add(new ContentItem { Sid = "tree_of_abundance" });
            content.Add(new ContentItem { Sid = "mirage", IsGuarded = true, SoloEncounter = true });

            for (int i = 0; i < 4; i++)
                content.Add(new ContentItem
                {
                    Sid = "watchtower", IsMine = false, IsGuarded = false,
                    Rules = [new ContentPlacementRule { Type = "Sid", Args = ["watchtower"], TargetMin = 0.75, TargetMax = 0.75, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.05, TargetMax = 0.1, Weight = 1 }]
                });

            for (int i = 0; i < 4; i++)
                content.Add(new ContentItem
                {
                    Sid = "mana_well", IsGuarded = false,
                    Rules = [new ContentPlacementRule { Type = "Sid", Args = ["mana_well"], TargetMin = 0.5, TargetMax = 0.5, Weight = 1 }]
                });

            for (int i = 0; i < 6; i++) content.Add(new ContentItem { IncludeLists = ["basic_content_list_pickup_mythic_scroll_box"] });
            for (int i = 0; i < 6; i++) content.Add(new ContentItem { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"] });
            for (int i = 0; i < 4; i++) content.Add(new ContentItem { Sid = "mine_gold", IsMine = true });

            return new MandatoryContentGroup { Name = "mandatory_content_center", Content = content };
        }

        private static MandatoryContentGroup BuildSpawnMandatoryContent(string letter, string mainConnName)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_side_{letter}",
                Content = BuildOuterZoneMandatoryContent(mainConnName, isNeutral: false)
            };
        }

        private static MandatoryContentGroup BuildNeutralMandatoryContent(string letter, string mainConnName)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_neutral_{letter}",
                Content = BuildOuterZoneMandatoryContent(mainConnName, isNeutral: true)
            };
        }

        private static List<ContentItem> BuildOuterZoneMandatoryContent(string mainConnName, bool isNeutral)
        {
            var content = new List<ContentItem>
            {
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_base"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_base"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_pro"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },

                new() {
                    Name = "name_remote_foothold_1", Sid = "remote_foothold", IsGuarded = false,
                    Rules = [
                        new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.2, TargetMax = 0.3, Weight = 0 },
                        new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.2, TargetMax = 0.4, Weight = 0 },
                        new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.5, TargetMax = 0.5, Weight = 2 },
                        new ContentPlacementRule { Type = "MainObject", Args = ["2"], TargetMin = 0.5, TargetMax = 0.5, Weight = 2 },
                        new ContentPlacementRule { Type = "Connection", Args = [mainConnName], TargetMin = 0.75, TargetMax = 0.75, Weight = 6 }
                    ]
                },

                new() { Sid = "fountain_2", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "fountain", IsGuarded = false },
                new() { Sid = "watchtower", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "mana_well", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "mana_well", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Sid", Args = ["mana_well"], TargetMin = 0.5, TargetMax = 0.5, Weight = 1 }] },
                new() { Sid = "market" },
                new() { Sid = "forge" },
                new() { Sid = "tavern", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.4, Weight = 1 }] },
                new() { Sid = "stables", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "university" },
                new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
                new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
                new() { Sid = "college_of_wonder" },
                new() { Sid = "mystical_tower" },
                new() { Sid = "mystical_tower" },
                new() { Sid = "prison", Variant = 0, IsGuarded = false },
                new() { Sid = "prison", Variant = 1 },
                new() { Sid = "prison", Variant = 3 },
                new() { Sid = "random_hire_7" },
                new() { Sid = "random_hire_6" },
                new() { Sid = "random_hire_5" },
                new() { IncludeLists = ["basic_content_list_building_random_hires"] },
                new() { IncludeLists = ["basic_content_list_building_random_hires"] },
                new() { IncludeLists = ["basic_content_list_building_random_hires"] },
                new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"] },
                new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"] },
                new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"], IsGuarded = isNeutral ? null : false },
                new() { Sid = "random_item_legendary", SoloEncounter = true },
                new() { Sid = "random_item_epic" },
                new() { Sid = "random_item_epic" },
                new() { Sid = "pandora_box", Variant = 0, SoloEncounter = true },
                new() { Sid = "pandora_box", Variant = 1 },
                new() { Sid = "pandora_box", Variant = 4, SoloEncounter = true },
                new() { Sid = "pandora_box", Variant = 5 },
                new() { Sid = "mine_wood", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.03, TargetMax = 0.05, Weight = 2 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_wood", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "MainObject", Args = ["2"], TargetMin = 0.03, TargetMax = 0.05, Weight = 2 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_wood", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.05, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_ore", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.03, TargetMax = 0.05, Weight = 2 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_ore", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "MainObject", Args = ["2"], TargetMin = 0.03, TargetMax = 0.05, Weight = 2 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_ore", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.05, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_gold", IsMine = true, Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 1 }] },
                new() { Sid = "mine_gold", IsMine = true, Rules = [new ContentPlacementRule { Type = "Sid", Args = ["mine_gold"], TargetMin = 0.5, TargetMax = 0.5, Weight = 1 }] },
                new() { Sid = "mine_crystals", IsMine = true },
                new() { Sid = "mine_mercury", IsMine = true },
                new() { Sid = "mine_gemstones", IsMine = true },
                new() { Sid = "alchemy_lab", IsMine = true },
                new() { Sid = "alchemy_lab", IsMine = true },
            };

            return content;
        }

        // ── Content count limits ─────────────────────────────────────────────────

        /// <summary>
        /// Replicates the full set of contentCountLimits from Jebus Cross Classic verbatim.
        /// These are referenced by name from zones and must exist at the root level.
        /// </summary>
        private static List<ContentCountLimit> BuildAllContentCountLimits()
        {
            var sidLimits = new List<ContentSidLimit>
            {
                new() { Sid = "fountain", MaxCount = 2 },
                new() { Sid = "fountain_2", MaxCount = 2 },
                new() { Sid = "mana_well", MaxCount = 2 },
                new() { Sid = "huntsmans_camp", MaxCount = 1 },
                new() { Sid = "market", MaxCount = 1 },
                new() { Sid = "forge", MaxCount = 2 },
                new() { Sid = "celestial_sphere", MaxCount = 2 },
                new() { Sid = "arena", MaxCount = 1 },
                new() { Sid = "sacrificial_shrine", MaxCount = 1 },
                new() { Sid = "chimerologist", MaxCount = 1 },
                new() { Sid = "wise_owl", MaxCount = 3 },
                new() { Sid = "circus", MaxCount = 2 },
                new() { Sid = "infernal_cirque", MaxCount = 2 },
                new() { Sid = "university", MaxCount = 2 },
                new() { Sid = "tree_of_abundance", MaxCount = 1 },
                new() { Sid = "fickle_shrine", MaxCount = 1 },
                new() { Sid = "insaras_eye", MaxCount = 2 },
                new() { Sid = "watchtower", MaxCount = 1 },
                new() { Sid = "flattering_mirror", MaxCount = 2 },
                new() { Sid = "wind_rose", MaxCount = 1 },
                new() { Sid = "jousting_range", MaxCount = 0 },
                new() { Sid = "unforgotten_grave", MaxCount = 0 },
                new() { Sid = "petrified_memorial", MaxCount = 0 },
                new() { Sid = "the_gorge", MaxCount = 0 },
                new() { Sid = "ritual_pyre", MaxCount = 99 },
                new() { Sid = "boreal_call", MaxCount = 99 },
                new() { Sid = "point_of_balance", MaxCount = 3 },
            };

            var limits = new List<ContentCountLimit>
            {
                new() { Name = "content_limits_center", Limits = sidLimits },
                new() { Name = "content_limits_center_0_0", PlayerMin = 0, PlayerMax = 0, Limits = sidLimits },
            };

            for (int i = 1; i <= 6; i++)
                limits.Add(new ContentCountLimit { Name = $"content_limits_center_{i}", PlayerMin = i, PlayerMax = i, Limits = sidLimits });

            limits.Add(new ContentCountLimit { Name = "content_limits_side", Limits = sidLimits });
            limits.Add(new ContentCountLimit { Name = "content_limits_side_0_0", PlayerMin = 0, PlayerMax = 0, Limits = sidLimits });

            for (int a = 1; a <= 5; a++)
                for (int b = a + 1; b <= 6; b++)
                    limits.Add(new ContentCountLimit { Name = $"content_limits_side_{a}_{b}", PlayerMin = a, PlayerMax = b, Limits = sidLimits });

            return limits;
        }
    }
}
