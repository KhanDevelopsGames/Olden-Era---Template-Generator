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

            // When no direct player connections are requested, ensure there are enough
            // neutral zones to separate every adjacent player pair in the ring.
            // A ring of P players needs at least P neutral buffers (one between each pair).
            int neutralCount = settings.NeutralZoneCount;
            if (settings.NoDirectPlayerConnections && settings.PlayerCount > 1)
                neutralCount = Math.Max(neutralCount, settings.PlayerCount);

            var neutralLetters = ZoneLetters.Skip(settings.PlayerCount).Take(neutralCount).ToList();

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
                MandatoryContent = BuildAllMandatoryContent(playerLetters, neutralLetters, settings),
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
            HeroCountIncrement = settings.HeroCountIncrement,
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
                HeroLighting = false,
                HeroLightingDay = 1,
                LostStartCity = false,
                LostStartCityDay = 3,
                LostStartHero = false,
                CityHold = false,
                CityHoldDays = 6
            }
        };

        // ── Variant ──────────────────────────────────────────────────────────────

        private static Variant BuildVariant(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            // When NoDirectPlayerConnections is on, interleave players and neutrals in the
            // ring so that every player zone is flanked only by neutral zones.
            // Layout: P0, N0, P1, N1, … with any extra neutrals appended at the end.
            List<string> orderedLetters;
            if (settings.NoDirectPlayerConnections && playerLetters.Count > 1 && neutralLetters.Count >= playerLetters.Count)
            {
                orderedLetters = [];
                for (int i = 0; i < playerLetters.Count; i++)
                {
                    orderedLetters.Add(playerLetters[i]);
                    orderedLetters.Add(neutralLetters[i]);
                }
                // Append any remaining neutral zones after the interleaved pairs.
                orderedLetters.AddRange(neutralLetters.Skip(playerLetters.Count));
            }
            else
            {
                orderedLetters = playerLetters.Concat(neutralLetters).ToList();
            }

            int totalZones = orderedLetters.Count;

            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Pre-compute ring connection names between adjacent zones in the ordered ring.
            int outerCount = orderedLetters.Count;
            var ringConnRight = new string[outerCount];
            var ringConnLeft  = new string[outerCount];
            for (int i = 0; i < outerCount; i++)
            {
                int next = (i + 1) % outerCount;
                ringConnRight[i] = $"Ring-{orderedLetters[i]}-{orderedLetters[next]}";
                ringConnLeft[next] = ringConnRight[i];
            }

            // Build zones in ring order.
            for (int i = 0; i < outerCount; i++)
            {
                string letter = orderedLetters[i];
                var ringConns = outerCount > 1
                    ? new[] { ringConnLeft[i], ringConnRight[i] }.Distinct().ToArray()
                    : [];

                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", ringConns, settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));
                else
                    zones.Add(BuildNeutralZone(letter, ringConns, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));
            }

            // Direct ring connections between adjacent zones.
            connections.AddRange(BuildRingConnections(playerLetters, neutralLetters, orderedLetters));

            // Random portal connections (non-adjacent pairings).
            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, orderedLetters));

            return new Variant
            {
                Orientation = new Orientation
                {
                    ZeroAngleZone = playerLetters.Count > 0 ? $"Spawn-{orderedLetters[0]}" : $"Neutral-{orderedLetters[0]}",
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

        // ── Spawn zone ───────────────────────────────────────────────────────────

        private static Zone BuildSpawnZone(string letter, string player, string[] ringConns, int castleCount, bool spawnFootholds)
        {
            // Index 0 = Spawn (player town), indices 1..castleCount = extra same-faction cities.
            var mainObjects = new List<MainObject>
            {
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
                }
            };

            for (int i = 1; i < castleCount; i++)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    Faction = new TypedSelector { Type = "Random", Args = [] },
                    GuardChance = 0.5,
                    GuardValue = 2500,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "poor_buildings_construction",
                    Placement = "Uniform",
                    PlacementArgs = ["false", "-0.8", "3"]
                });
            }

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
                GuardedContentValue = 200000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 50000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 80000,
                ResourcesValuePerArea = 0,
                MainObjects = mainObjects,
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds)
            };
        }

        // ── Neutral zone ─────────────────────────────────────────────────────────

        private static Zone BuildNeutralZone(string letter, string[] ringConns, int castleCount, bool spawnFootholds)
        {
            // Index 0 = primary neutral city; indices 1..castleCount-1 = extra neutral cities.
            // All cities use FromList [] (neutral faction, not biome-matched).
            var mainObjects = new List<MainObject>
            {
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
                }
            };

            for (int i = 1; i < castleCount; i++)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 0.5,
                    GuardValue = 5000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "poor_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Uniform",
                    PlacementArgs = ["false", "-0.8", "3"]
                });
            }

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
                GuardedContentValue = 260000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 40000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 60000,
                ResourcesValuePerArea = 0,
                MainObjects = mainObjects,
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds)
            };
        }

        // ── Connections

        /// <summary>
        /// Creates guarded Direct connections between every adjacent pair of outer zones
        /// arranged in a ring (zone[i] ↔ zone[i+1], wrapping around).
        /// Each pair gets one named connection plus road references.
        /// </summary>
        private static IEnumerable<Connection> BuildRingConnections(
            List<string> playerLetters, List<string> neutralLetters, List<string> orderedLetters)
        {
            int count = orderedLetters.Count;
            if (count < 2) yield break;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                string fromLetter = orderedLetters[i];
                string toLetter = orderedLetters[next];

                string fromZone = playerLetters.Contains(fromLetter)
                    ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone = playerLetters.Contains(toLetter)
                    ? $"Spawn-{toLetter}" : $"Neutral-{toLetter}";

                // Keep the normal Direct road connection.
                yield return new Connection
                {
                    Name = $"Ring-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = 30000,
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"ring_guard_{fromLetter}_{toLetter}"
                };
            }
        }

        /// <summary>
        /// Pairs each zone with a random non-adjacent zone and emits a Portal connection.
        /// Uses a shuffled derangement so every zone gets exactly one outgoing portal
        /// that does NOT lead to its immediate ring neighbours.
        /// </summary>
        private static IEnumerable<Connection> BuildRandomPortalConnections(
            List<string> playerLetters, List<string> orderedLetters)
        {
            int count = orderedLetters.Count;
            if (count < 2) yield break; // Need at least 2 zones for a portal.

            // Build a derangement where zone[i] -> zone[dest[i]] and dest[i] is never an
            // immediate neighbour (i-1, i, i+1 mod count).
            var rng = new Random();
            int[] dest = BuildNonAdjacentDerangement(count, rng);

            for (int i = 0; i < count; i++)
            {
                string fromLetter = orderedLetters[i];
                string toLetter   = orderedLetters[dest[i]];

                string fromZone = playerLetters.Contains(fromLetter)
                    ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone = playerLetters.Contains(toLetter)
                    ? $"Spawn-{toLetter}" : $"Neutral-{toLetter}";

                yield return new Connection
                {
                    Name = $"Portal-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Portal",
                    PortalPlacementRulesFrom = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 2 }],
                    PortalPlacementRulesTo   = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 2 }],
                    Road = true,
                    GuardEscape = false,
                    GuardValue = 25000,
                    GuardWeeklyIncrement = 0.15
                };
            }
        }

        /// <summary>
        /// Returns an array where result[i] != i and every index appears exactly once as a destination.
        /// Prefers non-adjacent targets; falls back to adjacent neighbours when no better option exists.
        /// </summary>
        private static int[] BuildNonAdjacentDerangement(int count, Random rng)
        {
            int[] dest = new int[count];
            int attempts = 0;
            while (true)
            {
                attempts++;
                var candidates = Enumerable.Range(0, count).OrderBy(_ => rng.Next()).ToList();
                bool valid = true;
                for (int i = 0; i < count; i++)
                {
                    // Prefer non-adjacent; fall back to adjacent (but never self).
                    int found = -1;
                    for (int j = 0; j < candidates.Count; j++)
                    {
                        int c = candidates[j];
                        if (c != i && c != (i + 1) % count && c != (i - 1 + count) % count)
                        { found = j; break; }
                    }
                    if (found < 0)
                    {
                        // No non-adjacent candidate left — accept any non-self candidate.
                        for (int j = 0; j < candidates.Count; j++)
                        {
                            if (candidates[j] != i) { found = j; break; }
                        }
                    }
                    if (found < 0) { valid = false; break; }
                    dest[i] = candidates[found];
                    candidates.RemoveAt(found);
                }
                if (valid) return dest;
                if (attempts > 100) break; // Should never happen, but guard against infinite loop.
            }
            // Ultimate fallback: simple rotation by half.
            int shift = Math.Max(1, count / 2);
            return Enumerable.Range(0, count).Select(i => (i + shift) % count).ToArray();
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

        /// <summary>
        /// Builds the road list for a spawn or neutral outer zone:
        /// roads to each adjacent ring connection, to the secondary city (index 1),
        /// and to the remote foothold.
        /// </summary>
        private static List<Road> BuildOuterZoneRoads(string[] ringConns, int castleCount, bool includeFoothold)
        {
            var roads = new List<Road>();
            for (int i = 1; i < castleCount; i++)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint(i.ToString())));

            if (includeFoothold)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint("name_remote_foothold_1")));
            foreach (var rc in ringConns)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(rc)));
            return roads;
        }

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
            List<string> playerLetters, List<string> neutralLetters, GeneratorSettings settings)
        {
            var groups = new List<MandatoryContentGroup>();

            foreach (var letter in playerLetters)
                groups.Add(BuildSpawnMandatoryContent(letter, settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));

            foreach (var letter in neutralLetters)
                groups.Add(BuildNeutralMandatoryContent(letter, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));

            return groups;
        }

        private static MandatoryContentGroup BuildSpawnMandatoryContent(string letter, int castleCount, bool spawnFootholds)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_side_{letter}",
                Content = BuildOuterZoneMandatoryContent(isNeutral: false, castleCount, spawnFootholds)
            };
        }

        private static MandatoryContentGroup BuildNeutralMandatoryContent(string letter, int castleCount, bool spawnFootholds)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_neutral_{letter}",
                Content = BuildOuterZoneMandatoryContent(isNeutral: true, castleCount, spawnFootholds)
            };
        }

        private static List<ContentItem> BuildOuterZoneMandatoryContent(bool isNeutral, int castleCount, bool spawnFootholds)
        {
            var footholdRules = new List<ContentPlacementRule>
            {
                new() { Type = "Crossroads", Args = [], TargetMin = 0.2, TargetMax = 0.3, Weight = 0 },
                new() { Type = "MainObject", Args = ["0"], TargetMin = 0.2, TargetMax = 0.4, Weight = 0 },
            };
            if (castleCount > 1)
                footholdRules.Add(new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.5, TargetMax = 0.5, Weight = 2 });

            var content = new List<ContentItem>();

            if (spawnFootholds)
                content.Add(new ContentItem { Name = "name_remote_foothold_1", Sid = "remote_foothold", IsGuarded = false, Rules = footholdRules });

            content.AddRange(new List<ContentItem>
            {
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_base"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_pro"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { Sid = "fountain", IsGuarded = false },
                new() { Sid = "watchtower", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "mana_well", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "market" },
                new() { Sid = "forge" },
                new() { Sid = "tavern", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.4, Weight = 1 }] },
                new() { Sid = "stables", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "university" },
                new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
                new() { Sid = "mystical_tower" },
                new() { Sid = "prison", Variant = 0, IsGuarded = false },
                new() { Sid = "random_hire_5" },
                new() { IncludeLists = ["basic_content_list_building_random_hires"] },
                new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"], IsGuarded = isNeutral ? null : false },
                new() { Sid = "pandora_box", Variant = 0, SoloEncounter = true },
                new() { Sid = "mine_wood", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_ore", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_gold", IsMine = true, Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 1 }] },
                new() { Sid = "mine_crystals", IsMine = true },
                new() { Sid = "mine_mercury", IsMine = true },
                new() { Sid = "mine_gemstones", IsMine = true },
                new() { Sid = "alchemy_lab", IsMine = true },
            });

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

            var limits = new List<ContentCountLimit>();

            limits.Add(new ContentCountLimit { Name = "content_limits_side", Limits = sidLimits });
            limits.Add(new ContentCountLimit { Name = "content_limits_side_0_0", PlayerMin = 0, PlayerMax = 0, Limits = sidLimits });

            for (int a = 1; a <= 5; a++)
                for (int b = a + 1; b <= 6; b++)
                    limits.Add(new ContentCountLimit { Name = $"content_limits_side_{a}_{b}", PlayerMin = a, PlayerMax = b, Limits = sidLimits });

            return limits;
        }
    }
}
