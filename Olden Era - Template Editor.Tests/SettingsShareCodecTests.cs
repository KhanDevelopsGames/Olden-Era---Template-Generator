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
}
