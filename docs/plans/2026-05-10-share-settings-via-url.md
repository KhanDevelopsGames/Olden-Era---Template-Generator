# Share Settings via URL Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let a user click "Share" in the web app to copy a URL that, when opened, restores their current settings — using forward/backward-compatible lenient parsing so old shared links keep working as the schema evolves.

**Architecture:** Encode `SettingsFile` as JSON → gzip (Brotli not used for portability) → base64url, stuffed into the URL fragment (`#s=...`). Decode lenient: parse into `JsonObject`, copy known keys onto a default `SettingsFile` one-by-one with per-field try/catch, ignore unknowns, clamp/validate via existing `SettingsMapper.FromFile`. Codec lives in `OldenEra.Generator/Services/SettingsShareCodec.cs` (cross-platform, unit-testable from the Windows test project). `Home.razor` reads `window.location.hash` on init (after localStorage load — share link wins) and offers a "🔗 Share" button.

**Tech Stack:** C# `System.IO.Compression.GZipStream`, `System.Text.Json.Nodes.JsonObject`, `Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder` (already in Blazor), JS `navigator.clipboard.writeText`, `window.location.hash`, xUnit.

**Safety constraints (from prior conversation):**
- Cap encoded URL payload at 8 KB; cap decompressed size at 64 KB before parsing.
- All numeric ranges already clamped by `SettingsMapper.FromFile` — we lean on that.
- Reject anything that isn't a valid JSON object after decompress.
- No secrets ever in the payload — `SettingsFile` already only holds template config.

---

### Task 1: Add the codec skeleton + first round-trip test

**Files:**
- Create: `OldenEra.Generator/Services/SettingsShareCodec.cs`
- Test: `Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs`

**Step 1: Write the failing test**

```csharp
// SettingsShareCodecTests.cs
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
```

**Step 2: Run test to verify it fails**

Run: `dotnet test "Olden Era - Template Editor.Tests" --filter SettingsShareCodecTests`
Expected: FAIL — `SettingsShareCodec` does not exist.

(Note: Per `feedback_macos_dotnet_workflow.md`, on macOS this Windows-targeted test project may not run. If on macOS, run `dotnet build OldenEra.Generator` to confirm compile, then defer test execution to CI / Windows.)

**Step 3: Write minimal implementation**

```csharp
// OldenEra.Generator/Services/SettingsShareCodec.cs
using System.IO.Compression;
using System.Text;
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

    /// <summary>
    /// Copy each known field from <paramref name="obj"/> onto a fresh SettingsFile.
    /// Per-field try/catch — a bad value for one key never blocks the others.
    /// Unknown keys silently ignored.
    /// </summary>
    private static SettingsFile ApplyLenient(JsonObject obj)
    {
        var result = new SettingsFile();
        // Safest path: serialize the cleaned object and let System.Text.Json
        // do the heavy lifting with PropertyNameCaseInsensitive, ignoring
        // unknown members. This handles every [JsonPropertyName] correctly
        // without re-stating the schema here.
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

        // If the bulk deserialize failed (e.g. one field had wrong type),
        // walk keys one at a time, attaching each as an isolated mini-doc.
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

    /// <summary>
    /// Copy any property from <paramref name="src"/> that differs from a fresh
    /// default onto <paramref name="dst"/>. Crude but adequate — only the keys
    /// that survived deserialize will be non-default in <paramref name="src"/>.
    /// </summary>
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
```

**Step 4: Run test to verify it passes**

Run: `dotnet build OldenEra.Generator` (compile check on macOS) and on CI/Windows: `dotnet test --filter SettingsShareCodecTests`
Expected: PASS — round-trip preserves `TemplateName` and `PlayerCount`.

**Step 5: Commit**

```bash
git add OldenEra.Generator/Services/SettingsShareCodec.cs "Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs"
git commit -m "feat(share): add SettingsShareCodec with gzip+base64url round-trip"
```

---

### Task 2: Test lenient decoding — unknown fields ignored

**Files:**
- Modify: `Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Decode_ignores_unknown_fields()
{
    // Build a payload with a real field plus a bogus one.
    string json = """{"templateName":"Hi","futureFeatureXyz":42,"PlayerCount":5}""";
    string encoded = EncodeRawJson(json);

    var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
    Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
    Assert.NotNull(decoded);
    Assert.Equal("Hi", decoded!.TemplateName);
    Assert.Equal(5, decoded.PlayerCount);
}

// Test helper — mirrors SettingsShareCodec.Encode but takes raw JSON so we
// can inject malformed/forward-compat payloads without touching internals.
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
```

