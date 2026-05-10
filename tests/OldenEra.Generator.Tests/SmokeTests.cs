using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class SmokeTests
{
    [Fact]
    public void TemplateGenerator_DefaultSettings_ProducesPlayerSpawnZonesAndConnections()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 2
            },
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template);
        Variant variant = Assert.Single(template.Variants ?? []);
        Assert.NotNull(variant.Zones);
        Assert.NotNull(variant.Connections);

        // One spawn zone per player plus the requested neutral zones.
        Assert.Equal(4, variant.Zones.Count(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)));
        Assert.Equal(2, variant.Zones.Count(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)));

        // Connections must reference real zones in the variant.
        var zoneNames = variant.Zones.Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(variant.Connections);
        Assert.All(variant.Connections, connection =>
        {
            Assert.Contains(connection.From, zoneNames);
            Assert.Contains(connection.To, zoneNames);
        });
    }
}
