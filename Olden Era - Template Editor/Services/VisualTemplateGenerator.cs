using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using System.Text.Json;

namespace Olden_Era___Template_Editor.Services
{
    public static class VisualTemplateGenerator
    {
        public static readonly IReadOnlyDictionary<string, string> FactionCodeByGameName =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Temple"] = "Human",
                ["Hive"] = "Demon",
                ["Dungeon"] = "Dungeon",
                ["Grove"] = "Nature",
                ["Necropolis"] = "Necro",
                ["Schism"] = "Unfrozen"
            };

        private const string SpawnLayoutName = "zone_layout_spawns";
        private const string LowLayoutName = "zone_layout_sides";
        private const string MediumLayoutName = "zone_layout_treasure_zone";
        private const string HighLayoutName = "zone_layout_center";

        private static readonly JsonSerializerOptions CloneOptions = new()
        {
            WriteIndented = false
        };

        public static RmgTemplate Generate(VisualTemplateDocument document)
        {
            VisualValidationResult validation = VisualTemplateValidator.Validate(document);
            if (!validation.IsValid)
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

            var visualZones = document.Zones
                .Select(VisualTemplateOperations.Clone)
                .ToList();
            foreach (VisualZone zone in visualZones)
                VisualTemplateOperations.NormalizeCastles(zone);

            int playerCount = visualZones
                .Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn)
                .Max(zone => zone.PlayerSlot ?? 0);

            GeneratorSettings prototypeSettings = BuildGeneratorSettings(document, visualZones, playerCount, useActualWinCondition: false);
            RmgTemplate template = TemplateGenerator.Generate(prototypeSettings);

            GeneratorSettings rulesSettings = BuildGeneratorSettings(document, visualZones, playerCount, useActualWinCondition: true);
            RmgTemplate rulesTemplate = TemplateGenerator.Generate(rulesSettings);
            template.GameRules = rulesTemplate.GameRules;

            template.Name = document.TemplateName.Trim();
            template.Description = string.IsNullOrWhiteSpace(document.Description) ? null : document.Description;
            template.GameMode = document.GameMode;
            template.DisplayWinCondition = document.VictoryCondition;
            template.SizeX = document.MapSize;
            template.SizeZ = document.MapSize;

            Variant baseVariant = template.Variants?.FirstOrDefault()
                ?? throw new InvalidOperationException("The base generator did not create a variant.");

            var prototypeZones = baseVariant.Zones ?? [];
            var mandatoryByName = (template.MandatoryContent ?? [])
                .ToDictionary(group => group.Name, StringComparer.Ordinal);