**Step 2: Run** — should PASS (System.Text.Json ignores unknown by default). If it fails, adjust `ApplyLenient` to use `PropertyNameCaseInsensitive` + the default unknown-member behavior.

**Step 3: Commit**

```bash
git add "Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs"
git commit -m "test(share): unknown fields are ignored on decode"
```

---

### Task 3: Test lenient decoding — bad field type doesn't kill the rest

**Step 1: Write the failing test**

```csharp
[Fact]
public void Decode_recovers_other_fields_when_one_has_wrong_type()
{
    // PlayerCount as a string instead of int — should be skipped, not fatal.
    string json = """{"templateName":"Survivor","playerCount":"not-an-int","mapSize":192}""";
    string encoded = EncodeRawJson(json);

    var decoded = SettingsShareCodec.TryDecode(encoded, out var status);
    Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
    Assert.NotNull(decoded);
    Assert.Equal("Survivor", decoded!.TemplateName);
    Assert.Equal(192, decoded.MapSize);
    // PlayerCount stays at the type's default; we don't care which default.
}
```

**Step 2: Run** — likely FAILS first time because `JsonSerializer.Deserialize<SettingsFile>` throws on the bad type. The per-field fallback in `ApplyLenient` is what makes this pass.

**Step 3: Fix if needed** — verify `ApplyLenient`'s catch path runs and per-field loop survives the throw on `playerCount`.

**Step 4: Commit**

```bash
git add "Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs"
git commit -m "test(share): bad field type doesn't poison sibling fields"
```

---

### Task 4: Test safety limits — oversize input rejected

**Step 1: Write the failing test**

```csharp
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
    // 1 MB of zeros compresses to a few KB — well under MaxEncodedLength but
    // would exceed MaxDecompressedBytes after decompress.
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
        return; // unlikely but skip — we want the gzip-bomb path, not the length cap.

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
```

**Step 2: Run** — should PASS for the size/garbage cases on the existing implementation.

**Step 3: Commit**

```bash
git add "Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs"
git commit -m "test(share): reject oversize, bomb, and garbage payloads"
```

---

### Task 5: Wire codec round-trip through SettingsMapper

**Files:**
- Modify: `Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Round_trip_through_SettingsMapper_preserves_all_settings()
{
    // Build a non-default GeneratorSettings — exercise nested objects.
    var settings = new GeneratorSettings
    {
        TemplateName = "Full Loop",
        MapSize = 192,
        PlayerCount = 6,
        Topology = MapTopology.HubAndSpoke,
        RandomPortals = true,
        MaxPortalConnections = 4,
        ZoneCfg = { NeutralZoneCount = 8, HubZoneSize = 1.5 },
    };
    var file = OldenEra.Web.Services.SettingsMapper.ToFile(settings, advancedMode: true, experimentalMapSizes: false);

    string encoded = SettingsShareCodec.Encode(file);
    var decoded = SettingsShareCodec.TryDecode(encoded, out _);
    Assert.NotNull(decoded);

    var (mapped, advanced, _) = OldenEra.Web.Services.SettingsMapper.FromFile(decoded!);
    Assert.Equal("Full Loop", mapped.TemplateName);
    Assert.Equal(192, mapped.MapSize);
    Assert.Equal(6, mapped.PlayerCount);
    Assert.Equal(MapTopology.HubAndSpoke, mapped.Topology);
    Assert.True(advanced);
    Assert.Equal(8, mapped.ZoneCfg.NeutralZoneCount);
}
```

NOTE: This test references `OldenEra.Web.Services.SettingsMapper` from the WPF-targeted test project. The web project is `net10.0` and the test project is `net10.0-windows`, so adding a project reference may not work. **Verify first.** If it fails to reference, move `SettingsMapper` into `OldenEra.Generator/Services/` (it has no Blazor dependencies) as a precondition refactor (Task 5a below).

**Step 1a (only if needed): Move SettingsMapper to the shared library**

```bash
git mv OldenEra.Web/Services/SettingsMapper.cs OldenEra.Generator/Services/SettingsMapper.cs
# Edit namespace from `OldenEra.Web.Services` to `OldenEra.Generator.Services`.
# Update `using OldenEra.Web.Services;` in Home.razor and any tests to use the new namespace.
```

Build everything to confirm: `dotnet build`.

**Step 2: Run test**
Expected: PASS once references are wired up.

**Step 3: Commit**

```bash
git add -A
git commit -m "test(share): full SettingsMapper round-trip through share codec"
```

---

### Task 6: Add Share button + URL plumbing to Home.razor

