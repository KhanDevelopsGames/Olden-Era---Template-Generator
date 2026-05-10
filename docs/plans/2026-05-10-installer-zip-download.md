# Installer-zip download for the web host — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the web host's two separate downloads (`.rmg.json` + PNG) with a single zip "Download map", and add a second "Download map w/ installer" button that bundles the same files plus a Windows install script and README.

**Architecture:** A new `InstallerPackager` service in `OldenEra.Web` builds zips in memory using `System.IO.Compression`. Three installer assets (`install.bat`, `install.ps1`, `README.txt`) ship as embedded resources under `OldenEra.Web/Resources/Installer/`. The packager substitutes `{TEMPLATE_NAME}` and `{STEAM_APP_ID}` placeholders. Steam app id and the `Heroes of Might and Magic Olden Era` folder name move to a new shared `OldenEraSteamInfo` constants class so the WPF detection code and the packaged script cannot drift. `Home.razor` loses the PNG button and rewires the JSON button to two zip-producing handlers.

**Tech Stack:** Blazor WebAssembly (`OldenEra.Web`, `net10.0`), shared library (`OldenEra.Generator`, `net10.0`), xUnit tests (`Olden Era - Template Editor.Tests`, `net10.0-windows`). PowerShell 5.1+ for the install script.

**Design doc:** `docs/plans/2026-05-10-installer-zip-download-design.md`.

**Working environment notes:**
- macOS dev box cannot run the WPF test project (`net10.0-windows`). Run `dotnet build` and any web-only test instead; full test suite is verified by CI on Windows.
- Don't change existing Skia / wasm settings in `OldenEra.Web.csproj`.

---

## Task 1: Shared Steam constants

**Files:**
- Create: `OldenEra.Generator/Constants/OldenEraSteamInfo.cs`
- Modify: `Olden Era - Template Editor/MainWindow.xaml.cs:1099` (use new constants)

**Step 1: Create the constants file**

```csharp
namespace OldenEra.Generator.Constants;

/// <summary>Single source of truth for Steam-related identifiers used by
/// both the WPF host's auto-detect and the web host's installer script.</summary>
public static class OldenEraSteamInfo
{
    public const string AppId = "3105440";
    public const string SteamFolderName = "Heroes of Might and Magic Olden Era";
    public const string TemplatesSubpath = @"HeroesOldenEra_Data\StreamingAssets\map_templates";
}
```

**Step 2: Wire WPF to use the constants**

In `MainWindow.xaml.cs`, replace the local `const string appId = "3105440";` and the literal `"Heroes of Might and Magic Olden Era"` / `"HeroesOldenEra_Data\\StreamingAssets\\map_templates"` strings (around lines 1087, 1099, 1115, 1126–1132) with references to `OldenEra.Generator.Constants.OldenEraSteamInfo`.

Add `using OldenEra.Generator.Constants;` to the top of the file.

**Step 3: Build**

Run: `dotnet build OldenEra.Generator/OldenEra.Generator.csproj`
Expected: Build succeeded, 0 errors.

Run: `dotnet build OldenEra.Web/OldenEra.Web.csproj`
Expected: Build succeeded.

(The WPF csproj cannot build on macOS — skip it locally; CI covers it.)

**Step 4: Commit**

```bash
git add OldenEra.Generator/Constants/OldenEraSteamInfo.cs "Olden Era - Template Editor/MainWindow.xaml.cs"
git commit -m "refactor: extract Steam app id + paths to OldenEraSteamInfo"
```

---

## Task 2: Embedded installer resources

**Files:**
- Create: `OldenEra.Web/Resources/Installer/install.bat`
- Create: `OldenEra.Web/Resources/Installer/install.ps1`
- Create: `OldenEra.Web/Resources/Installer/README.txt`
- Modify: `OldenEra.Web/OldenEra.Web.csproj`

**Step 1: Write `install.bat`**

```bat
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
```

**Step 2: Write `install.ps1`**

The script mirrors `MainWindow.xaml.cs:FindOldenEraTemplatesPath`. Placeholders `{STEAM_APP_ID}`, `{STEAM_FOLDER_NAME}`, `{TEMPLATES_SUBPATH}`, `{TEMPLATE_NAME}` are substituted by the packager.

