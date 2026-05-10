# Cross-Platform Test Project Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Drop the `net10.0-windows` target on `Olden Era - Template Editor.Tests` so `dotnet test` runs on macOS and Linux (and CI moves from `windows-latest` to `ubuntu-latest`).

**Architecture:** Replace WPF-imaging pixel sampling (`BitmapDecoder` / `BitmapSource` / `Int32Rect` / WPF `Color`) with SkiaSharp equivalents (`SKBitmap`, `SKColor`, `SKRectI`). Move the one piece of WPF-project code the test depends on (`WpfPreviewAdapter.GetSidecarPath` — a pure string helper) into `OldenEra.Generator`. Drop the test project's reference to the WPF project, then drop `-windows` from its TFM. Switch the CI workflow runner.

**Tech Stack:** .NET 10, SkiaSharp 3.119 (already a transitive dep via `OldenEra.Generator`). xUnit unchanged.

**Scope guard:** Keep the WPF app `net10.0-windows`. Don't touch `Olden Era - Template Editor.csproj` beyond removing one file. Don't restructure tests, don't rewrite assertions, don't fold tests into smaller helpers — port the existing helpers and move on.

---

## Pre-flight: confirm assumptions

Before starting, sanity-check these against the worktree (`feature/tests-mac-port` from main):

1. `Olden Era - Template Editor.Tests/TemplateGeneratorTests.cs` is the **only** test file using `System.Windows.*`. Verify with:
   ```bash
   grep -rln "System\.Windows\|BitmapDecoder\|BitmapSource\|Int32Rect" "Olden Era - Template Editor.Tests/" --include="*.cs"
   ```
   Expected output: only `TemplateGeneratorTests.cs`.

2. The only WPF-project symbol referenced from tests is `WpfPreviewAdapter.GetSidecarPath`. Verify with:
   ```bash
   grep -rn "Olden_Era___Template_Editor\." "Olden Era - Template Editor.Tests/" --include="*.cs" | grep -v "namespace\|\.Tests"
   ```
   Expected: exactly one hit, `using Olden_Era___Template_Editor.Services;` in `TemplateGeneratorTests.cs`.

3. SkiaSharp comes in transitively via `OldenEra.Generator`'s `<PackageReference Include="SkiaSharp" Version="3.119.0" />`. The test project does **not** need to add SkiaSharp directly — it already references `OldenEra.Generator`.

4. The unused helper `LoadBitmap(string path)` (lines 936–946) has zero callers (only `LoadBitmapFromBytes` is called). It can be deleted, not ported.

If any of these don't match, stop and reassess.

---

## Task 1: Move `GetSidecarPath` into `OldenEra.Generator`

**Why first:** It's the smallest piece, breaks no dependencies, and lets us drop the WPF project reference in Task 4. Doing it now means the SkiaSharp port in Task 2 doesn't have to think about it.

**Files:**
- Create: `OldenEra.Generator/Services/PreviewSidecar.cs`
- Modify: `Olden Era - Template Editor/Services/WpfPreviewAdapter.cs` (delete `GetSidecarPath`, keep `ToBitmapImage`)
- Modify: callers of `WpfPreviewAdapter.GetSidecarPath` in the WPF app

**Step 1: Find every caller of `WpfPreviewAdapter.GetSidecarPath`**

```bash
cd .worktrees/tests-mac-ports
grep -rn "WpfPreviewAdapter\.GetSidecarPath\|GetSidecarPath" --include="*.cs" --include="*.xaml.cs"
```

Note all hits. There should be: the test (line 773–774), the helper itself, and one or more callers in the WPF app's `MainWindow.xaml.cs` or similar. Don't miss any.

**Step 2: Create the new helper**

`OldenEra.Generator/Services/PreviewSidecar.cs`:

```csharp
namespace OldenEra.Generator.Services;

/// <summary>
/// Resolves the PNG sidecar path for an Olden Era `.rmg.json` template.
/// `Foo.rmg.json` → `Foo.png`; anything else → standard ChangeExtension.
/// </summary>
public static class PreviewSidecar
{
    public static string GetSidecarPath(string rmgJsonPath) =>
        rmgJsonPath.EndsWith(".rmg.json", System.StringComparison.OrdinalIgnoreCase)
            ? rmgJsonPath[..^".rmg.json".Length] + ".png"
            : System.IO.Path.ChangeExtension(rmgJsonPath, ".png");
}
```

