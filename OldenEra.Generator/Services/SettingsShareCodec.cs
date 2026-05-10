using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using OldenEra.Generator.Models;

namespace OldenEra.Generator.Services;

/// <summary>
/// Encodes a SettingsFile into a compact, URL-fragment-safe string and decodes
/// it back leniently — unknown fields are dropped, missing fields keep defaults,
/// per-field parse failures don't poison the whole payload.
/// </summary>
public static class SettingsShareCodec
{
    /// <summary>Hard cap on the encoded string length (URL-safety).</summary>
    public const int MaxEncodedLength = 8 * 1024;

    /// <summary>Hard cap on the decompressed JSON size (zip-bomb defence).</summary>
    public const int MaxDecompressedBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Encode(SettingsFile settings)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(json, 0, json.Length);
        }
        return Base64UrlEncode(ms.ToArray());
    }

    public enum DecodeStatus { Ok, Empty, TooLarge, MalformedBase64, MalformedGzip, MalformedJson, NotAnObject }

    /// <summary>
    /// Lenient decode: returns a SettingsFile with every field that parsed,
    /// defaults for the rest. Returns null only on outright failure
    /// (empty input, oversize payload, corrupt envelope, non-object JSON).
    /// </summary>
    public static SettingsFile? TryDecode(string? encoded, out DecodeStatus status)
    {
        if (string.IsNullOrWhiteSpace(encoded)) { status = DecodeStatus.Empty; return null; }
        if (encoded.Length > MaxEncodedLength) { status = DecodeStatus.TooLarge; return null; }

        byte[] compressed;
        try { compressed = Base64UrlDecode(encoded); }
        catch { status = DecodeStatus.MalformedBase64; return null; }

        byte[] json;
        try { json = GzipDecompressBounded(compressed, MaxDecompressedBytes); }
        catch { status = DecodeStatus.MalformedGzip; return null; }

        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch { status = DecodeStatus.MalformedJson; return null; }

        if (node is not JsonObject obj) { status = DecodeStatus.NotAnObject; return null; }

        status = DecodeStatus.Ok;
        return ApplyLenient(obj);
    }

    private static SettingsFile ApplyLenient(JsonObject obj)
    {
        var result = new SettingsFile();
        // Safest path: let System.Text.Json handle [JsonPropertyName] mapping
        // and silently drop unknown members. If one bad value blows up the
        // whole deserialize, fall back to per-field recovery.
        try
        {
            var lenientOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var attempt = JsonSerializer.Deserialize<SettingsFile>(obj.ToJsonString(), lenientOptions);
            if (attempt is not null) return attempt;
        }
        catch
        {
            // Fall through to per-field recovery below.
        }

        foreach (var kvp in obj)
        {
            try
            {
                var single = new JsonObject { [kvp.Key] = kvp.Value?.DeepClone() };
                var partial = JsonSerializer.Deserialize<SettingsFile>(single.ToJsonString());
                if (partial is null) continue;
                CopyNonDefault(partial, result);
            }
            catch
            {
                // Skip this field, keep going.
            }
        }
        return result;
    }

    private static void CopyNonDefault(SettingsFile src, SettingsFile dst)
    {
        var defaults = new SettingsFile();
        foreach (var prop in typeof(SettingsFile).GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            object? srcValue = prop.GetValue(src);
            object? defValue = prop.GetValue(defaults);
            if (!Equals(srcValue, defValue))
            {
                prop.SetValue(dst, srcValue);
            }
        }
    }

    private static byte[] GzipDecompressBounded(byte[] compressed, int maxBytes)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int total = 0;
        int read;
        while ((read = gz.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new InvalidDataException("Decompressed payload exceeds limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        string padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: throw new FormatException("Invalid base64url length.");
        }
        return Convert.FromBase64String(padded);
    }
}