```powershell
$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$appId          = '{STEAM_APP_ID}'
$steamFolder    = '{STEAM_FOLDER_NAME}'
$templatesTail  = '{TEMPLATES_SUBPATH}'
$templateName   = '{TEMPLATE_NAME}'

function Find-TemplatesDir {
    foreach ($root in @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App $appId",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App $appId"
    )) {
        try {
            $install = (Get-ItemProperty -Path $root -ErrorAction Stop).InstallLocation
            if ($install -and (Test-Path $install)) {
                $candidate = Join-Path $install $templatesTail
                if (Test-Path $candidate) { return $candidate }
            }
        } catch { }
    }

    foreach ($base in @(
        (Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\$steamFolder"),
        (Join-Path ${env:ProgramFiles}      "Steam\steamapps\common\$steamFolder")
    )) {
        $candidate = Join-Path $base $templatesTail
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

$dest = Find-TemplatesDir
if (-not $dest) {
    Write-Host ""
    Write-Host "Could not locate your Olden Era installation." -ForegroundColor Red
    Write-Host "See README.txt in this folder for manual install steps." -ForegroundColor Red
    exit 1
}

$jsonSrc = Join-Path $scriptDir "$templateName.rmg.json"
$pngSrc  = Join-Path $scriptDir "$templateName.png"

Copy-Item -Path $jsonSrc -Destination $dest -Force
Copy-Item -Path $pngSrc  -Destination $dest -Force

Write-Host ""
Write-Host "Installed to: $dest" -ForegroundColor Green
```

**Step 3: Write `README.txt`**

```
Olden Era template installer
============================

To install automatically:
  Double-click install.bat.

To install manually:
  1. Locate your Olden Era install folder (Steam -> right-click the
     game -> Manage -> Browse local files).
  2. Open the subfolder:
       HeroesOldenEra_Data\StreamingAssets\map_templates
  3. Copy {TEMPLATE_NAME}.rmg.json and {TEMPLATE_NAME}.png into it.
  4. Launch the game; the template will appear in the random map list.
```

**Step 4: Embed the resources**

In `OldenEra.Web/OldenEra.Web.csproj`, add an `<ItemGroup>`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources\Installer\install.bat" />
  <EmbeddedResource Include="Resources\Installer\install.ps1" />
  <EmbeddedResource Include="Resources\Installer\README.txt" />
</ItemGroup>
```

**Step 5: Build**

Run: `dotnet build OldenEra.Web/OldenEra.Web.csproj`
Expected: Build succeeded.

**Step 6: Commit**

```bash
git add OldenEra.Web/Resources/Installer OldenEra.Web/OldenEra.Web.csproj
git commit -m "feat(web): add Windows installer script + README templates"
```

---

## Task 3: InstallerPackager — failing test

**Files:**
- Modify: `Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj` (add ref if missing)
- Create: `Olden Era - Template Editor.Tests/InstallerPackagerTests.cs`

**Note:** The test project targets `net10.0-windows` and cannot run on macOS. Author the test, push, let CI verify. Locally, ensure it compiles when invoked from the web project's perspective by running `dotnet build` on the web project (which does not pull tests in).

**Step 1: Confirm test project references the web project**

```bash
grep -i "OldenEra.Web" "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
```

If absent, add inside an `<ItemGroup>`:

```xml
<ProjectReference Include="..\OldenEra.Web\OldenEra.Web.csproj" />
```

**Step 2: Write the failing test**

```csharp
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Web.Services;
using Xunit;

namespace OldenEra.Tests;

public class InstallerPackagerTests
{
    private const string TemplateName = "My Test Template";

    private static (byte[] json, byte[] png) Fixture()
    {
        var rmg = new RmgTemplate { Name = TemplateName };
        var json = JsonSerializer.SerializeToUtf8Bytes(rmg);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        return (json, png);
    }

    [Fact]
    public void BuildPlainZip_ContainsJsonAndPng()
    {
        var (json, png) = Fixture();
        byte[] zip = InstallerPackager.BuildPlainZip(TemplateName, json, png);

        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.Equal(2, archive.Entries.Count);
        Assert.NotNull(archive.GetEntry($"{TemplateName}.rmg.json"));
        Assert.NotNull(archive.GetEntry($"{TemplateName}.png"));
    }

    [Fact]
    public void BuildInstallerZip_ContainsAllFivePiecesWithSubstitutions()
    {
        var (json, png) = Fixture();
        byte[] zip = InstallerPackager.BuildInstallerZip(TemplateName, json, png);

        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.Equal(5, archive.Entries.Count);

        var jsonEntry = archive.GetEntry($"{TemplateName}.rmg.json");
        Assert.NotNull(jsonEntry);
        using (var s = jsonEntry!.Open())
        {
            var roundTrip = JsonSerializer.Deserialize<RmgTemplate>(s);
            Assert.Equal(TemplateName, roundTrip!.Name);
        }

        var pngEntry = archive.GetEntry($"{TemplateName}.png");
        Assert.NotNull(pngEntry);
        var pngBytes = new byte[8];
        using (var s = pngEntry!.Open())
            s.ReadExactly(pngBytes);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, pngBytes);