That's a verbatim move of the existing logic. No behavior change.

**Step 3: Update WPF callers**

For each caller found in Step 1 (excluding the test, which is updated in Task 3):
- Add `using OldenEra.Generator.Services;` if not present.
- Replace `WpfPreviewAdapter.GetSidecarPath(...)` with `PreviewSidecar.GetSidecarPath(...)`.

Then in `Olden Era - Template Editor/Services/WpfPreviewAdapter.cs`, delete the `GetSidecarPath` method. The class keeps `ToBitmapImage`. Resulting file:

```csharp
using System.IO;
using System.Windows.Media.Imaging;

namespace Olden_Era___Template_Editor.Services;

public static class WpfPreviewAdapter
{
    public static BitmapImage ToBitmapImage(byte[] png)
    {
        var image = new BitmapImage();
        using var stream = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
```

**Step 4: Build the WPF app to verify nothing broke**

```bash
dotnet build "Olden Era - Template Editor.slnx"
```

Expected: build succeeds. If a caller is missed, the compiler will tell you exactly where.

**Step 5: Commit**

```bash
git add OldenEra.Generator/Services/PreviewSidecar.cs "Olden Era - Template Editor/Services/WpfPreviewAdapter.cs" <other-modified-WPF-files>
git commit -m "refactor: move GetSidecarPath to generator project (no WPF deps)"
```

---

## Task 2: Port pixel-sampling helpers from WPF imaging to SkiaSharp

**Files:**
- Modify: `Olden Era - Template Editor.Tests/TemplateGeneratorTests.cs`

**Why a single commit covers all helpers:** They're tightly coupled. Half-ported is not buildable on either platform.

**Step 1: Replace the `using` block at the top of `TemplateGeneratorTests.cs`**

Current (lines 1–9):

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using OldenEra.Generator.Models.Unfrozen;
using Olden_Era___Template_Editor.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
```

Replace with:

```csharp
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using OldenEra.Generator.Models.Unfrozen;
using SkiaSharp;
using System.Diagnostics;
using System.Text.Json;
```

Notes:
- `Olden_Era___Template_Editor.Services` goes away — `WpfPreviewAdapter` is no longer referenced from tests (Task 3 retargets the one assertion).
- `System.Windows*` goes away.
- `SkiaSharp` is added.

**Step 2: Replace the `Point` usage**

The test at line 806–812 builds a `Dictionary<string, Point>` from `ComputeLayout`'s `(double X, double Y)` tuples, only to read `.X` and `.Y`. The `System.Windows.Point` indirection is pointless. Delete the dictionary conversion and use the tuple directly:

Find:

```csharp
Dictionary<string, Point> layout = rawLayout.ToDictionary(
    kv => kv.Key,
    kv => new Point(kv.Value.X, kv.Value.Y));

Point pA = layout["Neutral-A"]; // Bronze (sides)
Point pB = layout["Neutral-B"]; // Silver (treasure) — 3 castles
Point pC = layout["Neutral-C"]; // Gold  (center)  — 2 castles
```

Replace with:

```csharp
var pA = rawLayout["Neutral-A"]; // Bronze (sides)
var pB = rawLayout["Neutral-B"]; // Silver (treasure) — 3 castles
var pC = rawLayout["Neutral-C"]; // Gold  (center)  — 2 castles
```

`pA.X` / `pA.Y` still work — they're tuple components. Keep all the comments; they're load-bearing for understanding the assertions.

**Step 3: Rewrite the helper region (lines 913–987)**

Delete the entire block from `RunOnStaThread` through `CountCreamPixels` (the `LoadBitmap(string)` overload included — it has zero callers). Replace with:

```csharp
private static SKBitmap LoadBitmapFromBytes(byte[] pngBytes)
{
    var bitmap = SKBitmap.Decode(pngBytes)
        ?? throw new InvalidOperationException("SKBitmap.Decode returned null.");
    return bitmap;
}

