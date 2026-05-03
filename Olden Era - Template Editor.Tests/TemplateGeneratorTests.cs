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
            VictoryCondition = "win_condition_2",
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
}