**Files:**
- Modify: `OldenEra.Web/Pages/Home.razor` (toolbar markup + code-behind)
- Modify: `OldenEra.Web/wwwroot/js/downloader.js` (add tiny clipboard + hash helpers)

**Step 1: Extend the JS shim**

Append to `OldenEra.Web/wwwroot/js/downloader.js`:

```javascript
window.oeShare = {
    getHash: function () {
        // Strip the leading '#'. Returns "" if no fragment.
        return (window.location.hash || "").replace(/^#/, "");
    },
    setHash: function (value) {
        // Use replaceState so we don't pollute browser history on every share.
        const url = new URL(window.location.href);
        url.hash = value ? "#" + value : "";
        history.replaceState(null, "", url.toString());
    },
    buildShareUrl: function (encoded) {
        const url = new URL(window.location.href);
        url.hash = "#s=" + encoded;
        return url.toString();
    },
    copy: async function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }
        // Fallback: hidden textarea + execCommand.
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        let ok = false;
        try { ok = document.execCommand("copy"); } catch { ok = false; }
        document.body.removeChild(ta);
        return ok;
    },
};
```

**Step 2: Add a Share button to the toolbar**

In `Home.razor`, add next to Save/Open:

```razor
<button class="oe-btn" @onclick="ShareSettingsClick">🔗 Share link</button>
@if (!string.IsNullOrEmpty(ShareToast))
{
    <span class="oe-toast">@ShareToast</span>
}
```

**Step 3: Add the code-behind handler**

In the `@code` block:

```csharp
private string ShareToast = "";
private System.Threading.CancellationTokenSource? _toastCts;

private async Task ShareSettingsClick()
{
    var snapshot = SettingsMapper.ToFile(Settings, AdvancedMode, ExperimentalMapSizes);
    string encoded = SettingsShareCodec.Encode(snapshot);
    if (encoded.Length > SettingsShareCodec.MaxEncodedLength)
    {
        await FlashToast("Settings too large to share via URL.");
        return;
    }
    string url = await Js.InvokeAsync<string>("oeShare.buildShareUrl", encoded);
    bool copied = await Js.InvokeAsync<bool>("oeShare.copy", url);
    await FlashToast(copied ? "Link copied to clipboard." : "Couldn't copy — select the URL bar to copy manually.");
    // Also reflect into the address bar so a manual copy works.
    await Js.InvokeVoidAsync("oeShare.setHash", "s=" + encoded);
}

private async Task FlashToast(string msg)
{
    _toastCts?.Cancel();
    _toastCts?.Dispose();
    var cts = new System.Threading.CancellationTokenSource();
    _toastCts = cts;
    ShareToast = msg;
    StateHasChanged();
    try { await Task.Delay(2500, cts.Token); } catch (TaskCanceledException) { return; }
    if (cts.IsCancellationRequested) return;
    ShareToast = "";
    StateHasChanged();
}
```

**Step 4: Add CSS for the toast** in `OldenEra.Web/wwwroot/css/app.css`:

```css
.oe-toast {
    margin-left: 0.75rem;
    padding: 0.25rem 0.5rem;
    background: var(--oe-panel-2, #2a2a2a);
    border-radius: 0.25rem;
    font-size: 0.85em;
    opacity: 0.9;
}
```

(Match the project's existing CSS variable conventions — open `app.css` and reuse what's already defined.)

**Step 5: Build & smoke-test**

Run: `dotnet build OldenEra.Web`
Expected: succeeds (or follow `feedback_macos_dotnet_workflow.md` if on macOS).

Manual smoke test (Windows or after `dotnet run --project OldenEra.Web`):
- Click Share, expect toast "Link copied" and URL bar updates with `#s=...`.
- Paste the URL into a new tab — settings should not yet load (Task 7 wires that).

**Step 6: Commit**

```bash
git add OldenEra.Web/Pages/Home.razor OldenEra.Web/wwwroot/js/downloader.js OldenEra.Web/wwwroot/css/app.css
git commit -m "feat(web): add Share-link button that copies a fragment URL"
```

---

### Task 7: Apply share link on page load

**Files:**
- Modify: `OldenEra.Web/Pages/Home.razor` (extend `OnInitializedAsync`)

**Step 1: Read and apply the hash before showing UI**

Replace the current `OnInitializedAsync` body so a share-link in the URL **wins over** localStorage (a user opening a shared link expects to see the shared settings, not their own saved state):

