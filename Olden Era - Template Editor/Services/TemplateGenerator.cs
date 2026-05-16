using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;
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
                ValueOverrides = BuildValueOverrides(settings.ValueOverridesText),
                GlobalBans = BuildGlobalBans(settings.BannedItems, settings.BannedMagics),
                Variants = [BuildVariant(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter, useCityHold && settings.Topology == MapTopology.HubAndSpoke)],
                ZoneLayouts = BuildZoneLayouts(),
                MandatoryContent = BuildAllMandatoryContent(playerLetters, neutralZones, settings),
                ContentCountLimits = ZoneContentManager.BuildAllContentCountLimits(settings),
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
            MapTopology.Balanced => "Balanced",
            _ => topology.ToString()
        };

        private sealed record NeutralZonePlan(string Letter, NeutralZoneQuality Quality, int CastleCount);

        private sealed record NeutralZoneProfile(
            string Layout,
            double GuardMultiplier,
            string[] GuardedContentPool,
            string[] UnguardedContentPool,
            string[] ResourcesContentPool,
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
                case MapTopology.Balanced:
                {
                    // Delaunay adjacency is computed from positions at generation time and
                    // can't be reproduced exactly during the pick phase. Use the balanced
                    // ring ordering as the best structural proxy for both topologies.
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
            Bonuses = BuildBonuses(settings.Bonuses),
            WinConditions = BuildAdvancedWinConditions(settings, effectiveVictoryCondition)
        };

        private static double PercentToModifier(int percent) =>
            Math.Round(Math.Clamp(percent, 25, 200) / 100.0, 2, MidpointRounding.AwayFromZero);

        private static List<Bonus>? BuildBonuses(List<OldenEraTemplateEditor.Models.BonusEntry> entries)
        {
            if (entries.Count == 0) return null;
            var result = new List<Bonus>();
            foreach (var entry in entries)
                result.AddRange(entry.ToBonuses());
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Parses newline-separated "sid=guardValue" lines into a ValueOverride list.
        /// Lines that are blank or unparseable are silently skipped.
        /// </summary>
        private static List<ValueOverride>? BuildValueOverrides(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var list = new List<ValueOverride>();
            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;
                var sid = trimmed[..eq].Trim();
                if (string.IsNullOrEmpty(sid)) continue;
                if (!int.TryParse(trimmed[(eq + 1)..].Trim(), out int gv)) continue;
                list.Add(new ValueOverride { Sid = sid, Variant = -1, GuardValue = gv });
            }
            return list.Count > 0 ? list : null;
        }

        /// <summary>
        /// Builds a GlobalBans object from newline-separated item and magic ID strings.
        /// Returns null when both are empty.
        /// </summary>
        private static GlobalBans? BuildGlobalBans(string rawItems, string rawMagics)
        {
            static List<string>? ParseLines(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var list = raw.Split('\n')
                              .Select(l => l.Trim())
                              .Where(l => l.Length > 0)
                              .ToList();
                return list.Count > 0 ? list : null;
            }

            var items  = ParseLines(rawItems);
            var magics = ParseLines(rawMagics);
            if (items == null && magics == null) return null;
            return new GlobalBans { Items = items, Magics = magics };
        }



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
                int firstTournamentDay = Math.Clamp(settings.TournamentRules.FirstTournamentDay, 3, 60);
                int tournamentInterval = Math.Clamp(settings.TournamentRules.Interval, 3, 30);
                int pointsToWin = Math.Clamp(settings.TournamentRules.PointsToWin, 1, 10);
                // Round count is derived from points to win: with N points to win, the maximum number of rounds is 2N-1
                int roundCount = pointsToWin * 2 - 1;
                winConditions.ChampionSelectRule = "StartHero";
                winConditions.Tournament = true;
                winConditions.TournamentSaveArmy = true;

                // Announce each round as early as possible:
                // - Round 0: announced on turn 1 (minimum the game supports), battle on firstTournamentDay.
                // - Round i>0: announced 1 day after the previous battle, battle tournamentInterval days later.
                // tournamentAnnounceDays[i] = absolute turn of the announcement.
                // tournamentDays[i]         = relative offset from announcement to battle.
                var announceDays = new List<int>(roundCount);
                var battleOffsets = new List<int>(roundCount);
                int previousBattleTurn = 0;
                for (int i = 0; i < roundCount; i++)
                {
                    int announceTurn = i == 0 ? 1 : previousBattleTurn + 1; // turn 1 for round 0; 1 day after previous battle for subsequent rounds
                    int offset = i == 0 ? firstTournamentDay - 1 : tournamentInterval - 1;
                    announceDays.Add(announceTurn);
                    battleOffsets.Add(offset);
                    previousBattleTurn = announceTurn + offset;
                }
                winConditions.TournamentAnnounceDays = announceDays;
                winConditions.TournamentDays = battleOffsets;
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

        /// <summary>
        /// Returns the base border guard value for a connection between two zone letters,
        /// scaled by the tuning multiplier.
        /// <list type="bullet">
        ///   <item>Player ↔ Player  → 30 000</item>
        ///   <item>Player ↔ Neutral → based on the neutral zone quality</item>
        ///   <item>Neutral ↔ Neutral → based on the higher-quality neutral zone</item>
        /// </list>
        /// </summary>
        private static int BorderGuardValue(
            string letterA, string letterB,
            ICollection<string> playerLetters,
            IReadOnlyDictionary<string, NeutralZonePlan>? neutralByLetter,
            GenerationTuning tuning)
        {
            bool aIsPlayer = playerLetters.Contains(letterA);
            bool bIsPlayer = playerLetters.Contains(letterB);

            if (aIsPlayer && bIsPlayer)
                return ScaleBorderGuardValue(30000, tuning);

            static int QualityGuardBase(NeutralZoneQuality quality) => quality switch
            {
                NeutralZoneQuality.Low    => 15000,
                NeutralZoneQuality.High   => 25000,
                _                         => 20000, // Medium
            };

            if (neutralByLetter == null)
                return ScaleBorderGuardValue(30000, tuning);

            if (!aIsPlayer && !bIsPlayer)
            {
                // Neutral ↔ Neutral: use the higher quality zone.
                neutralByLetter.TryGetValue(letterA, out var planA);
                neutralByLetter.TryGetValue(letterB, out var planB);
                NeutralZoneQuality qa = planA?.Quality ?? NeutralZoneQuality.Medium;
                NeutralZoneQuality qb = planB?.Quality ?? NeutralZoneQuality.Medium;
                NeutralZoneQuality higher = (int)qa >= (int)qb ? qa : qb;
                return ScaleBorderGuardValue(QualityGuardBase(higher), tuning);
            }

            // Player ↔ Neutral: use the neutral zone quality.
            string neutralLetter = aIsPlayer ? letterB : letterA;
            neutralByLetter.TryGetValue(neutralLetter, out var plan);
            NeutralZoneQuality q = plan?.Quality ?? NeutralZoneQuality.Medium;
            return ScaleBorderGuardValue(QualityGuardBase(q), tuning);
        }

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
                _ => false, // Random and Balanced use position-based adjacency, no fixed separation guarantee
            };
        }

        private static List<string> BuildOrderedLetters(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, bool isRing)
        {
            var neutralLetters = neutralZones.Select(zone => zone.Letter).ToList();

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

            // Distribute extra neutrals only into interior gaps so that the first and last
            // positions in the chain are always player zones (not neutral zones).
            int interiorGapCount = Math.Max(0, gapCount - 2);
            if (interiorGapCount > 0)
            {
                int[] extras = BuildEvenGapCapacities(interiorGapCount, remaining, minimumPerGap: 0);
                for (int i = 1; i < gapCount - 1; i++)
                    capacities[i] += extras[i - 1];
            }
            else
            {
                // Degenerate case (0 or 1 player): fall back to distributing across all gaps.
                int[] extras = BuildEvenGapCapacities(gapCount, remaining, minimumPerGap: 0);
                for (int i = 0; i < gapCount; i++)
                    capacities[i] += extras[i];
            }

            var gaps = AssignNeutralZonesToGaps(neutralZones, capacities, preferInteriorGaps: true);
            var ordered = new List<string>(playerLetters.Count + neutralZones.Count);

            ordered.AddRange(OrderEdgeGap(gaps[0], playerAtEnd: true).Select(zone => zone.Letter));
            for (int i = 0; i < playerLetters.Count; i++)
            {
                ordered.Add(playerLetters[i]);
                var gap = gaps[i + 1];
                bool trailingEdge = i == playerLetters.Count - 1;
                ordered.AddRange((trailingEdge
                    ? OrderEdgeGap(gap, playerAtEnd: false)
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

            // Sort ascending: lowest quality first.
            // Then fill from the outside in, placing the lowest zones at the player-adjacent
            // ends and the highest zones in the centre.  This produces a symmetric gradient:
            //   player → low → medium → high → … → high → medium → low → player
            var sorted = neutralZones
                .OrderBy(NeutralZoneBalanceScore)
                .ThenBy(zone => zone.Letter, StringComparer.Ordinal)
                .ToList();

            var slots = new NeutralZonePlan[count];
            int lo = 0, hi = count - 1;
            for (int i = 0; i < count; i++)
            {
                if (i % 2 == 0)
                    slots[lo++] = sorted[i];
                else
                    slots[hi--] = sorted[i];
            }

            return slots.ToList();
        }

        private static List<NeutralZonePlan> OrderEdgeGap(List<NeutralZonePlan> neutralZones, bool playerAtEnd)
        {
            // Always place low-quality neutrals nearest the player: player → low → medium → high.
            // playerAtEnd=true  (leading gap):  the player follows this list, so low must be last → reverse.
            // playerAtEnd=false (trailing gap): the player precedes this list, so low must be first → ascending.
            var ordered = neutralZones
                .OrderBy(zone => NeutralZoneBalanceScore(zone))
                .ThenBy(zone => zone.Letter, StringComparer.Ordinal)
                .ToList();

            if (playerAtEnd)
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
                MapTopology.Balanced    => BuildVariantBalanced(settings, playerLetters, neutralZones, tuning, holdCityNeutralLetter),
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

            // Order each player's neutrals ascending by quality so the chain reads:
            // player → low → medium → high  (mirrors the ordering used in non-tournament layouts).
            for (int p = 0; p < 2; p++)
                neutralsForPlayer[p] = neutralsForPlayer[p]
                    .OrderBy(NeutralZoneBalanceScore)
                    .ThenBy(n => n.Letter, StringComparer.Ordinal)
                    .ToList();

            int maxSlots = Math.Max(neutralsForPlayer[0].Count, neutralsForPlayer[1].Count);

            var zones = new List<Zone>();
            var connections = new List<Connection>();

            bool useRandom   = settings.Topology == MapTopology.Random;
            bool useHub      = settings.Topology == MapTopology.HubAndSpoke;
            bool useBalanced = settings.Topology == MapTopology.Balanced;
            bool useRing     = settings.Topology == MapTopology.Default;

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

                // Normalise templatePos to [0,1] so we can map each cluster to its own
                // canvas half for the preview.  Cluster 0 → left, cluster 1 → right,
                // with a deliberate centre gap so the clusters never touch in the preview.
                double tMinX = templatePos.Min(p2 => p2.X), tMaxX = templatePos.Max(p2 => p2.X);
                double tMinY = templatePos.Min(p2 => p2.Y), tMaxY = templatePos.Max(p2 => p2.Y);
                double tSpanX = Math.Max(tMaxX - tMinX, 0.001), tSpanY = Math.Max(tMaxY - tMinY, 0.001);

                // Each cluster occupies roughly 43 % of the canvas width; the centre 14 % is gap.
                var previewPositions = new List<(double X, double Y)>[2];
                for (int p = 0; p < 2; p++)
                {
                    double xMin = p == 0 ? 0.03 : 0.57;
                    double xMax = p == 0 ? 0.43 : 0.97;
                    previewPositions[p] = templatePos
                        .Select(pt => (
                            X: xMin + (pt.X - tMinX) / tSpanX * (xMax - xMin),
                            Y: 0.05 + (pt.Y - tMinY) / tSpanY * 0.90))
                        .ToList();
                }

                for (int p = 0; p < 2; p++)
                    BuildTournamentRandomCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections, templateEdges, previewPositions[p]);
            }
            else if (useBalanced)
            {
                // Each player gets their own fully isolated balanced (concentric-ring) cluster.
                // Both clusters share the same structure but are mapped to opposite canvas halves.
                for (int p = 0; p < 2; p++)
                    BuildTournamentBalancedCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections);
            }
            else if (useRing)
            {
                for (int p = 0; p < 2; p++)
                    BuildTournamentRingCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections);
            }
            else
            {
                for (int p = 0; p < 2; p++)
                    BuildTournamentChainCluster(p, playerLetters[p], neutralsForPlayer[p], neutralByLetter, settings, tuning, zones, connections);
            }

            int totalZones = zones.Count;

            if (settings.RandomPortals)
            {
                // Add portals scoped to each cluster individually so they never cross
                // the isolation boundary between the two players.
                for (int p = 0; p < 2; p++)
                {
                    var clusterLetters = new List<string> { playerLetters[p] };
                    clusterLetters.AddRange(neutralsForPlayer[p].Select(n => n.Letter));
                    connections.AddRange(BuildRandomPortalConnections(playerLetters, clusterLetters, tuning, settings.MaxPortalConnections));
                }
            }

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
                    GuardValue = BorderGuardValue(fromLetter, toLetter, [playerLetter], neutralByLetter, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_guard_{fromLetter}_{toLetter}"
                });
            }
        }

        /// <summary>
        /// Builds one player's isolated cluster as a ring.
        /// Neutrals are arranged so the lowest-quality zones sit immediately adjacent to
        /// the player on both sides and the highest-quality zone sits at the midpoint of
        /// the ring (maximum hop distance from the player):
        ///   player → low → … → high → … → low → player
        /// </summary>
        private static void BuildTournamentRingCluster(
            int playerIndex,
            string playerLetter,
            List<NeutralZonePlan> myNeutrals,
            Dictionary<string, NeutralZonePlan> neutralByLetter,
            GeneratorSettings settings,
            GenerationTuning tuning,
            List<Zone> zones,
            List<Connection> connections)
        {
            // Arrange neutrals so the ring reads: player → low → … → high → … → low → player.
            // Sort ascending first, then fill from the player-adjacent positions inward so
            // that the highest-quality zones end up at the midpoint of the ring.
            //
            // Example (5 neutrals, sorted [L0, L1, M0, M1, H]):
            //   slots = [L0, M0, H, M1, L1]
            //   ring  = player → L0 → M0 → H → M1 → L1 → player
            //   hops  =            1     2   3     2     1
            var sortedNeutrals = myNeutrals
                .OrderBy(NeutralZoneBalanceScore)
                .ThenBy(n => n.Letter, StringComparer.Ordinal)
                .ToList();

            int n = sortedNeutrals.Count;
            var orderedNeutrals = new NeutralZonePlan[n];
            int lo = 0, hi = n - 1;
            for (int i = 0; i < n; i++)
            {
                if (i % 2 == 0) orderedNeutrals[lo++] = sortedNeutrals[i];
                else             orderedNeutrals[hi--] = sortedNeutrals[i];
            }

            // Ring order: player, then neutrals in the arranged order, wrapping back.
            var ringLetters = new List<string> { playerLetter };
            ringLetters.AddRange(orderedNeutrals.Select(neutral => neutral.Letter));
            int count = ringLetters.Count;

            // One connection per adjacent pair in the ring (including wrap-around).
            var connNames = new string[count];
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                connNames[i] = $"TRing-{ringLetters[i]}-{ringLetters[next]}";
            }

            // Build zones, giving each zone its two ring connections.
            for (int i = 0; i < count; i++)
            {
                string letter = ringLetters[i];
                int prev = (i - 1 + count) % count;
                var myConns = new[] { connNames[prev], connNames[i] }.Distinct().ToArray();

                if (i == 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIndex + 1}", myConns,
                        settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions,
                        settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
                else
                    zones.Add(BuildNeutralZone(neutralByLetter[letter], myConns,
                        settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning));
            }

            // Build connections (one per ring edge).
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                string fromLetter = ringLetters[i];
                string toLetter   = ringLetters[next];
                string fromZone   = i == 0            ? $"Spawn-{fromLetter}"   : $"Neutral-{fromLetter}";
                string toZone     = next == 0         ? $"Spawn-{toLetter}"     : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connNames[i],
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = BorderGuardValue(fromLetter, toLetter, [playerLetter], neutralByLetter, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_ring_guard_{fromLetter}_{toLetter}"
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

            // Build the hub zone itself via the shared builder so castles, roads,
            // biomes, content pools, and guard settings are always consistent.
            var hubZone = BuildHubZone(spokeConnNames.ToArray(), tuning,
                isHoldCity: false,
                size: settings.ZoneCfg.HubZoneSize,
                castleCount: settings.ZoneCfg.HubZoneCastles,
                generateRoads: settings.GenerateRoads);
            hubZone.Name = hubName;
            zones.Add(hubZone);

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
                    GuardValue = BorderGuardValue(playerLetter, spokeLetter, [playerLetter], neutralByLetter, tuning),
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
            List<(int A, int B)> templateEdges,
            List<(double X, double Y)> previewPositions)
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
                    GuardValue = BorderGuardValue(fromLetter, toLetter, [playerLetter], neutralByLetter, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_rnd_guard_{fromLetter}_{toLetter}"
                });
            }

            for (int i = 0; i < count; i++)
            {
                string letter = clusterLetters[i];
                var myConns = connsByIndex[i].ToArray();
                Zone zone;
                if (i == 0)
                    zone = BuildSpawnZone(letter, $"Player{playerIndex + 1}", myConns,
                        settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions,
                        settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning);
                else
                    zone = BuildNeutralZone(neutralByLetter[letter], myConns,
                        settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning);
                // Stamp preview position so the renderer places each cluster in its own canvas half.
                if (i < previewPositions.Count)
                    zone.GeneratorPosition = previewPositions[i];
                zones.Add(zone);
            }
        }

        /// <summary>
        /// Builds one player's isolated cluster using the balanced concentric-ring layout,
        /// mirroring <see cref="BuildVariantBalanced"/> but scoped to a single player's
        /// exclusive neutrals.  Both clusters are mapped to opposite canvas halves so the
        /// preview shows them side-by-side with a clear gap.
        /// </summary>
        private static void BuildTournamentBalancedCluster(
            int playerIndex,
            string playerLetter,
            List<NeutralZonePlan> myNeutrals,
            Dictionary<string, NeutralZonePlan> neutralByLetter,
            GeneratorSettings settings,
            GenerationTuning tuning,
            List<Zone> zones,
            List<Connection> connections)
        {
            // Build the ordered letter list using the same balanced ring helper so that
            // neutral zones are interleaved by quality tier, identical to the non-tournament
            // balanced layout.
            var clusterLettersList = new List<string> { playerLetter };
            clusterLettersList.AddRange(myNeutrals.Select(n => n.Letter));

            var singlePlayerList = new List<string> { playerLetter };
            var orderedLetters = BuildBalancedRingLetters(singlePlayerList, myNeutrals, minNeutralZonesBetweenPlayers: 0);

            // Generate concentric-ring positions for this cluster, then remap them onto
            // the player's canvas half: cluster 0 → left half, cluster 1 → right half.
            var rawPos = BuildBalancedRandomPositions(orderedLetters, singlePlayerList, neutralByLetter);

            double xMin = playerIndex == 0 ? 0.03 : 0.57;
            double xMax = playerIndex == 0 ? 0.43 : 0.97;
            double rawXMin = rawPos.Count > 0 ? rawPos.Min(p => p.X) : 0.05;
            double rawXMax = rawPos.Count > 0 ? rawPos.Max(p => p.X) : 0.95;
            double rawYMin = rawPos.Count > 0 ? rawPos.Min(p => p.Y) : 0.05;
            double rawYMax = rawPos.Count > 0 ? rawPos.Max(p => p.Y) : 0.95;
            double spanX = Math.Max(rawXMax - rawXMin, 0.001);
            double spanY = Math.Max(rawYMax - rawYMin, 0.001);

            // Use a uniform scale (preserve aspect ratio) so concentric rings
            // stay circular rather than being stretched into a tall ellipse.
            double targetW = xMax - xMin;     // 0.40 per half
            const double targetH = 0.90;
            double scale = Math.Min(targetW / spanX, targetH / spanY);
            double xCentre = (xMin + xMax) / 2.0;
            const double yCentre = 0.5;

            var pos = rawPos
                .Select(pt => (
                    X: xCentre + (pt.X - (rawXMin + rawXMax) / 2.0) * scale,
                    Y: yCentre + (pt.Y - (rawYMin + rawYMax) / 2.0) * scale))
                .ToList();

            // Build connections directly from ring structure — identical logic to BuildVariantBalanced.
            // rawPos is centred at (0.5, 0.5) so angle arithmetic works without adjustment.
            // Same-ring  : each zone connects to its two circle-neighbors (skip if n < 3).
            // Cross-ring : bidirectional nearest-neighbor between adjacent tiers (tie-safe).
            var pairs = new HashSet<(int A, int B)>();

            static double TbalAngDist(double a, double b)
            {
                double d = Math.Abs(a - b) % (2 * Math.PI);
                return d > Math.PI ? 2 * Math.PI - d : d;
            }

            var tbalTierIndices = new Dictionary<int, List<int>>();
            for (int i = 0; i < orderedLetters.Count; i++)
            {
                int tier = ZoneTierRank(orderedLetters[i], singlePlayerList, neutralByLetter);
                if (!tbalTierIndices.TryGetValue(tier, out var lst)) tbalTierIndices[tier] = lst = [];
                lst.Add(i);
            }
            var tbalSortedPresentTiers = tbalTierIndices.Keys.OrderBy(t => t).ToList();

            // Sort each tier's indices by angle (using rawPos, centred at 0.5).
            var tbalTierSorted = new Dictionary<int, List<int>>();
            var tbalTierAngles = new Dictionary<int, double[]>();
            foreach (var (tier, indices) in tbalTierIndices)
            {
                var s = indices.OrderBy(i => Math.Atan2(rawPos[i].Y - 0.5, rawPos[i].X - 0.5)).ToList();
                tbalTierSorted[tier] = s;
                tbalTierAngles[tier] = s.Select(i => Math.Atan2(rawPos[i].Y - 0.5, rawPos[i].X - 0.5)).ToArray();
            }

            // Same-ring: circle-neighbors only; skip degenerate 2-zone rings.
            foreach (var (_, sorted) in tbalTierSorted)
            {
                int n = sorted.Count;
                if (n < 3) continue;
                for (int j = 0; j < n; j++)
                {
                    int a = sorted[j], b = sorted[(j + 1) % n];
                    pairs.Add((Math.Min(a, b), Math.Max(a, b)));
                }
            }

            // Cross-ring: bidirectional nearest-neighbor between adjacent tiers.
            for (int ti = 0; ti + 1 < tbalSortedPresentTiers.Count; ti++)
            {
                int outerTier   = tbalSortedPresentTiers[ti];
                int innerTier   = tbalSortedPresentTiers[ti + 1];
                var outerSorted = tbalTierSorted[outerTier];
                var innerSorted = tbalTierSorted[innerTier];
                var outerAngles = tbalTierAngles[outerTier];
                var innerAngles = tbalTierAngles[innerTier];

                // Each outer zone → its nearest inner zone.
                for (int oi = 0; oi < outerSorted.Count; oi++)
                {
                    int best = 0;
                    double bestD = double.MaxValue;
                    for (int ii = 0; ii < innerSorted.Count; ii++)
                    {
                        double d = TbalAngDist(outerAngles[oi], innerAngles[ii]);
                        if (d < bestD) { bestD = d; best = ii; }
                    }
                    pairs.Add((Math.Min(outerSorted[oi], innerSorted[best]), Math.Max(outerSorted[oi], innerSorted[best])));
                }

                // Each inner zone → its nearest outer zone(s); connect to ALL ties.
                for (int ii = 0; ii < innerSorted.Count; ii++)
                {
                    double bestD = double.MaxValue;
                    for (int oi = 0; oi < outerSorted.Count; oi++)
                    {
                        double d = TbalAngDist(innerAngles[ii], outerAngles[oi]);
                        if (d < bestD) bestD = d;
                    }
                    const double epsilon = 1e-9;
                    for (int oi = 0; oi < outerSorted.Count; oi++)
                    {
                        double d = TbalAngDist(innerAngles[ii], outerAngles[oi]);
                        if (d <= bestD + epsilon)
                            pairs.Add((Math.Min(innerSorted[ii], outerSorted[oi]), Math.Max(innerSorted[ii], outerSorted[oi])));
                    }
                }
            }

            int count = orderedLetters.Count;
            var connsByZone = Enumerable.Range(0, count).ToDictionary(i => i, _ => new List<string>());

            foreach (var (a, b) in pairs)
            {
                string fromLetter = orderedLetters[a];
                string toLetter   = orderedLetters[b];
                string connName   = $"TBal-{fromLetter}-{toLetter}";
                connsByZone[a].Add(connName);
                connsByZone[b].Add(connName);

                string fromZone = fromLetter == playerLetter ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = toLetter   == playerLetter ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connName,
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = BorderGuardValue(fromLetter, toLetter, [playerLetter], neutralByLetter, tuning),
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"tourney_bal_guard_{fromLetter}_{toLetter}"
                });
            }

            for (int i = 0; i < count; i++)
            {
                string letter = orderedLetters[i];
                var myConns = connsByZone[i].ToArray();
                Zone zone;
                if (letter == playerLetter)
                    zone = BuildSpawnZone(letter, $"Player{playerIndex + 1}", myConns,
                        settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions,
                        settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning);
                else
                    zone = BuildNeutralZone(neutralByLetter[letter], myConns,
                        settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning);
                zone.GeneratorPosition = pos[i];
                zone.GeneratorRing = ZoneTierRank(letter, singlePlayerList, neutralByLetter);
                zones.Add(zone);
            }

            // Ensure the cluster is fully connected (same guarantee as the standard balanced variant).
            EnsureFullConnectivity(singlePlayerList, orderedLetters, pos, zones, connections, tuning, neutralByLetter);
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
            connections.AddRange(BuildRingConnections(playerLetters, orderedLetters, tuning, isolate, neutralByLetter));
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
            // Shuffle zones so player/neutral order is fully random.
            var allLetters = playerLetters.Concat(neutralLetters).OrderBy(_ => rng.Next()).ToList();
            var pos = allLetters.Select(_ => (rng.NextDouble() * 0.9 + 0.05, rng.NextDouble() * 0.9 + 0.05)).ToList();

            var pairs = DelaunayEdges(pos);

            int count = allLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

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
                    GuardValue = BorderGuardValue(fromLetter, toLetter, playerLetters, neutralByLetter, tuning),
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
                Zone zone;
                if (playerIdx >= 0)
                    zone = BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns, settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning);
                else
                    zone = BuildNeutralZone(neutralByLetter[letter], myConns, settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning, letter == holdCityNeutralLetter);
                // Stamp the Delaunay position so the preview can reproduce the exact geometry.
                zone.GeneratorPosition = pos[i];
                zones.Add(zone);
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, allLetters, tuning, settings.MaxPortalConnections));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections, tuning);
            EnsureFullConnectivity(playerLetters, allLetters, pos, zones, connections, tuning, neutralByLetter: null);
            return MakeVariant(playerLetters, allLetters[0], count, zones, connections);
        }

        // ── Topology: Balanced (concentric rings) ─────────────────────────────────

        private static Variant BuildVariantBalanced(GeneratorSettings settings, List<string> playerLetters, List<NeutralZonePlan> neutralZones, GenerationTuning tuning, string? holdCityNeutralLetter = null)
        {
            var neutralByLetter = neutralZones.ToDictionary(zone => zone.Letter);
            var allLetters = BuildBalancedRingLetters(playerLetters, neutralZones, minNeutralZonesBetweenPlayers: 0);
            var pos = BuildBalancedRandomPositions(allLetters, playerLetters, neutralByLetter);

            var playerSet = playerLetters.ToHashSet(StringComparer.Ordinal);

            // Build the connection graph explicitly from the ring structure — no Delaunay.
            // Rules:
            //   Same-ring  : each zone connects to its two circle-neighbors (ring).
            //   Cross-ring : each zone on ring A connects to its single nearest zone
            //                on ring B (by angular distance), applied in both directions
            //                and deduplicated. This gives a symmetric, balanced bipartite
            //                ladder between every pair of adjacent rings.
            var pairs = new HashSet<(int, int)>();

            // Group zone indices by tier rank, sorted by angle.
            var tierIndices = new Dictionary<int, List<int>>();
            for (int i = 0; i < allLetters.Count; i++)
            {
                int tier = ZoneTierRank(allLetters[i], playerLetters, neutralByLetter);
                if (!tierIndices.TryGetValue(tier, out var lst)) tierIndices[tier] = lst = [];
                lst.Add(i);
            }
            var sortedPresentTiers = tierIndices.Keys.OrderBy(t => t).ToList();

            // Helper: angular distance on the circle [-π, π] → [0, π].
            static double AngDist(double a, double b)
            {
                double d = Math.Abs(a - b) % (2 * Math.PI);
                return d > Math.PI ? 2 * Math.PI - d : d;
            }

            // Sort each tier's indices by angle once.
            var tierSorted  = new Dictionary<int, List<int>>();
            var tierAngles  = new Dictionary<int, double[]>();
            foreach (var (tier, indices) in tierIndices)
            {
                var s = indices.OrderBy(i => Math.Atan2(pos[i].Y - 0.5, pos[i].X - 0.5)).ToList();
                tierSorted[tier] = s;
                tierAngles[tier] = s.Select(i => Math.Atan2(pos[i].Y - 0.5, pos[i].X - 0.5)).ToArray();
            }

            // Same-ring: connect each zone to its two circle-neighbors.
            // Player ring: skip player↔player — EnsurePlayerZonesConnected handles that.
            // Skip rings with fewer than 3 zones: with n=2 the only "ring" edge is a
            // diameter (180° chord) that tunnels straight through all inner rings, which
            // is geographically wrong.  Those zones are reachable via cross-ring edges.
            foreach (var (tier, sorted) in tierSorted)
            {
                int n = sorted.Count;
                if (n < 3) continue;
                for (int j = 0; j < n; j++)
                {
                    int a = sorted[j], b = sorted[(j + 1) % n];
                    bool bothPlayers = playerSet.Contains(allLetters[a]) && playerSet.Contains(allLetters[b]);
                    if (bothPlayers) continue;
                    pairs.Add((Math.Min(a, b), Math.Max(a, b)));
                }
            }

            // Cross-ring: bidirectional nearest-neighbor between adjacent tiers.
            for (int ti = 0; ti + 1 < sortedPresentTiers.Count; ti++)
            {
                int outerTier = sortedPresentTiers[ti];
                int innerTier = sortedPresentTiers[ti + 1];
                var outerSorted = tierSorted[outerTier];
                var innerSorted = tierSorted[innerTier];
                var outerAngles = tierAngles[outerTier];
                var innerAngles = tierAngles[innerTier];

                // Each outer zone → its nearest inner zone.
                for (int oi = 0; oi < outerSorted.Count; oi++)
                {
                    int best = 0;
                    double bestD = double.MaxValue;
                    for (int ii = 0; ii < innerSorted.Count; ii++)
                    {
                        double d = AngDist(outerAngles[oi], innerAngles[ii]);
                        if (d < bestD) { bestD = d; best = ii; }
                    }
                    int a = outerSorted[oi], b = innerSorted[best];
                    bool bothPlayers = playerSet.Contains(allLetters[a]) && playerSet.Contains(allLetters[b]);
                    if (!bothPlayers) pairs.Add((Math.Min(a, b), Math.Max(a, b)));
                }

                // Each inner zone → its nearest outer zone(s).
                // When multiple outer zones are equally close (e.g. two players at ±90° from
                // a neutral that sits exactly between them), connect to ALL of them so that
                // ties never cause one player to receive zero connections from that ring.
                for (int ii = 0; ii < innerSorted.Count; ii++)
                {
                    double bestD = double.MaxValue;
                    for (int oi = 0; oi < outerSorted.Count; oi++)
                    {
                        double d = AngDist(innerAngles[ii], outerAngles[oi]);
                        if (d < bestD) bestD = d;
                    }
                    const double epsilon = 1e-9;
                    for (int oi = 0; oi < outerSorted.Count; oi++)
                    {
                        double d = AngDist(innerAngles[ii], outerAngles[oi]);
                        if (d > bestD + epsilon) continue;
                        int a = innerSorted[ii], b = outerSorted[oi];
                        bool bothPlayers = playerSet.Contains(allLetters[a]) && playerSet.Contains(allLetters[b]);
                        if (!bothPlayers) pairs.Add((Math.Min(a, b), Math.Max(a, b)));
                    }
                }
            }

            int count = allLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

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
                    GuardValue = BorderGuardValue(fromLetter, toLetter, playerLetters, neutralByLetter, tuning),
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
                Zone zone;
                if (playerIdx >= 0)
                    zone = BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns, settings.ZoneCfg.PlayerZoneCastles, settings.MatchPlayerCastleFactions, settings.ZoneCfg.Advanced.PlayerZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning);
                else
                    zone = BuildNeutralZone(neutralByLetter[letter], myConns, settings.ZoneCfg.Advanced.NeutralZoneSize, settings.SpawnRemoteFootholds, settings.GenerateRoads, tuning, letter == holdCityNeutralLetter);
                // Stamp the position and tier so the preview can reproduce the exact geometry.
                zone.GeneratorPosition = pos[i];
                zone.GeneratorRing = ZoneTierRank(letter, playerLetters, neutralByLetter);
                zones.Add(zone);
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, allLetters, tuning, settings.MaxPortalConnections));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections, tuning);
            EnsureFullConnectivity(playerLetters, allLetters, pos, zones, connections, tuning, neutralByLetter);
            return MakeVariant(playerLetters, allLetters[0], count, zones, connections);
        }

        /// <summary>
        /// Returns the tier rank of a zone used for concentric ring placement:
        ///   0 = player
        ///   1 = low neutral (no castle)
        ///   2 = low neutral city      — own ring between low-plain and medium-plain
        ///   3 = medium neutral (no castle)
        ///   4 = medium neutral city   — own ring between medium-plain and high-plain
        ///   5 = high neutral (no castle)
        ///   6 = high neutral city     — innermost ring
        /// Each city variant has its own ring visually distinct from both the plain zone
        /// of the same quality and the plain zone of the next quality.
        /// </summary>
        private static int ZoneTierRank(
            string letter,
            List<string> playerLetters,
            Dictionary<string, NeutralZonePlan> neutralByLetter)
        {
            if (playerLetters.Contains(letter)) return 0;
            if (!neutralByLetter.TryGetValue(letter, out var plan)) return 1;
            bool isCity = plan.CastleCount > 0;
            return plan.Quality switch
            {
                NeutralZoneQuality.High   => isCity ? 6 : 5,
                NeutralZoneQuality.Medium => isCity ? 4 : 3,
                _                         => isCity ? 2 : 1
            };
        }

        /// <summary>
        /// Returns the quality group used for adjacency filtering and bridge penalty.
        /// Uses a doubled scale so city sub-tiers can be represented without fractions:
        ///   0  = player
        ///   2  = low plain
        ///   3  = low city         (between low-plain and medium-plain)
        ///   4  = medium plain
        ///   5  = medium city      (between medium-plain and high-plain)
        ///   6  = high plain
        ///   7  = high city
        /// Adjacency is allowed when |ga - gb| &lt;= 2 (one full quality step or one city sub-step).
        /// This prevents e.g. low-city (3) connecting directly to high-city (7): gap = 4 > 2.
        /// </summary>
        private static int ZoneQualityGroup(
            string letter,
            List<string> playerLetters,
            Dictionary<string, NeutralZonePlan> neutralByLetter)
        {
            if (playerLetters.Contains(letter)) return 0;
            if (!neutralByLetter.TryGetValue(letter, out var plan)) return 2;
            bool isCity = plan.CastleCount > 0;
            return plan.Quality switch
            {
                NeutralZoneQuality.High   => isCity ? 7 : 6,
                NeutralZoneQuality.Medium => isCity ? 5 : 4,
                _                         => isCity ? 3 : 2
            };
        }

        private static List<(double X, double Y)> BuildBalancedRandomPositions(
            List<string> orderedLetters,
            List<string> playerLetters,
            Dictionary<string, NeutralZonePlan> neutralByLetter)
        {
            int count = orderedLetters.Count;
            if (count == 0) return [];

            // Zones are placed on concentric rings by tier rank (7 distinct rings).
            // City zones sit on their own ring between the plain zone of the same quality
            // and the plain zone of the next quality — visually distinct from both.
            //   Tier 0 – players          : outermost  (radius 0.42)
            //   Tier 1 – low plain        : (radius 0.35)
            //   Tier 2 – low city         : (radius 0.28)
            //   Tier 3 – medium plain     : (radius 0.21)
            //   Tier 4 – medium city      : (radius 0.14)
            //   Tier 5 – high plain       : (radius 0.08)
            //   Tier 6 – high city        : innermost  (radius 0.03)
            static double TierRadius(int tier) => tier switch
            {
                0 => 0.42,
                1 => 0.35,
                2 => 0.28,
                3 => 0.21,
                4 => 0.14,
                5 => 0.08,
                _ => 0.03
            };

            // Group letters by tier so we can space each ring evenly.
            var byTier = new Dictionary<int, List<int>>(); // tier → indices into orderedLetters
            for (int i = 0; i < count; i++)
            {
                int tier = ZoneTierRank(orderedLetters[i], playerLetters, neutralByLetter);
                if (!byTier.TryGetValue(tier, out var lst)) byTier[tier] = lst = [];
                lst.Add(i);
            }

            var positions = new (double X, double Y)[count];
            foreach (var (tier, indices) in byTier)
            {
                double radius = TierRadius(tier);
                int n = indices.Count;
                // Offset each ring by half a step relative to the previous tier so that
                // adjacent-tier zones interleave angularly and form clean Delaunay edges.
                double offset = tier * (n > 0 ? Math.PI / n : 0.0);
                for (int j = 0; j < n; j++)
                {
                    double angle = 2.0 * Math.PI * j / n + offset;
                    positions[indices[j]] = (
                        Math.Clamp(0.5 + Math.Cos(angle) * radius, 0.05, 0.95),
                        Math.Clamp(0.5 + Math.Sin(angle) * radius, 0.05, 0.95));
                }
            }

            return [.. positions];
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
            {
                int honoredSeparation = settings.MinNeutralZonesBetweenPlayers > 0
                    && CanHonorNeutralSeparation(settings, neutralZones.Count)
                        ? settings.MinNeutralZonesBetweenPlayers
                        : 0;
                outerLetters = BuildBalancedRingLetters(playerLetters, neutralZones, honoredSeparation);
            }
            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Hub zone (neutral, configurable castles, high loot).
            var hubConns = outerLetters.Select(l => $"Hub-{l}").ToArray();
            zones.Add(BuildHubZone(hubConns, tuning, hubIsHoldCity, settings.ZoneCfg.HubZoneSize, settings.ZoneCfg.HubZoneCastles, settings.GenerateRoads));

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
                int guardBase = BorderGuardValue(
                    playerLetters.Count > 0 ? playerLetters[0] : letter, letter,
                    playerLetters, neutralByLetter, tuning);
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
                    GuardValue = guardBase,
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
                        GuardValue = guardBase,
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
                    GuardValue = BorderGuardValue(fromLetter, toLetter, playerLetters, neutralByLetter, tuning),
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
            var neutrals = BuildBalancedNeutralRing(neutralZones, playerLetters.Count);

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
                        GuardValue = BorderGuardValue(playerLetters[i], neutralLetter, playerLetters, neutralByLetter, tuning),
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
                        GuardValue = BorderGuardValue(neutrals[i], neutrals[next], playerLetters, neutralByLetter, tuning),
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

        private static Zone BuildHubZone(string[] spokeConns, GenerationTuning tuning, bool isHoldCity = false, double size = 1.0, int castleCount = 0, bool generateRoads = true)
        {
            // When hold city is active, ensure at least one castle exists.
            // If castles are already configured (>0), pick one at random as the hold city castle;
            // otherwise force-generate exactly one.
            int effectiveCastleCount = isHoldCity ? Math.Max(1, castleCount) : castleCount;

            var mainObjects = new List<MainObject>();
            for (int i = 0; i < effectiveCastleCount; i++)
            {
                bool isHoldCastleSlot = isHoldCity && i == 0;
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = isHoldCastleSlot ? 1.0 : 0.5,
                    GuardValue = ScaleNeutralGuardValue(isHoldCastleSlot ? Math.Max(25000, 20000) : 16000, tuning),
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = isHoldCastleSlot ? "ultra_rich_buildings_construction" : "rich_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = isHoldCastleSlot ? "Center" : "Uniform",
                    PlacementArgs = isHoldCastleSlot ? [] : ["true", "0.8", "2"],
                    HoldCityWinCon = isHoldCastleSlot ? true : null
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
            GuardedContentPool = [.. T3GuardedPools],
            UnguardedContentPool = [.. T3UnguardedPools],
            ResourcesContentPool = [.. GeneralResourcesMedium],
            MandatoryContent = [],
            ContentCountLimits = BuildSideContentLimits(),
            GuardedContentValue = ScaleStructureValue(300000 * tuning.ContentScale, tuning),
            GuardedContentValuePerArea = ScaleStructureValue(2400 * Math.Sqrt(tuning.ContentScale), tuning),
            UnguardedContentValue = ScaleStructureValue(50000 * tuning.ContentScale, tuning),
            UnguardedContentValuePerArea = ScaleStructureValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
            ResourcesValue = ScaleResourceValue(80000 * tuning.ContentScale, tuning),
            ResourcesValuePerArea = ScaleResourceValue(600 * Math.Sqrt(tuning.ContentScale), tuning),
            MainObjects = mainObjects,
            ZoneBiome = effectiveCastleCount > 0
                ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                : new BiomeSelector { Type = "MatchZone", Args = [] },
            ContentBiome = effectiveCastleCount > 0
                ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                : new BiomeSelector { Type = "MatchZone", Args = [] },
            MetaObjectsBiome = effectiveCastleCount > 0
                ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                : new BiomeSelector { Type = "MatchZone", Args = [] },
                    CrossroadsPosition = 0,
                    Roads = effectiveCastleCount > 0
                        ? BuildOuterZoneRoads(spokeConns, effectiveCastleCount, includeFoothold: false, generateRoads: generateRoads)
                        : BuildConnectorZoneRoads(spokeConns, generateRoads: generateRoads)
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

        // ── Full-graph connectivity failsafe ────────────────────────────────────────

        /// <summary>
        /// Verifies that every zone is reachable from every other zone via Direct connections
        /// and, if not, adds minimal bridge connections to join isolated components.
        /// Uses Euclidean positions (<paramref name="pos"/>) to pick the shortest cross-component
        /// bridge at each step, keeping added connections as geographically plausible as possible.
        /// </summary>
        private static void EnsureFullConnectivity(
            List<string> playerLetters,
            List<string> allLetters,
            List<(double X, double Y)> pos,
            List<Zone> zones,
            List<Connection> connections,
            GenerationTuning tuning,
            Dictionary<string, NeutralZonePlan>? neutralByLetter = null)
        {
            if (allLetters.Count <= 1) return;

            // Build adjacency from existing Direct connections (Portal connections also traverse).
            var zoneNameToIndex = allLetters
                .Select((l, i) => (l, i))
                .ToDictionary(
                    t => playerLetters.Contains(t.l) ? $"Spawn-{t.l}" : $"Neutral-{t.l}",
                    t => t.i,
                    StringComparer.Ordinal);

            var adj = Enumerable.Range(0, allLetters.Count)
                .ToDictionary(i => i, _ => new HashSet<int>());

            foreach (var conn in connections)
            {
                if (conn.ConnectionType is not ("Direct" or "Portal")) continue;
                if (conn.From == null || conn.To == null) continue;
                if (!zoneNameToIndex.TryGetValue(conn.From, out int a)) continue;
                if (!zoneNameToIndex.TryGetValue(conn.To,   out int b)) continue;
                adj[a].Add(b);
                adj[b].Add(a);
            }

            // BFS to find connected components.
            static List<List<int>> FindComponents(Dictionary<int, HashSet<int>> graph, int nodeCount)
            {
                var visited = new bool[nodeCount];
                var components = new List<List<int>>();
                for (int start = 0; start < nodeCount; start++)
                {
                    if (visited[start]) continue;
                    var component = new List<int>();
                    var queue = new Queue<int>();
                    queue.Enqueue(start);
                    visited[start] = true;
                    while (queue.Count > 0)
                    {
                        int cur = queue.Dequeue();
                        component.Add(cur);
                        foreach (int nb in graph[cur])
                        {
                            if (!visited[nb]) { visited[nb] = true; queue.Enqueue(nb); }
                        }
                    }
                    components.Add(component);
                }
                return components;
            }

            // Returns a penalty score for bridging two zone indices.
            // Lower = preferred.  When balanced placement is not active, all bridges cost 0
            // (pure-distance selection is preserved).
            // Penalty tiers:
            //   0 – same tier or adjacent tier (ideal, always allowed)
            //   N – tier gap of N (skipping intermediate tiers; allowed only if those tiers
            //       are absent, otherwise this candidate will be outscored by a real bridge)
            //   100 – player ↔ player (last resort)
            int BridgePenalty(int idxA, int idxB)
            {
                if (neutralByLetter == null) return 0;
                int ga = ZoneQualityGroup(allLetters[idxA], playerLetters, neutralByLetter);
                int gb = ZoneQualityGroup(allLetters[idxB], playerLetters, neutralByLetter);
                if (ga == 0 && gb == 0) return 100; // player↔player: last resort
                int gap = Math.Abs(ga - gb);
                return gap; // 0 = same group, 1 = adjacent, 2+ = skipping groups
            }

            // Iteratively merge the closest pair of components until the graph is connected.
            var connNameSet = connections
                .Select(c => c.Name)
                .Where(n => n != null)
                .ToHashSet(StringComparer.Ordinal);

            while (true)
            {
                var components = FindComponents(adj, allLetters.Count);
                if (components.Count <= 1) break;

                // Find the best bridge: primary key = tier-skip penalty (lowest first),
                // secondary key = Euclidean distance (shortest first).
                int bestA = -1, bestB = -1;
                int bestPenalty = int.MaxValue;
                double bestDist = double.MaxValue;

                foreach (int a in components[0])
                {
                    foreach (var otherComp in components.Skip(1))
                    {
                        foreach (int b in otherComp)
                        {
                            int penalty = BridgePenalty(a, b);
                            double dx = pos[a].X - pos[b].X;
                            double dy = pos[a].Y - pos[b].Y;
                            double dist = dx * dx + dy * dy;
                            if (penalty < bestPenalty || (penalty == bestPenalty && dist < bestDist))
                            {
                                bestPenalty = penalty;
                                bestDist    = dist;
                                bestA = a; bestB = b;
                            }
                        }
                    }
                }

                if (bestA < 0 || bestB < 0) break; // should never happen

                string letterA = allLetters[bestA];
                string letterB = allLetters[bestB];
                bool aIsPlayer = playerLetters.Contains(letterA);
                bool bIsPlayer = playerLetters.Contains(letterB);
                string zoneA = aIsPlayer ? $"Spawn-{letterA}" : $"Neutral-{letterA}";
                string zoneB = bIsPlayer ? $"Spawn-{letterB}" : $"Neutral-{letterB}";

                string pair = string.Compare(letterA, letterB, StringComparison.Ordinal) < 0
                    ? $"{letterA}-{letterB}" : $"{letterB}-{letterA}";
                string bridgeName = $"Bridge-{pair}";

                if (!connNameSet.Contains(bridgeName))
                {
                    connections.Add(new Connection
                    {
                        Name = bridgeName,
                        From = zoneA,
                        To = zoneB,
                        ConnectionType = "Direct",
                        GuardZone = zoneA,
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = BorderGuardValue(letterA, letterB, playerLetters, neutralByLetter, tuning),
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"bridge_guard_{pair}"
                    });
                    connNameSet.Add(bridgeName);

                    // Add roads to both zone objects so roads render correctly.
                    foreach (var (zoneName, isPlayer) in new[] { (zoneA, aIsPlayer), (zoneB, bIsPlayer) })
                    {
                        var zone = zones.FirstOrDefault(z => z.Name == zoneName);
                        if (zone == null) continue;
                        zone.Roads ??= [];
                        var endpoint = isPlayer
                            ? MainObjectEndpoint("0")
                            : (zone.MainObjects?.Count > 0 ? MainObjectEndpoint("0") : ConnectionEndpoint(bridgeName));
                        // For zones with a main object, road from object to connection;
                        // for connector zones, road from the bridge connection to itself (single-conn pattern).
                        if (isPlayer || zone.MainObjects?.Count > 0)
                            zone.Roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(bridgeName)));
                        else
                        {
                            // Connector-style: link from first existing connection endpoint to this one.
                            var existingConn = zone.Roads
                                .Select(r => r.From?.Type == "Connection" ? r.From.Args?[0] : null)
                                .FirstOrDefault(n => n != null)
                                ?? zone.Roads.Select(r => r.To?.Type == "Connection" ? r.To.Args?[0] : null)
                                             .FirstOrDefault(n => n != null);
                            if (existingConn != null)
                                zone.Roads.Add(PlainRoad(ConnectionEndpoint(existingConn), ConnectionEndpoint(bridgeName)));
                            else
                                zone.Roads.Add(PlainRoad(ConnectionEndpoint(bridgeName), ConnectionEndpoint(bridgeName)));
                        }
                    }
                }

                // Update adjacency so BFS sees the new edge.
                adj[bestA].Add(bestB);
                adj[bestB].Add(bestA);
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
                    GuardChance = 1,
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
                    GuardChance = 1,
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
                GuardCutoffValue = 1500,
                GuardRandomization = tuning.GuardRandomization,
                GuardMultiplier = ScaleGuardMultiplier(1.0, tuning),
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = [.. T2GuardedPools],
                UnguardedContentPool = [.. T2UnguardedPools],
                ResourcesContentPool = [.. GeneralResourcesPoor],
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
                Roads = castleCount > 0
                    ? BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds, generateRoads)
                    : BuildConnectorZoneRoads(ringConns, generateRoads)
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
                    GuardChance = isHoldCity ? 1.0 : 1.0,
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
                    GuardChance = 1.0,
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
                GuardedContentPool = [.. profile.GuardedContentPool],
                UnguardedContentPool = [.. profile.UnguardedContentPool],
                ResourcesContentPool = [.. profile.ResourcesContentPool],
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
                Roads = castleCount > 0
                    ? BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds, generateRoads)
                    : BuildConnectorZoneRoads(ringConns, generateRoads)
            };
        }

        // Generic tiered pool sets
        // t2 = low-tier sides/spawns, t3 = medium-tier sides/treasures, t4/t5 = high-tier treasures/center
        private static readonly string[] T2GuardedPools =
        [
            "classic_template_pool_random_t2_item",
            "classic_template_pool_random_t2_pandora",
            "classic_template_pool_random_t2_hire",
            "classic_template_pool_random_t2_unit_bank",
            "classic_template_pool_random_t2_res_bank",
            "classic_template_pool_random_t2_stat",
            "classic_template_pool_random_t2_magic",
        ];
        private static readonly string[] T2UnguardedPools =
        [
            "classic_template_pool_random_unguarded_t2_item",
            "classic_template_pool_random_unguarded_t2_pandora",
            "classic_template_pool_random_unguarded_t2_hire",
            "classic_template_pool_random_unguarded_t2_unit_bank",
            "classic_template_pool_random_unguarded_t2_res_bank",
            "classic_template_pool_random_unguarded_t2_stat",
            "classic_template_pool_random_unguarded_t2_magic",
        ];
        private static readonly string[] T3GuardedPools =
        [
            "classic_template_pool_random_t3_item",
            "classic_template_pool_random_t3_pandora",
            "classic_template_pool_random_t3_hire",
            "classic_template_pool_random_t3_unit_bank",
            "classic_template_pool_random_t3_res_bank",
            "classic_template_pool_random_t3_stat",
            "classic_template_pool_random_t3_magic",
        ];
        private static readonly string[] T3UnguardedPools =
        [
            "classic_template_pool_random_unguarded_t3_item",
            "classic_template_pool_random_unguarded_t3_pandora",
            "classic_template_pool_random_unguarded_t3_hire",
            "classic_template_pool_random_unguarded_t3_unit_bank",
            "classic_template_pool_random_unguarded_t3_res_bank",
            "classic_template_pool_random_unguarded_t3_stat",
            "classic_template_pool_random_unguarded_t3_magic",
        ];
        private static readonly string[] T4GuardedPools =
        [
            "classic_template_pool_random_t4_item",
            "classic_template_pool_random_t4_pandora",
            "classic_template_pool_random_t4_hire",
            "classic_template_pool_random_t4_unit_bank",
            "classic_template_pool_random_t4_res_bank",
            "classic_template_pool_random_t4_stat",
            "classic_template_pool_random_t4_magic",
        ];
        private static readonly string[] T4UnguardedPools =
        [
            "classic_template_pool_random_unguarded_t4_item",
            "classic_template_pool_random_unguarded_t4_pandora",
            "classic_template_pool_random_unguarded_t4_hire",
            "classic_template_pool_random_unguarded_t4_unit_bank",
            "classic_template_pool_random_unguarded_t4_res_bank",
            "classic_template_pool_random_unguarded_t4_stat",
            "classic_template_pool_random_unguarded_t4_magic",
        ];
        private static readonly string[] T5GuardedPools =
        [
            "classic_template_pool_random_t5_item",
            "classic_template_pool_random_t5_pandora",
            "classic_template_pool_random_t5_hire",
            "classic_template_pool_random_t5_unit_bank",
            "classic_template_pool_random_t5_res_bank",
            "classic_template_pool_random_t5_stat",
            "classic_template_pool_random_t5_magic",
        ];
        private static readonly string[] T5UnguardedPools =
        [
            "classic_template_pool_random_unguarded_t5_item",
            "classic_template_pool_random_unguarded_t5_pandora",
            "classic_template_pool_random_unguarded_t5_hire",
            "classic_template_pool_random_unguarded_t5_unit_bank",
            "classic_template_pool_random_unguarded_t5_res_bank",
            "classic_template_pool_random_unguarded_t5_stat",
            "classic_template_pool_random_unguarded_t5_magic",
        ];
        private static readonly string[] GeneralResourcesPoor   = ["content_pool_general_resources_start_zone_poor"];
        private static readonly string[] GeneralResourcesMedium = ["content_pool_general_resources_start_zone_medium"];
        private static readonly string[] GeneralResourcesRich   = ["content_pool_general_resources_start_zone_rich"];

        private static NeutralZoneProfile GetNeutralZoneProfile(NeutralZoneQuality quality) => quality switch
        {
            // Low — t2 pools, zone_layout_sides, guard mult ~1.1, poor resources
            NeutralZoneQuality.Low => new NeutralZoneProfile(
                SideLayoutName,
                1.1,
                T2GuardedPools,
                T2UnguardedPools,
                GeneralResourcesPoor,
                120000,
                1000,
                25000,
                200,
                30000,
                240,
                4000,
                2000,
                "poor_buildings_construction",
                "poor_buildings_construction"),
            // High — t4+t5 pools mixed, zone_layout_treasures, guard mult ~1.8, rich resources
            NeutralZoneQuality.High => new NeutralZoneProfile(
                TreasureLayoutName,
                1.8,
                [.. T4GuardedPools, .. T5GuardedPools],
                [.. T4UnguardedPools, .. T5UnguardedPools],
                GeneralResourcesRich,
                480000,
                3000,
                80000,
                620,
                90000,
                580,
                16000,
                8000,
                "rich_buildings_construction",
                "rich_buildings_construction"),
            // Medium — t3 pools, zone_layout_sides or treasures, guard mult ~1.4, medium resources
            _ => new NeutralZoneProfile(
                TreasureLayoutName,
                1.4,
                T3GuardedPools,
                T3UnguardedPools,
                GeneralResourcesMedium,
                240000,
                2000,
                38000,
                300,
                55000,
                420,
                8000,
                4000,
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
            List<string> playerLetters, List<string> orderedLetters, GenerationTuning tuning, bool isolatePlayers = false,
            IReadOnlyDictionary<string, NeutralZonePlan>? neutralByLetter = null)
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
                    GuardValue = BorderGuardValue(fromLetter, toLetter, playerLetters, neutralByLetter, tuning),
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
                    PortalPlacementRulesFrom = [RulePresets.CrossroadsDistance(DistancePresets.Near, weight:2)],
                    PortalPlacementRulesTo   = [RulePresets.CrossroadsDistance(DistancePresets.Near, weight:2)],
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
                groups.Add(BuildSpawnMandatoryContent(letter, settings));

            foreach (var neutralZone in neutralZones)
                groups.Add(BuildNeutralMandatoryContent(neutralZone.Letter, neutralZone.CastleCount, settings.SpawnRemoteFootholds, neutralZone.Quality));

            return groups;
        }

        private static MandatoryContentGroup BuildSpawnMandatoryContent(string letter, GeneratorSettings settings)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_side_{letter}",
                Content = ZoneContentManager.BuildPlayerZoneMandatoryContent(settings)
            };
        }

        private static MandatoryContentGroup BuildNeutralMandatoryContent(string letter, int castleCount, bool spawnFootholds, NeutralZoneQuality quality)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_neutral_{letter}",
                Content = quality switch
                {
                    NeutralZoneQuality.Low    => ZoneContentManager.BuildLowNeutralMandatoryContent(castleCount, spawnFootholds),
                    NeutralZoneQuality.High   => ZoneContentManager.BuildHighNeutralMandatoryContent(castleCount, spawnFootholds),
                    _                         => ZoneContentManager.BuildMediumNeutralMandatoryContent(castleCount, spawnFootholds),
                }
            };
        }
    }
}