private static SKColor PixelAt(SKBitmap bitmap, int x, int y) =>
    bitmap.GetPixel(x, y);

private static int CountCreamPixels(SKBitmap bitmap, SKRectI rect)
{
    int count = 0;
    for (int y = rect.Top; y < rect.Bottom; y++)
    {
        for (int x = rect.Left; x < rect.Right; x++)
        {
            SKColor c = bitmap.GetPixel(x, y);
            // Cinzel cream numerals (#F1D990 → R-B≈97) on a dark coin.
            // Parchment cream (#E7D6A8 → R-B≈63) must NOT match, so the threshold
            // sits between those values.
            if (c.Red >= 200 && c.Green >= 180 && c.Red - c.Blue >= 80)
                count++;
        }
    }
    return count;
}
```

Notes:
- `SKBitmap.Decode` returns null on failure — the `??` throw mirrors what a `BitmapDecoder` exception would have done.
- `SKColor.Red/Green/Blue` are byte components, identical semantics to the existing thresholds.
- No STA thread ceremony needed — Skia is thread-agnostic.
- `SKRectI` (integer rect) replaces `Int32Rect`. `Top`/`Left`/`Bottom`/`Right` are the obvious accessors.

**Step 4: Rewrite `AssertColorNear` (lines 989–996)**

Replace:

```csharp
private static void AssertColorNear(Color expected, Color actual, int tolerance = 8)
{
    Assert.True(
        Math.Abs(expected.R - actual.R) <= tolerance &&
        Math.Abs(expected.G - actual.G) <= tolerance &&
        Math.Abs(expected.B - actual.B) <= tolerance,
        $"Expected color near RGB({expected.R}, {expected.G}, {expected.B}), got RGB({actual.R}, {actual.G}, {actual.B}).");
}
```

with:

```csharp
private static void AssertColorNear(SKColor expected, SKColor actual, int tolerance = 8)
{
    Assert.True(
        Math.Abs(expected.Red   - actual.Red)   <= tolerance &&
        Math.Abs(expected.Green - actual.Green) <= tolerance &&
        Math.Abs(expected.Blue  - actual.Blue)  <= tolerance,
        $"Expected color near RGB({expected.Red}, {expected.Green}, {expected.Blue}), got RGB({actual.Red}, {actual.Green}, {actual.Blue}).");
}
```

**Step 5: Update the call sites inside `TemplatePreviewRenderer_EncodesNeutralQualityAndCastleCounts`**

Change the locals' types and the rect constructions. Specifically, in the test body around lines 798–841:

```csharp
byte[] pngBytes = TemplatePreviewRenderer.RenderPng(template);
SKBitmap bitmap = LoadBitmapFromBytes(pngBytes);
// (delete the RunOnStaThread wrapper — gone)

var rawLayout = TemplatePreviewRenderer.ComputeLayout(template);
double zoneRadius = TemplatePreviewRenderer.GetLastZoneRadius();

var pA = rawLayout["Neutral-A"];
var pB = rawLayout["Neutral-B"];
var pC = rawLayout["Neutral-C"];

int bgX = 50, bgY = 50;
SKColor bg = PixelAt(bitmap, bgX, bgY);
Assert.True(bg.Red > 80 && bg.Red > bg.Green && bg.Green > bg.Blue,
    $"Expected warm parchment background, got RGB({bg.Red},{bg.Green},{bg.Blue}).");

int bX = (int)Math.Round(pB.X);
int bY = (int)Math.Round(pB.Y - zoneRadius);
bX = Math.Clamp(bX, 0, bitmap.Width  - 1);
bY = Math.Clamp(bY, 0, bitmap.Height - 1);
AssertColorNear(new SKColor(192, 192, 192), PixelAt(bitmap, bX, bY), tolerance: 40);

var bgRect = SKRectI.Create(bgX, bgY, 24, 20);
var labelRect = SKRectI.Create(
    Math.Clamp((int)pB.X - 12, 0, bitmap.Width  - 25),
    Math.Clamp((int)pB.Y - 10, 0, bitmap.Height - 21),
    24, 20);
