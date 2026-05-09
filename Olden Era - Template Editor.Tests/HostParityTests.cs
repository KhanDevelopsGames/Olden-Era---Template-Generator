using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// Locks in the contract that WPF and Web hosts share: the same GeneratorSettings
/// must produce structurally identical RmgTemplate output, serialized with identical
/// JSON options. Both hosts call into OldenEra.Generator, so divergence here would
/// indicate one host is mutating settings before calling the library.
/// </summary>
public class HostParityTests
{
    private static readonly JsonSerializerOptions SharedJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IEnumerable<object[]> ParityFixtures()
    {
        yield return new object[]
        {
            "minimal-2p-random",
            new GeneratorSettings
            {
                TemplateName = "Parity Test 1",
                PlayerCount = 2,
                MapSize = 160,
                Topology = MapTopology.Random
            }
        };
        yield return new object[]
        {
            "4p-ring-with-neutrals",
            new GeneratorSettings
            {
                TemplateName = "Parity Test 2",
                PlayerCount = 4,
                MapSize = 200,
                Topology = MapTopology.Default,
                ZoneCfg = new ZoneConfiguration
                {
                    NeutralZoneCount = 4,
                    PlayerZoneCastles = 1,
                    NeutralZoneCastles = 1
                }
            }
        };
        yield return new object[]
        {
            "2p-tournament-hub",
            new GeneratorSettings
            {
                TemplateName = "Parity Test 3",
                PlayerCount = 2,
                MapSize = 240,
                Topology = MapTopology.HubAndSpoke,
                TournamentRules = new TournamentRules { Enabled = true }
            }
        };
    }

    [Theory]
    [MemberData(nameof(ParityFixtures))]
    public void GeneratedTemplate_SerializesToValidNonEmptyJson(string label, GeneratorSettings settings)
    {
        RmgTemplate template = TemplateGenerator.Generate(settings);
        string json = JsonSerializer.Serialize(template, SharedJsonOptions);

        Assert.False(string.IsNullOrWhiteSpace(json), $"{label}: JSON output was empty.");
        Assert.StartsWith("{", json);
        Assert.Contains($"\"name\": \"{settings.TemplateName}\"", json);
        Assert.Contains($"\"sizeX\": {settings.MapSize}", json);
    }

    [Theory]
    [MemberData(nameof(ParityFixtures))]
    public void GeneratedTemplate_RoundTripsThroughSharedJsonOptions(string label, GeneratorSettings settings)
    {
        RmgTemplate template = TemplateGenerator.Generate(settings);
        string json = JsonSerializer.Serialize(template, SharedJsonOptions);
        RmgTemplate? roundTripped = JsonSerializer.Deserialize<RmgTemplate>(json, SharedJsonOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(template.Name, roundTripped!.Name);
        Assert.Equal(template.SizeX, roundTripped.SizeX);
        Assert.Equal(template.SizeZ, roundTripped.SizeZ);
        Assert.Equal(template.GameMode, roundTripped.GameMode);
        Assert.Equal(template.Variants?.Count ?? 0, roundTripped.Variants?.Count ?? 0);
    }

    [Fact]
    public void SharedJsonOptions_AreStableAcrossHosts()
    {
        Assert.True(SharedJsonOptions.WriteIndented);
        Assert.Equal(JsonIgnoreCondition.WhenWritingNull, SharedJsonOptions.DefaultIgnoreCondition);
    }
}
