using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Best-effort importer that maps exported .rmg.json templates back into editable UI settings.
    /// </summary>
    public static class TemplateImportService
    {
        public static SettingsFile ImportToSettings(RmgTemplate template)
        {
            var settings = new SettingsFile
            {
                TemplateName = string.IsNullOrWhiteSpace(template.Name) ? "Custom Template" : template.Name,
                MapSize = Math.Clamp(template.SizeX > 0 ? template.SizeX : 160, 72, 512),
                VictoryCondition = NormalizeVictoryCondition(template.DisplayWinCondition),
                BannedItems = string.Join("\n", template.GlobalBans?.Items ?? []),
                BannedMagics = string.Join("\n", template.GlobalBans?.Magics ?? []),
                ValueOverridesText = BuildValueOverrideText(template.ValueOverrides),
            };

            var variant = template.Variants?.FirstOrDefault();
            var zones = variant?.Zones ?? [];
            var connections = variant?.Connections ?? [];

            var spawnZones = zones.Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal)).ToList();
            var neutralZones = zones.Where(z => z.Name.StartsWith("Neutral-", StringComparison.Ordinal)).ToList();
            var playerLetters = spawnZones
                .Select(z => TryGetLetterFromZoneName(z.Name))
                .Where(l => l != null)
                .Select(l => l!)
                .ToList();

            bool isTournament = template.GameRules?.WinConditions?.Tournament == true
                                || settings.VictoryCondition == "win_condition_6";

            settings.PlayerCount = Math.Clamp(spawnZones.Count > 0 ? spawnZones.Count : settings.PlayerCount, 2, 8);
            settings.Topology = InferTopology(zones, connections, neutralZones, playerLetters, isTournament);
            settings.RandomPortals = connections.Any(c => string.Equals(c.ConnectionType, "Portal", StringComparison.OrdinalIgnoreCase));
            int portalCount = connections.Count(c => string.Equals(c.ConnectionType, "Portal", StringComparison.OrdinalIgnoreCase));
            settings.MaxPortalConnections = Math.Clamp(portalCount > 0 ? portalCount : 32, 1, 32);

            string description = template.Description ?? string.Empty;
            settings.NoDirectPlayerConn = description.Contains("isolated player starts", StringComparison.OrdinalIgnoreCase);
            settings.GenerateRoads = !description.Contains("roads disabled", StringComparison.OrdinalIgnoreCase)
                                     && zones.Any(z => (z.Roads?.Count ?? 0) > 0);
            settings.SpawnRemoteFootholds = !description.Contains("no remote footholds", StringComparison.OrdinalIgnoreCase);

            settings.PlayerZoneCastles = Math.Clamp(InferPlayerZoneCastles(spawnZones), 0, 4);
            settings.NeutralZoneCastles = 1;
            settings.HubZoneCastles = Math.Clamp(InferHubZone(zones)?.MainObjects?.Count ?? 0, 0, 4);
            settings.HubZoneSize = ClampDouble(InferHubZone(zones)?.Size, 0.25, 3.0, 1.0);

            settings.PlayerZoneSize = ClampDouble(Median(spawnZones.Select(z => z.Size ?? 1.0)), 0.1, 2.0, 1.0);
            settings.NeutralZoneSize = ClampDouble(Median(neutralZones.Select(z => z.Size ?? 1.0)), 0.1, 2.0, 1.0);

            double guardRandomization = Median(spawnZones.Select(z => z.GuardRandomization)
                .Concat(neutralZones.Select(z => z.GuardRandomization))
                .Where(v => v.HasValue)
                .Select(v => v!.Value));
            settings.GuardRandomization = ClampDouble(guardRandomization, 0.0, 0.5, 0.05);

            settings.MatchPlayerCastleFactions = InferMatchPlayerCastleFactions(spawnZones);

            var gameRules = template.GameRules;
            var win = gameRules?.WinConditions;
            int heroInc = Math.Clamp(gameRules?.HeroCountIncrement ?? 1, 1, 12);
            int exportedHeroMin = gameRules?.HeroCountMin ?? 3;
            int heroMax = Math.Clamp(gameRules?.HeroCountMax ?? 8, 1, 12);
            settings.HeroCountIncrement = heroInc;
            settings.HeroCountMax = heroMax;
            settings.HeroCountMin = Math.Clamp(exportedHeroMin + heroInc, 1, heroMax);

            settings.FactionLawsExpPercent = ModifierToPercent(gameRules?.FactionLawsExpModifier, 100);
            settings.AstrologyExpPercent = ModifierToPercent(gameRules?.AstrologyExpModifier, 100);

            settings.LostStartCity = win?.LostStartCity == true || settings.VictoryCondition == "win_condition_3";
            settings.LostStartCityDay = Math.Clamp(win?.LostStartCityDay ?? 3, 1, 30);
            settings.LostStartHero = win?.LostStartHero == true;
            settings.CityHold = win?.CityHold == true || settings.VictoryCondition == "win_condition_5";
            settings.CityHoldDays = Math.Clamp(win?.CityHoldDays ?? 6, 1, 30);

            settings.GladiatorArena = win?.GladiatorArena == true || settings.VictoryCondition == "win_condition_4";
            settings.GladiatorArenaDaysDelayStart = Math.Clamp(win?.GladiatorArenaDaysDelayStart ?? 30, 1, 60);
            settings.GladiatorArenaCountDay = Math.Clamp(win?.GladiatorArenaCountDay ?? 3, 1, 30);

            settings.Tournament = isTournament;
            settings.TournamentPointsToWin = Math.Clamp(win?.TournamentPointsToWin ?? 2, 1, 10);
            settings.TournamentSaveArmy = win?.TournamentSaveArmy ?? true;
            InferTournamentTiming(win, out int firstDay, out int interval);
            settings.TournamentFirstTournamentDay = firstDay;
            settings.TournamentInterval = interval;

            settings.BonusesJson = string.Join("\n", ParseBonusEntries(gameRules?.Bonuses).Select(b => b.ToString()));

            var playerMandatory = template.MandatoryContent?
                .FirstOrDefault(g => g.Name.StartsWith("mandatory_content_side_", StringComparison.OrdinalIgnoreCase));
            settings.PlayerZoneContentRows = BuildZoneContentRows(playerMandatory?.Content);

            InferNeutralZoneSettings(settings, neutralZones, settings.CityHold);

            settings.ResourceDensityPercent = InferStructureOrResourcePercent(
                spawnZones.FirstOrDefault()?.ResourcesValue,
                80000.0 * ComputeContentScale(settings.MapSize, Math.Max(1, zones.Count)),
                multiplierToPercent: 200.0,
                fallback: 100);

            settings.StructureDensityPercent = InferStructureOrResourcePercent(
                spawnZones.FirstOrDefault()?.GuardedContentValue,
                200000.0 * ComputeContentScale(settings.MapSize, Math.Max(1, zones.Count)),
                multiplierToPercent: 100.0,
                fallback: 100);

            settings.NeutralStackStrengthPercent = InferNeutralStackStrengthPercent(spawnZones, neutralZones);
            settings.BorderGuardStrengthPercent = InferBorderGuardPercent(connections, playerLetters, neutralZones);

            settings.ExperimentalMapSizes = KnownValues.IsExperimentalMapSize(settings.MapSize);

            return settings;
        }

        /// <summary>
        /// Converts imported mandatory-content items into the editor's content-row save model.
        /// Mirrors the legacy load path: items that are identical (same SID, group/mine/guarded
        /// flags, and placement rules) collapse into one row with a Count. The deprecated row
        /// fields (IsGuarded / NearCastle / RoadDistance) are populated so the settings loader's
        /// legacy-restore path rebuilds the same content rules on the new ZoneContent model.
        /// </summary>
        private static List<ZoneContentRowSave>? BuildZoneContentRows(List<ContentItem>? contentItems)
        {
            if (contentItems is null || contentItems.Count == 0)
                return null;

            var counts = new Dictionary<ContentRowKey, int>();
            var order  = new List<ContentRowKey>();

            foreach (var item in contentItems)
            {
                /* Grouped entries carry their include-list id as the SID, matching the load path. */
                bool isGroup = item.IncludeLists is { Count: > 0 };
                string? sid  = isGroup ? item.IncludeLists![0] : item.Sid;
                if (string.IsNullOrWhiteSpace(sid))
                    continue;

                var key = new ContentRowKey(
                    sid,
                    isGroup,
                    item.IsMine == true,
                    item.IsGuarded == true,
                    HasNearCastleRule(item.Rules),
                    GetRoadDistanceLabel(item.Rules));

                if (counts.TryGetValue(key, out int count))
                {
                    counts[key] = count + 1;
                }
                else
                {
                    counts[key] = 1;
                    order.Add(key);
                }
            }

            if (order.Count == 0)
                return null;

            return order.Select(key => new ZoneContentRowSave
            {
                Sid          = key.Sid,
                Count        = counts[key],
                IsGroup      = key.IsGroup,
                IsMine       = key.IsMine,
                IsGuarded    = key.IsGuarded,
                NearCastle   = key.NearCastle,
                RoadDistance = key.RoadDistance,
            }).ToList();
        }

        private readonly record struct ContentRowKey(
            string Sid,
            bool IsGroup,
            bool IsMine,
            bool IsGuarded,
            bool NearCastle,
            string RoadDistance);

        private static bool HasNearCastleRule(List<ContentPlacementRule>? rules)
            => rules?.Any(rule =>
                string.Equals(rule.Type, "MainObject", StringComparison.OrdinalIgnoreCase) &&
                rule.Args?.Any(arg => arg == "0") == true) == true;

        private static string GetRoadDistanceLabel(List<ContentPlacementRule>? rules)
        {
            ContentPlacementRule? roadRule = rules?.FirstOrDefault(rule =>
                string.Equals(rule.Type, "Road", StringComparison.OrdinalIgnoreCase));

            if (roadRule?.TargetMin is null || roadRule.TargetMax is null)
                return "Any";

            double min = roadRule.TargetMin.Value;
            double max = roadRule.TargetMax.Value;

            if (IsDistance(min, max, DistancePresets.NextTo)) return "Next To";
            if (IsDistance(min, max, DistancePresets.Near)) return "Near";
            if (IsDistance(min, max, DistancePresets.Far)) return "Far";
            if (IsDistance(min, max, DistancePresets.VeryFar)) return "Very Far";
            if (IsDistance(min, max, DistancePresets.Medium)) return "Medium";

            return "Medium";
        }

        private static bool IsDistance(double min, double max, DistanceVariation preset)
            => Math.Abs(min - preset.Min) < 0.0001 && Math.Abs(max - preset.Max) < 0.0001;

        private static string NormalizeVictoryCondition(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "win_condition_1";

            return KnownValues.VictoryConditionIds.Contains(value, StringComparer.Ordinal)
                ? value
                : "win_condition_1";
        }

        private static string BuildValueOverrideText(List<ValueOverride>? overrides)
        {
            if (overrides == null || overrides.Count == 0)
                return string.Empty;

            return string.Join("\n", overrides
                .Where(v => !string.IsNullOrWhiteSpace(v.Sid) && v.GuardValue.HasValue)
                .Select(v => $"{v.Sid}={v.GuardValue!.Value}"));
        }

        private static Zone? InferHubZone(List<Zone> zones)
            => zones.FirstOrDefault(z => string.Equals(z.Name, "Hub", StringComparison.Ordinal)
                                       || z.Name.StartsWith("Hub-", StringComparison.Ordinal));

        private static int InferPlayerZoneCastles(List<Zone> spawnZones)
        {
            var counts = spawnZones
                .Select(z => z.MainObjects?.Count ?? 0)
                .Where(c => c >= 0)
                .ToList();

            return counts.Count == 0 ? 1 : Mode(counts);
        }

        private static bool InferMatchPlayerCastleFactions(List<Zone> spawnZones)
        {
            foreach (var zone in spawnZones)
            {
                var extras = (zone.MainObjects ?? []).Skip(1);
                foreach (var obj in extras)
                {
                    if (!string.Equals(obj.Type, "City", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(obj.Faction?.Type, "Match", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private static MapTopology InferTopology(
            List<Zone> zones,
            List<Connection> connections,
            List<Zone> neutralZones,
            List<string> playerLetters,
            bool isTournament)
        {
            var names = connections.Select(c => c.Name ?? string.Empty).ToList();

            if (names.Any(n => n.StartsWith("Web-", StringComparison.Ordinal)
                             || n.StartsWith("NRing-", StringComparison.Ordinal)))
                return MapTopology.SharedWeb;

            if (zones.Any(z => string.Equals(z.Name, "Hub", StringComparison.Ordinal)
                            || z.Name.StartsWith("Hub-", StringComparison.Ordinal)))
                return MapTopology.HubAndSpoke;

            if (isTournament)
            {
                if (names.Any(n => n.StartsWith("THubSpoke-", StringComparison.Ordinal))) return MapTopology.HubAndSpoke;
                if (names.Any(n => n.StartsWith("TRing-", StringComparison.Ordinal))) return MapTopology.Default;
                if (names.Any(n => n.StartsWith("TBal-", StringComparison.Ordinal))) return MapTopology.Balanced;
                if (names.Any(n => n.StartsWith("TourneyRnd-", StringComparison.Ordinal))) return MapTopology.Random;
                if (names.Any(n => n.StartsWith("Tourney-", StringComparison.Ordinal))) return MapTopology.Chain;
            }

            if (names.Any(n => n.StartsWith("Chain-", StringComparison.Ordinal))) return MapTopology.Chain;
            if (names.Any(n => n.StartsWith("Ring-", StringComparison.Ordinal))) return MapTopology.Default;

            if (names.Any(n => n.StartsWith("Rnd-", StringComparison.Ordinal)))
            {
                return LooksLikeBalancedGraph(connections, neutralZones, playerLetters)
                    ? MapTopology.Balanced
                    : MapTopology.Random;
            }

            return MapTopology.Balanced;
        }

        private static bool LooksLikeBalancedGraph(List<Connection> connections, List<Zone> neutralZones, List<string> playerLetters)
        {
            var qualityByLetter = neutralZones
                .Select(n => (letter: TryGetLetterFromZoneName(n.Name), quality: InferNeutralQuality(n)))
                .Where(x => x.letter != null)
                .ToDictionary(x => x.letter!, x => x.quality, StringComparer.Ordinal);

            bool hasChecked = false;
            foreach (var c in connections)
            {
                if (!string.Equals(c.ConnectionType, "Direct", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? fromLetter = TryGetLetterFromZoneName(c.From);
                string? toLetter = TryGetLetterFromZoneName(c.To);
                if (fromLetter == null || toLetter == null) continue;

                int ga = QualityGroup(fromLetter, playerLetters, qualityByLetter);
                int gb = QualityGroup(toLetter, playerLetters, qualityByLetter);
                hasChecked = true;

                if (Math.Abs(ga - gb) > 2)
                    return false;
            }

            return hasChecked;
        }

        private static int QualityGroup(string letter, List<string> players, Dictionary<string, NeutralZoneQuality> neutralQuality)
        {
            if (players.Contains(letter, StringComparer.Ordinal)) return 0;
            if (!neutralQuality.TryGetValue(letter, out var q)) return 4;
            return q switch
            {
                NeutralZoneQuality.Low => 2,
                NeutralZoneQuality.High => 6,
                _ => 4
            };
        }

        private static void InferNeutralZoneSettings(SettingsFile settings, List<Zone> neutralZones, bool cityHold)
        {
            var infos = neutralZones.Select(zone =>
            {
                int castles = zone.MainObjects?.Count(m => string.Equals(m.Type, "City", StringComparison.OrdinalIgnoreCase)) ?? 0;
                bool hasCastle = castles > 0;
                return (quality: InferNeutralQuality(zone), hasCastle, castles);
            }).ToList();

            bool hasAny = infos.Count > 0;
            bool nonMedium = infos.Any(i => i.quality != NeutralZoneQuality.Medium);
            bool mixedCastlePresence = infos.Select(i => i.hasCastle).Distinct().Count() > 1;
            bool customSizes = Math.Abs(settings.PlayerZoneSize - 1.0) > 0.0001 || Math.Abs(settings.NeutralZoneSize - 1.0) > 0.0001;
            bool customGuardRandomization = Math.Abs(settings.GuardRandomization - 0.05) > 0.0001;

            settings.AdvancedMode = hasAny && (nonMedium || mixedCastlePresence || customSizes || customGuardRandomization);

            if (!hasAny)
            {
                settings.NeutralZoneCount = 0;
                settings.NeutralZoneCastles = 0;
                return;
            }

            int lowNoCastle = infos.Count(i => i.quality == NeutralZoneQuality.Low && !i.hasCastle);
            int lowCastle = infos.Count(i => i.quality == NeutralZoneQuality.Low && i.hasCastle);
            int medNoCastle = infos.Count(i => i.quality == NeutralZoneQuality.Medium && !i.hasCastle);
            int medCastle = infos.Count(i => i.quality == NeutralZoneQuality.Medium && i.hasCastle);
            int highNoCastle = infos.Count(i => i.quality == NeutralZoneQuality.High && !i.hasCastle);
            int highCastle = infos.Count(i => i.quality == NeutralZoneQuality.High && i.hasCastle);

            settings.NeutralLowNoCastleCount = lowNoCastle;
            settings.NeutralLowCastleCount = lowCastle;
            settings.NeutralMediumNoCastleCount = medNoCastle;
            settings.NeutralMediumCastleCount = medCastle;
            settings.NeutralHighNoCastleCount = highNoCastle;
            settings.NeutralHighCastleCount = highCastle;

            var castleCounts = infos.Where(i => i.hasCastle).Select(i => i.castles).ToList();
            settings.NeutralZoneCastles = castleCounts.Count > 0
                ? Math.Clamp(Mode(castleCounts), 0, 4)
                : 0;

            if (!settings.AdvancedMode)
            {
                settings.NeutralZoneCount = infos.Count;

                var allCastleCounts = infos.Select(i => i.castles).ToList();
                if (cityHold
                    && allCastleCounts.Count(c => c == 1) == 1
                    && allCastleCounts.Count(c => c == 0) == allCastleCounts.Count - 1)
                {
                    settings.NeutralZoneCastles = 0;
                }
                else
                {
                    settings.NeutralZoneCastles = Math.Clamp(Mode(allCastleCounts), 0, 4);
                }

                // Keep advanced buckets aligned with simple mode for stability.
                settings.NeutralLowNoCastleCount = 0;
                settings.NeutralLowCastleCount = 0;
                settings.NeutralMediumNoCastleCount = settings.NeutralZoneCastles > 0 ? 0 : settings.NeutralZoneCount;
                settings.NeutralMediumCastleCount = settings.NeutralZoneCastles > 0 ? settings.NeutralZoneCount : 0;
                settings.NeutralHighNoCastleCount = 0;
                settings.NeutralHighCastleCount = 0;
            }
        }

        private static NeutralZoneQuality InferNeutralQuality(Zone zone)
        {
            string joined = string.Join(" ", zone.GuardedContentPool ?? []);
            if (joined.Contains("_t5_", StringComparison.OrdinalIgnoreCase)
                || joined.Contains("_t4_", StringComparison.OrdinalIgnoreCase))
                return NeutralZoneQuality.High;
            if (joined.Contains("_t2_", StringComparison.OrdinalIgnoreCase))
                return NeutralZoneQuality.Low;
            return NeutralZoneQuality.Medium;
        }

        private static int InferStructureOrResourcePercent(int? observed, double baseValue, double multiplierToPercent, int fallback)
        {
            if (!observed.HasValue || observed.Value <= 0 || baseValue <= 0)
                return fallback;

            double pct = observed.Value / baseValue * multiplierToPercent;
            if (double.IsNaN(pct) || double.IsInfinity(pct))
                return fallback;

            return Math.Clamp((int)Math.Round(pct, MidpointRounding.AwayFromZero), 25, 200);
        }

        private static int InferNeutralStackStrengthPercent(List<Zone> spawnZones, List<Zone> neutralZones)
        {
            // Spawn uses base 1.0; neutral uses 1.1/1.4/1.8. Spawn is the cleanest inversion when available.
            double? fromSpawn = spawnZones
                .Select(z => z.GuardMultiplier)
                .FirstOrDefault(v => v.HasValue);
            if (fromSpawn.HasValue && fromSpawn.Value > 0)
                return Math.Clamp((int)Math.Round(fromSpawn.Value * 100.0, MidpointRounding.AwayFromZero), 25, 200);

            foreach (var neutral in neutralZones)
            {
                if (!neutral.GuardMultiplier.HasValue || neutral.GuardMultiplier.Value <= 0) continue;
                double baseMult = InferNeutralQuality(neutral) switch
                {
                    NeutralZoneQuality.Low => 1.1,
                    NeutralZoneQuality.High => 1.8,
                    _ => 1.4,
                };
                double pct = neutral.GuardMultiplier.Value / baseMult * 100.0;
                if (double.IsNaN(pct) || double.IsInfinity(pct)) continue;
                return Math.Clamp((int)Math.Round(pct, MidpointRounding.AwayFromZero), 25, 200);
            }

            return 100;
        }

        private static int InferBorderGuardPercent(List<Connection> connections, List<string> playerLetters, List<Zone> neutralZones)
        {
            var qualityByLetter = neutralZones
                .Select(n => (letter: TryGetLetterFromZoneName(n.Name), quality: InferNeutralQuality(n)))
                .Where(x => x.letter != null)
                .ToDictionary(x => x.letter!, x => x.quality, StringComparer.Ordinal);

            var ratios = new List<double>();
            foreach (var connection in connections)
            {
                if (!string.Equals(connection.ConnectionType, "Direct", StringComparison.OrdinalIgnoreCase)) continue;
                if (!connection.GuardValue.HasValue || connection.GuardValue.Value <= 0) continue;

                string? a = TryGetLetterFromZoneName(connection.From);
                string? b = TryGetLetterFromZoneName(connection.To);
                if (a == null || b == null) continue;

                double baseValue = BaseBorderGuardValue(a, b, playerLetters, qualityByLetter);
                if (baseValue <= 0) continue;

                ratios.Add(connection.GuardValue.Value / baseValue);
            }

            if (ratios.Count == 0) return 100;

            double median = Median(ratios);
            if (double.IsNaN(median) || double.IsInfinity(median) || median <= 0) return 100;
            return Math.Clamp((int)Math.Round(median * 100.0, MidpointRounding.AwayFromZero), 25, 200);
        }

        private static double BaseBorderGuardValue(
            string letterA,
            string letterB,
            List<string> playerLetters,
            Dictionary<string, NeutralZoneQuality> qualityByLetter)
        {
            bool aIsPlayer = playerLetters.Contains(letterA, StringComparer.Ordinal);
            bool bIsPlayer = playerLetters.Contains(letterB, StringComparer.Ordinal);

            if (aIsPlayer && bIsPlayer)
                return 30000;

            static int QualityBase(NeutralZoneQuality q) => q switch
            {
                NeutralZoneQuality.Low => 15000,
                NeutralZoneQuality.High => 25000,
                _ => 20000,
            };

            if (!aIsPlayer && !bIsPlayer)
            {
                qualityByLetter.TryGetValue(letterA, out var qa);
                qualityByLetter.TryGetValue(letterB, out var qb);
                return QualityBase((int)qa >= (int)qb ? qa : qb);
            }

            string neutralLetter = aIsPlayer ? letterB : letterA;
            qualityByLetter.TryGetValue(neutralLetter, out var q);
            return QualityBase(q);
        }

        private static void InferTournamentTiming(WinConditions? win, out int firstDay, out int interval)
        {
            firstDay = 14;
            interval = 7;

            var announce = win?.TournamentAnnounceDays;
            var offsets = win?.TournamentDays;
            if (announce == null || offsets == null || announce.Count == 0 || offsets.Count == 0)
                return;

            int rounds = Math.Min(announce.Count, offsets.Count);
            if (rounds <= 0) return;

            var battleDays = new List<int>(rounds);
            for (int i = 0; i < rounds; i++)
                battleDays.Add(announce[i] + offsets[i]);

            firstDay = Math.Clamp(battleDays[0], 1, 60);
            if (battleDays.Count >= 2)
                interval = Math.Clamp(battleDays[1] - battleDays[0], 1, 30);
            else
                interval = Math.Clamp(offsets[0] + 1, 1, 30);
        }

        private static List<BonusEntry> ParseBonusEntries(List<Bonus>? bonuses)
        {
            if (bonuses == null || bonuses.Count == 0)
                return [];

            var result = new List<BonusEntry>();
            var consumed = new HashSet<int>();

            for (int i = 0; i < bonuses.Count; i++)
            {
                if (consumed.Contains(i)) continue;
                var bonus = bonuses[i];

                if (string.Equals(bonus.Sid, "add_bonus_hero_spell", StringComparison.OrdinalIgnoreCase)
                    && bonus.Parameters is { Count: > 0 })
                {
                    string spell = bonus.Parameters[0];
                    string receiver = string.IsNullOrWhiteSpace(bonus.ReceiverFilter) ? "start_hero" : bonus.ReceiverFilter!;

                    int pairedStatIdx = -1;
                    for (int j = 0; j < bonuses.Count; j++)
                    {
                        if (j == i || consumed.Contains(j)) continue;
                        var other = bonuses[j];
                        if (!string.Equals(other.Sid, "add_bonus_hero_stat", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!string.Equals(other.ReceiverFilter, receiver, StringComparison.Ordinal)) continue;
                        if (other.Parameters is not { Count: >= 4 }) continue;
                        if (!string.Equals(other.Parameters[0], "magicCostSidSet", StringComparison.Ordinal)) continue;
                        if (!string.Equals(other.Parameters[1], spell, StringComparison.Ordinal)) continue;
                        if (!string.Equals(other.Parameters[2], "-999", StringComparison.Ordinal)) continue;
                        if (!string.Equals(other.Parameters[3], "0", StringComparison.Ordinal)) continue;
                        pairedStatIdx = j;
                        break;
                    }

                    bool free = pairedStatIdx >= 0;
                    if (free) consumed.Add(pairedStatIdx);

                    if (string.Equals(spell, "neutral_magic_town_portal", StringComparison.Ordinal) && free)
                    {
                        result.Add(new BonusEntry
                        {
                            PresetType = BonusPresetType.TownPortalFree,
                            ReceiverFilter = receiver,
                            Param = string.Empty,
                            Param2 = "0"
                        });
                    }
                    else
                    {
                        result.Add(new BonusEntry
                        {
                            PresetType = BonusPresetType.Spell,
                            ReceiverFilter = receiver,
                            Param = spell,
                            Param2 = free ? "1" : "0"
                        });
                    }

                    consumed.Add(i);
                    continue;
                }

                if (string.Equals(bonus.Sid, "add_bonus_hero_unit_multipler", StringComparison.OrdinalIgnoreCase)
                    && bonus.Parameters is { Count: > 0 })
                {
                    result.Add(new BonusEntry
                    {
                        PresetType = BonusPresetType.UnitMultiplier,
                        ReceiverFilter = bonus.ReceiverFilter ?? "start_hero",
                        Param = bonus.Parameters[0],
                        Param2 = "0"
                    });
                    consumed.Add(i);
                    continue;
                }

                if (string.Equals(bonus.Sid, "add_bonus_hero_stat", StringComparison.OrdinalIgnoreCase)
                    && bonus.Parameters is { Count: >= 2 }
                    && string.Equals(bonus.Parameters[0], "movementBonus", StringComparison.Ordinal))
                {
                    result.Add(new BonusEntry
                    {
                        PresetType = BonusPresetType.MovementBonus,
                        ReceiverFilter = bonus.ReceiverFilter ?? "start_hero",
                        Param = bonus.Parameters[1],
                        Param2 = "0"
                    });
                    consumed.Add(i);
                    continue;
                }

                if (string.Equals(bonus.Sid, "add_bonus_hero_item", StringComparison.OrdinalIgnoreCase)
                    && bonus.Parameters is { Count: > 0 })
                {
                    result.Add(new BonusEntry
                    {
                        PresetType = BonusPresetType.StartingItem,
                        ReceiverFilter = bonus.ReceiverFilter ?? "start_hero",
                        Param = bonus.Parameters[0],
                        Param2 = "0"
                    });
                    consumed.Add(i);
                    continue;
                }

                if (string.Equals(bonus.Sid, "add_bonus_res", StringComparison.OrdinalIgnoreCase)
                    && bonus.Parameters is { Count: >= 2 })
                {
                    BonusPresetType? type = bonus.Parameters[0] switch
                    {
                        "gold" => BonusPresetType.StartingGold,
                        "gemstones" => BonusPresetType.StartingGems,
                        "crystals" => BonusPresetType.StartingCrystals,
                        "mercury" => BonusPresetType.StartingMercury,
                        "wood" => BonusPresetType.StartingWood,
                        "ore" => BonusPresetType.StartingOre,
                        _ => null,
                    };

                    if (type.HasValue)
                    {
                        result.Add(new BonusEntry
                        {
                            PresetType = type.Value,
                            ReceiverFilter = bonus.ReceiverFilter ?? "start_hero",
                            Param = bonus.Parameters[1],
                            Param2 = "0"
                        });
                        consumed.Add(i);
                    }
                }
            }

            return result;
        }

        private static string? TryGetLetterFromZoneName(string? zoneName)
        {
            if (string.IsNullOrWhiteSpace(zoneName)) return null;
            if (zoneName.StartsWith("Spawn-", StringComparison.Ordinal)) return zoneName[6..];
            if (zoneName.StartsWith("Neutral-", StringComparison.Ordinal)) return zoneName[8..];
            return null;
        }

        private static int ModifierToPercent(double? modifier, int fallback)
        {
            if (!modifier.HasValue || modifier.Value <= 0 || double.IsNaN(modifier.Value) || double.IsInfinity(modifier.Value))
                return fallback;
            return Math.Clamp((int)Math.Round(modifier.Value * 100.0, MidpointRounding.AwayFromZero), 25, 200);
        }

        private static int Mode(IEnumerable<int> values)
        {
            return values
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
            if (sorted.Count == 0) return 0.0;
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
        }

        private static double ClampDouble(double? value, double min, double max, double fallback)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                return fallback;
            return Math.Clamp(value.Value, min, max);
        }

        private static double ComputeContentScale(int mapSize, int totalZones)
        {
            const double referenceArea = 160.0 * 160.0 / 4.0;
            double zoneArea = (double)mapSize * mapSize / Math.Max(1, totalZones);
            return Math.Clamp(Math.Sqrt(zoneArea / referenceArea), 0.5, 2.5);
        }
    }
}