            var playerSlotToZoneName = visualZones
                .Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn && zone.PlayerSlot.HasValue)
                .ToDictionary(
                    zone => zone.PlayerSlot!.Value,
                    zone => ZoneName(zone),
                    EqualityComparer<int>.Default);
            string? holdCityZoneId = document.CityHold || document.VictoryCondition == "win_condition_5"
                ? visualZones.FirstOrDefault(zone => zone.ZoneType != VisualZoneType.PlayerSpawn && zone.CastleCount > 0)?.Id
                : null;

            var spawnPrototypes = prototypeZones
                .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal))
                .Select(zone => (Zone: zone, PlayerSlot: zone.MainObjects?.FirstOrDefault(o => o.Type == "Spawn")?.Spawn))
                .Where(item => item.PlayerSlot?.StartsWith("Player", StringComparison.Ordinal) == true)
                .ToDictionary(
                    item => int.Parse(item.PlayerSlot!["Player".Length..]),
                    item => item.Zone);

            var neutralPrototypes = BuildNeutralPrototypeQueues(prototypeZones);
            var generatedZones = new List<Zone>();
            var mandatoryGroups = new List<MandatoryContentGroup>();
            var selectedPrototypeByVisualId = new Dictionary<string, Zone>(StringComparer.Ordinal);

            foreach (VisualZone visualZone in visualZones)
            {
                Zone prototype = PickPrototype(visualZone, spawnPrototypes, neutralPrototypes);
                selectedPrototypeByVisualId[visualZone.Id] = prototype;

                Zone zone = Clone(prototype);
                string newName = ZoneName(visualZone);
                string? oldMandatoryName = zone.MandatoryContent?.FirstOrDefault();
                zone.Name = newName;
                zone.Size = NormalizeZoneSize(visualZone.ZoneSize);
                zone.GuardRandomization = NormalizeGuardRandomization(visualZone.GuardRandomization);
                ApplyZoneScaling(zone, visualZone);
                ApplyMainObjects(zone, visualZone, playerSlotToZoneName);
                if (visualZone.Id == holdCityZoneId && zone.MainObjects is { Count: > 0 })
                    ApplyHoldCity(zone.MainObjects[0]);
                ApplyBiomes(zone);

                string newMandatoryName = visualZone.ZoneType == VisualZoneType.PlayerSpawn
                    ? $"mandatory_content_side_{visualZone.ExportLetter.Trim().ToUpperInvariant()}"
                    : $"mandatory_content_neutral_{visualZone.ExportLetter.Trim().ToUpperInvariant()}";

                if (!string.IsNullOrWhiteSpace(oldMandatoryName) && mandatoryByName.TryGetValue(oldMandatoryName, out MandatoryContentGroup? group))
                {
                    MandatoryContentGroup clonedGroup = Clone(group);
                    clonedGroup.Name = newMandatoryName;
                    if (!ShouldIncludeRemoteFoothold(document, visualZone))
                        clonedGroup.Content = clonedGroup.Content?
                            .Where(item => item.Name != "name_remote_foothold_1")
                            .ToList();
                    mandatoryGroups.Add(clonedGroup);
                    zone.MandatoryContent = [newMandatoryName];
                }

                generatedZones.Add(zone);
            }

            List<Connection> generatedConnections = BuildConnections(document, visualZones);
            ApplyRoads(document, visualZones, generatedZones, generatedConnections);

            baseVariant.Zones = generatedZones;
            baseVariant.Connections = generatedConnections;
            if (baseVariant.Orientation is not null && generatedZones.Count > 0)
            {
                baseVariant.Orientation.ZeroAngleZone = generatedZones[0].Name;
                baseVariant.Orientation.RandomAngleStep = generatedZones.Count == 0 ? 0 : 360.0 / generatedZones.Count;
            }

            template.MandatoryContent = mandatoryGroups;
            template.Variants = [baseVariant];
            return template;
        }

        private static GeneratorSettings BuildGeneratorSettings(
            VisualTemplateDocument document,
            List<VisualZone> visualZones,
            int playerCount,
            bool useActualWinCondition)
        {
            int maxPlayerCastles = visualZones
                .Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn)
                .Select(zone => Math.Clamp(zone.CastleCount, 1, 4))
                .DefaultIfEmpty(1)
                .Max();
            int maxNeutralCastles = visualZones
                .Where(zone => zone.ZoneType != VisualZoneType.PlayerSpawn)
                .Select(zone => Math.Clamp(zone.CastleCount, 0, 4))
                .DefaultIfEmpty(0)
                .Max();

            return new GeneratorSettings
            {
                TemplateName = document.TemplateName,
                GameMode = document.GameMode,
                PlayerCount = playerCount,
                MapSize = document.MapSize,
                AdvancedMode = true,
                VictoryCondition = useActualWinCondition ? document.VictoryCondition : "win_condition_1",
                PlayerZoneCastles = maxPlayerCastles,
                NeutralZoneCastles = Math.Max(1, maxNeutralCastles),
                NeutralLowNoCastleCount = CountNeutral(visualZones, VisualZoneType.NeutralLow, hasCastle: false),
                NeutralLowCastleCount = CountNeutral(visualZones, VisualZoneType.NeutralLow, hasCastle: true),
                NeutralMediumNoCastleCount = CountNeutral(visualZones, VisualZoneType.NeutralMedium, hasCastle: false),
                NeutralMediumCastleCount = CountNeutral(visualZones, VisualZoneType.NeutralMedium, hasCastle: true),
                NeutralHighNoCastleCount = CountNeutral(visualZones, VisualZoneType.NeutralHigh, hasCastle: false),
                NeutralHighCastleCount = CountNeutral(visualZones, VisualZoneType.NeutralHigh, hasCastle: true),
                HeroCountMin = document.HeroCountMin,
                HeroCountMax = document.HeroCountMax,
                HeroCountIncrement = document.HeroCountIncrement,
                Topology = MapTopology.Default,
                ResourceDensityPercent = 100,
                StructureDensityPercent = 100,
                NeutralStackStrengthPercent = 100,
                BorderGuardStrengthPercent = 100,
                FactionLawsExpPercent = document.FactionLawsExpPercent,
                AstrologyExpPercent = document.AstrologyExpPercent,
                LostStartCity = useActualWinCondition && document.LostStartCity,
                LostStartCityDay = document.LostStartCityDay,
                LostStartHero = useActualWinCondition && document.LostStartHero,
                CityHold = useActualWinCondition && document.CityHold,
                CityHoldDays = document.CityHoldDays,
                GladiatorArena = useActualWinCondition && document.GladiatorArena,
                GladiatorArenaDaysDelayStart = document.GladiatorArenaDaysDelayStart,
                GladiatorArenaCountDay = document.GladiatorArenaCountDay,
                Tournament = useActualWinCondition && document.Tournament,
                TournamentFirstTournamentDay = document.TournamentFirstTournamentDay,
                TournamentAnnouncementLeadDays = document.TournamentAnnouncementLeadDays,
                TournamentInterval = document.TournamentInterval,
                TournamentPointsToWin = document.TournamentPointsToWin,
                GenerateRoads = document.GenerateRoads,
                SpawnRemoteFootholds = document.SpawnRemoteFootholds
            };
        }

        private static int CountNeutral(List<VisualZone> zones, VisualZoneType type, bool hasCastle) =>
            zones.Count(zone => zone.ZoneType == type && (zone.CastleCount > 0) == hasCastle);

        private static Dictionary<(VisualZoneType Type, bool HasCastle), Queue<Zone>> BuildNeutralPrototypeQueues(List<Zone> prototypeZones)
        {
            var queues = new Dictionary<(VisualZoneType Type, bool HasCastle), Queue<Zone>>();
            foreach (Zone zone in prototypeZones.Where(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)))
            {
                var key = (TypeFromLayout(zone.Layout), (zone.MainObjects?.Count ?? 0) > 0);
                if (!queues.TryGetValue(key, out Queue<Zone>? queue))
                    queues[key] = queue = new Queue<Zone>();
                queue.Enqueue(zone);
            }

            return queues;
        }

        private static Zone PickPrototype(
            VisualZone visualZone,
            Dictionary<int, Zone> spawnPrototypes,
            Dictionary<(VisualZoneType Type, bool HasCastle), Queue<Zone>> neutralPrototypes)
        {
            if (visualZone.ZoneType == VisualZoneType.PlayerSpawn)
            {
                int playerSlot = visualZone.PlayerSlot ?? 1;
                if (spawnPrototypes.TryGetValue(playerSlot, out Zone? spawnPrototype))
                    return spawnPrototype;
                return spawnPrototypes.Values.First();
            }

            var key = (visualZone.ZoneType, visualZone.CastleCount > 0);
            if (neutralPrototypes.TryGetValue(key, out Queue<Zone>? queue) && queue.Count > 0)
                return queue.Dequeue();

            throw new InvalidOperationException($"No generated prototype was available for {visualZone.ZoneType}.");
        }

        private static List<Connection> BuildConnections(VisualTemplateDocument document, List<VisualZone> visualZones)
        {
            var zonesById = visualZones.ToDictionary(zone => zone.Id, StringComparer.Ordinal);
            var connections = new List<Connection>();
            int index = 1;
            foreach (VisualConnection visualConnection in document.Connections)
            {
                VisualZone fromZone = zonesById[visualConnection.FromZoneId];
                VisualZone toZone = zonesById[visualConnection.ToZoneId];
                string fromName = ZoneName(fromZone);
                string toName = ZoneName(toZone);
                string fromLetter = fromZone.ExportLetter.Trim().ToUpperInvariant();
                string toLetter = toZone.ExportLetter.Trim().ToUpperInvariant();
                double globalMultiplier = Math.Clamp(document.BorderGuardStrengthPercent, 25, 300) / 100.0;
                double edgeMultiplier = Math.Clamp(visualConnection.GuardStrengthPercent, 25, 300) / 100.0;

                if (visualConnection.ConnectionType == VisualConnectionType.Portal)
                {
                    connections.Add(new Connection
                    {
                        Name = $"Portal-{fromLetter}-{toLetter}-{index}",
                        From = fromName,
                        To = toName,
                        ConnectionType = "Portal",
                        PortalPlacementRulesFrom = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 2 }],
                        PortalPlacementRulesTo = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 2 }],
                        Road = true,
                        GuardEscape = false,
                        GuardValue = ScaleInt(25000, globalMultiplier * edgeMultiplier),
                        GuardWeeklyIncrement = 0.15
                    });
                }
                else
                {
                    connections.Add(new Connection
                    {
                        Name = $"Visual-{fromLetter}-{toLetter}-{index}",
                        From = fromName,
                        To = toName,
                        ConnectionType = "Direct",
                        GuardZone = fromName,
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = ScaleInt(30000, globalMultiplier * edgeMultiplier),
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"visual_guard_{fromLetter}_{toLetter}_{index}"
                    });
                }

                index++;
            }

            return connections;
        }

        private static void ApplyZoneScaling(Zone zone, VisualZone visualZone)
        {
            double neutralMultiplier = Math.Clamp(visualZone.NeutralStackStrengthPercent, 25, 300) / 100.0;
            double resourceMultiplier = Math.Clamp(visualZone.ResourceDensityPercent, 20, 400) / 100.0;
            double structureMultiplier = Math.Clamp(visualZone.StructureDensityPercent, 20, 400) / 100.0;

            if (zone.GuardMultiplier.HasValue)
                zone.GuardMultiplier = Math.Round(zone.GuardMultiplier.Value * neutralMultiplier, 3, MidpointRounding.AwayFromZero);

            zone.GuardedContentValue = ScaleNullable(zone.GuardedContentValue, structureMultiplier);
            zone.GuardedContentValuePerArea = ScaleNullable(zone.GuardedContentValuePerArea, structureMultiplier);
            zone.UnguardedContentValue = ScaleNullable(zone.UnguardedContentValue, structureMultiplier);
            zone.UnguardedContentValuePerArea = ScaleNullable(zone.UnguardedContentValuePerArea, structureMultiplier);
            zone.ResourcesValue = ScaleNullable(zone.ResourcesValue, resourceMultiplier);
            zone.ResourcesValuePerArea = ScaleNullable(zone.ResourcesValuePerArea, resourceMultiplier);

            foreach (MainObject mainObject in zone.MainObjects ?? [])
                mainObject.GuardValue = ScaleNullable(mainObject.GuardValue, neutralMultiplier);
        }

        private static void ApplyMainObjects(
            Zone zone,
            VisualZone visualZone,
            Dictionary<int, string> playerSlotToZoneName)
        {
            int castleCount = Math.Clamp(visualZone.CastleCount, visualZone.ZoneType == VisualZoneType.PlayerSpawn ? 1 : 0, 4);
            zone.MainObjects ??= [];
            if (zone.MainObjects.Count > castleCount)
                zone.MainObjects.RemoveRange(castleCount, zone.MainObjects.Count - castleCount);

            while (zone.MainObjects.Count < castleCount && zone.MainObjects.Count > 0)
                zone.MainObjects.Add(Clone(zone.MainObjects[^1]));

            if (castleCount == 0)
            {
                zone.MainObjects.Clear();
                return;
            }

            for (int i = 0; i < castleCount; i++)
            {
                MainObject mainObject = zone.MainObjects[i];
                if (visualZone.ZoneType == VisualZoneType.PlayerSpawn && i == 0)
                {
                    mainObject.Type = "Spawn";
                    mainObject.Spawn = $"Player{visualZone.PlayerSlot ?? 1}";
                }
                else if (mainObject.Type != "City")
                {
                    mainObject.Type = "City";
                    mainObject.Spawn = null;
                }

                VisualCastle castle = i < visualZone.Castles.Count ? visualZone.Castles[i] : new VisualCastle();
                mainObject.Faction = BuildFactionSelector(castle, playerSlotToZoneName, mainObject.Type == "Spawn");
            }
        }

        private static void ApplyHoldCity(MainObject mainObject)
        {
            mainObject.Type = "City";
            mainObject.Spawn = null;
            mainObject.GuardChance = 1.0;
            mainObject.BuildingsConstructionSid = "ultra_rich_buildings_construction";
            mainObject.Placement = "Center";
            mainObject.PlacementArgs = [];
            mainObject.HoldCityWinCon = true;
        }

        private static TypedSelector? BuildFactionSelector(
            VisualCastle castle,
            Dictionary<int, string> playerSlotToZoneName,
            bool isSpawnObject)
        {
            return castle.FactionMode switch
            {
                VisualCastleFactionMode.Restricted => new TypedSelector
                {
                    Type = "FromList",
                    Args = castle.AllowedFactions
                        .Where(FactionCodeByGameName.ContainsKey)
                        .Select(faction => FactionCodeByGameName[faction])
                        .Distinct(StringComparer.Ordinal)
                        .ToList()
                },
                VisualCastleFactionMode.MatchPlayer when castle.MatchPlayerSlot.HasValue
                    && playerSlotToZoneName.TryGetValue(castle.MatchPlayerSlot.Value, out string? zoneName) => new TypedSelector
                    {
                        Type = "Match",
                        Args = ["0", zoneName]
                    },
                _ => isSpawnObject ? null : new TypedSelector { Type = "FromList", Args = [] }
            };
        }

        private static void ApplyBiomes(Zone zone)
        {
            bool hasMainObject = zone.MainObjects?.Count > 0;
            var selector = hasMainObject
                ? new BiomeSelector { Type = "MatchMainObject", Args = ["0"] }
                : new BiomeSelector { Type = "MatchZone", Args = [] };

            zone.ZoneBiome = Clone(selector);
            zone.ContentBiome = Clone(selector);
            zone.MetaObjectsBiome = Clone(selector);
        }

        private static void ApplyRoads(
            VisualTemplateDocument document,
            List<VisualZone> visualZones,
            List<Zone> generatedZones,
            List<Connection> generatedConnections)
        {
            var connectionNamesByZone = generatedZones.ToDictionary(zone => zone.Name, _ => new List<string>(), StringComparer.Ordinal);
            foreach (Connection connection in generatedConnections)
            {
                if (!string.IsNullOrWhiteSpace(connection.Name))
                {
                    connectionNamesByZone[connection.From].Add(connection.Name);
                    connectionNamesByZone[connection.To].Add(connection.Name);
                }
            }

            var visualByName = visualZones.ToDictionary(ZoneName, StringComparer.Ordinal);
            foreach (Zone zone in generatedZones)
            {
                VisualZone visual = visualByName[zone.Name];
                bool generateRoads = document.GenerateRoads && visual.GenerateRoads;
                bool includeFoothold = ShouldIncludeRemoteFoothold(document, visual);
                int castleCount = zone.MainObjects?.Count ?? 0;
                string[] connNames = connectionNamesByZone[zone.Name].Distinct(StringComparer.Ordinal).ToArray();
                zone.Roads = castleCount > 0
                    ? BuildMainObjectRoads(connNames, castleCount, includeFoothold, generateRoads)
                    : BuildConnectorRoads(connNames, generateRoads);
            }
        }

        private static List<Road> BuildMainObjectRoads(string[] connectionNames, int castleCount, bool includeFoothold, bool generateRoads)
        {
            var roads = new List<Road>();
            if (!generateRoads)
                return roads;

            for (int i = 1; i < castleCount; i++)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint(i.ToString())));

            if (includeFoothold)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint("name_remote_foothold_1")));

            foreach (string connectionName in connectionNames)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(connectionName)));

            return roads;
        }

        private static List<Road> BuildConnectorRoads(string[] connectionNames, bool generateRoads)
        {
            var roads = new List<Road>();
            if (!generateRoads || connectionNames.Length == 0)
                return roads;

            if (connectionNames.Length == 1)
            {
                roads.Add(PlainRoad(ConnectionEndpoint(connectionNames[0]), ConnectionEndpoint(connectionNames[0])));
                return roads;
            }

            string anchor = connectionNames[0];
            foreach (string connectionName in connectionNames.Skip(1))
                roads.Add(PlainRoad(ConnectionEndpoint(anchor), ConnectionEndpoint(connectionName)));

            return roads;
        }

        private static bool ShouldIncludeRemoteFoothold(VisualTemplateDocument document, VisualZone visualZone) =>
            document.SpawnRemoteFootholds && visualZone.SpawnRemoteFoothold;

        public static string ZoneName(VisualZone zone)
        {
            string letter = zone.ExportLetter.Trim().ToUpperInvariant();
            return zone.ZoneType == VisualZoneType.PlayerSpawn ? $"Spawn-{letter}" : $"Neutral-{letter}";
        }

        private static VisualZoneType TypeFromLayout(string? layout) => layout switch
        {
            LowLayoutName => VisualZoneType.NeutralLow,
            HighLayoutName => VisualZoneType.NeutralHigh,
            _ => VisualZoneType.NeutralMedium
        };

        private static int? ScaleNullable(int? value, double multiplier) =>
            value.HasValue ? ScaleInt(value.Value, multiplier) : null;

        private static int ScaleInt(int value, double multiplier) =>
            Math.Max(0, (int)(value * multiplier));

        private static double NormalizeZoneSize(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;
            return Math.Round(Math.Clamp(value, 0.1, 2.0), 2, MidpointRounding.AwayFromZero);
        }

        private static double NormalizeGuardRandomization(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.05;
            return Math.Round(Math.Clamp(value, 0.0, 0.5), 3, MidpointRounding.AwayFromZero);
        }

        private static Road PlainRoad(RoadEndpoint from, RoadEndpoint to) =>
            new() { From = from, To = to };

        private static RoadEndpoint MainObjectEndpoint(string index) =>
            new() { Type = "MainObject", Args = [index] };

        private static RoadEndpoint ConnectionEndpoint(string name) =>
            new() { Type = "Connection", Args = [name] };

        private static RoadEndpoint MandatoryContentEndpoint(string name) =>
            new() { Type = "MandatoryContent", Args = [name] };

        private static T Clone<T>(T value)
        {
            string json = JsonSerializer.Serialize(value, CloneOptions);
            return JsonSerializer.Deserialize<T>(json, CloneOptions)
                ?? throw new InvalidOperationException("Unable to clone template data.");
        }
    }
}