int bgCream    = CountCreamPixels(bitmap, bgRect);
int labelCream = CountCreamPixels(bitmap, labelRect);
Assert.True(labelCream > bgCream + 5,
    $"Expected cream label pixels in coin region (got {labelCream}) to exceed parchment corner ({bgCream}).");
```

Key API renames:
- `bitmap.PixelWidth` → `bitmap.Width`
- `bitmap.PixelHeight` → `bitmap.Height`
- `Color.FromRgb(r, g, b)` → `new SKColor(r, g, b)`
- `new Int32Rect(x, y, w, h)` → `SKRectI.Create(x, y, w, h)`
- `bg.R` / `.G` / `.B` → `bg.Red` / `.Green` / `.Blue`
- The `RunOnStaThread(() => bitmap = LoadBitmapFromBytes(pngBytes))` wrapper goes away — it's just `SKBitmap bitmap = LoadBitmapFromBytes(pngBytes);` now.
- The `using` of `bitmap` is optional. If you want to be tidy, wrap the test body's bitmap usage in `using SKBitmap bitmap = LoadBitmapFromBytes(pngBytes);` — `SKBitmap` is `IDisposable`. Don't introduce a `using` if it makes the diff messier; the test process tears down anyway.

**Step 6: Delete the `LoadBitmap(string)` overload entirely**

Lines 936–946 (the file-path overload) have no callers. Confirm with:

```bash
grep -nE "LoadBitmap\b\s*\(" "Olden Era - Template Editor.Tests/TemplateGeneratorTests.cs"
```

If only `LoadBitmapFromBytes` shows up (or `LoadBitmap` shows only at its definition), it's safe to delete. Don't keep dead code "just in case."

**Step 7: Build the test project**

The project still has `<TargetFramework>net10.0-windows</TargetFramework>` at this point — that's fine. Build to confirm the SkiaSharp port compiles:

```bash
dotnet build "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
```

Expected: builds clean. **Tests can't run yet on Mac** — the project still pulls WindowsDesktop runtime via the `Olden Era - Template Editor.csproj` reference. That's removed in Task 4.

**Step 8: Commit**

```bash
git add "Olden Era - Template Editor.Tests/TemplateGeneratorTests.cs"
git commit -m "test: port pixel sampling from WPF imaging to SkiaSharp"
```

---

## Task 3: Update the sidecar test to use the moved helper

**Files:**
- Modify: `Olden Era - Template Editor.Tests/TemplateGeneratorTests.cs`

**Step 1: Update the test body**

Find:

```csharp
[Fact]
public void WpfPreviewAdapter_UsesOfficialSidecarNaming()
{
    Assert.Equal(@"C:\maps\My Template.png", WpfPreviewAdapter.GetSidecarPath(@"C:\maps\My Template.rmg.json"));
    Assert.Equal(@"C:\maps\Other.png", WpfPreviewAdapter.GetSidecarPath(@"C:\maps\Other.json"));
}
```

Replace with:

```csharp
[Fact]
public void PreviewSidecar_UsesOfficialSidecarNaming()
{
    Assert.Equal(@"C:\maps\My Template.png", PreviewSidecar.GetSidecarPath(@"C:\maps\My Template.rmg.json"));
    Assert.Equal(@"C:\maps\Other.png", PreviewSidecar.GetSidecarPath(@"C:\maps\Other.json"));
}
```

The test name changes to match the new class. The assertions are unchanged — they exercise the same string logic, just at its new home.

`PreviewSidecar` resolves through `using OldenEra.Generator.Services;` which is already in the file's using block (added in Task 2 as part of the using cleanup, or already present from `OldenEra.Generator.Services` for `TemplateGenerator`/`TemplatePreviewRenderer`).

**Step 2: Build**

```bash
dotnet build "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
```

Expected: clean build.

**Step 3: Commit**

```bash
git add "Olden Era - Template Editor.Tests/TemplateGeneratorTests.cs"
git commit -m "test: retarget sidecar test at moved PreviewSidecar helper"
```

---

## Task 4: Drop WPF project reference and `-windows` TFM from the test project

**Files:**
- Modify: `Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj`

**Step 1: Update the csproj**

Current contents:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RootNamespace>Olden_Era___Template_Editor.Tests</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Olden Era - Template Editor\Olden Era - Template Editor.csproj" />
    <ProjectReference Include="..\OldenEra.Generator\OldenEra.Generator.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

Make two edits:

1. Change `<TargetFramework>net10.0-windows</TargetFramework>` to `<TargetFramework>net10.0</TargetFramework>`.
2. Delete the `<ProjectReference Include="..\Olden Era - Template Editor\..."/>` line. The remaining ref to `OldenEra.Generator` stays.

The `RootNamespace` keeps the existing `Olden_Era___Template_Editor.Tests` name — don't rename it. That's the namespace existing tests are declared in; renaming it would touch every test file.

**Step 2: Restore + build the whole solution to make sure nothing else broke**

```bash
dotnet restore "Olden Era - Template Editor.slnx"
dotnet build "Olden Era - Template Editor.slnx"
```

The WPF app and the test project still both build. `Directory.Build.props` already sets `EnableWindowsTargeting=true`, so the WPF app compiles on Mac.

**Step 3: Run the tests on Mac**

This is the moment of truth.

```bash
dotnet test "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
```

Expected: all tests pass. If the renderer test fails:

- `TemplatePreviewRenderer_EncodesNeutralQualityAndCastleCounts` is the only one that does pixel sampling. If pixel values differ between SkiaSharp's decode (which is what the renderer also writes through) and WPF's decode (unpremultiplied BGRA32), the most likely failure modes are:
  - Cream-pixel count off by a small amount → adjust the `+ 5` margin if needed (don't loosen the threshold itself; the threshold encodes physical color identity).
  - Background sample failing the `R > G > B` warmth check → the sample point may sit inside an antialiased gradient boundary; bump `bgX, bgY` by a few pixels, but document why.
- If `SKBitmap.Decode` returns a premultiplied bitmap, `GetPixel` automatically unpremultiplies for you. No manual conversion is needed.
- Don't change the test's *intent*. If you find yourself relaxing thresholds substantially, stop and investigate. The renderer is producing the same PNG bytes; the test is reading them back through a different decoder. Differences should be sub-pixel quantization, not semantic.

If a test that has nothing to do with WPF imaging fails (say, `Generate_*`), something in the build broke unrelated to this port — investigate before continuing.

**Step 4: Commit**

```bash
git add "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
git commit -m "test: drop -windows TFM and WPF project ref; tests run on any OS"
```

---

## Task 5: Switch CI runner to ubuntu

**Files:**
- Modify: `.github/workflows/tests.yml`

**Step 1: Replace the workflow**

Current `tests.yml` runs on `windows-latest` and installs the `wasm-tools` workload. The test project no longer needs Windows. The workload is needed by `OldenEra.Web` only — and `tests.yml` builds the **whole solution**, which includes `OldenEra.Web`, which still requires `wasm-tools`. So we keep the workload install but switch the runner.

Verify before editing: the solution `.slnx` includes `OldenEra.Web` (it does, per existing CI). If you skip that workload install, the build of the solution fails on `OldenEra.Web` with `NETSDK1147`.

Replace the file with:

```yaml
name: Olden Era Tests

