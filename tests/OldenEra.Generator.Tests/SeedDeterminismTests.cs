using System.Text.Json;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SeedDeterminismTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static GeneratorSettings BuildBaseSettings(int? seed) => new()
    {
        TemplateName = "SeedTest",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Random,
        Seed = seed,
    };

    [Fact]
    public void SameSeed_Produces_Identical_Json()
    {
        var a = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
        var b = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_Produce_Different_Json()
    {
        var a = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(42)), Opts);
        var b = JsonSerializer.Serialize(TemplateGenerator.Generate(BuildBaseSettings(43)), Opts);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NullSeed_Still_Generates_Without_Error()
    {
        var t = TemplateGenerator.Generate(BuildBaseSettings(null));
        Assert.NotNull(t);
    }
}
