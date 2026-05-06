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
        private const int ConnectionsPerZone = 1;
        private const double DefaultGuardRandomization = 0.05;
        private const string SpawnLayoutName = "zone_layout_spawns";
        private const string SideLayoutName = "zone_layout_sides";
        private const string TreasureLayoutName = "zone_layout_treasure_zone";
        private const string CenterLayoutName = "zone_layout_center";

        // Labels used to name zones, up to the advanced-mode maximum of 32 total zones.
        public static readonly string[] ZoneLetters =
        [
            "A", "B", "C", "D", "E", "F", "G", "H",
            "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X",
            "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF"
        ];
        public static RmgTemplate Generate(GeneratorSettings settings)
        {
            var playerLetters = ZoneLetters.Take(settings.PlayerCount).ToList();
            var neutralZones = BuildNeutralZonePlan(settings);

            // When city hold is active on Hub & Spoke the hub itself becomes the hold city,
            // so no neutral zone needs to carry that flag. For all other topologies we pick
            // the best-candidate neutral zone now so every downstream builder can query it.
            bool useCityHold = settings.GameEndConditions.CityHold || settings.GameEndConditions.VictoryCondition == "win_condition_5";
            string? holdCityNeutralLetter = null;
            if (useCityHold && settings.Topology != MapTopology.HubAndSpoke)
            {
                var adjacency = BuildTopologyAdjacency(settings, playerLetters, neutralZones);
                holdCityNeutralLetter = PickHoldCityNeutralLetter(neutralZones, playerLetters, adjacency);
            }

            // Hub & Spoke handles isolation via hub; for all other topologies the
            // isolation is done by skipping player–player connections, so no extra
            // neutral zones need to be auto-created.
            int neutralCount = neutralZones.Count;
            var neutralLetters = neutralZones.Select(zone => zone.Letter).ToList();

            int totalZones = settings.PlayerCount + neutralCount;
            var tuning = new GenerationTuning(
                ComputeContentScale(settings.MapSize, totalZones),
                settings.ZoneCfg.ResourceDensityPercent / 200.0,
                settings.ZoneCfg.StructureDensityPercent / 100.0,
                settings.ZoneCfg.NeutralStackStrengthPercent / 100.0,
                settings.ZoneCfg.BorderGuardStrengthPercent / 100.0,
                EffectiveGuardRandomization(settings));

            string effectiveVictoryCondition = settings.GameEndConditions.VictoryCondition;

            var template = new RmgTemplate
            {
                Name = settings.TemplateName,
                GameMode = settings.GameMode,
                Description = BuildTemplateDescription(settings, neutralCount),
                DisplayWinCondition = effectiveVictoryCondition,
                SizeX = settings.MapSize,
                SizeZ = settings.MapSize,
                GameRules = BuildGameRules(settings, effectiveVictoryCondition),
                Variants = [BuildVariant(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter, useCityHold && settings.Topology == MapTopology.HubAndSpoke)],
                ZoneLayouts = BuildZoneLayouts(),
                MandatoryContent = BuildAllMandatoryContent(playerLetters, neutralZones, settings),
                ContentCountLimits = BuildAllContentCountLimits(),
                ContentPools = [],
                ContentLists = []
            };

            return template;
        }

        private static string BuildTemplateDescription(GeneratorSettings settings, int neutralZoneCount)
        {
            var parts = new List<string>
            {
                $"{TopologyLabel(settings.Topology)} layout",
                CountPhrase(neutralZoneCount, "neutral zone", "neutral zones"),
                $"{CountPhrase(settings.ZoneCfg.PlayerZoneCastles, "castle", "castles")} per player zone"
            };

            if (neutralZoneCount > 0)
            {
                string neutralCastlePhrase = settings.ZoneCfg.Advanced.Enabled
                    ? "mixed neutral zone tiers"
                    : $"{CountPhrase(settings.ZoneCfg.NeutralZoneCastles, "castle", "castles")} per neutral zone";
                parts.Add(neutralCastlePhrase);
            }

            var options = new List<string>();
            if (settings.NoDirectPlayerConnections)
                options.Add("isolated player starts");
            if (settings.ExperimentalBalancedZonePlacement)
                options.Add("balanced zone placement");
            if (settings.RandomPortals)
                options.Add("random portals");
            if (!settings.SpawnRemoteFootholds)
                options.Add("no remote footholds");
            if (!settings.GenerateRoads)
                options.Add("roads disabled");

            if (options.Count > 0)
                parts.Add($"options: {string.Join(", ", options)}");

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string versionLabel = version != null ? $"v{version.Major}.{version.Minor}" : "v?";
            return $"Generated with Olden Era Template Generator {versionLabel}: {string.Join(", ", parts)}.";
        }

        private static string CountPhrase(int count, string singular, string plural) =>
            count == 0 ? $"no {plural}" : $"{count} {(count == 1 ? singular : plural)}";

        private static string TopologyLabel(MapTopology topology) => topology switch
        {
            MapTopology.Default => "Ring",
            MapTopology.HubAndSpoke => "Hub",
            MapTopology.Chain => "Chain",
            MapTopology.SharedWeb => "Shared Web",
            MapTopology.Random => "Random",
            _ => topology.ToString()
        };

        private sealed record NeutralZonePlan(string Letter, NeutralZoneQuality Quality, int CastleCount);

        private sealed record NeutralZoneProfile(
            string Layout,
            double GuardMultiplier,
            string GuardedContentPool,
            string UnguardedContentPool,
            string ResourcesContentPool,
            int GuardedContentValue,
            int GuardedContentValuePerArea,
            int UnguardedContentValue,
            int UnguardedContentValuePerArea,
            int ResourcesValue,
            int ResourcesValuePerArea,
            int PrimaryCityGuardValue,
            int ExtraCityGuardValue,
            string PrimaryBuildingsConstructionSid,
            string ExtraBuildingsConstructionSid);

        private static List<NeutralZonePlan> BuildNeutralZonePlan(GeneratorSettings settings)
        {
            var plans = new List<NeutralZonePlan>();
            int maxNeutralZones = Math.Max(0, ZoneLetters.Length - settings.PlayerCount);
            int castleZoneCastleCount = Math.Clamp(settings.ZoneCfg.NeutralZoneCastles, 1, 4);

            void Add(int requestedCount, NeutralZoneQuality quality, int castleCount)
            {
                int count = Math.Clamp(requestedCount, 0, 30);
                for (int i = 0; i < count && plans.Count < maxNeutralZones; i++)
                {
                    string letter = ZoneLetters[settings.PlayerCount + plans.Count];
                    plans.Add(new NeutralZonePlan(letter, quality, castleCount));
                }
            }

            if (settings.ZoneCfg.Advanced.Enabled)
            {
                Add(settings.ZoneCfg.Advanced.NeutralLowNoCastleCount, NeutralZoneQuality.Low, 0);
                Add(settings.ZoneCfg.Advanced.NeutralLowCastleCount, NeutralZoneQuality.Low, castleZoneCastleCount);
                Add(settings.ZoneCfg.Advanced.NeutralMediumNoCastleCount, NeutralZoneQuality.Medium, 0);
                Add(settings.ZoneCfg.Advanced.NeutralMediumCastleCount, NeutralZoneQuality.Medium, castleZoneCastleCount);
                Add(settings.ZoneCfg.Advanced.NeutralHighNoCastleCount, NeutralZoneQuality.High, 0);
                Add(settings.ZoneCfg.Advanced.NeutralHighCastleCount, NeutralZoneQuality.High, castleZoneCastleCount);
            }
            else
            {
                int castleCount = Math.Clamp(settings.ZoneCfg.NeutralZoneCastles, 0, 4);
                Add(settings.ZoneCfg.NeutralZoneCount, NeutralZoneQuality.Medium, castleCount);
            }

            if (settings.Topology == MapTopology.SharedWeb && plans.Count == 0 && maxNeutralZones > 0)
            {
                string letter = ZoneLetters[settings.PlayerCount];
                int castleCount = Math.Clamp(settings.ZoneCfg.NeutralZoneCastles, 0, 4);
                plans.Add(new NeutralZonePlan(letter, NeutralZoneQuality.Medium, castleCount));
            }

            return plans;
        }

        /// <summary>
        /// Picks the letter of the neutral zone that should host the hold city.
        ///
        /// Uses BFS over the actual topology adjacency graph to compute exact hop-distances
        /// from every player zone to every neutral zone, then selects the neutral that is
        /// most equidistant from all players:
        ///   1. Maximise the minimum hop-distance to any player (farthest from all players).
        ///   2. Minimise the variance of distances across players (most equidistant).
        ///   3. Prefer higher quality tier.
        ///   4. Prefer zones that already have a castle.
        ///
        /// <paramref name="adjacency"/> maps each zone letter to the set of directly adjacent
        /// zone letters (undirected graph). Returns null when there are no eligible neutral zones.
        /// </summary>
        private static string? PickHoldCityNeutralLetter(
            List<NeutralZonePlan> neutralZones,
            List<string> playerLetters,
            Dictionary<string, HashSet<string>> adjacency)
        {
            if (neutralZones.Count == 0) return null;

            var neutralByLetter = neutralZones.ToDictionary(z => z.Letter);

            // BFS from a given player letter; returns hop-distance to every reachable zone.
            static Dictionary<string, int> Bfs(string start, Dictionary<string, HashSet<string>> adj)
            {
                var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
                var queue = new Queue<string>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    string cur = queue.Dequeue();
                    if (!adj.TryGetValue(cur, out var neighbours)) continue;
                    foreach (string nb in neighbours)
                    {
                        if (!dist.ContainsKey(nb))
                        {
                            dist[nb] = dist[cur] + 1;
                            queue.Enqueue(nb);
                        }
                    }
                }
                return dist;
            }

            // Compute BFS distances from each player to every neutral zone.
            var distsByPlayer = playerLetters
                .Select(p => Bfs(p, adjacency))
                .ToList();

            var best = neutralZones
                .Select(plan =>
                {
                    string letter = plan.Letter;
                    var dists = distsByPlayer
                        .Select(d => d.TryGetValue(letter, out int v) ? v : int.MaxValue / 2)
                        .ToList();
                    int    minDist  = dists.Min();
                    double mean     = dists.Average();
                    double variance = dists.Average(d => (d - mean) * (d - mean));
                    return (letter, minDist, variance, quality: (int)plan.Quality, hasCastle: plan.CastleCount > 0 ? 1 : 0);
                })
                .OrderByDescending(t => t.minDist)
                .ThenBy(t => t.variance)
                .ThenByDescending(t => t.quality)
                .ThenByDescending(t => t.hasCastle)
                .FirstOrDefault();

            return best.letter;
        }

        /// <summary>
        /// Builds a zone-letter adjacency graph for the given topology so that
        /// <see cref="PickHoldCityNeutralLetter"/> can compute exact hop-distances.
        /// </summary>
        private static Dictionary<string, HashSet<string>> BuildTopologyAdjacency(
            GeneratorSettings settings,
            List<string> playerLetters,
            List<NeutralZonePlan> neutralZones)
        {
            var adj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            void Link(string a, string b)
            {
                if (!adj.TryGetValue(a, out var sa)) adj[a] = sa = new HashSet<string>(StringComparer.Ordinal);
                if (!adj.TryGetValue(b, out var sb)) adj[b] = sb = new HashSet<string>(StringComparer.Ordinal);
                sa.Add(b);
                sb.Add(a);
            }

            switch (settings.Topology)
            {
                case MapTopology.Chain:
                {
                    var ordered = BuildOrderedLetters(settings, playerLetters, neutralZones, isRing: false);
                    bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;
                    var playerSet = playerLetters.ToHashSet(StringComparer.Ordinal);
                    for (int i = 0; i < ordered.Count - 1; i++)
                    {
                        bool bothPlayers = playerSet.Contains(ordered[i]) && playerSet.Contains(ordered[i + 1]);
                        if (isolate && bothPlayers) continue;
                        Link(ordered[i], ordered[i + 1]);
                    }
                    break;
                }

                case MapTopology.Default:
                {
                    // Mirror exactly what BuildVariantDefault does: ring edges, with
                    // player–player pairs skipped when isolation is enabled.
                    var ordered = BuildOrderedLetters(settings, playerLetters, neutralZones, isRing: true);
                    bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;
                    var playerSet = playerLetters.ToHashSet(StringComparer.Ordinal);
                    int n = ordered.Count;
                    for (int i = 0; i < n; i++)
                    {
                        int next = (i + 1) % n;
                        bool bothPlayers = playerSet.Contains(ordered[i]) && playerSet.Contains(ordered[next]);
                        if (isolate && bothPlayers) continue;
                        Link(ordered[i], ordered[next]);
                    }
                    break;
                }

                case MapTopology.Random:
                {
                    // Delaunay adjacency is computed from random positions at generation
                    // time and can't be reproduced exactly during the pick phase.
                    // We use the balanced ring ordering as the best structural proxy:
                    // BuildBalancedRingLetters places players evenly around a circle and
                    // fills gaps with neutrals in quality order, which closely matches the
                    // ring-like adjacency that Delaunay produces on those positions.
                    // For non-balanced random placement the same ring proxy is used, since
                    // any position-independent approximation is equivalent in expectation.
                    var ordered = BuildOrderedLetters(settings, playerLetters, neutralZones, isRing: true);
                    int rn = ordered.Count;
                    for (int i = 0; i < rn; i++)
                        Link(ordered[i], ordered[(i + 1) % rn]);
                    break;
                }

                default:
                {
                    var ordered = BuildOrderedLetters(settings, playerLetters, neutralZones, isRing: true);
                    int dn = ordered.Count;
                    for (int i = 0; i < dn; i++)
                        Link(ordered[i], ordered[(i + 1) % dn]);
                    break;
                }
            }

            return adj;
        }

        // ── Game rules ───────────────────────────────────────────────────────────

        private static GameRules BuildGameRules(GeneratorSettings settings, string effectiveVictoryCondition) => new()
        {
            HeroCountMin = settings.HeroSettings.HeroCountMin-settings.HeroSettings.HeroCountIncrement,
            HeroCountMax = settings.HeroSettings.HeroCountMax,
            HeroCountIncrement = settings.HeroSettings.HeroCountIncrement,
            HeroHireBan = false,
            EncounterHoles = false,
            FactionLawsExpModifier = PercentToModifier(settings.FactionLawsExpPercent),
            AstrologyExpModifier = PercentToModifier(settings.AstrologyExpPercent),
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
            WinConditions = BuildAdvancedWinConditions(settings, effectiveVictoryCondition)
        };

        private static double PercentToModifier(int percent) =>
            Math.Round(Math.Clamp(percent, 25, 200) / 100.0, 2, MidpointRounding.AwayFromZero);



        private static WinConditions BuildAdvancedWinConditions(GeneratorSettings settings, string effectiveVictoryCondition)
        {
            bool useLostStartCity = settings.GameEndConditions.LostStartCity || effectiveVictoryCondition == "win_condition_3";
            bool useCityHold = settings.GameEndConditions.CityHold || effectiveVictoryCondition == "win_condition_5";
            bool useGladiator = settings.GladiatorArenaRules.Enabled || effectiveVictoryCondition == "win_condition_4";
            bool useTournament = settings.TournamentRules.Enabled || effectiveVictoryCondition == "win_condition_6";

            var winConditions = new WinConditions
            {
                Classic = true,
                Desertion = true,
                DesertionDay = 3,
                DesertionValue = 3000,
                HeroLighting = true,
                HeroLightingDay = 1,
                LostStartCity = useLostStartCity,
                LostStartCityDay = Math.Clamp(settings.GameEndConditions.LostStartCityDay, 1, 30),
                LostStartHero = settings.GameEndConditions.LostStartHero || useGladiator,
                CityHold = useCityHold,
                CityHoldDays = Math.Clamp(settings.GameEndConditions.CityHoldDays, 1, 30)
            };

            if (useGladiator)
            {
                winConditions.GladiatorArena = true;
                winConditions.GladiatorArenaRegistrationStartWork = false;
                winConditions.GladiatorArenaRegistrationStartFight = true;
                winConditions.GladiatorArenaDaysDelayStart = Math.Clamp(settings.GladiatorArenaRules.DaysDelayStart, 1, 60);
                winConditions.GladiatorArenaCountDay = Math.Clamp(settings.GladiatorArenaRules.CountDay, 1, 30);
                winConditions.ChampionSelectRule = "StartHero";
            }

            if (useTournament)
            {
                int firstTournamentDay = Math.Clamp(settings.TournamentRules.FirstTournamentDay, 1, 60);
                int announcementLeadDays = Math.Clamp(settings.TournamentRules.AnnouncementLeadDays, 1, 30);
                int tournamentInterval = Math.Clamp(settings.TournamentRules.Interval, 1, 30);
                int pointsToWin = Math.Clamp(settings.TournamentRules.PointsToWin, 1, 5);
                // Round count is derived from points to win: with N points to win, the maximum number of rounds is 2N-1
                int roundCount = pointsToWin * 2 - 1;
                // Common tournament settings, for now not editable
                winConditions.ChampionSelectRule = "StartHero";
                winConditions.Tournament = true;
                winConditions.TournamentSaveArmy = true;

                // Tournament annouce days are the starting point to tournament countdown.
                winConditions.TournamentAnnounceDays = Enumerable.Range(0, roundCount)
                    .Select(i => (firstTournamentDay - announcementLeadDays) + i * tournamentInterval)
                    .ToList();
                    
                // Tournament days are relative to announcement days
                winConditions.TournamentDays = Enumerable.Repeat(announcementLeadDays, roundCount).ToList();
                winConditions.TournamentPointsToWin = pointsToWin;
            }
            return winConditions;
        }

        private readonly record struct GenerationTuning(
            double ContentScale,
            double ResourceDensityMultiplier,
            double StructureDensityMultiplier,
            double NeutralStackStrengthMultiplier,
            double BorderGuardStrengthMultiplier,
            double GuardRandomization);

        private static int ScaleValue(double value, double multiplier) =>
            Math.Max(0, (int)(value * multiplier));

        private static int ScaleStructureValue(double value, GenerationTuning tuning) =>
            ScaleValue(value, tuning.StructureDensityMultiplier);

        private static int ScaleResourceValue(double value, GenerationTuning tuning) =>
            ScaleValue(value, tuning.ResourceDensityMultiplier);

        private static int ScaleNeutralGuardValue(int value, GenerationTuning tuning) =>
            ScaleValue(value, tuning.NeutralStackStrengthMultiplier);

        private static int ScaleBorderGuardValue(int value, GenerationTuning tuning) =>
            ScaleValue(value, tuning.BorderGuardStrengthMultiplier);

        private static double ScaleGuardMultiplier(double value, GenerationTuning tuning) =>
            Math.Round(value * tuning.NeutralStackStrengthMultiplier, 3, MidpointRounding.AwayFromZero);

        private static double EffectiveGuardRandomization(GeneratorSettings settings)
        {
            if (!settings.ZoneCfg.Advanced.Enabled)
                return DefaultGuardRandomization;

            double value = settings.ZoneCfg.Advanced.GuardRandomization;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return DefaultGuardRandomization;

            return Math.Round(Math.Clamp(value, 0.0, 0.5), 3, MidpointRounding.AwayFromZero);
        }

        // ── Content scale ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a multiplier for content values based on how large each zone is.
        /// Reference baseline: 160×160 map with 4 zones (6400 tiles/zone → scale 1.0).
        /// Uses sqrt so the curve is gentle; clamped to [0.5, 2.5].
        /// </summary>
        private static double ComputeContentScale(int mapSize, int totalZones)
        {
            const double referenceArea = 160.0 * 160.0 / 4.0; // 6400
            double zoneArea = (double)mapSize * mapSize / Math.Max(1, totalZones);
            return Math.Clamp(Math.Sqrt(zoneArea / referenceArea), 0.5, 2.5);
        }

        private static double NormalizeZoneSize(double zoneSize)
        {
            if (double.IsNaN(zoneSize) || double.IsInfinity(zoneSize))
                return 1.0;

            return Math.Round(Math.Clamp(zoneSize, 0.1, 2.0), 2, MidpointRounding.AwayFromZero);
        }

        public static bool CanHonorNeutralSeparation(GeneratorSettings settings, int neutralZoneCount)
        {
            int min = settings.MinNeutralZonesBetweenPlayers;
            if (min <= 0) return true;
            if (settings.RandomPortals) return false;

            return settings.Topology switch
            {
                MapTopology.Default => neutralZoneCount >= settings.PlayerCount * min,
                MapTopology.Chain => neutralZoneCount >= (settings.PlayerCount - 1) * min,
                MapTopology.HubAndSpoke => min <= 1,
                MapTopology.SharedWeb => min <= 1 && neutralZoneCount >= 1,
                _ => false,
            };
        }

        private static List<string> BuildOrderedLetters(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, bool isRing)
        {
            var neutralLetters = neutralZones.Select(zone => zone.Letter).ToList();

            if (settings.ExperimentalBalancedZonePlacement)
            {
                int honoredSeparation = settings.MinNeutralZonesBetweenPlayers > 0
                    && CanHonorNeutralSeparation(settings, neutralLetters.Count)
                        ? settings.MinNeutralZonesBetweenPlayers
                        : 0;

                return isRing
                    ? BuildBalancedRingLetters(playerLetters, neutralZones, honoredSeparation)
                    : BuildBalancedChainLetters(playerLetters, neutralZones, honoredSeparation);
            }

            int min = settings.MinNeutralZonesBetweenPlayers;
            if (min <= 0 || settings.RandomPortals || !CanHonorNeutralSeparation(settings, neutralLetters.Count))
                return playerLetters.Concat(neutralLetters).ToList();

            var ordered = new List<string>();
            var remainingNeutrals = new Queue<string>(neutralLetters);

            for (int i = 0; i < playerLetters.Count; i++)
            {
                ordered.Add(playerLetters[i]);
                bool needsSeparatorAfterPlayer = isRing || i < playerLetters.Count - 1;
                if (!needsSeparatorAfterPlayer) continue;

                for (int j = 0; j < min && remainingNeutrals.Count > 0; j++)
                    ordered.Add(remainingNeutrals.Dequeue());
            }

            while (remainingNeutrals.Count > 0)
                ordered.Add(remainingNeutrals.Dequeue());

            return ordered.Count > 0 ? ordered : playerLetters.Concat(neutralLetters).ToList();
        }

        private static List<string> BuildBalancedRingLetters(
            List<string> playerLetters,
            List<NeutralZonePlan> neutralZones,
            int minNeutralZonesBetweenPlayers)
        {
            if (playerLetters.Count == 0)
                return BuildBalancedNeutralRing(neutralZones, 1);

            if (neutralZones.Count == 0)
                return [.. playerLetters];

            int[] gapCapacities = BuildEvenGapCapacities(
                playerLetters.Count,
                neutralZones.Count,
                minNeutralZonesBetweenPlayers);
            var gaps = AssignNeutralZonesToGaps(neutralZones, gapCapacities, preferInteriorGaps: false);

            var ordered = new List<string>(playerLetters.Count + neutralZones.Count);
            for (int i = 0; i < playerLetters.Count; i++)
            {
                ordered.Add(playerLetters[i]);
                ordered.AddRange(OrderNeutralsWithinGap(gaps[i]).Select(zone => zone.Letter));
            }

            return ordered;
        }

        private static List<string> BuildBalancedChainLetters(
            List<string> playerLetters,
            List<NeutralZonePlan> neutralZones,
            int minNeutralZonesBetweenPlayers)
        {
            if (playerLetters.Count == 0)
                return neutralZones.Select(zone => zone.Letter).ToList();

            int gapCount = playerLetters.Count + 1;
            var capacities = new int[gapCount];
            int remaining = neutralZones.Count;

            int requiredInterior = Math.Max(0, playerLetters.Count - 1) * minNeutralZonesBetweenPlayers;
            if (minNeutralZonesBetweenPlayers > 0 && neutralZones.Count >= requiredInterior)
            {
                for (int i = 1; i < gapCount - 1; i++)
                    capacities[i] = minNeutralZonesBetweenPlayers;
                remaining -= requiredInterior;
            }

            int[] extras = BuildEvenGapCapacities(gapCount, remaining, minimumPerGap: 0);
            for (int i = 0; i < gapCount; i++)
                capacities[i] += extras[i];

            var gaps = AssignNeutralZonesToGaps(neutralZones, capacities, preferInteriorGaps: true);
            var ordered = new List<string>(playerLetters.Count + neutralZones.Count);

            ordered.AddRange(OrderEdgeGap(gaps[0], highQualityNearPlayer: true).Select(zone => zone.Letter));
            for (int i = 0; i < playerLetters.Count; i++)
            {
                ordered.Add(playerLetters[i]);
                var gap = gaps[i + 1];
                bool trailingEdge = i == playerLetters.Count - 1;
                ordered.AddRange((trailingEdge
                    ? OrderEdgeGap(gap, highQualityNearPlayer: false)
                    : OrderNeutralsWithinGap(gap)).Select(zone => zone.Letter));
            }

            return ordered.Count > 0 ? ordered : playerLetters.Concat(neutralZones.Select(zone => zone.Letter)).ToList();
        }

        private static List<string> BuildBalancedNeutralRing(List<NeutralZonePlan> neutralZones, int playerCount)
        {
            if (neutralZones.Count <= 1)
                return neutralZones.Select(zone => zone.Letter).ToList();

            int gapCount = Math.Max(1, playerCount);
            int[] gapCapacities = BuildEvenGapCapacities(gapCount, neutralZones.Count, minimumPerGap: 0);
            var gaps = AssignNeutralZonesToGaps(neutralZones, gapCapacities, preferInteriorGaps: false);
            return gaps
                .SelectMany(gap => OrderNeutralsWithinGap(gap))
                .Select(zone => zone.Letter)
                .ToList();
        }

        private static int[] BuildEvenGapCapacities(int gapCount, int itemCount, int minimumPerGap)
        {
            var capacities = new int[Math.Max(0, gapCount)];
            if (gapCount <= 0 || itemCount <= 0)
                return capacities;

            int minimum = Math.Max(0, minimumPerGap);
            int reserved = minimum * gapCount;
            int remaining = itemCount;
            if (minimum > 0 && itemCount >= reserved)
            {
                for (int i = 0; i < gapCount; i++)
                    capacities[i] = minimum;
                remaining -= reserved;
            }

            for (int i = 0; i < remaining; i++)
            {
                int gap = (int)Math.Floor((i + 0.5) * gapCount / remaining);
                capacities[Math.Clamp(gap, 0, gapCount - 1)]++;
            }

            return capacities;
        }

        private static List<List<NeutralZonePlan>> AssignNeutralZonesToGaps(
            List<NeutralZonePlan> neutralZones,
            int[] gapCapacities,
            bool preferInteriorGaps)
        {
            var gaps = gapCapacities.Select(_ => new List<NeutralZonePlan>()).ToList();
            var loads = new double[gapCapacities.Length];
            var orderedNeutrals = neutralZones
                .OrderByDescending(NeutralZoneBalanceScore)
                .ThenBy(zone => zone.Letter, StringComparer.Ordinal)
                .ToList();

            foreach (var neutralZone in orderedNeutrals)
            {
                var candidates = Enumerable.Range(0, gapCapacities.Length)
                    .Where(i => gaps[i].Count < gapCapacities[i])
                    .ToList();

                if (candidates.Count == 0)
                    break;

                if (preferInteriorGaps)
                {
                    var interiorCandidates = candidates
                        .Where(i => i > 0 && i < gapCapacities.Length - 1)
                        .ToList();
                    if (interiorCandidates.Count > 0)
                        candidates = interiorCandidates;
                }

                int selectedGap = candidates
                    .OrderBy(i => loads[i])
                    .ThenBy(i => gaps[i].Count)
                    .ThenBy(i => i)
                    .First();

                gaps[selectedGap].Add(neutralZone);
                loads[selectedGap] += NeutralZoneBalanceScore(neutralZone);
            }

            return gaps;
        }

        private static List<NeutralZonePlan> OrderNeutralsWithinGap(List<NeutralZonePlan> neutralZones)
        {
            int count = neutralZones.Count;
            if (count <= 1)
                return [.. neutralZones];

            var sorted = neutralZones
                .OrderByDescending(NeutralZoneBalanceScore)
                .ThenBy(zone => zone.Letter, StringComparer.Ordinal)
                .ToList();
            var slots = new NeutralZonePlan[count];
            var positions = Enumerable.Range(0, count)
                .OrderBy(position => Math.Abs(position - (count - 1) / 2.0))
                .ThenBy(position => position)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
                slots[positions[i]] = sorted[i];

            return slots.ToList();
        }

        private static List<NeutralZonePlan> OrderEdgeGap(List<NeutralZonePlan> neutralZones, bool highQualityNearPlayer)
        {
            var ordered = neutralZones
                .OrderBy(zone => NeutralZoneBalanceScore(zone))
                .ThenBy(zone => zone.Letter, StringComparer.Ordinal)
                .ToList();

            if (highQualityNearPlayer)
                return ordered;

            ordered.Reverse();
            return ordered;
        }

        private static double NeutralZoneBalanceScore(NeutralZonePlan zone)
        {
            double quality = zone.Quality switch
            {
                NeutralZoneQuality.High => 3.0,
                NeutralZoneQuality.Medium => 2.0,
                _ => 1.0
            };

            return quality + Math.Min(zone.CastleCount, 4) * 0.15;
        }

        // ── Variant ──────────────────────────────────────────────────────────────

        private static Variant BuildVariant(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, string? holdCityNeutralLetter = null, bool hubIsHoldCity = false)
        {
            // Always shuffle player letters so players are not always at the same geometric positions.
            playerLetters = [.. playerLetters.OrderBy(_ => Random.Shared.Next())];

            bool isTournament = settings.TournamentRules.Enabled || settings.GameEndConditions.VictoryCondition == "win_condition_6";
            if (isTournament && playerLetters.Count == 2)
                return BuildVariantTournament(settings, playerLetters, neutralZones, tuning);

            return settings.Topology switch
            {
                MapTopology.HubAndSpoke => BuildVariantHubAndSpoke(settings, playerLetters, neutralZones, tuning, hubIsHoldCity),
                MapTopology.Chain       => BuildVariantChain(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter),
                MapTopology.SharedWeb   => BuildVariantSharedWeb(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter),
                MapTopology.Random      => BuildVariantRandom(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter),
                _                       => BuildVariantDefault(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter),
            };
        }

        // ── Topology: Tournament (2 fully-isolated player clusters) ──────────────

        /// <summary>
        /// Builds a variant where both players are completely isolated from each other.
        /// Neutral zones are split roughly evenly between the two players; each player
        /// connects to their exclusive neutrals using the selected topology.
        /// Chain / Default (Ring) / SharedWeb → simple chain per cluster.
        /// Random → Delaunay-connected cluster per player.
        /// Hub &amp; Spoke → not meaningful for isolation; falls back to chain.
        /// There is never a path that crosses from one player's cluster to the other.
        /// </summary>
        private static Variant BuildVariantTournament(
            GeneratorSettings settings,
            List<string> playerLetters,
            List<NeutralZonePlan> neutralZones,
            GenerationTuning tuning)
        {
            var neutralByLetter = neutralZones.ToDictionary(z => z.Letter);

            // Distribute neutrals in a balanced round-robin so that quality tiers are
            // spread evenly: sort by descending quality/castle score first, then assign
            // zones alternately: index 0,2,4,… → player 0; index 1,3,5,… → player 1.
            var sorted = neutralZones
                .OrderByDescending(z => (int)z.Quality)
                .ThenByDescending(z => z.CastleCount)
                .ThenBy(z => z.Letter, StringComparer.Ordinal)
                .ToList();

            var neutralsForPlayer = new List<List<NeutralZonePlan>> { new(), new() };
            for (int i = 0; i < sorted.Count; i++)
                neutralsForPlayer[i % 2].Add(sorted[i]);

            // Randomize the chain order, but use the same permutation for both players
            // so each cluster has an identical slot structure (mirrored layout).
            var rng = new Random();
            int maxSlots = Math.Max(neutralsForPlayer[0].Count, neutralsForPlayer[1].Count);
            var slotOrder = Enumerable.Range(0, maxSlots).OrderBy(_ => rng.Next()).ToList();
            for (int p = 0; p < 2; p++)
            {
                var original = neutralsForPlayer[p];
                neutralsForPlayer[p] = slotOrder
                    .Where(i => i < original.Count)
                    .Select(i => original[i])
                    .ToList();
            }

            var zones = new List<Zone>();
            var connections = new List<Connection>();

            bool useRandom = settings.Topology == MapTopology.Random;
            bool useHub    = settings.Topology == MapTopology.HubAndSpoke;

            if (useHub)
            {
                // Each player gets their own private mini-hub. The slot permutation is
                // not meaningful for hub (all neutrals connect directly to the hub), so
                // we skip it and build each cluster independently.
                for (int p = 0; p < 2; p++)
                    BuildTournamentHubCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections);
            }
            else if (useRandom)
            {
                // Generate positions and Delaunay edges once from the larger cluster size,
                // then reuse the identical edge topology for both clusters so the layouts mirror each other.
                int templateSize = maxSlots + 1; // +1 for player zone
                var templatePos = new List<(double X, double Y)>(templateSize);
                for (int i = 0; i < templateSize; i++)
                    templatePos.Add((rng.NextDouble() * 0.9 + 0.05, rng.NextDouble() * 0.9 + 0.05));
                var templateEdges = DelaunayEdges(templatePos);

                for (int p = 0; p < 2; p++)
                    BuildTournamentRandomCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections, templateEdges);
            }
            else
            {
                for (int p = 0; p < 2; p++)
                    BuildTournamentChainCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections);
            }

            int totalZones = zones.Count;
            return MakeVariant(playerLetters, playerLetters[0], totalZones, zones, connections);
        }

        /// <summary>
        /// Builds one player's isolated cluster as a chain: player → n0 → n1 → …
        /// </summary>
        private static void BuildTournamentChainCluster(
            int playerIndex,
            string playerLetter,
            List<NeutralZonePlan> myNeutrals,
            Dictionary<string, NeutralZonePlan> neutralByLetter,
            GeneratorSettings settings,
            GenerationTuning tuning,
            List<Zone> zones,
            List<Connection> connections)
        {
            var chainLetters = new List<string> { playerLetter };
            chainLetters.AddRange(myNeutrals.Select(n => n.Letter));

            var connNamesInChain = new string[chainLetters.Count - 1];
            for (int i = 0; i < connNamesInChain.Length; i++)
                connNamesInChain[i] = $"Tourney-{chainLetters[i]}-{chainLetters[i + 1]}";

            for (int i = 0; i < chainLetters.Count; i++)
            {
                string letter = chainLetters[i];
                var myConns = new List<string>();
                if (i > 0)                        myConns.Add(connNamesInChain[i - 1]);
                if (i < connNamesInChain.Length)  myConns.Add(connNamesInChain[i]);

                if (i == 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIndex + 1}", myConns.ToArray(),
                        settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions,
                        settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], myConns.ToArray(),
                        settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
            }

            for (int i = 0; i < connNamesInChain.Length; i++)
            {
                string fromLetter = chainLetters[i];
                string toLetter   = chainLetters[i + 1];
                string fromZone = i == 0 ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connNamesInChain[i],
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_guard_{fromLetter}_{toLetter}"
                });
            }
        }

        /// <summary>
        /// Builds one player's isolated cluster as a private hub-and-spoke layout.
        /// A dedicated mini-hub zone (named "Hub-{playerLetter}") sits at the centre
        /// and connects directly to the player's spawn and all of their exclusive
        /// neutral zones.  No connection touches the other player's cluster.
        /// </summary>
        private static void BuildTournamentHubCluster(
            int playerIndex,
            string playerLetter,
            List<NeutralZonePlan> myNeutrals,
            Dictionary<string, NeutralZonePlan> neutralByLetter,
            GeneratorSettings settings,
            GenerationTuning tuning,
            List<Zone> zones,
            List<Connection> connections)
        {
            string hubName = $"Hub-{playerLetter}";

            // All spokes: player spawn + each neutral.
            var spokeLetters = new List<string> { playerLetter };
            spokeLetters.AddRange(myNeutrals.Select(n => n.Letter));

            // Connection name for each spoke: "THubSpoke-{playerLetter}-{spokeLetter}"
            var spokeConnNames = spokeLetters
                .Select(l => $"THubSpoke-{playerLetter}-{l}")
                .ToList();

            // Build the hub zone itself.
            zones.Add(new Zone
            {
                Name = hubName,
                Size = settings.HubZoneSize,
                Layout = CenterLayoutName,
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = ScaleGuardMultiplier(1.5, tuning),
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [0, 10, 10, 20, 10, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_side_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_side_zone"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
                MandatoryContent = [],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = ScaleStructureValue(300000 * tuning.ContentScale, tuning),
                GuardedContentValuePerArea = ScaleStructureValue(2400 * Math.Sqrt(tuning.ContentScale), tuning),
                UnguardedContentValue = ScaleStructureValue(50000 * tuning.ContentScale, tuning),
                UnguardedContentValuePerArea = ScaleStructureValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
                ResourcesValue = ScaleResourceValue(80000 * tuning.ContentScale, tuning),
                ResourcesValuePerArea = ScaleResourceValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
                MainObjects = [],
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = spokeConnNames.Select(c => PlainRoad(ConnectionEndpoint(c), ConnectionEndpoint(c))).ToList()
            });

            // Build each spoke zone (player spawn or neutral).
            for (int i = 0; i < spokeLetters.Count; i++)
            {
                string letter = spokeLetters[i];
                string connName = spokeConnNames[i];

                if (i == 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIndex + 1}", [connName],
                        settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions,
                        settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], [connName],
                        settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
            }

            // Build the hub → spoke connections.
            for (int i = 0; i < spokeLetters.Count; i++)
            {
                string spokeLetter = spokeLetters[i];
                string spokeZone   = i == 0 ? $"Spawn-{spokeLetter}" : $"Neutral-{spokeLetter}";
                connections.Add(new Connection
                {
                    Name = spokeConnNames[i],
                    From = hubName,
                    To = spokeZone,
                    ConnectionType = "Direct",
                    GuardZone = hubName,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_hub_guard_{playerLetter}_{spokeLetter}"
                });
            }

            // Proximity ring around the spokes so the engine places them in a sensible
            // order around the hub rather than arbitrarily.
            for (int i = 0; i < spokeLetters.Count; i++)
            {
                int next = (i + 1) % spokeLetters.Count;
                string fromLetter = spokeLetters[i];
                string toLetter   = spokeLetters[next];
                string fromZone = i    == 0 ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = next == 0 ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = $"TPseudo-{playerLetter}-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Proximity"
                });
            }
        }


        /// <summary>
        /// Builds one player's isolated cluster using a shared Delaunay edge template so
        /// both clusters have an identical internal topology, ensuring a mirrored layout.
        /// Only edges whose both endpoints fall within this cluster's zone count are used.
        /// </summary>
        private static void BuildTournamentRandomCluster(
            int playerIndex,
            string playerLetter,
            List<NeutralZonePlan> myNeutrals,
            Dictionary<string, NeutralZonePlan> neutralByLetter,
            GeneratorSettings settings,
            GenerationTuning tuning,
            List<Zone> zones,
            List<Connection> connections,
            List<(int A, int B)> templateEdges)
        {
            var clusterLetters = new List<string> { playerLetter };
            clusterLetters.AddRange(myNeutrals.Select(n => n.Letter));
            int count = clusterLetters.Count;

            // Use only edges where both endpoints exist in this cluster (handles odd splits
            // where one cluster has fewer zones than the template).
            var pairs = templateEdges.Where(e => e.A < count && e.B < count).ToList();

            var connsByIndex = Enumerable.Range(0, count).ToDictionary(i => i, _ => new List<string>());

            foreach (var (a, b) in pairs)
            {
                string fromLetter = clusterLetters[a];
                string toLetter   = clusterLetters[b];
                string connName   = $"TourneyRnd-{fromLetter}-{toLetter}";
                connsByIndex[a].Add(connName);
                connsByIndex[b].Add(connName);

                string fromZone = a == 0 ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = b == 0 ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connName,
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_rnd_guard_{fromLetter}_{toLetter}"
                });
            }

            for (int i = 0; i < count; i++)
            {
                string letter = clusterLetters[i];
                var myConns = connsByIndex[i].ToArray();
                if (i == 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIndex + 1}", myConns,
                        settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions,
                        settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], myConns,
                        settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
            }
        }

        // ── Topology: Default (Ring) ──────────────────────────────────────────────

        private static Variant BuildVariantDefault(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, string? holdCityNeutralLetter = null)
        {
            var neutralByLetter = neutralZones.ToDictionary(zone => zone.Letter);
            var orderedLetters = BuildOrderedLetters(settings, playerLetters, neutralZones, isRing: true);
            int outerCount = orderedLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

            // Pre-compute ring connection names, but only for pairs that are actually connected.
            // A pair is skipped when isolation is on and both zones are player zones.
            var ringConnRight = new string[outerCount]; // name of the connection from i to i+1
            var ringConnLeft  = new string[outerCount]; // name of the connection from i-1 to i
            for (int i = 0; i < outerCount; i++)
            {
                int next = (i + 1) % outerCount;
                bool bothPlayers = playerLetters.Contains(orderedLetters[i])
                                && playerLetters.Contains(orderedLetters[next]);
                if (isolate && bothPlayers) continue; // leave entries as null — no connection here
                string name = $"Ring-{orderedLetters[i]}-{orderedLetters[next]}";
                ringConnRight[i]    = name;
                ringConnLeft[next]  = name;
            }

            var zones = new List<Zone>();
            for (int i = 0; i < outerCount; i++)
            {
                string letter = orderedLetters[i];
                // Only include non-null connection names for this zone's roads.
                var myConns = new[] { ringConnLeft[i], ringConnRight[i] }
                    .Where(c => c != null).Distinct().ToArray();

                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns, settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], myConns, settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning, letter == holdCityNeutralLetter));
            }

            var connections = new List<Connection>();
            connections.AddRange(BuildRingConnections(playerLetters, orderedLetters, tuning, isolate));
            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, orderedLetters, tuning, settings.MaxPortalConnections));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections, tuning);
            return MakeVariant(playerLetters, orderedLetters[0], outerCount, zones, connections);
        }

        // ── Topology: Random Proximity ────────────────────────────────────────────

        private static Variant BuildVariantRandom(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, string? holdCityNeutralLetter = null)
        {
            var rng = new Random();
            var neutralByLetter = neutralZones.ToDictionary(zone => zone.Letter);
            var neutralLetters = neutralZones.Select(zone => zone.Letter).ToList();
            // Shuffle zones so player/neutral order is random.
            var allLetters = settings.ExperimentalBalancedZonePlacement
                ? BuildBalancedRingLetters(playerLetters, neutralZones, minNeutralZonesBetweenPlayers: 0)
                : playerLetters.Concat(neutralLetters).OrderBy(_ => rng.Next()).ToList();
            int count = allLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

            // Assign random 2D positions in the unit square.
            // Jitter positions slightly apart to avoid degenerate collinear/cocircular cases.
            var pos = settings.ExperimentalBalancedZonePlacement
                ? BuildBalancedRandomPositions(allLetters, playerLetters, neutralByLetter)
                : new List<(double X, double Y)>();
            if (!settings.ExperimentalBalancedZonePlacement)
            {
                for (int i = 0; i < count; i++)
                    pos.Add((rng.NextDouble() * 0.9 + 0.05, rng.NextDouble() * 0.9 + 0.05));
            }

            // Compute Delaunay triangulation — every edge is a true zone-to-zone border.
            // With only up to 32 points this is fast enough to do with Bowyer-Watson.
            var pairs = DelaunayEdges(pos);

            // Build connection name lookup per zone index.
            var connsByZone = Enumerable.Range(0, count).ToDictionary(i => i, _ => new List<string>());
            var connections = new List<Connection>();

            foreach (var (a, b) in pairs)
            {
                string fromLetter = allLetters[a];
                string toLetter   = allLetters[b];
                if (isolate && playerLetters.Contains(fromLetter) && playerLetters.Contains(toLetter))
                    continue;

                string connName = $"Rnd-{fromLetter}-{toLetter}";
                connsByZone[a].Add(connName);
                connsByZone[b].Add(connName);

                string fromZone = playerLetters.Contains(fromLetter) ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = playerLetters.Contains(toLetter)   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connName,
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"rnd_guard_{fromLetter}_{toLetter}"
                });
            }

            var zones = new List<Zone>();
            for (int i = 0; i < count; i++)
            {
                string letter = allLetters[i];
                var myConns = connsByZone[i].ToArray();
                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns, settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], myConns, settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning, letter == holdCityNeutralLetter));
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, allLetters, tuning, settings.MaxPortalConnections));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections, tuning);
            return MakeVariant(playerLetters, allLetters[0], count, zones, connections);
        }

        private static List<(double X, double Y)> BuildBalancedRandomPositions(
            List<string> orderedLetters,
            List<string> playerLetters,
            Dictionary<string, NeutralZonePlan> neutralByLetter)
        {
            int count = orderedLetters.Count;
            if (count == 0) return [];

            var playerSet = playerLetters.ToHashSet(StringComparer.Ordinal);
            var positions = new List<(double X, double Y)>(count);
            for (int i = 0; i < count; i++)
            {
                string letter = orderedLetters[i];
                bool isPlayer = playerSet.Contains(letter);
                double angle = 2.0 * Math.PI * i / count;
                double jitter = isPlayer ? 0.0 : ((i % 3) - 1) * 0.018;

                double radius = 0.43;
                if (!isPlayer && neutralByLetter.TryGetValue(letter, out var neutralZone))
                    radius = 0.30 + NeutralZoneBalanceScore(neutralZone) * 0.035 + (i % 2) * 0.012;

                positions.Add((
                    Math.Clamp(0.5 + Math.Cos(angle + jitter) * radius, 0.05, 0.95),
                    Math.Clamp(0.5 + Math.Sin(angle + jitter) * radius, 0.05, 0.95)));
            }

            return positions;
        }

        // ── Delaunay triangulation (Bowyer-Watson) ────────────────────────────────

        /// <summary>
        /// Returns the unique undirected edges of the Delaunay triangulation of the given points.
        /// Each edge (A, B) has A &lt; B.
        /// </summary>
        private static List<(int A, int B)> DelaunayEdges(List<(double X, double Y)> pts)
        {
            int n = pts.Count;
            if (n == 1) return [];
            if (n == 2) return [(0, 1)];

            // Super-triangle large enough to contain all points.
            double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y);
            double maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
            double dx = maxX - minX, dy = maxY - minY;
            double delta = Math.Max(dx, dy) * 10;
            var superPts = new List<(double X, double Y)>(pts)
            {
                (minX - delta,     minY - delta * 3),
                (minX + delta * 3, minY - delta),
                (minX,             minY + delta * 3)
            };
            int s0 = n, s1 = n + 1, s2 = n + 2;

            // Each triangle is (i0, i1, i2) into superPts.
            var triangles = new List<(int I0, int I1, int I2)> { (s0, s1, s2) };

            for (int p = 0; p < n; p++)
            {
                double px = superPts[p].X, py = superPts[p].Y;

                // Find all triangles whose circumcircle contains this point.
                var bad = triangles.Where(t => InCircumcircle(superPts, t, px, py)).ToList();

                // Collect the boundary polygon of the bad triangles (edges not shared by two bad triangles).
                var boundary = new List<(int A, int B)>();
                foreach (var t in bad)
                {
                    (int A, int B)[] edges = [(t.I0, t.I1), (t.I1, t.I2), (t.I2, t.I0)];
                    foreach (var e in edges)
                    {
                        bool shared = bad.Any(other => other != t &&
                            ((other.I0 == e.A && other.I1 == e.B) || (other.I1 == e.A && other.I0 == e.B) ||
                             (other.I1 == e.A && other.I2 == e.B) || (other.I2 == e.A && other.I1 == e.B) ||
                             (other.I2 == e.A && other.I0 == e.B) || (other.I0 == e.A && other.I2 == e.B)));
                        if (!shared) boundary.Add(e);
                    }
                }

                foreach (var t in bad) triangles.Remove(t);
                foreach (var (a, b) in boundary)
                    triangles.Add((a, b, p));
            }

            // Remove triangles that share a vertex with the super-triangle.
            triangles.RemoveAll(t => t.I0 >= n || t.I1 >= n || t.I2 >= n);

            // Extract unique edges from real points only.
            var edgeSet = new HashSet<(int, int)>();
            foreach (var t in triangles)
            {
                edgeSet.Add((Math.Min(t.I0, t.I1), Math.Max(t.I0, t.I1)));
                edgeSet.Add((Math.Min(t.I1, t.I2), Math.Max(t.I1, t.I2)));
                edgeSet.Add((Math.Min(t.I2, t.I0), Math.Max(t.I2, t.I0)));
            }
            return [.. edgeSet];
        }

        private static bool InCircumcircle(List<(double X, double Y)> pts, (int I0, int I1, int I2) t, double px, double py)
        {
            double ax = pts[t.I0].X - px, ay = pts[t.I0].Y - py;
            double bx = pts[t.I1].X - px, by = pts[t.I1].Y - py;
            double cx = pts[t.I2].X - px, cy = pts[t.I2].Y - py;
            double det = ax * (by * (cx * cx + cy * cy) - cy * (bx * bx + by * by))
                       - ay * (bx * (cx * cx + cy * cy) - cx * (bx * bx + by * by))
                       + (ax * ax + ay * ay) * (bx * cy - by * cx);
            return det > 0;
        }

        // ── Topology: Hub & Spoke ─────────────────────────────────────────────────

        private static Variant BuildVariantHubAndSpoke(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, bool hubIsHoldCity = false)
        {
            // A central "Hub" zone connects to every outer zone.
            // Outer zones are players + neutrals arranged around the hub.
            var neutralByLetter = neutralZones.ToDictionary(zone => zone.Letter);
            var neutralLetters = neutralZones.Select(zone => zone.Letter).ToList();
            List<string> outerLetters;
            if (settings.ExperimentalBalancedZonePlacement)
            {
                int honoredSeparation = settings.MinNeutralZonesBetweenPlayers > 0
                    && CanHonorNeutralSeparation(settings, neutralZones.Count)
                        ? settings.MinNeutralZonesBetweenPlayers
                        : 0;
                outerLetters = BuildBalancedRingLetters(playerLetters, neutralZones, honoredSeparation);
            }
            else
            {
                outerLetters = playerLetters.Concat(neutralLetters).ToList();
            }
            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Hub zone (neutral, no castle, high loot).
            var hubConns = outerLetters.Select(l => $"Hub-{l}").ToArray();
            zones.Add(BuildHubZone(hubConns, tuning, hubIsHoldCity, settings.HubZoneSize));

            // Outer zones each connect only to the hub.
            for (int i = 0; i < outerLetters.Count; i++)
            {
                string letter = outerLetters[i];
                var spokeConns = new[] { $"Hub-{letter}" };
                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", spokeConns, settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], spokeConns, settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
            }

            // Hub → each outer zone: Direct guarded connections (multiple per zone like JCC).
            foreach (var letter in outerLetters)
            {
                string outerZone = playerLetters.Contains(letter) ? $"Spawn-{letter}" : $"Neutral-{letter}";
                // Main named connection.
                connections.Add(new Connection
                {
                    Name = $"Hub-{letter}",
                    From = "Hub",
                    To = outerZone,
                    ConnectionType = "Direct",
                    GuardZone = "Hub",
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"hub_guard_{letter}"
                });
                // Extra Direct connections with unique guardMatchGroups (matches JCC pattern).
                for (int e = 1; e < ConnectionsPerZone; e++)
                    connections.Add(new Connection
                    {
                        From = "Hub",
                        To = outerZone,
                        ConnectionType = "Direct",
                        GuardZone = "Hub",
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = ScaleBorderGuardValue(30000, tuning),
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"hub_guard_{letter}_{e}"
                    });
            }

            // Proximity connections around the full outer ring to tell the engine which
            // zones are neighbours. Without these hints the engine ignores the zone ordering
            // and places zones arbitrarily, causing players to end up next to each other
            // even when neutrals separate them in the ordered list.
            for (int i = 0; i < outerLetters.Count; i++)
            {
                int next = (i + 1) % outerLetters.Count;
                string fromLetter = outerLetters[i];
                string toLetter   = outerLetters[next];
                bool fromIsPlayer = playerLetters.Contains(fromLetter);
                bool toIsPlayer   = playerLetters.Contains(toLetter);

                // Skip direct player–player proximity when isolation is requested.
                if (settings.NoDirectPlayerConnections && fromIsPlayer && toIsPlayer)
                    continue;

                string fromZone = fromIsPlayer ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = toIsPlayer   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = $"Pseudo-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Proximity"
                });
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, outerLetters, tuning, settings.MaxPortalConnections));

            return MakeVariant(playerLetters, outerLetters[0], outerLetters.Count + 1, zones, connections);
        }

        // ── Topology: Chain ───────────────────────────────────────────────────────

        private static Variant BuildVariantChain(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, string? holdCityNeutralLetter = null)
        {
            var neutralByLetter = neutralZones.ToDictionary(zone => zone.Letter);
            var orderedLetters = BuildOrderedLetters(settings, playerLetters, neutralZones, isRing: false);
            int count = orderedLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

            // Pre-compute connection names for each adjacent pair, skipping player–player
            // pairs when isolation is enabled. null means no connection at that position.
            var connNames = new string[count - 1];
            for (int i = 0; i < count - 1; i++)
            {
                bool bothPlayers = playerLetters.Contains(orderedLetters[i])
                                && playerLetters.Contains(orderedLetters[i + 1]);
                if (isolate && bothPlayers) continue;
                connNames[i] = $"Chain-{orderedLetters[i]}-{orderedLetters[i + 1]}";
            }

            var zones = new List<Zone>();
            for (int i = 0; i < count; i++)
            {
                string letter = orderedLetters[i];
                var myConns = new List<string>();
                if (i > 0         && connNames[i - 1] != null) myConns.Add(connNames[i - 1]);
                if (i < count - 1 && connNames[i]     != null) myConns.Add(connNames[i]);

                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns.ToArray(), settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], myConns.ToArray(), settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning, letter == holdCityNeutralLetter));
            }

            var connections = new List<Connection>();
            for (int i = 0; i < count - 1; i++)
            {
                if (connNames[i] == null) continue;
                string fromLetter = orderedLetters[i];
                string toLetter   = orderedLetters[i + 1];
                string fromZone = playerLetters.Contains(fromLetter) ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = playerLetters.Contains(toLetter)   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connNames[i],
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"chain_guard_{fromLetter}_{toLetter}"
                });
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, orderedLetters, tuning, settings.MaxPortalConnections));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections, tuning);
            return MakeVariant(playerLetters, orderedLetters[0], count, zones, connections);
        }

        // ── Topology: Shared Web ──────────────────────────────────────────────────

        private static Variant BuildVariantSharedWeb(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, string? holdCityNeutralLetter = null)
        {
            // Each player connects to two neutral zones.
            // Neutral zones are arranged in a ring connecting all players.
            // If there are fewer neutrals than needed, we wrap around (multiple players share a neutral).
            // Requires at least 1 neutral zone.
            var neutralByLetter = neutralZones.ToDictionary(zone => zone.Letter);
            var neutrals = settings.ExperimentalBalancedZonePlacement
                ? BuildBalancedNeutralRing(neutralZones, playerLetters.Count)
                : neutralZones.Select(zone => zone.Letter).ToList();

            int p = playerLetters.Count;
            int n = neutrals.Count;

            // Pre-compute neutral ring connection names.
            var neutralRingConns = new string[n];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                neutralRingConns[i] = $"NRing-{neutrals[i]}-{neutrals[next]}";
            }

            var spokeConnsByPlayer = playerLetters.ToDictionary(letter => letter, _ => new List<string>());
            var spokeConnsByNeutral = neutrals.ToDictionary(letter => letter, _ => new List<string>());
            for (int i = 0; i < p; i++)
            {
                // Spread players across neutral ring evenly.
                int n1 = (i * n / p) % n;
                int n2 = ((i * n / p) + 1) % n;

                AddSpoke(playerLetters[i], neutrals[n1]);
                if (n1 != n2)
                    AddSpoke(playerLetters[i], neutrals[n2]);
            }

            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Build neutral zones. Each neutral receives its ring and player-spoke endpoints.
            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                var neutralConns = new List<string>();
                if (n > 1)
                {
                    neutralConns.Add(neutralRingConns[prev]);
                    neutralConns.Add(neutralRingConns[i]);
                }

                neutralConns.AddRange(spokeConnsByNeutral[neutrals[i]]);
                string[] nConns = neutralConns.Distinct().ToArray();
                Zone neutralZone = BuildNeutralZone(neutralByLetter[neutrals[i]], nConns, settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning, neutrals[i] == holdCityNeutralLetter);
                if (neutralByLetter[neutrals[i]].CastleCount == 0)
                    neutralZone.Roads = BuildConnectorZoneRoads(nConns, settings.GenerateRoads);
                zones.Add(neutralZone);
            }

            // Build player zones — each connects to two neutrals (evenly distributed).
            for (int i = 0; i < p; i++)
            {
                var spokeConns = spokeConnsByPlayer[playerLetters[i]];

                zones.Add(BuildSpawnZone(playerLetters[i], $"Player{i + 1}", spokeConns.ToArray(), settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));

                // Player → neutral Direct connections.
                foreach (var connName in spokeConns)
                {
                    string neutralLetter = connName.Split('-')[2]; // "Web-A-I" → index 2 = "I"
                    string neutralZone = $"Neutral-{neutralLetter}";
                    connections.Add(new Connection
                    {
                        Name = connName,
                        From = $"Spawn-{playerLetters[i]}",
                        To = neutralZone,
                        ConnectionType = "Direct",
                        GuardZone = neutralZone,
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = ScaleBorderGuardValue(30000, tuning),
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"web_guard_{playerLetters[i]}_{neutralLetter}"
                    });
                }
            }

            // Neutral ring connections.
            if (n > 1)
            {
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    connections.Add(new Connection
                    {
                        Name = neutralRingConns[i],
                        From = $"Neutral-{neutrals[i]}",
                        To = $"Neutral-{neutrals[next]}",
                        ConnectionType = "Direct",
                        GuardZone = $"Neutral-{neutrals[i]}",
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = ScaleBorderGuardValue(20000, tuning),
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"nring_guard_{neutrals[i]}_{neutrals[next]}"
                    });
                }
            }

            if (settings.RandomPortals)
            {
                var allLetters = playerLetters.Concat(neutrals).ToList();
                connections.AddRange(BuildRandomPortalConnections(playerLetters, allLetters, tuning, settings.MaxPortalConnections));
            }

            bool isolateWeb = settings.NoDirectPlayerConnections && playerLetters.Count > 1;
            if (isolateWeb) EnsurePlayerZonesConnected(playerLetters, zones, connections, tuning);

            return MakeVariant(playerLetters, playerLetters[0], zones.Count, zones, connections);

            void AddSpoke(string playerLetter, string neutralLetter)
            {
                string connName = $"Web-{playerLetter}-{neutralLetter}";
                spokeConnsByPlayer[playerLetter].Add(connName);
                spokeConnsByNeutral[neutralLetter].Add(connName);
            }
        }

        // ── Hub zone (only used by Hub & Spoke topology) ─────────────────────────

        private static Zone BuildHubZone(string[] spokeConns, GenerationTuning tuning, bool isHoldCity = false, double size = 1.0)
        {
            var mainObjects = new List<MainObject>();
            if (isHoldCity)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 1.0,
                    GuardValue = ScaleNeutralGuardValue(25000, tuning),
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "ultra_rich_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Center",
                    PlacementArgs = [],
                    HoldCityWinCon = true
                });
            }

            return new Zone
            {
            Name = "Hub",
            Size = size,
            Layout = CenterLayoutName,
            GuardCutoffValue = 2000,
            GuardRandomization = 0.05,
            GuardMultiplier = ScaleGuardMultiplier(1.5, tuning),
            GuardWeeklyIncrement = 0.20,
            GuardReactionDistribution = [0, 10, 10, 20, 10, 0],
            DiplomacyModifier = -0.5,
            GuardedContentPool = ["template_pool_jebus_cross_guarded_side_zone"],
            UnguardedContentPool = ["template_pool_jebus_cross_unguarded_side_zone"],
            ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
            MandatoryContent = [],
            ContentCountLimits = BuildSideContentLimits(),
            GuardedContentValue = ScaleStructureValue(300000 * tuning.ContentScale, tuning),
            GuardedContentValuePerArea = ScaleStructureValue(2400 * Math.Sqrt(tuning.ContentScale), tuning),
            UnguardedContentValue = ScaleStructureValue(50000 * tuning.ContentScale, tuning),
            UnguardedContentValuePerArea = ScaleStructureValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
            ResourcesValue = ScaleResourceValue(80000 * tuning.ContentScale, tuning),
            ResourcesValuePerArea = ScaleResourceValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
            MainObjects = mainObjects,
            ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
            ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
            MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                    CrossroadsPosition = 0,
                    Roads = spokeConns.Select(c => PlainRoad(
                        isHoldCity ? MainObjectEndpoint("0") : ConnectionEndpoint(c),
                        ConnectionEndpoint(c))).ToList()
                };
            }

        // ── Isolation failsafe ───────────────────────────────────────────────────

        /// <summary>
        /// When "isolate player zones" is active, player zones that ended up with no connections
        /// (because all their neighbours were also player zones) must still be reachable.
        /// This method adds a minimal direct player–player fallback connection for each such zone.
        /// </summary>
        private static void EnsurePlayerZonesConnected(
            List<string> playerLetters, List<Zone> zones, List<Connection> connections, GenerationTuning tuning)
        {
            if (playerLetters.Count < 2) return;

            // Collect the set of connection names already present in the connection list.
            var connNames = connections.Select(c => c.Name).Where(n => n != null).ToHashSet();

            foreach (var letter in playerLetters)
            {
                string zoneName = $"Spawn-{letter}";
                var zone = zones.FirstOrDefault(z => z.Name == zoneName);
                if (zone == null) continue;

                // Check whether this zone has any named road pointing to an existing connection.
                bool hasConnection = zone.Roads != null && zone.Roads
                    .Any(r => r.To?.Type == "Connection" && connNames.Contains(r.To.Args?[0]));

                if (hasConnection) continue;

                // Find another player zone that is also disconnected, or any other player zone.
                string? partnerLetter = playerLetters
                    .Where(pl => pl != letter)
                    .OrderBy(pl =>
                    {
                        var pZone = zones.FirstOrDefault(z => z.Name == $"Spawn-{pl}");
                        bool partnerHasConn = pZone?.Roads != null && pZone.Roads
                            .Any(r => r.To?.Type == "Connection" && connNames.Contains(r.To.Args?[0]));
                        return partnerHasConn ? 1 : 0; // prefer pairing two isolated players together
                    })
                    .FirstOrDefault();

                if (partnerLetter == null) continue;

                // Only add if we don't already have a fallback connection between these two.
                string pair = (string.Compare(letter, partnerLetter, StringComparison.Ordinal) < 0)
                    ? letter + "-" + partnerLetter
                    : partnerLetter + "-" + letter;
                string fallbackName = $"Fallback-{pair}";
                if (connNames.Contains(fallbackName)) continue;

                // Register the connection.
                connections.Add(new Connection
                {
                    Name = fallbackName,
                    From = $"Spawn-{letter}",
                    To = $"Spawn-{partnerLetter}",
                    ConnectionType = "Direct",
                    GuardZone = $"Spawn-{letter}",
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"fallback_guard_{fallbackName}"
                });
                connNames.Add(fallbackName);

                // Add road endpoints to both zones.
                foreach (var pLetter in new[] { letter, partnerLetter })
                {
                    var pZone = zones.FirstOrDefault(z => z.Name == $"Spawn-{pLetter}");
                    if (pZone != null)
                    {
                        pZone.Roads ??= [];
                        pZone.Roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(fallbackName)));
                    }
                }
            }
        }

        // ── Variant factory helper ────────────────────────────────────────────────

        private static Variant MakeVariant(List<string> playerLetters, string firstLetter, int totalZones, List<Zone> zones, List<Connection> connections) => new()
        {
            Orientation = new Orientation
            {
                ZeroAngleZone = playerLetters.Contains(firstLetter) ? $"Spawn-{firstLetter}" : $"Neutral-{firstLetter}",
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

        // ── Spawn zone ───────────────────────────────────────────────────────────

        private static Zone BuildSpawnZone(string letter, string player, string[] ringConns, int castleCount, bool matchCastleFactions, double zoneSize, bool spawnFootholds, bool generateRoads, GenerationTuning tuning)
        {
            // Index 0 = Spawn (player town), indices 1..castleCount-1 = extra cities.
            var mainObjects = new List<MainObject>
            {
                new()
                {
                    Type = "Spawn",
                    Spawn = player,
                    RemoveGuardIfHasOwner = true,
                    GuardChance = 0.5,
                    GuardValue = ScaleNeutralGuardValue(5000, tuning),
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
                    Faction = matchCastleFactions
                        ? new TypedSelector { Type = "Match", Args = ["0"] }
                        : new TypedSelector { Type = "Random", Args = [] },
                    GuardChance = 0.5,
                    GuardValue = ScaleNeutralGuardValue(2500, tuning),
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "poor_buildings_construction",
                    Placement = "Uniform",
                    PlacementArgs = ["false", "-0.8", "3"]
                });
            }

            return new Zone
            {
                Name = $"Spawn-{letter}",
                Size = NormalizeZoneSize(zoneSize),
                Layout = SpawnLayoutName,
                GuardCutoffValue = 2000,
                GuardRandomization = tuning.GuardRandomization,
                GuardMultiplier = ScaleGuardMultiplier(1.0, tuning),
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_start_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_start_zone"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
                MandatoryContent = [$"mandatory_content_side_{letter}"],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = ScaleStructureValue(200000 * tuning.ContentScale, tuning),
                GuardedContentValuePerArea = ScaleStructureValue(2000 * Math.Sqrt(tuning.ContentScale), tuning),
                UnguardedContentValue = ScaleStructureValue(50000 * tuning.ContentScale, tuning),
                UnguardedContentValuePerArea = ScaleStructureValue(400 * Math.Sqrt(tuning.ContentScale), tuning),
                ResourcesValue = ScaleResourceValue(80000 * tuning.ContentScale, tuning),
                ResourcesValuePerArea = ScaleResourceValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
                MainObjects = mainObjects,
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds, generateRoads)
            };
        }

        // ── Neutral zone ─────────────────────────────────────────────────────────

        private static Zone BuildNeutralZone(NeutralZonePlan plan, string[] ringConns, double zoneSize, bool spawnFootholds, bool generateRoads, GenerationTuning tuning, bool isHoldCity = false)
        {
            string letter = plan.Letter;
            // When this zone is the hold city target, guarantee it has at least one castle.
            int castleCount = isHoldCity ? Math.Max(1, plan.CastleCount) : plan.CastleCount;
            var profile = GetNeutralZoneProfile(plan.Quality);

            var mainObjects = new List<MainObject>();
            if (castleCount > 0)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = isHoldCity ? 1.0 : 0.5,
                    GuardValue = ScaleNeutralGuardValue(isHoldCity ? Math.Max(profile.PrimaryCityGuardValue, 20000) : profile.PrimaryCityGuardValue, tuning),
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = isHoldCity ? "ultra_rich_buildings_construction" : profile.PrimaryBuildingsConstructionSid,
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = isHoldCity ? "Center" : "Uniform",
                    PlacementArgs = isHoldCity ? [] : ["true", "0.8", "2"],
                    HoldCityWinCon = isHoldCity ? true : null
                });
            }

            for (int i = 1; i < castleCount; i++)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 0.5,
                    GuardValue = ScaleNeutralGuardValue(profile.ExtraCityGuardValue, tuning),
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = profile.ExtraBuildingsConstructionSid,
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Uniform",
                    PlacementArgs = ["false", "-0.8", "3"]
                });
            }

            return new Zone
            {
                Name = $"Neutral-{letter}",
                Size = NormalizeZoneSize(zoneSize),
                Layout = profile.Layout,
                GuardCutoffValue = 2000,
                GuardRandomization = tuning.GuardRandomization,
                GuardMultiplier = ScaleGuardMultiplier(profile.GuardMultiplier, tuning),
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = plan.Quality == NeutralZoneQuality.High ? [0, 10, 10, 20, 10, 0] : [0, 10, 10, 10, 10, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = [profile.GuardedContentPool],
                UnguardedContentPool = [profile.UnguardedContentPool],
                ResourcesContentPool = [profile.ResourcesContentPool],
                MandatoryContent = [$"mandatory_content_neutral_{letter}"],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = ScaleStructureValue(profile.GuardedContentValue * tuning.ContentScale, tuning),
                GuardedContentValuePerArea = ScaleStructureValue(profile.GuardedContentValuePerArea * Math.Sqrt(tuning.ContentScale), tuning),
                UnguardedContentValue = ScaleStructureValue(profile.UnguardedContentValue * tuning.ContentScale, tuning),
                UnguardedContentValuePerArea = ScaleStructureValue(profile.UnguardedContentValuePerArea * Math.Sqrt(tuning.ContentScale), tuning),
                ResourcesValue = ScaleResourceValue(profile.ResourcesValue * tuning.ContentScale, tuning),
                ResourcesValuePerArea = ScaleResourceValue(profile.ResourcesValuePerArea * Math.Sqrt(tuning.ContentScale), tuning),
                MainObjects = mainObjects,
                ZoneBiome = castleCount > 0
                    ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                    : new BiomeSelector { Type = "MatchZone", Args = [] },
                ContentBiome = castleCount > 0
                    ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                    : new BiomeSelector { Type = "MatchZone", Args = [] },
                MetaObjectsBiome = castleCount > 0
                    ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                    : new BiomeSelector { Type = "MatchZone", Args = [] },
                CrossroadsPosition = 0,
                Roads = BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds, generateRoads)
            };
        }

        private static NeutralZoneProfile GetNeutralZoneProfile(NeutralZoneQuality quality) => quality switch
        {
            NeutralZoneQuality.Low => new NeutralZoneProfile(
                SideLayoutName,
                1.0,
                "template_pool_jebus_cross_guarded_start_zone",
                "template_pool_jebus_cross_unguarded_start_zone",
                "content_pool_general_resources_start_zone_rich",
                140000,
                1200,
                30000,
                240,
                40000,
                320,
                5000,
                2500,
                "poor_buildings_construction",
                "poor_buildings_construction"),
            NeutralZoneQuality.High => new NeutralZoneProfile(
                CenterLayoutName,
                1.6,
                "template_pool_jebus_cross_guarded_center_zone",
                "template_pool_jebus_cross_unguarded_center_zone",
                "content_pool_general_resources_treasure_zone_rich_no_scrolls",
                520000,
                3200,
                90000,
                700,
                100000,
                650,
                18000,
                9000,
                "rich_buildings_construction",
                "rich_buildings_construction"),
            _ => new NeutralZoneProfile(
                TreasureLayoutName,
                1.3,
                "template_pool_jebus_cross_guarded_side_zone",
                "template_pool_jebus_cross_unguarded_side_zone",
                "content_pool_general_resources_start_zone_rich",
                260000,
                2400,
                40000,
                320,
                60000,
                480,
                10000,
                5000,
                "rich_buildings_construction",
                "poor_buildings_construction"),
        };

        // ── Connections

        /// <summary>
        /// Creates guarded Direct connections between every adjacent pair of outer zones
        /// arranged in a ring (zone[i] ↔ zone[i+1], wrapping around).
        /// When <paramref name="isolatePlayers"/> is true, player–player adjacent pairs are skipped.
        /// </summary>
        private static IEnumerable<Connection> BuildRingConnections(
            List<string> playerLetters, List<string> orderedLetters, GenerationTuning tuning, bool isolatePlayers = false)
        {
            int count = orderedLetters.Count;
            if (count < 2) yield break;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                string fromLetter = orderedLetters[i];
                string toLetter   = orderedLetters[next];

                if (isolatePlayers && playerLetters.Contains(fromLetter) && playerLetters.Contains(toLetter))
                    continue;

                string fromZone = playerLetters.Contains(fromLetter) ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = playerLetters.Contains(toLetter)   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";

                yield return new Connection
                {
                    Name = $"Ring-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = ScaleBorderGuardValue(30000, tuning),
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
            List<string> playerLetters, List<string> orderedLetters, GenerationTuning tuning, int maxCount = 32)
        {
            int count = orderedLetters.Count;
            if (count < 2) yield break; // Need at least 2 zones for a portal.

            // Build a derangement where zone[i] -> zone[dest[i]] and dest[i] is never an
            // immediate neighbour (i-1, i, i+1 mod count).
            var rng = new Random();
            int[] dest = BuildNonAdjacentDerangement(count, rng);

            // Shuffle which zones get portals so limiting the count picks random zones,
            // not always the first ones in layout order.
            int[] indices = Enumerable.Range(0, count).OrderBy(_ => rng.Next()).ToArray();

            int limit = Math.Min(count, maxCount);
            for (int i = 0; i < limit; i++)
            {
                int idx = indices[i];
                string fromLetter = orderedLetters[idx];
                string toLetter   = orderedLetters[dest[idx]];

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
                    GuardValue = ScaleBorderGuardValue(25000, tuning),
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
        private static List<Road> BuildOuterZoneRoads(string[] ringConns, int castleCount, bool includeFoothold, bool generateRoads)
        {
            var roads = new List<Road>();
            if (!generateRoads || castleCount == 0) return roads;

            for (int i = 1; i < castleCount; i++)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint(i.ToString())));

            if (includeFoothold)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint("name_remote_foothold_1")));
            foreach (var rc in ringConns)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(rc)));
            return roads;
        }

        private static List<Road> BuildConnectorZoneRoads(string[] connectionNames, bool generateRoads)
        {
            var roads = new List<Road>();
            if (!generateRoads) return roads;

            var distinctConnections = connectionNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            if (distinctConnections.Count == 1)
            {
                string connectionName = distinctConnections[0];
                roads.Add(PlainRoad(ConnectionEndpoint(connectionName), ConnectionEndpoint(connectionName)));
                return roads;
            }

            string? anchor = distinctConnections.FirstOrDefault();
            if (anchor == null) return roads;

            foreach (string connectionName in distinctConnections.Skip(1))
                roads.Add(PlainRoad(ConnectionEndpoint(anchor), ConnectionEndpoint(connectionName)));
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
            BuildZoneLayout(SpawnLayoutName, 0.24, 0.48, 0.30, 16, 0.16, 160, -0.30, 0.4, [20, 2, 1]),
            BuildZoneLayout(SideLayoutName, 0.36, 0.50, 0.25, 16, 0.128, 128, -0.30, 0.3, [20, 2, 1]),
            BuildZoneLayout(TreasureLayoutName, 0.50, 0.50, 0.45, 12, 0.12, 96, -0.30, 0.3, [12, 3, 1]),
            BuildZoneLayout(CenterLayoutName, 0.56, 0.60, 0.30, 10, 0.128, 96, -0.25, 0.3, [12, 4, 1])
        ];

        private static ZoneLayout BuildZoneLayout(
            string name,
            double obstaclesFill,
            double obstaclesFillVoid,
            double lakesFill,
            int minLakeArea,
            double elevationClusterScale,
            int roadClusterArea,
            double roadAttraction,
            double ambientNoise,
            int[] groupSizeWeights) => new()
            {
                Name = name,
                ObstaclesFill = obstaclesFill,
                ObstaclesFillVoid = obstaclesFillVoid,
                LakesFill = lakesFill,
                MinLakeArea = minLakeArea,
                ElevationClusterScale = elevationClusterScale,
                ElevationModes =
                [
                    new ElevationMode { Weight = 2, MinElevatedFraction = 0.2, MaxElevatedFraction = 0.4 },
                    new ElevationMode { Weight = 1, MinElevatedFraction = 0.6, MaxElevatedFraction = 0.8 }
                ],
                RoadClusterArea = roadClusterArea,
                GuardedEncounterResourceFractions = new GuardedEncounterResourceFractions
                {
                    CountBounds = [],
                    Fractions = [0.66]
                },
                AmbientPickupDistribution = new AmbientPickupDistribution
                {
                    Repulsion = 1.0,
                    Noise = ambientNoise,
                    RoadAttraction = roadAttraction,
                    ObstacleAttraction = 0.0,
                    GroupSizeWeights = groupSizeWeights.ToList()
                }
            };

        // ── Mandatory content ────────────────────────────────────────────────────

        private static List<MandatoryContentGroup> BuildAllMandatoryContent(
            List<string> playerLetters, List<NeutralZonePlan> neutralZones, GeneratorSettings settings)
        {
            var groups = new List<MandatoryContentGroup>();

            foreach (var letter in playerLetters)
                groups.Add(BuildSpawnMandatoryContent(letter, settings.ZoneCfg.PlayerZoneCastles, settings.SpawnRemoteFootholds));

            foreach (var neutralZone in neutralZones)
                groups.Add(BuildNeutralMandatoryContent(neutralZone.Letter, neutralZone.CastleCount, settings.SpawnRemoteFootholds));

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
            };
            if (castleCount > 0)
                footholdRules.Add(new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.2, TargetMax = 0.4, Weight = 0 });
            if (castleCount > 1)
                footholdRules.Add(new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.5, TargetMax = 0.5, Weight = 2 });

            var content = new List<ContentItem>();

            if (spawnFootholds)
                content.Add(new ContentItem { Name = "name_remote_foothold_1", Sid = "remote_foothold", IsGuarded = false, Rules = footholdRules });

            content.AddRange(new List<ContentItem>
            {
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_base"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_pro"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
            });

            // One wood mine and one ore mine anchored near the initial castle.
            // Rules match the consensus pattern across all example templates:
            // MainObject(0) biases placement toward the castle, Crossroads biases toward road intersections.
            if (!isNeutral)
            {
                content.AddRange(new List<ContentItem>
                {
                    new() { Sid = "mine_wood", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.35, Weight = 1 }, new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                    new() { Sid = "mine_ore",  IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.35, Weight = 1 }, new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                });
            }

            content.AddRange(new List<ContentItem>
            {
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
                new() { Sid = "mine_wood", IsMine = true },
                new() { Sid = "mine_ore", IsMine = true },
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
        /// Builds the full set of contentCountLimits derived from all example templates.
        /// Counts reflect the typical maximum values observed across templates.
        /// </summary>
        private static List<ContentCountLimit> BuildAllContentCountLimits()
        {
            var sidLimits = new List<ContentSidLimit>
            {
                // ── Utility / buff buildings ─────────────────────────────────────
                new() { Sid = "fountain",             MaxCount = 2 },
                new() { Sid = "fountain_2",           MaxCount = 2 },
                new() { Sid = "mana_well",            MaxCount = 2 },
                new() { Sid = "beer_fountain",        MaxCount = 2 },
                new() { Sid = "market",               MaxCount = 1 },
                new() { Sid = "forge",                MaxCount = 2 },
                new() { Sid = "stables",              MaxCount = 1 },
                new() { Sid = "watchtower",           MaxCount = 2 },
                new() { Sid = "wind_rose",            MaxCount = 1 },
                new() { Sid = "quixs_path",           MaxCount = 2 },
                new() { Sid = "crystal_trail",        MaxCount = 3 },
                new() { Sid = "mysterious_stone",     MaxCount = 2 },

                // ── Learning / XP buildings ──────────────────────────────────────
                new() { Sid = "university",           MaxCount = 2 },
                new() { Sid = "wise_owl",             MaxCount = 4 },
                new() { Sid = "celestial_sphere",     MaxCount = 2 },
                new() { Sid = "pile_of_books",        MaxCount = 2 },
                new() { Sid = "insaras_eye",          MaxCount = 2 },
                new() { Sid = "tear_of_truth",        MaxCount = 3 },
                new() { Sid = "tree_of_abundance",    MaxCount = 2 },

                // ── Hire buildings ───────────────────────────────────────────────
                new() { Sid = "huntsmans_camp",       MaxCount = 2 },
                new() { Sid = "shady_den",            MaxCount = 2 },
                new() { Sid = "random_hire_1",        MaxCount = 6 },
                new() { Sid = "random_hire_2",        MaxCount = 6 },
                new() { Sid = "random_hire_3",        MaxCount = 6 },
                new() { Sid = "random_hire_4",        MaxCount = 6 },
                new() { Sid = "random_hire_5",        MaxCount = 6 },
                new() { Sid = "random_hire_6",        MaxCount = 6 },
                new() { Sid = "random_hire_7",        MaxCount = 6 },

                // ── Combat / encounter buildings ─────────────────────────────────
                new() { Sid = "arena",                MaxCount = 2 },
                new() { Sid = "sacrificial_shrine",   MaxCount = 2 },
                new() { Sid = "chimerologist",        MaxCount = 2 },
                new() { Sid = "circus",               MaxCount = 2 },
                new() { Sid = "infernal_cirque",      MaxCount = 2 },
                new() { Sid = "flattering_mirror",    MaxCount = 2 },
                new() { Sid = "fickle_shrine",        MaxCount = 1 },
                new() { Sid = "point_of_balance",     MaxCount = 3 },

                // ── Special / loot ───────────────────────────────────────────────
                new() { Sid = "pandora_box",          MaxCount = 4 },

                // ── Map-feature objects (typically 0 = disabled, 99 = unlimited;
                //    we cap at a sensible value so they can occasionally appear) ──
                new() { Sid = "ritual_pyre",          MaxCount = 3 },
                new() { Sid = "boreal_call",          MaxCount = 3 },
                new() { Sid = "jousting_range",       MaxCount = 1 },
                new() { Sid = "unforgotten_grave",    MaxCount = 1 },
                new() { Sid = "petrified_memorial",   MaxCount = 1 },
                new() { Sid = "the_gorge",            MaxCount = 1 },
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