on:
  push:
    branches: [ "**" ]
  pull_request:
    branches: [ "**" ]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
    - name: Checkout repository
      uses: actions/checkout@v5

    - name: Setup .NET
      uses: actions/setup-dotnet@v5
      with:
        dotnet-version: '10.0.x'

    - name: Install wasm-tools workload
      run: dotnet workload install wasm-tools

    - name: Restore dependencies
      run: dotnet restore "Olden Era - Template Editor.slnx"

    - name: Build
      run: dotnet build "Olden Era - Template Editor.slnx" --configuration Release --no-restore

    - name: Run tests
      run: dotnet test --no-build --configuration Release --verbosity normal
```

Notes:
- Bumped action versions to `@v5` to match the e2e workflow we just shipped — keeps the deprecation warnings off.
- `--no-restore` on the build step + matching the existing test invocation pattern. The existing workflow's `dotnet test` uses `--no-build`; keep that.
- The tests project on `net10.0` runs on Ubuntu without further system deps. SkiaSharp 3.x bundles its native lib for linux-x64; if the test run fails with a missing `libfontconfig` or similar, add `sudo apt-get update && sudo apt-get install -y libfontconfig1` as a step before "Run tests". Don't preemptively add that — first see if it's needed.

**Step 2: Update AGENTS.md**

In `AGENTS.md`, the "Cross-platform development" section currently says:

> ❌ `dotnet test` works only on Windows for the same reason — the testhost requires `Microsoft.WindowsDesktop.App`. CI runs tests on `windows-latest` via `.github/workflows/tests.yml`.

Replace that bullet with:

> ✅ `dotnet test "Olden Era - Template Editor.slnx"` works on macOS/Linux/Windows. Tests target `net10.0` and use SkiaSharp for any pixel sampling. CI runs them on `ubuntu-latest` via `.github/workflows/tests.yml`.

Don't rewrite the section — just swap that one bullet. Leave the WPF-app bullet (`❌ dotnet run --project "Olden Era - Template Editor"` Windows-only) as-is — that's still true.

**Step 3: Commit**

```bash
git add .github/workflows/tests.yml AGENTS.md
git commit -m "ci: run tests on ubuntu-latest (test project is now net10.0)"
```

---

## Task 6: Push, watch CI, merge

**Step 1: Push**

```bash
git push -u origin feature/tests-mac-port
```

**Step 2: Open the PR**

```bash
gh pr create --title "Port tests off WPF imaging; run on any OS" --body "..."
```

PR body should reference: drops `-windows` TFM, ports pixel sampling to SkiaSharp, moves `GetSidecarPath` to generator, switches CI to ubuntu. Note that the WPF app still builds Windows-only (intentional; out of scope).

**Step 3: Watch all three workflows on the PR**

- `Olden Era Tests` — must pass on ubuntu now.
- `Olden Era Web E2E` — should be unaffected, still passes.
- `Deploy web` — only runs on `main`, not on PRs, so won't show.

If the test workflow fails on a specific `TemplatePreviewRenderer` assertion, see Task 4 Step 3's debugging notes. **Do not** loosen thresholds without understanding why values differ; the renderer's output is the same PNG. The most plausible cause is a sample-point landing at an antialiased boundary in a way that didn't matter under WPF's `BitmapDecoder` for some subtle decoder reason.

**Step 4: Merge once green**

Squash. Delete the branch.

**Step 5: Watch main CI after merge**

Confirm `Olden Era Tests` on main is green on ubuntu.

---

## Done criteria

- `dotnet test "Olden Era - Template Editor.slnx"` runs to completion on macOS without `Microsoft.WindowsDesktop.App` errors.
- All test assertions pass — same count as before the port (no tests deleted; the dead `LoadBitmap(string)` overload doesn't count, it had no `[Fact]`).
- CI workflow `tests.yml` runs on `ubuntu-latest` and is green on main.
- `Olden Era - Template Editor.csproj` (WPF) is untouched except for the `WpfPreviewAdapter.GetSidecarPath` deletion + caller updates.
- `AGENTS.md` no longer says tests are Windows-only.

## What this plan deliberately doesn't do

- **No WPF UI testing.** Out of scope; needs FlaUI / WinAppDriver / a Windows VM. If you want it later, it's a separate workstream.
- **No SkiaSharp version bump or package add to the test project.** It comes through transitively from `OldenEra.Generator`. Don't add it explicitly.
- **No test rewrites.** Port the helpers, update call sites, leave assertions and structure alone.
- **No matrix CI run on macOS/Windows in addition to Ubuntu.** One Linux runner is enough — the platforms differ at the SkiaSharp native layer, not in our managed code, and that's not a difference our tests cover.
- **No retargeting of `RootNamespace`.** Cosmetic only; would touch every test file.