        foreach (var name in new[] { "install.bat", "install.ps1", "README.txt" })
        {
            var entry = archive.GetEntry(name);
            Assert.NotNull(entry);
            using var sr = new StreamReader(entry!.Open(), Encoding.UTF8);
            string text = sr.ReadToEnd();
            Assert.DoesNotContain("{TEMPLATE_NAME}", text);
            Assert.DoesNotContain("{STEAM_APP_ID}", text);
            Assert.DoesNotContain("{STEAM_FOLDER_NAME}", text);
            Assert.DoesNotContain("{TEMPLATES_SUBPATH}", text);
        }

        var psEntry = archive.GetEntry("install.ps1")!;
        using var psReader = new StreamReader(psEntry.Open(), Encoding.UTF8);
        string psText = psReader.ReadToEnd();
        Assert.Contains(TemplateName, psText);
        Assert.Contains("3105440", psText);
    }
}
```

**Step 3: Run test — expect FAIL**

Run: `dotnet build OldenEra.Web/OldenEra.Web.csproj` (compile-time fail proves the API is missing).
Expected: error CS0103: The name 'InstallerPackager' does not exist.

**Step 4: Commit**

```bash
git add "Olden Era - Template Editor.Tests/InstallerPackagerTests.cs" "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
git commit -m "test(web): failing tests for InstallerPackager zip output"
```

---

## Task 4: InstallerPackager implementation

**Files:**
- Create: `OldenEra.Web/Services/InstallerPackager.cs`

**Step 1: Implement the packager**

```csharp
using System.IO.Compression;
using System.Reflection;
using System.Text;
using OldenEra.Generator.Constants;

namespace OldenEra.Web.Services;

public static class InstallerPackager
{
    public static byte[] BuildPlainZip(string templateName, byte[] rmgJson, byte[] png)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, $"{templateName}.rmg.json", rmgJson);
            WriteEntry(archive, $"{templateName}.png", png);
        }
        return ms.ToArray();
    }

    public static byte[] BuildInstallerZip(string templateName, byte[] rmgJson, byte[] png)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, $"{templateName}.rmg.json", rmgJson);
            WriteEntry(archive, $"{templateName}.png", png);
            WriteEntry(archive, "install.bat", LoadResource("install.bat", templateName));
            WriteEntry(archive, "install.ps1", LoadResource("install.ps1", templateName));
            WriteEntry(archive, "README.txt", LoadResource("README.txt", templateName));
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    private static byte[] LoadResource(string fileName, string templateName)
    {
        var asm = typeof(InstallerPackager).Assembly;
        string resourceName = $"OldenEra.Web.Resources.Installer.{fileName}";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string text = reader.ReadToEnd()
            .Replace("{TEMPLATE_NAME}", templateName)
            .Replace("{STEAM_APP_ID}", OldenEraSteamInfo.AppId)
            .Replace("{STEAM_FOLDER_NAME}", OldenEraSteamInfo.SteamFolderName)
            .Replace("{TEMPLATES_SUBPATH}", OldenEraSteamInfo.TemplatesSubpath);
        return Encoding.UTF8.GetBytes(text);
    }
}
```

**Step 2: Build**

Run: `dotnet build OldenEra.Web/OldenEra.Web.csproj`
Expected: Build succeeded.

**Step 3: (Where possible) run tests**

On Windows / CI: `dotnet test "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj" --filter "FullyQualifiedName~InstallerPackager"`
Expected: 2 passing.

On macOS: skip; record as "must verify on CI".

**Step 4: Commit**

```bash
git add OldenEra.Web/Services/InstallerPackager.cs
git commit -m "feat(web): InstallerPackager builds plain + installer zips"
```

---

## Task 5: Wire the new buttons into Home.razor

**Files:**
- Modify: `OldenEra.Web/Pages/Home.razor` (button block ~lines 117–126; handler block ~lines 501–514)

**Step 1: Replace the two existing buttons**

Find:

```razor
<button class="oe-btn"
        disabled="@(GeneratedTemplate is null)"
        @onclick="DownloadJson">
    💾 Download .rmg.json
</button>
<button class="oe-btn"
        disabled="@(PreviewPng is null)"
        @onclick="DownloadPng">
    🖼 Download Preview PNG
