using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

/// <summary>
/// Asserts the generator can produce shipped-template-like *shapes*. Most shipped
/// templates use hand-authored zone names ("Center", "Side-C", "Treasure-1") that
/// the generator cannot reproduce — it emits "Spawn-A.." and "Neutral-E..". So
/// these tests compare structural fingerprints (zone count, win condition flags,
/// hold-city presence), not zone names.
///
/// Templates that use a hand-authored shape we cannot match are documented as
/// Skip facts with the reason; revisit if the generator ever supports custom
/// zone-naming.
/// </summary>
public class ReferenceTemplateParityTests
{
    private static readonly string ExampleTemplatesDir = Path.Combine(
        RepoPaths.GeneratorDataRoot(), "..", "ExampleTemplates");

    [Fact]
    public void JebusCross_HubAndSpoke_CityHold_ShapeMatches()
    {
        var reference = LoadTemplate("Jebus Cross.rmg.json");

        // Jebus Cross: HubAndSpoke-shaped layout (1 hub + N players + side neutrals),
        // win_condition_5 (City Hold).
        Assert.Equal("win_condition_5", reference.DisplayWinCondition);

        // Reproduce the *generator's* approximation: HubAndSpoke + CityHold + 2 players + 2 neutrals.
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 2,
            MapSize = reference.SizeX,
            Topology = MapTopology.HubAndSpoke,
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_5",
                CityHold = true,
            },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 2 },
        };
        var emitted = TemplateGenerator.Generate(settings);

        // Same map size.
        Assert.Equal(reference.SizeX, emitted.SizeX);
        // CityHold should be active in both.
        Assert.True(emitted.GameRules?.WinConditions?.CityHold ?? false);
        // Same number of variant zones (1 hub + 2 spawns + 2 neutrals = 5; reference is also 5).
        var refZones = reference.Variants![0].Zones!;
        var emZones = emitted.Variants![0].Zones!;
        Assert.Equal(refZones.Count, emZones.Count);
        // Hub is the hold city in HubAndSpoke + CityHold.
        var hub = emZones.Single(z => z.Name == "Hub");
        Assert.Contains(hub.MainObjects ?? [], o => o.HoldCityWinCon == true);
    }

    [Fact(Skip = "Anarchy uses hand-authored zone names (Treasure-N, SuperTreasure-N, Center) " +
                 "that the generator cannot reproduce. Revisit if/when the generator supports " +
                 "custom zone naming.")]
    public void Anarchy_RandomTopology_ManyNeutrals_ShapeMatches() { }

    [Fact(Skip = "Hallway uses paired Spawn-{letter}/Side-{letter} zones plus Hallway-{n}/Treasure-{n} " +
                 "neutrals. The Chain topology generator emits Spawn-/Neutral- only and cannot match.")]
    public void Hallway_ChainTopology_ShapeMatches() { }

    [Fact(Skip = "Diamond uses 8 Spawn-{letter} + 8 Treasure-{n} hand-authored zones. " +
                 "Generator emits Neutral-{letter} suffix; cannot match without naming support.")]
    public void Diamond_DefaultTopology_ShapeMatches() { }

    private static RmgTemplate LoadTemplate(string fileName)
    {
        string path = Path.Combine(ExampleTemplatesDir, fileName);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<RmgTemplate>(stream, options)
            ?? throw new InvalidOperationException($"Failed to deserialize {path}");
    }
}
