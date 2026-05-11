using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SettingsShareCodecSeedTests
{
    [Fact]
    public void Seed_RoundTrips_Through_Encode_Decode()
    {
        var original = new SettingsFile { Seed = 987654 };
        string encoded = SettingsShareCodec.Encode(original);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Equal(987654, decoded!.Seed);
    }

    [Fact]
    public void NullSeed_RoundTrips_As_Null()
    {
        var original = new SettingsFile { Seed = null };
        string encoded = SettingsShareCodec.Encode(original);
        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.NotNull(decoded);
        Assert.Null(decoded!.Seed);
    }
}
