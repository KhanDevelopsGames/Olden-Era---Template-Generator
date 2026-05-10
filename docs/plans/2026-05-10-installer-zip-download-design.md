# Installer-zip download for the web host

**Date:** 2026-05-10
**Status:** Implemented (2026-05-10) — packager lives in `OldenEra.Generator` (not `OldenEra.Web`) so the WPF-target test project can reference it without pulling in the Blazor WASM SDK.

## Motivation

The web host currently exposes two buttons: **Download .rmg.json** and **Download Preview PNG**. The game requires the PNG to sit beside the `.rmg.json` inside `HeroesOldenEra_Data\StreamingAssets\map_templates`, so two separate downloads place the burden of pairing and placement on the user. The WPF host already pairs the files (sidecar PNG) and tells the user where they belong; the web host should match that, and ideally go one step further by offering an automated copy.

## User-facing change

Replace the two existing buttons with:

- **Download map** — a zip containing `<name>.rmg.json` and `<name>.png`.
- **Download map w/ installer** — the same zip plus `install.bat`, `install.ps1`, and `README.txt`.

The standalone PNG button is removed. Both buttons require a generated template (same guard as today).

The installer is Windows-only. macOS and Linux users still get the plain "Download map" zip and use the README's manual steps.

## Zip contents

**Plain zip — `<name>.zip`:**

```
<name>.rmg.json
<name>.png
```

**Installer zip — `<name>-installer.zip`:**

```
<name>.rmg.json
<name>.png
install.bat
install.ps1
README.txt
```

## Implementation

### Packager

New file `OldenEra.Web/Services/InstallerPackager.cs`:

```csharp
public static byte[] BuildPlainZip(string name, byte[] rmgJson, byte[] png);
public static byte[] BuildInstallerZip(string name, byte[] rmgJson, byte[] png);
```

Both helpers build the zip in memory via `System.IO.Compression.ZipArchive`.

The installer script, launcher, and README ship as embedded resources under `OldenEra.Web/Resources/Installer/` (`install.bat`, `install.ps1`, `README.txt`). `BuildInstallerZip` reads each resource, substitutes `{TEMPLATE_NAME}` (and `{STEAM_APP_ID}` in the script) before writing the entry.

The Steam app id lives in a single shared constant — `OldenEra.Generator.Constants.OldenEraSteamInfo` (new file in the shared library) — so the WPF detection code and the packager cannot drift.

### Home.razor wiring

Remove `DownloadPng`. Replace `DownloadJson` with:

```csharp
private async Task DownloadMap();              // BuildPlainZip
private async Task DownloadMapWithInstaller(); // BuildInstallerZip
```

Both call the existing `FileDownloader` with `application/zip`. Button enable state mirrors today's: enabled iff `PreviewPng is not null` (i.e. a generation has happened).

### install.bat

```bat
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
```

`%~dp0` pins the working directory to the script folder. `-ExecutionPolicy Bypass` is per-invocation, not machine-wide. `pause` keeps the window open on failure.

### install.ps1

Mirrors the WPF detection in `MainWindow.xaml.cs:1093–1135`:

1. **Registry probe.** Read `InstallLocation` from `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {STEAM_APP_ID}` and its `WOW6432Node` sibling.
2. **Library scan fallback.** Parse `libraryfolders.vdf` from the Steam install (resolved via `HKCU:\Software\Valve\Steam\SteamPath` or the standard `Program Files (x86)\Steam` path). Probe each library for `steamapps\common\Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates`.
3. **Validate.** Candidate must exist and end in `HeroesOldenEra_Data\StreamingAssets\map_templates`.
4. **Success.** `Copy-Item` `{TEMPLATE_NAME}.rmg.json` and `{TEMPLATE_NAME}.png` into the folder; print `Installed to: <path>`; exit 0.
5. **Failure.** Print a red error pointing to `README.txt`; exit 1.

### README.txt

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

## Testing

One new test class, `InstallerPackagerTests`, in `Olden Era - Template Editor.Tests/`:

- Build an installer zip from a fixture template.
- Open with `ZipArchive` and assert exactly five entries with the expected names.
- Round-trip the `.rmg.json` entry through `JsonSerializer.Deserialize<RmgTemplate>` and confirm a property survives.
- Assert the `.png` entry starts with the PNG magic bytes (`89 50 4E 47`).
- Assert `install.ps1` and `README.txt` contain the substituted template name and no leftover `{TEMPLATE_NAME}` / `{STEAM_APP_ID}` placeholders.

No runtime test of the PowerShell script — that requires a Windows machine with the game installed, and the detection logic is a port of code already exercised by the WPF host.

## Out of scope

- macOS / Linux installer scripts.
- Auto-launching the game after install.
- Uninstall / overwrite-confirmation prompts (the script overwrites silently, matching the WPF "Save As" behavior).
- Changing the WPF host's save flow.
