using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace Olden_Era___Template_Editor.Tests;

public class SettingsShareCodecTests
{
    [Fact]
    public void Encode_then_decode_round_trips_default_settings()
    {
        var original = new SettingsFile { TemplateName = "RoundTrip", PlayerCount = 4 };
        string encoded = SettingsShareCodec.Encode(original);
        SettingsFile? decoded = SettingsShareCodec.TryDecode(encoded, out _);
        Assert.NotNull(decoded);
        Assert.Equal("RoundTrip", decoded!.TemplateName);
        Assert.Equal(4, decoded.PlayerCount);
    }

    [Fact]
    public void Decode_ignores_unknown_fields()
    {
        string json = """{"templateName":"Hi","futureFeatureXyz":42,"PlayerCount":5}""";
        string encoded = EncodeRawJson(json);

        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Equal("Hi", decoded!.TemplateName);
        Assert.Equal(5, decoded.PlayerCount);
    }

    [Fact]
    public void Decode_recovers_other_fields_when_one_has_wrong_type()
    {
        // playerCount as a string instead of int — should be skipped, not fatal.
        string json = """{"templateName":"Survivor","playerCount":"not-an-int","mapSize":192}""";
        string encoded = EncodeRawJson(json);

        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Equal("Survivor", decoded!.TemplateName);
        Assert.Equal(192, decoded.MapSize);
    }

    private static string EncodeRawJson(string json)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var ms = new System.IO.MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms,
            System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(bytes, 0, bytes.Length);
        }
        return System.Convert.ToBase64String(ms.ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
