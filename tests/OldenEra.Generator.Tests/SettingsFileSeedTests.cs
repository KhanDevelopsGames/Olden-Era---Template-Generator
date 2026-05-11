using System.Text.Json;
using OldenEra.Generator.Models;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsFileSeedTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Seed_RoundTrips_Through_Json()
    {
        var s = new SettingsFile { Seed = 12345 };
        var json = JsonSerializer.Serialize(s, Opts);
        var back = JsonSerializer.Deserialize<SettingsFile>(json, Opts)!;
        Assert.Equal(12345, back.Seed);
    }

    [Fact]
    public void Seed_Defaults_To_Null_And_Is_Omitted_From_Json()
    {
        var s = new SettingsFile();
        var json = JsonSerializer.Serialize(s, Opts);
        Assert.Null(s.Seed);
        Assert.DoesNotContain("\"seed\"", json);
    }
}
