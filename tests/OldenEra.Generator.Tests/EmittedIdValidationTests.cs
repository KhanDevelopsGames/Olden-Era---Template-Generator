using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class EmittedIdValidationTests
{
    private static readonly GameDataCatalog Catalog =
        GameDataCatalog.LoadFromDirectory(RepoPaths.GeneratorDataRoot());

    [Fact]
    public void Catalog_LoadsNonEmpty_ForEachCategory()
    {
        Assert.NotEmpty(Catalog.Pools);
        Assert.NotEmpty(Catalog.ContentLists);
        Assert.NotEmpty(Catalog.ZoneLayouts);
        // Encounter templates may legitimately be empty if the folder ships no name-keyed arrays;
        // assert known IDs the generator depends on instead of a non-empty count.
    }

    [Fact]
    public void Catalog_ContainsKnownStartZoneResourcePoolIds()
    {
        // These are the literals at TemplateGenerator.cs (search "content_pool_general_resources_start_zone").
        // A typo here would silently produce broken templates — guard explicitly.
        Assert.Contains("content_pool_general_resources_start_zone_poor", Catalog.Pools);
        Assert.Contains("content_pool_general_resources_start_zone_medium", Catalog.Pools);
        Assert.Contains("content_pool_general_resources_start_zone_rich", Catalog.Pools);
    }

    [Fact]
    public void Catalog_ContainsKnownZoneLayoutIds()
    {
        Assert.Contains("zone_layout_default", Catalog.ZoneLayouts);
    }

    public static IEnumerable<object[]> SettingsMatrix()
    {
        var topologies = new[]
        {
            MapTopology.Default,
            MapTopology.HubAndSpoke,
            MapTopology.Chain,
            MapTopology.Random,
        };
        var playerCounts = new[] { 2, 4, 8 };

        foreach (var topology in topologies)
        foreach (int players in playerCounts)
        {
            yield return new object[] { topology, players, false };
        }

        // Tournament variant requires exactly 2 players.
        yield return new object[] { MapTopology.Default, 2, true };
    }

    [Theory]
    [MemberData(nameof(SettingsMatrix))]
    public void EmittedZoneIds_AllExistInShippedGameData(
        MapTopology topology, int playerCount, bool tournament)
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = playerCount,
            Topology = topology,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 4,
            },
        };
        if (tournament)
        {
            settings.TournamentRules.Enabled = true;
            settings.GameEndConditions.VictoryCondition = "win_condition_6";
        }

        RmgTemplate template = TemplateGenerator.Generate(settings);

        // Some zone layouts (zone_layout_spawns/sides/treasure_zone/center) are defined
        // inline on the template by BuildZoneLayouts, not shipped under GameData. Treat
        // those as locally valid for the layout check.
        var inlineLayoutIds = (template.ZoneLayouts ?? [])
            .Select(l => l.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);

        var failures = new List<string>();
        foreach (var variant in template.Variants ?? [])
        {
            foreach (var zone in variant.Zones ?? [])
            {
                if (!string.IsNullOrEmpty(zone.Layout)
                    && !inlineLayoutIds.Contains(zone.Layout)
                    && !Catalog.Contains(GameDataCategory.ZoneLayout, zone.Layout))
                {
                    failures.Add($"{zone.Name}.layout = '{zone.Layout}' (not shipped, not defined inline)");
                }
                CheckIds(zone.GuardedContentPool, GameDataCategory.ContentPool, zone.Name, nameof(zone.GuardedContentPool), failures);
                CheckIds(zone.UnguardedContentPool, GameDataCategory.ContentPool, zone.Name, nameof(zone.UnguardedContentPool), failures);
                CheckIds(zone.ResourcesContentPool, GameDataCategory.ContentPool, zone.Name, nameof(zone.ResourcesContentPool), failures);
            }
        }

        // Template-level mandatoryContent groups embed includeLists referencing content_lists/.
        foreach (var group in template.MandatoryContent ?? [])
        {
            foreach (var item in group.Content ?? [])
            {
                CheckIds(item.IncludeLists, GameDataCategory.ContentList, $"mandatory:{group.Name}", nameof(item.IncludeLists), failures);
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail(
                $"Emitted IDs not found in shipped GameData (settings: topology={topology}, players={playerCount}, tournament={tournament}):\n  - "
                + string.Join("\n  - ", failures));
        }
    }

    private static void CheckIds(IEnumerable<string>? ids, GameDataCategory category, string ownerName, string fieldName, List<string> failures)
    {
        if (ids is null) return;
        foreach (string id in ids)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (!Catalog.Contains(category, id))
            {
                failures.Add($"{ownerName}.{fieldName} = '{id}' (category {category})");
            }
        }
    }
}