</button>
```

Replace with:

```razor
<button class="oe-btn"
        disabled="@(GeneratedTemplate is null || PreviewPng is null)"
        @onclick="DownloadMap">
    💾 Download map
</button>
<button class="oe-btn"
        disabled="@(GeneratedTemplate is null || PreviewPng is null)"
        @onclick="DownloadMapWithInstaller">
    📦 Download map w/ installer
</button>
```

**Step 2: Replace the handlers**

Find `private async Task DownloadJson()` and `private async Task DownloadPng()` (~lines 501–514). Replace both with:

```csharp
private async Task DownloadMap()
{
    if (GeneratedTemplate is null || PreviewPng is null) return;
    string name = ResolveTemplateName();
    byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(GeneratedTemplate, JsonOptions);
    byte[] zip = InstallerPackager.BuildPlainZip(name, json, PreviewPng);
    await Downloader.DownloadAsync($"{name}.zip", "application/zip", zip);
}

private async Task DownloadMapWithInstaller()
{
    if (GeneratedTemplate is null || PreviewPng is null) return;
    string name = ResolveTemplateName();
    byte[] json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(GeneratedTemplate, JsonOptions);
    byte[] zip = InstallerPackager.BuildInstallerZip(name, json, PreviewPng);
    await Downloader.DownloadAsync($"{name}-installer.zip", "application/zip", zip);
}

private string ResolveTemplateName() =>
    string.IsNullOrWhiteSpace(Settings.TemplateName) ? "Custom Template" : Settings.TemplateName.Trim();
```

**Step 3: Add the using if missing**

At the top of `Home.razor`, ensure `@using OldenEra.Web.Services` is present (the existing `FileDownloader` injection already pulls it in via DI registration; verify the type resolves at build time).

**Step 4: Build**

Run: `dotnet build OldenEra.Web/OldenEra.Web.csproj`
Expected: Build succeeded.

**Step 5: Smoke-test in the dev server (manual)**

Run: `dotnet run --project OldenEra.Web`
- Open the printed localhost URL.
- Click Generate.
- Click "Download map" — confirm a `.zip` downloads with two entries.
- Click "Download map w/ installer" — confirm a `-installer.zip` downloads with five entries.
- Open `install.ps1` from the zip and confirm `{TEMPLATE_NAME}` was substituted.

**Step 6: Commit**

```bash
git add OldenEra.Web/Pages/Home.razor
git commit -m "feat(web): merge .rmg.json + PNG into single zip download with optional installer"
```

---

## Task 6: Refresh the design doc cross-link & wrap-up

**Files:**
- Modify: `docs/plans/2026-05-10-installer-zip-download-design.md` (add a "Status: Implemented" line if all tasks landed cleanly)

**Step 1: Mark the design as implemented**

Change `**Status:** Design` to `**Status:** Implemented (2026-05-10)`.

**Step 2: Run the full local build**

Run: `dotnet build OldenEra.Generator/OldenEra.Generator.csproj && dotnet build OldenEra.Web/OldenEra.Web.csproj`
Expected: both succeeded.

**Step 3: Push and open a PR**

```bash
git push -u origin feature/installer-zip
gh pr create --title "Single-zip map download + optional Windows installer" --body "$(cat <<'EOF'
## Summary
- Replaces the two-button download (`.rmg.json` + PNG) with a single zipped pair, plus a second button that adds `install.bat` / `install.ps1` / `README.txt` for one-click placement into the Steam install.
- Centralizes the Steam app id and template subpath in a new `OldenEraSteamInfo` constants class shared between the WPF auto-detect and the packaged installer script.

## Test plan
- [ ] CI: `InstallerPackagerTests` passes on Windows
- [ ] Manual: Web preview \"Download map\" yields a 2-entry zip
- [ ] Manual: \"Download map w/ installer\" yields a 5-entry zip with substituted placeholders
- [ ] Manual on Windows w/ Olden Era installed: `install.bat` copies both files into `map_templates`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Step 4: Commit the design status update**

```bash
git add docs/plans/2026-05-10-installer-zip-download-design.md
git commit -m "docs(plans): mark installer-zip design implemented"
```

---

## Final verification checklist

- [ ] `OldenEraSteamInfo` is the only place that hard-codes `3105440`.
- [ ] `Home.razor` has zero references to `DownloadJson` / `DownloadPng`.
- [ ] `OldenEra.Web.csproj` lists three `EmbeddedResource` entries.
- [ ] `InstallerPackagerTests.cs` exists and is referenced from the test csproj.
- [ ] CI green on the PR.
