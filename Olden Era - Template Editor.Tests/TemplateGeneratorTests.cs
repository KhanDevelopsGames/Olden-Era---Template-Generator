using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        Assert.Matches(@"^Generated with Olden Era Template Generator v\d+\.\d+: Ring layout, no neutral zones, 1 castle per player zone\.$", template.Description);
        Assert.NotNull(template.GameRules);
        Assert.Equal(settings.HeroCountMin, template.GameRules.HeroCountMin);
        Assert.Equal(settings.HeroCountMax, template.GameRules.HeroCountMax);
        Assert.Equal(settings.HeroCountIncrement, template.GameRules.HeroCountIncrement);
        Assert.NotEmpty(template.ZoneLayouts ?? []);
        Assert.NotEmpty(template.ContentCountLimits ?? []);
    }

    [Fact]
    public void Generate_WritesBasicTemplateDescription()
    {
        var settings = new GeneratorSettings
        {
            GameMode = "SingleHero",
            PlayerCount = 4,
            NeutralZoneCount = 2,
            MapSize = 192,
            PlayerZoneCastles = 2,
            NeutralZoneCastles = 0,
            Topology = MapTopology.Chain,
            NoDirectPlayerConnections = true,
            RandomPortals = true,
            SpawnRemoteFootholds = false,
            GenerateRoads = false
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Matches(@"^Generated with Olden Era Template Generator v\d+\.\d+: Chain layout, 2 neutral zones, 2 castles per player zone, no castles per neutral zone, options: isolated player starts, random portals, no remote footholds, roads disabled\.$", template.Description);
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
    public void Generate_SharedWebReferencesSpokeConnectionsFromBothEndpointZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            NeutralZoneCount = 3,
            NeutralZoneCastles = 1,
            Topology = MapTopology.SharedWeb,
            RandomPortals = false
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant).ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var webConnections = RequiredConnections(variant)
            .Where(connection => connection.Name?.StartsWith("Web-", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(webConnections);
        Assert.All(webConnections, connection =>
        {
            Assert.False(string.IsNullOrWhiteSpace(connection.Name));
            string name = connection.Name!;
            Assert.Contains(name, RoadConnectionNames(zones[connection.From]));
            Assert.Contains(name, RoadConnectionNames(zones[connection.To]));
        });
    }

    [Fact]
    public void Generate_SharedWebCastlelessNeutralZonesUseConnectionRoads()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            NeutralZoneCount = 0,
            NeutralZoneCastles = 0,
            Topology = MapTopology.SharedWeb,
            RandomPortals = false
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        Zone neutralZone = Assert.Single(RequiredZones(variant),
            zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal));
        var webConnectionNames = RequiredConnections(variant)
            .Where(connection => connection.Name?.StartsWith("Web-", StringComparison.Ordinal) == true)
            .Select(connection => connection.Name!)
            .ToList();

        Assert.Empty(neutralZone.MainObjects ?? []);
        Assert.NotEmpty(neutralZone.Roads ?? []);
        Assert.All(neutralZone.Roads ?? [], road =>
        {
            Assert.Equal("Connection", road.From?.Type);
            Assert.Equal("Connection", road.To?.Type);
        });
        Assert.Equal(2, webConnectionNames.Count);
        Assert.All(webConnectionNames, name => Assert.Contains(name, RoadConnectionNames(neutralZone)));
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
    public void Generate_BalancedRingEvenlySpacesPlayersAndNeutralQuality()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            AdvancedMode = true,
            NeutralZoneCastles = 1,
            NeutralLowNoCastleCount = 4,
            NeutralHighCastleCount = 4,
            Topology = MapTopology.Default,
            ExperimentalBalancedZonePlacement = true
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var sequence = zones.Select(zone => zone.Name).ToList();
        var zonesByName = zones.ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var gaps = RingNeutralGapsBetweenPlayers(sequence);

        Assert.Equal(4, gaps.Count);
        Assert.All(gaps, gap =>
        {
            Assert.Equal(2, gap.Count);
            Assert.Single(gap, zoneName => zonesByName[zoneName].Layout == "zone_layout_center");
            Assert.Single(gap, zoneName => zonesByName[zoneName].Layout == "zone_layout_sides");
        });

        var playerDistanceTotals = RingPlayerDistanceTotals(sequence);
        Assert.Single(playerDistanceTotals.Values.Distinct());
    }

    [Fact]
    public void Generate_BalancedSharedWebGivesEachPlayerMixedNeutralQuality()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            AdvancedMode = true,
            NeutralZoneCastles = 1,
            NeutralLowNoCastleCount = 4,
            NeutralHighCastleCount = 4,
            Topology = MapTopology.SharedWeb,
            ExperimentalBalancedZonePlacement = true
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zonesByName = RequiredZones(variant).ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var webConnectionsByPlayer = RequiredConnections(variant)
            .Where(connection => connection.Name?.StartsWith("Web-", StringComparison.Ordinal) == true)
            .GroupBy(connection => connection.From, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(4, webConnectionsByPlayer.Count);
        Assert.All(webConnectionsByPlayer, group =>
        {
            var connectedLayouts = group
                .Select(connection => zonesByName[connection.To].Layout)
                .ToList();

            Assert.Equal(2, connectedLayouts.Count);
            Assert.Contains("zone_layout_center", connectedLayouts);
            Assert.Contains("zone_layout_sides", connectedLayouts);
        });
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
            ["win_condition_1", "win_condition_3", "win_condition_5", "win_condition_6"],
            KnownValues.VictoryConditionIds);
        Assert.DoesNotContain("Gladiator Arena", KnownValues.VictoryConditionLabels);
        Assert.Contains("Tournament [EXPERIMENTAL]", KnownValues.VictoryConditionLabels);
    }

    [Fact]
    public void Generate_AdvancedModeWritesOfficialTournamentWinConditionSettings()
    {
        var settings = new GeneratorSettings
        {
            AdvancedMode = true,
            VictoryCondition = "win_condition_6",
            TournamentFirstTournamentDay = 8,
            TournamentAnnouncementLeadDays = 3,
            TournamentInterval = 7,
            TournamentPointsToWin = 2,
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        WinConditions? winConditions = template.GameRules?.WinConditions;

        Assert.Equal("win_condition_6", template.DisplayWinCondition);
        Assert.NotNull(winConditions);
        Assert.True(winConditions.Tournament);
        Assert.Null(winConditions.GladiatorArena);
        Assert.Equal([3, 3, 3], winConditions.TournamentDays);
        Assert.Equal([5, 12, 19], winConditions.TournamentAnnounceDays);
        Assert.Equal(2, winConditions.TournamentPointsToWin);
        Assert.True(winConditions.TournamentSaveArmy);
        Assert.False(winConditions.LostStartHero);
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
    public void Generate_SimpleModeAppliesWinConditionAndExperienceOverrides()
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

        Assert.Equal("win_condition_6", template.DisplayWinCondition);
        Assert.Equal(0.5, template.GameRules?.FactionLawsExpModifier);
        Assert.Equal(2.0, template.GameRules?.AstrologyExpModifier);
        Assert.True(template.GameRules?.WinConditions?.Tournament);
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
    public void TemplatePreviewPngWriter_Save_EncodesNeutralQualityAndCastleCounts()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"olden-preview-test-{Guid.NewGuid():N}");
        string previewPath = Path.Combine(directory, "Preview.png");
        var template = new RmgTemplate
        {
            Name = "Preview Test",
            Variants =
            [
                new Variant
                {
                    Zones =
                    [
                        new Zone { Name = "Neutral-A", Layout = "zone_layout_sides", MainObjects = [] },
                        new Zone { Name = "Neutral-B", Layout = "zone_layout_treasure_zone", MainObjects = Cities(3) },
                        new Zone { Name = "Neutral-C", Layout = "zone_layout_center", MainObjects = Cities(2) }
                    ],
                    Connections = []
                }
            ]
        };

        try
        {
            Directory.CreateDirectory(directory);
            RunOnStaThread(() => TemplatePreviewPngWriter.Save(template, previewPath));
            BitmapSource bitmap = LoadBitmap(previewPath);

            AssertColorNear(Color.FromRgb(28, 22, 16), PixelAt(bitmap, 256, 72));   // background of map
            AssertColorNear(Color.FromRgb(192, 192, 192), PixelAt(bitmap, 598, 534));   // border of circle 3
            AssertColorNear(Color.FromRgb(220, 220, 200), PixelAt(bitmap, 93, 502));    // house icon

            int lowLabelPixels = CountBrightPixels(bitmap, new Int32Rect(244, 78, 24, 20));     // sample of the background
            int castleLabelPixels = CountBrightPixels(bitmap, new Int32Rect(560, 490, 24, 20)); // sample near circle 3
            Assert.True(castleLabelPixels > lowLabelPixels + 10);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
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

    private static List<MainObject> Cities(int count) =>
        Enumerable.Range(0, count).Select(_ => new MainObject { Type = "City" }).ToList();

    private static void RunOnStaThread(Action action)
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (thrown is not null)
            throw new InvalidOperationException("The STA action failed.", thrown);
    }

    private static BitmapSource LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        BitmapSource source = BitmapDecoder
            .Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
            .Frames[0];

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static Color PixelAt(BitmapSource bitmap, int x, int y)
    {
        byte[] pixels = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
        return Color.FromRgb(pixels[2], pixels[1], pixels[0]);
    }

    private static int CountBrightPixels(BitmapSource bitmap, Int32Rect rect)
    {
        int stride = rect.Width * 4;
        byte[] pixels = new byte[stride * rect.Height];
        bitmap.CopyPixels(rect, pixels, stride, 0);

        int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte blue = pixels[i];
            byte green = pixels[i + 1];
            byte red = pixels[i + 2];
            if (red >= 180 && green >= 180 && blue >= 180)
                count++;
        }

        return count;
    }

    private static void AssertColorNear(Color expected, Color actual, int tolerance = 8)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= tolerance &&
            Math.Abs(expected.G - actual.G) <= tolerance &&
            Math.Abs(expected.B - actual.B) <= tolerance,
            $"Expected color near RGB({expected.R}, {expected.G}, {expected.B}), got RGB({actual.R}, {actual.G}, {actual.B}).");
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

    private static List<string> RoadConnectionNames(Zone zone)
    {
        var names = new List<string>();
        foreach (Road road in zone.Roads ?? [])
        {
            AddConnectionName(road.From);
            AddConnectionName(road.To);
        }

        return names;

        void AddConnectionName(RoadEndpoint? endpoint)
        {
            if (endpoint?.Type == "Connection" && endpoint.Args is { Count: > 0 })
                names.Add(endpoint.Args[0]);
        }
    }

    private static List<List<string>> RingNeutralGapsBetweenPlayers(List<string> sequence)
    {
        var playerPositions = sequence
            .Select((zoneName, index) => (zoneName, index))
            .Where(item => item.zoneName.StartsWith("Spawn-", StringComparison.Ordinal))
            .ToList();

        var gaps = new List<List<string>>();
        for (int i = 0; i < playerPositions.Count; i++)
        {
            int start = playerPositions[i].index;
            int end = playerPositions[(i + 1) % playerPositions.Count].index;
            var gap = new List<string>();

            for (int cursor = (start + 1) % sequence.Count; cursor != end; cursor = (cursor + 1) % sequence.Count)
                gap.Add(sequence[cursor]);

            gaps.Add(gap);
        }

        return gaps;
    }

    private static Dictionary<string, int> RingPlayerDistanceTotals(List<string> sequence)
    {
        var playerPositions = sequence
            .Select((zoneName, index) => (zoneName, index))
            .Where(item => item.zoneName.StartsWith("Spawn-", StringComparison.Ordinal))
            .ToList();

        return playerPositions.ToDictionary(
            player => player.zoneName,
            player => playerPositions
                .Where(other => other.zoneName != player.zoneName)
                .Sum(other =>
                {
                    int clockwise = Math.Abs(player.index - other.index);
                    return Math.Min(clockwise, sequence.Count - clockwise);
                }),
            StringComparer.Ordinal);
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