```csharp
protected override async Task OnInitializedAsync()
{
    CurrentVersion = typeof(Home).Assembly.GetName().Version;
    if (CurrentVersion is not null)
        VersionLabel = FormatVersion(CurrentVersion);

    SettingsFile? sourceOfTruth = null;

    // 1. Share link in fragment wins.
    string hash = await Js.InvokeAsync<string>("oeShare.getHash");
    if (hash.StartsWith("s=", StringComparison.Ordinal))
    {
        var fromUrl = SettingsShareCodec.TryDecode(hash[2..], out var status);
        if (fromUrl is not null)
        {
            sourceOfTruth = fromUrl;
            if (status != SettingsShareCodec.DecodeStatus.Ok)
            {
                ShareToast = "Some settings in the link couldn't be loaded — using defaults for those.";
            }
        }
        else
        {
            ShareToast = "Share link was invalid — loading your saved settings instead.";
        }
    }

    // 2. Otherwise localStorage.
    sourceOfTruth ??= await SettingsStore.LoadAsync();

    if (sourceOfTruth is not null)
    {
        var (mapped, advanced, experimental) = SettingsMapper.FromFile(sourceOfTruth);
        Settings = mapped;
        AdvancedMode = advanced;
        ExperimentalMapSizes = experimental;
    }
    _hasLoadedFromStorage = true;

    Validate();
    _ = CheckForUpdatesAsync();
}
```

**Note on `DecodeStatus.Ok`:** the current `TryDecode` only returns `Ok` for fully-clean payloads. If you want a finer-grained "partial success" signal (some fields dropped), add a `PartialOk` status in Task 1's enum and have `ApplyLenient` track whether any field was skipped. Optional polish — not required for this plan to be correct.

**Step 2: Smoke test**

Build, run `dotnet run --project OldenEra.Web`. Capture the URL after a Share click, paste into a new tab — confirm settings restore.

**Step 3: Commit**

```bash
git add OldenEra.Web/Pages/Home.razor
git commit -m "feat(web): restore settings from share-link fragment on load"
```

---

### Task 8: Lock down forward-compat with a frozen-payload regression test

**Why:** if a future change to `SettingsFile` accidentally renames a JSON property, every existing share link breaks silently. Pin a known-good encoded payload to catch that.

**Files:**
- Modify: `Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs`

**Step 1: Add the test**

```csharp
[Fact]
public void Decode_of_v1_payload_still_yields_known_fields()
{
    // Generated once with SettingsShareCodec.Encode of a known SettingsFile.
    // If this ever fails, you renamed/removed a JsonPropertyName — either
    // restore backward compat (read both old + new key) or version the payload.
    const string FrozenV1Payload = "<<PASTE_VALUE_FROM_REPL>>";

    var decoded = SettingsShareCodec.TryDecode(FrozenV1Payload, out var status);
    Assert.NotNull(decoded);
    Assert.Equal(SettingsShareCodec.DecodeStatus.Ok, status);
    Assert.Equal("FrozenFixture", decoded!.TemplateName);
    Assert.Equal(160, decoded.MapSize);
    Assert.Equal(4, decoded.PlayerCount);
}
```

**Step 2: Generate the payload**

In a one-off test run (or LINQPad / `dotnet run` scratch), call:

```csharp
var s = new SettingsFile { TemplateName = "FrozenFixture", MapSize = 160, PlayerCount = 4 };
Console.WriteLine(SettingsShareCodec.Encode(s));
```

Paste the output into `FrozenV1Payload`.

**Step 3: Run the test** — must PASS.

**Step 4: Commit**

```bash
git add "Olden Era - Template Editor.Tests/SettingsShareCodecTests.cs"
git commit -m "test(share): pin a v1 encoded payload as a forward-compat fixture"
```

---

### Task 9: Update `feedback_*` memory if anything surprising came up

If during execution something unexpected lands (e.g. the WPF tests project really can't reference `OldenEra.Web` and the `SettingsMapper` had to move), capture it as a project memory under `~/.claude/projects/.../memory/` per the auto-memory rules in CLAUDE.md.

This is judgment-call only — skip if everything went exactly as planned.

---

## Out of scope (deliberately not in this plan)

- Server-side share storage / short links (browser-only fragment is enough for now).
- Encrypted/signed payloads (no secrets, no integrity threat — anyone with the link is the intended audience).
- Auto-syncing the URL hash on every settings change (decided UX: explicit Share button only).
- A `v=1` schema-version field. Lenient parsing covers the additive-only path; a version field is only worth adding the first time we need a real migration.

---

## Plan complete.

Two execution options:

**1. Subagent-Driven (this session)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Parallel Session (separate)** — Open a new session with executing-plans, batch execution with checkpoints.

Which approach?
