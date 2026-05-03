using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateGeneratorTests
{
    [Fact]
    public void Generate_UsesRequestedSettingsForTemplateAndGameRules()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Baseline Test Template",
            GameMode = "Classic",
            MapSize = 200,
            AdvancedMode = true,
            VictoryCondition = "win_condition_5",
            HeroCountMin = 7,
            HeroCountMax = 15,
            HeroCountIncrement = 3,
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Equal(settings.TemplateName, template.Name);
        Assert.Equal(settings.GameMode, template.GameMode);
        Assert.Equal(settings.MapSize, template.SizeX);
        Assert.Equal(settings.MapSize, template.SizeZ);
        Assert.Equal(settings.VictoryCondition, template.DisplayWinCondition);
        Assert.Equal("custom_generated_template", template.Description);
        Assert.NotNull(template.GameRules);
        Assert.Equal(settings.HeroCountMin, template.GameRules.HeroCountMin);
        Assert.Equal(settings.HeroCountMax, template.GameRules.HeroCountMax);
        Assert.Equal(settings.HeroCountIncrement, template.GameRules.HeroCountIncrement);
        Assert.NotEmpty(template.ZoneLayouts ?? []);
        Assert.NotEmpty(template.ContentCountLimits ?? []);
    }

    [Fact]
    public void Generate_DefaultTopologyCreatesExpectedZonesAndRingConnections()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 3,
            NeutralZoneCount = 2,
            PlayerZoneCastles = 2,
            NeutralZoneCastles = 1,
            Topology = MapTopology.Default,
            RandomPortals = false
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var connections = RequiredConnections(variant);

        Assert.Equal(5, zones.Count);
        Assert.Equal(3, zones.Count(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)));
        Assert.Equal(2, zones.Count(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)));
        Assert.Equal(5, connections.Count);
        Assert.All(connections, connection =>
        {
            Assert.StartsWith("Ring-", connection.Name);
            Assert.Equal("Direct", connection.ConnectionType);
        });
        Assert.All(zones.Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)),
            zone => Assert.Equal(2, zone.MainObjects?.Count));
        Assert.All(zones.Where(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)),
            zone => Assert.Single(zone.MainObjects ?? []));
    }

    [Fact]
    public void Generate_WhenRoadsAreDisabledLeavesZoneRoadListsEmpty()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 1,
            PlayerZoneCastles = 3,
            NeutralZoneCastles = 2,
            SpawnRemoteFootholds = true,
            GenerateRoads = false,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));

        Assert.All(RequiredZones(variant), zone => Assert.Empty(zone.Roads ?? []));
    }

    [Fact]
    public void Generate_WithPlayerIsolationAndNeutralZonesDoesNotCreateDirectPlayerConnections()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            NoDirectPlayerConnections = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var playerToPlayerConnections = RequiredConnections(variant)
            .Where(connection =>
                connection.From.StartsWith("Spawn-", StringComparison.Ordinal) &&
                connection.To.StartsWith("Spawn-", StringComparison.Ordinal));

        Assert.Empty(playerToPlayerConnections);
    }

    [Fact]
    public void Generate_WithRandomPortalsAddsOnePortalPerZone()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var portals = RequiredConnections(variant)
            .Where(connection => connection.ConnectionType == "Portal")
            .ToList();

        Assert.Equal(zones.Count, portals.Count);
        Assert.All(portals, portal =>
        {
            Assert.True(portal.Road);
            Assert.NotEmpty(portal.PortalPlacementRulesFrom ?? []);
            Assert.NotEmpty(portal.PortalPlacementRulesTo ?? []);
        });
    }

    [Fact]
    public void Generate_AppliesResourceAndStructureDensitySeparately()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            MapSize = 160,
            Topology = MapTopology.Default,
            ResourceDensityPercent = 50,
            StructureDensityPercent = 150
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        Zone spawnZone = RequiredZones(variant)
            .First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));

        Assert.Equal(300000, spawnZone.GuardedContentValue);
        Assert.Equal(3000, spawnZone.GuardedContentValuePerArea);
        Assert.Equal(75000, spawnZone.UnguardedContentValue);
        Assert.Equal(600, spawnZone.UnguardedContentValuePerArea);
        Assert.Equal(40000, spawnZone.ResourcesValue);
        Assert.Equal(300, spawnZone.ResourcesValuePerArea);
    }

    [Fact]
    public void Generate_AppliesZoneNeutralStrengthToZoneAndMainObjectGuardsOnly()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            MapSize = 160,
            PlayerZoneCastles = 2,
            NeutralZoneCastles = 2,
            Topology = MapTopology.Default,
            NeutralStackStrengthPercent = 200,
            BorderGuardStrengthPercent = 100
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        Zone spawnZone = zones.First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));
        Zone neutralZone = zones.First(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal));

        Assert.Equal(2.0, spawnZone.GuardMultiplier);
        Assert.Equal([10000, 5000], spawnZone.MainObjects?.Select(mainObject => mainObject.GuardValue).ToArray());
        Assert.Equal(2.6, neutralZone.GuardMultiplier);
        Assert.Equal([20000, 10000], neutralZone.MainObjects?.Select(mainObject => mainObject.GuardValue).ToArray());
        Assert.All(RequiredConnections(variant), connection => Assert.Equal(30000, connection.GuardValue));
    }

    [Fact]
    public void Generate_AppliesBorderGuardStrengthToDirectAndPortalConnectionsOnly()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            MapSize = 160,
            RandomPortals = true,
            Topology = MapTopology.Default,
            NeutralStackStrengthPercent = 100,
            BorderGuardStrengthPercent = 50
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        Zone spawnZone = RequiredZones(variant)
            .First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));
        var directConnections = RequiredConnections(variant)
            .Where(connection => connection.ConnectionType == "Direct")
            .ToList();
        var portalConnections = RequiredConnections(variant)
            .Where(connection => connection.ConnectionType == "Portal")
            .ToList();

        Assert.Equal(1.0, spawnZone.GuardMultiplier);
        Assert.Equal(5000, Assert.Single(spawnZone.MainObjects ?? []).GuardValue);
        Assert.All(directConnections, connection => Assert.Equal(15000, connection.GuardValue));
        Assert.All(portalConnections, connection => Assert.Equal(12500, connection.GuardValue));
    }

    [Fact]
    public void Generate_AdvancedModeAppliesGuardRandomizationToSpawnAndNeutralZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            AdvancedMode = true,
            GuardRandomization = 0.23,
            Topology = MapTopology.Default
        };

        var zones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)))
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)
                || zone.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, zone => Assert.Equal(0.23, zone.GuardRandomization));
    }

    [Fact]
    public void Generate_SimpleModeIgnoresGuardRandomizationOverride()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            AdvancedMode = false,
            GuardRandomization = 0.23,
            Topology = MapTopology.Default
        };

        var zones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)))
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)
                || zone.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, zone => Assert.Equal(0.05, zone.GuardRandomization));
    }

    [Fact]
    public void Generate_AdvancedNeutralCountsCreateTieredCastleAndNoCastleZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            AdvancedMode = true,
            NeutralZoneCastles = 2,
            NeutralLowNoCastleCount = 1,
            NeutralLowCastleCount = 1,
            NeutralMediumNoCastleCount = 1,
            NeutralMediumCastleCount = 1,
            NeutralHighNoCastleCount = 1,
            NeutralHighCastleCount = 1,
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        Variant variant = SingleVariant(template);
        var zones = RequiredZones(variant).ToDictionary(zone => zone.Name, StringComparer.Ordinal);

        Assert.Equal(8, zones.Count);
        Assert.Empty(zones["Neutral-C"].MainObjects ?? []);
        Assert.Equal(2, zones["Neutral-D"].MainObjects?.Count);
        Assert.Empty(zones["Neutral-E"].MainObjects ?? []);
        Assert.Equal(2, zones["Neutral-F"].MainObjects?.Count);
        Assert.Empty(zones["Neutral-G"].MainObjects ?? []);
        Assert.Equal(2, zones["Neutral-H"].MainObjects?.Count);
        Assert.Equal("MatchZone", zones["Neutral-C"].ZoneBiome?.Type);
        Assert.Equal("MatchMainObject", zones["Neutral-D"].ZoneBiome?.Type);
        Assert.True(zones["Neutral-C"].GuardedContentValue < zones["Neutral-E"].GuardedContentValue);
        Assert.True(zones["Neutral-E"].GuardedContentValue < zones["Neutral-G"].GuardedContentValue);
        Assert.Equal("zone_layout_sides", zones["Neutral-C"].Layout);
        Assert.Equal("zone_layout_sides", zones["Neutral-D"].Layout);
        Assert.Equal("zone_layout_treasure_zone", zones["Neutral-E"].Layout);
        Assert.Equal("zone_layout_treasure_zone", zones["Neutral-F"].Layout);
        Assert.Equal("zone_layout_center", zones["Neutral-G"].Layout);
        Assert.Equal("zone_layout_center", zones["Neutral-H"].Layout);
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_spawns");
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_sides");
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_treasure_zone");
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_center");

        string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
        Assert.DoesNotContain("\"tier\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"quality\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_AdvancedModeCanCreateThirtyTwoTotalZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 8,
            AdvancedMode = true,
            NeutralLowNoCastleCount = 8,
            NeutralLowCastleCount = 8,
            NeutralMediumNoCastleCount = 8,
            Topology = MapTopology.Default,
            RandomPortals = true
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var zoneNames = zones.Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(32, zones.Count);
        Assert.Contains("Neutral-AF", zoneNames);
        Assert.All(RequiredConnections(variant), connection =>
        {
            Assert.Contains(connection.From, zoneNames);
            Assert.Contains(connection.To, zoneNames);
        });
    }

    [Fact]
    public void Generate_AppliesPlayerAndNeutralZoneSizes()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 2,
            PlayerZoneSize = 2.0,
            NeutralZoneSize = 0.5,
            Topology = MapTopology.Default
        };

        var zones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)));

        Assert.All(zones.Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)),
            zone => Assert.Equal(2.0, zone.Size));
        Assert.All(zones.Where(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)),
            zone => Assert.Equal(0.5, zone.Size));
    }

    [Fact]
    public void KnownValues_ExperimentalMapSizesContinueOfficialIncrementThrough512()
    {
        Assert.Equal(240, KnownValues.MaxOfficialMapSize);
        Assert.Equal(256, KnownValues.ExperimentalMapSizes[0]);
        Assert.Equal(512, KnownValues.ExperimentalMapSizes[^1]);
        Assert.Equal(17, KnownValues.ExperimentalMapSizes.Length);
        Assert.All(KnownValues.ExperimentalMapSizes, size => Assert.Equal(0, size % 16));
        Assert.Equal(KnownValues.MapSizes.Length + KnownValues.ExperimentalMapSizes.Length, KnownValues.AllMapSizes.Length);
    }

    [Fact]
    public void KnownValues_VictoryConditionsUseOfficialObservedMapping()
    {
        Assert.Equal(
            ["win_condition_1", "win_condition_3", "win_condition_4", "win_condition_5", "win_condition_6"],
            KnownValues.VictoryConditionIds);
        Assert.Contains("Gladiator Arena", KnownValues.VictoryConditionLabels);
        Assert.Contains("Tournament", KnownValues.VictoryConditionLabels);
    }

    [Fact]
    public void Generate_AdvancedModeWritesOfficialTournamentWinConditionSettings()
    {
        var settings = new GeneratorSettings
        {
            AdvancedMode = true,
            VictoryCondition = "win_condition_6",
            TournamentRoundCount = 3,
            TournamentRoundDuration = 5,
            TournamentFirstAnnounceDay = 7,
            TournamentRoundInterval = 7,
            TournamentPointsToWin = 2,
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        WinConditions? winConditions = template.GameRules?.WinConditions;

        Assert.Equal("win_condition_6", template.DisplayWinCondition);
        Assert.NotNull(winConditions);
        Assert.True(winConditions.Tournament);
        Assert.False(winConditions.GladiatorArena);
        Assert.Equal([5, 5, 5], winConditions.TournamentDays);
        Assert.Equal([7, 14, 21], winConditions.TournamentAnnounceDays);
        Assert.Equal(2, winConditions.TournamentPointsToWin);
        Assert.True(winConditions.TournamentSaveArmy);
        Assert.True(winConditions.LostStartHero);
        Assert.Equal("StartHero", winConditions.ChampionSelectRule);
    }

    [Fact]
    public void Generate_AdvancedModeAppliesSeparateExperienceModifiers()
    {
        var settings = new GeneratorSettings
        {
            AdvancedMode = true,
            FactionLawsExpPercent = 50,
            AstrologyExpPercent = 200,
            Topology = MapTopology.Default
        };

        GameRules? rules = TemplateGenerator.Generate(settings).GameRules;

        Assert.NotNull(rules);
        Assert.Equal(0.5, rules.FactionLawsExpModifier);
        Assert.Equal(2.0, rules.AstrologyExpModifier);
    }

    [Fact]
    public void Generate_SimpleModeIgnoresAdvancedWinConditionAndExperienceOverrides()
    {
        var settings = new GeneratorSettings
        {
            AdvancedMode = false,
            VictoryCondition = "win_condition_6",
            FactionLawsExpPercent = 50,
            AstrologyExpPercent = 200,
            Tournament = true,
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Equal("win_condition_1", template.DisplayWinCondition);
        Assert.Equal(1.0, template.GameRules?.FactionLawsExpModifier);
        Assert.Equal(1.0, template.GameRules?.AstrologyExpModifier);
        Assert.Null(template.GameRules?.WinConditions?.Tournament);
    }

    [Fact]
    public void Generate_WhenPlayerCastleFactionMatchingIsEnabled_ExtraCitiesMatchSpawnFaction()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            PlayerZoneCastles = 3,
            MatchPlayerCastleFactions = true,
            Topology = MapTopology.Default
        };

        var spawnZones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)))
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal))
            .ToList();

        Assert.All(spawnZones, zone =>
        {
            var mainObjects = zone.MainObjects ?? [];
            Assert.Equal(3, mainObjects.Count);
            Assert.All(mainObjects.Skip(1), mainObject =>
            {
                Assert.Equal("City", mainObject.Type);
                Assert.Equal("Match", mainObject.Faction?.Type);
                Assert.Equal(["0"], mainObject.Faction?.Args);
            });
        });
    }

    [Fact]
    public void Generate_RingHonorsMinimumNeutralSeparation_WhenEnoughNeutrals()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 3,
            NeutralZoneCount = 6,
            MinNeutralZonesBetweenPlayers = 2,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var spawnNames = RequiredZones(variant)
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal))
            .Select(zone => zone.Name)
            .ToList();

        for (int i = 0; i < spawnNames.Count; i++)
        {
            for (int j = i + 1; j < spawnNames.Count; j++)
                Assert.True(ShortestNeutralIntermediates(variant, spawnNames[i], spawnNames[j]) >= 2);
        }
    }

    [Fact]
    public void CanHonorNeutralSeparation_ReturnsFalseWhenPortalsCouldShortenPaths()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 3,
            NeutralZoneCount = 6,
            MinNeutralZonesBetweenPlayers = 2,
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Assert.False(TemplateGenerator.CanHonorNeutralSeparation(settings, settings.NeutralZoneCount));
    }

    [Fact]
    public void TemplatePreviewPngWriter_UsesOfficialSidecarNaming()
    {
        Assert.Equal(@"C:\maps\My Template.png", TemplatePreviewPngWriter.GetSidecarPath(@"C:\maps\My Template.rmg.json"));
        Assert.Equal(@"C:\maps\Other.png", TemplatePreviewPngWriter.GetSidecarPath(@"C:\maps\Other.json"));
    }

    [Fact]
    public void SettingsFile_LegacyContentDensitySeedsSplitDensitySettings()
    {
        const string json = """
            {
              "contentDensity": 130
            }
            """;

        SettingsFile? settings = JsonSerializer.Deserialize<SettingsFile>(json);

        Assert.NotNull(settings);
        Assert.Equal(130, settings.EffectiveResourceDensityPercent);
        Assert.Equal(130, settings.EffectiveStructureDensityPercent);
        Assert.Equal(100, settings.NeutralStackStrengthPercent);
        Assert.Equal(100, settings.BorderGuardStrengthPercent);
    }

    [Fact]
    public void Generate_CanSerializeAndDeserializeRmgJson()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Round Trip Template",
            PlayerCount = 2,
            NeutralZoneCount = 1,
            Topology = MapTopology.Default
        };

        RmgTemplate generated = TemplateGenerator.Generate(settings);
        string json = JsonSerializer.Serialize(generated, new JsonSerializerOptions { WriteIndented = true });
        RmgTemplate? deserialized = JsonSerializer.Deserialize<RmgTemplate>(json);

        Assert.Contains("\"name\": \"Round Trip Template\"", json, StringComparison.Ordinal);
        Assert.NotNull(deserialized);
        Assert.Equal(generated.Name, deserialized.Name);
        Assert.Single(deserialized.Variants ?? []);
    }

    [Fact]
    public void Generate_ConnectionsReferToGeneratedZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            NeutralZoneCount = 3,
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zoneNames = RequiredZones(variant).Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(RequiredConnections(variant), connection =>
        {
            Assert.Contains(connection.From, zoneNames);
            Assert.Contains(connection.To, zoneNames);
        });
    }

    private static Variant SingleVariant(RmgTemplate template) =>
        Assert.Single(template.Variants ?? []);

    private static List<Zone> RequiredZones(Variant variant)
    {
        Assert.NotNull(variant.Zones);
        return variant.Zones;
    }

    private static List<Connection> RequiredConnections(Variant variant)
    {
        Assert.NotNull(variant.Connections);
        return variant.Connections;
    }

    private static int ShortestNeutralIntermediates(Variant variant, string from, string to)
    {
        var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Connection connection in RequiredConnections(variant)
            .Where(connection => connection.ConnectionType is "Direct" or "Portal"))
        {
            if (!graph.TryGetValue(connection.From, out var fromEdges))
                graph[connection.From] = fromEdges = [];
            if (!graph.TryGetValue(connection.To, out var toEdges))
                graph[connection.To] = toEdges = [];

            fromEdges.Add(connection.To);
            toEdges.Add(connection.From);
        }

        var queue = new Queue<(string Zone, int Neutrals)>();
        var best = new Dictionary<string, int>(StringComparer.Ordinal) { [from] = 0 };
        queue.Enqueue((from, 0));

        while (queue.Count > 0)
        {
            var (zone, neutrals) = queue.Dequeue();
            if (!graph.TryGetValue(zone, out var neighbors)) continue;

            foreach (string neighbor in neighbors)
            {
                int nextNeutrals = neutrals + (neighbor != to && !neighbor.StartsWith("Spawn-", StringComparison.Ordinal) ? 1 : 0);
                if (best.TryGetValue(neighbor, out int existing) && existing <= nextNeutrals)
                    continue;

                best[neighbor] = nextNeutrals;
                queue.Enqueue((neighbor, nextNeutrals));
            }
        }

        return best.TryGetValue(to, out int shortest) ? shortest : int.MaxValue;
    }
}
