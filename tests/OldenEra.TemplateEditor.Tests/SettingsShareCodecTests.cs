using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Tests;

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

    [Fact]
    public void Decode_rejects_payload_over_max_encoded_length()
    {
        string oversize = new string('A', SettingsShareCodec.MaxEncodedLength + 1);
        var decoded = SettingsShareCodec.TryDecode(oversize, out var status);
        Assert.Null(decoded);
        Assert.Equal(SettingsShareCodec.DecodeStatus.TooLarge, status);
    }

    [Fact]
    public void Decode_rejects_compression_bomb()
    {
        byte[] bigJsonish = new byte[1024 * 1024];
        Array.Fill(bigJsonish, (byte)' ');
        using var ms = new System.IO.MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms,
            System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(bigJsonish, 0, bigJsonish.Length);
        }
        string encoded = System.Convert.ToBase64String(ms.ToArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (encoded.Length > SettingsShareCodec.MaxEncodedLength)
            return; // unlikely; we want the gzip-bomb path, not the length cap.

        var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
        Assert.Null(decoded);
        Assert.Equal(SettingsShareCodec.DecodeStatus.MalformedGzip, status);
    }

    [Fact]
    public void Decode_rejects_garbage_string()
    {
        var decoded = SettingsShareCodec.TryDecode("!!!not-valid-base64!!!", out var status);
        Assert.Null(decoded);
        Assert.Equal(SettingsShareCodec.DecodeStatus.MalformedBase64, status);
    }

    [Fact]
    public void Round_trip_through_SettingsMapper_preserves_all_settings()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Full Loop",
            MapSize = 192,
            PlayerCount = 6,
            Topology = MapTopology.HubAndSpoke,
            RandomPortals = true,
            MaxPortalConnections = 4,
        };
        settings.ZoneCfg.NeutralZoneCount = 8;
        settings.ZoneCfg.HubZoneSize = 1.5;

        var file = SettingsMapper.ToFile(settings, advancedMode: true, experimentalMapSizes: false);

        string encoded = SettingsShareCodec.Encode(file);
        var decoded = SettingsShareCodec.TryDecode(encoded, out _);
        Assert.NotNull(decoded);

        var (mapped, advanced, _, _) = SettingsMapper.FromFile(decoded!);
        Assert.Equal("Full Loop", mapped.TemplateName);
        Assert.Equal(192, mapped.MapSize);
        Assert.Equal(6, mapped.PlayerCount);
        Assert.Equal(MapTopology.HubAndSpoke, mapped.Topology);
        Assert.True(advanced);
        Assert.Equal(8, mapped.ZoneCfg.NeutralZoneCount);
    }

    [Fact]
    public void Decode_of_v1_payload_still_yields_known_fields()
    {
        // Pinned encoded payload of: new SettingsFile { TemplateName = "FrozenFixture", MapSize = 160, PlayerCount = 4 }.
        // If this ever fails, you renamed/removed a [JsonPropertyName] — restore
        // backward compat (read both old + new key) or version the payload.
        const string FrozenV1Payload = "H4sIAAAAAAAAE21TXW_aQBD8L_eMKkjStOItISVECgiFSJX6Em3sDZxyvrX21oCp-t-7PoM_IG--2dm9mbn1XyOY5Q4EF5ChGZsp0wH91O6lYDQDk0G-sgetjG6HA6PMEnlChRczvhkYj4UwuD_k8Qi2JAjiMGhjQ-tCkG7BJ5jOKdXhH-ACNrxn2i2oJseBLXwJzjG1RfYVva5c4jO73nzFr_AOmoEkm2XHyhQSseRDozazftH6D_coO0Rft4Q4A_c5ss3QC7h7cNFwxVVOghXczOoy53Xk7UV1oFXj8Sl6wTfYpng_PzeJq5h1AZy-gE8psweorCj8bfhdicg0tz6-aPyGvRn_rL-ffMJHqTpSKCdH6zJSOc5aEqvmTiqwr6EJ-Sqs66uBCTns_JRINuRSxYQLZa7RI-vmvRC0oA1UbWN-SrG_GSuB5HMljH4tG1U0VF_vxCnyY-XurLK1iRCXKiS1tV-zs_4tOZ3fRrrgH_WzPsMu_Nrnx06NjaPRFnIURK9nmVgpG1099AG0cN0BZxpgQ02UMVP_F4C2qdFbDcRBakEl36kNaHh9uGI_oMYTb9D7hueM-CeetAgV7KG3ay00tRzktTnGntFNl_HkBXkLzox_dOElWS_hlX5XW3PVraxgi3eclfV7_vsPQr-4K2AEAAA";

        var decoded = SettingsShareCodec.TryDecode(FrozenV1Payload, out var status);
        Assert.NotNull(decoded);
        Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
        Assert.Equal("FrozenFixture", decoded!.TemplateName);
        Assert.Equal(160, decoded.MapSize);
        Assert.Equal(4, decoded.PlayerCount);
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
