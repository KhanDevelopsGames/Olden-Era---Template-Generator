# Web + Cross-Platform Distribution Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extract the generator into a shared `OldenEra.Generator` library, then add a Blazor WebAssembly frontend deployed to GitHub Pages, while keeping the existing WPF Windows app intact.

**Architecture:** Three-project solution: shared `net10.0` class library holds all generator logic + a SkiaSharp PNG renderer; the WPF app and a new Blazor WASM project both reference the library and add only host-specific concerns (Steam detection in WPF, browser download in web). Library is pure — no file I/O, no Steam, no UI.

**Tech Stack:** .NET 10, C#, WPF (existing), Blazor WebAssembly (new), SkiaSharp (replacing `System.Windows.Media` in the renderer), xUnit, GitHub Actions + Pages.

**Companion document:** [`2026-05-09-web-and-cross-platform-design.md`](./2026-05-09-web-and-cross-platform-design.md) — read this first for the why.

**Working directory:** `/Users/rparn/Developer/personal-projects/Olden-Era---Template-Generator/.worktrees/web-and-cross-platform-design/`
**Branch:** `feature/web-and-cross-platform-design`

---

## Conventions for every task

- Each task is one focused change. Run tests after every task. Commit only when green.
- Build command: `dotnet build "Olden Era - Template Editor.slnx"`
- Test command: `dotnet test "Olden Era - Template Editor.slnx"`
- On macOS the WPF app cannot be built (`net10.0-windows` requires Windows). Tasks marked **[Windows-only verification]** require a Windows host or VM. Tasks marked **[macOS-OK]** can be done on the dev Mac.
- All paths in this plan are relative to the worktree root above. Quote paths that contain spaces.
- The existing app's `RootNamespace` is `Olden_Era___Template_Editor`. The new library uses `OldenEra.Generator` as its root namespace. **The existing inner namespaces (`Olden_Era___Template_Editor.Models`, `OldenEra___Template_Editor.Services`, `OldenEraTemplateEditor.Models`) all collapse into a single new namespace tree under `OldenEra.Generator.*` once moved.**

---

## Phase 1 — Library extraction (mechanical, no behavior change)

**Phase goal:** The `TemplateGenerator`, all generator/template models, and the (still WPF-based) preview renderer move out of the app project into a new `OldenEra.Generator` class library. The WPF app and the test project both consume the library. After Phase 1, byte-for-byte identical templates and PNGs as before.

**Phase 1 validation gate (run before declaring Phase 1 done):**
1. `dotnet build "Olden Era - Template Editor.slnx"` succeeds.
2. `dotnet test "Olden Era - Template Editor.slnx"` — all existing tests pass.
3. **[Windows-only verification]** Launch WPF app, generate one Random topology template at default settings, save. Diff the generated `.rmg.json` against a baseline captured before Phase 1 started. Expect byte-identical output (modulo any non-deterministic seed).

### Task 1.0: Capture pre-refactor baseline templates **[Windows-only verification]**

**Why:** Provides the diff target for the Phase 1 validation gate. Without this, "byte-identical" is unverifiable.

**Files:**
- Create: `docs/plans/baselines/` (directory)
- Create: `docs/plans/baselines/baseline-random-default.rmg.json`
- Create: `docs/plans/baselines/baseline-hub-default.rmg.json`
- Create: `docs/plans/baselines/baseline-tournament-2p.rmg.json`

**Step 1:** Build and run WPF app on Windows.

**Step 2:** With three preset configurations (Random/default, Hub/default, Tournament 2-player), click Generate & Save and place the resulting `.rmg.json` files into `docs/plans/baselines/` with the names above.

**Step 3:** Commit:
```bash
git add docs/plans/baselines/
git commit -m "test: capture pre-refactor template baselines for Phase 1 diff"
```

> **macOS note:** If running on Mac, skip this task and accept that the validation gate becomes "tests pass + manual eyeball on Windows later." Mark Phase 1 completion as provisional until Windows verification happens.

---

### Task 1.1: Add empty `OldenEra.Generator` library project to solution **[macOS-OK]**

**Files:**
- Create: `OldenEra.Generator/OldenEra.Generator.csproj`
- Modify: `Olden Era - Template Editor.slnx`

**Step 1:** Create the directory and csproj.

```bash
mkdir -p OldenEra.Generator
```

`OldenEra.Generator/OldenEra.Generator.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>OldenEra.Generator</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

**Step 2:** Add the project to the solution.

Run: `dotnet sln "Olden Era - Template Editor.slnx" add "OldenEra.Generator/OldenEra.Generator.csproj"`

**Step 3:** Verify it builds (empty project should build trivially).

Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: build succeeds, three projects build (WPF app, tests, new library).

**Step 4:** Commit.
```bash
git add OldenEra.Generator/ "Olden Era - Template Editor.slnx"
git commit -m "feat(library): scaffold empty OldenEra.Generator class library"
```

---

### Task 1.2: Add WPF app's project reference to the library **[macOS-OK]**

**Why:** Wire the dependency before moving any code, so each subsequent move-task can verify both projects still compile.

**Files:**
- Modify: `Olden Era - Template Editor/Olden Era - Template Editor.csproj`

**Step 1:** Add `<ProjectReference>` block.

```xml
<ItemGroup>
  <ProjectReference Include="..\OldenEra.Generator\OldenEra.Generator.csproj" />
</ItemGroup>
```

**Step 2:** Build.

Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: build succeeds.

**Step 3:** Commit.
```bash
git add "Olden Era - Template Editor/Olden Era - Template Editor.csproj"
git commit -m "chore(wpf): reference OldenEra.Generator library"
```

---

### Task 1.3: Move `Models/Generator/*` to library and renamespace **[macOS-OK]**

**Files:**
- Move: `Olden Era - Template Editor/Models/Generator/GeneratorSettings.cs` → `OldenEra.Generator/Models/Generator/GeneratorSettings.cs`
- Move: `Olden Era - Template Editor/Models/Generator/MapTopology.cs` → `OldenEra.Generator/Models/Generator/MapTopology.cs`
- Move: `Olden Era - Template Editor/Models/Generator/SettingsFile.cs` → `OldenEra.Generator/Models/Generator/SettingsFile.cs`

**Step 1:** Move the files.
```bash
mkdir -p OldenEra.Generator/Models/Generator
git mv "Olden Era - Template Editor/Models/Generator/"*.cs OldenEra.Generator/Models/Generator/
```

**Step 2:** In each moved file, change the namespace:

From: `namespace Olden_Era___Template_Editor.Models`
To:   `namespace OldenEra.Generator.Models`

**Step 3:** Build — expect failures referencing the old namespace.

Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: failures in the WPF project at every `using Olden_Era___Template_Editor.Models;` and at unqualified type references like `GeneratorSettings`, `MapTopology`.

**Step 4:** Update `using` statements project-wide in the WPF and Tests projects.

Strategy: replace all `using Olden_Era___Template_Editor.Models;` with `using OldenEra.Generator.Models;` in `.cs` files inside both `Olden Era - Template Editor/` and `Olden Era - Template Editor.Tests/`.

```bash
# macOS / BSD sed needs the empty backup arg
grep -rl "using Olden_Era___Template_Editor.Models;" \
  "Olden Era - Template Editor" "Olden Era - Template Editor.Tests" \
  | xargs sed -i '' 's|using Olden_Era___Template_Editor\.Models;|using OldenEra.Generator.Models;|g'
```

**Step 5:** Build again until clean.

Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: build succeeds.

**Step 6:** Test.

Run: `dotnet test "Olden Era - Template Editor.slnx"`
Expected: all tests pass.

**Step 7:** Commit.
```bash
git add -A
git commit -m "refactor: move generator settings models into OldenEra.Generator library"
```

---

### Task 1.4: Move `Models/Unfrozen/*` to library and renamespace **[macOS-OK]**

**Why:** These are the output template DTOs (`RmgTemplate`, `Zone`, `Variant`, etc.). Currently in namespace `OldenEraTemplateEditor.Models` (note: no underscores — different from the generator settings namespace).

**Files:**
- Move: `Olden Era - Template Editor/Models/Unfrozen/*.cs` → `OldenEra.Generator/Models/Unfrozen/*.cs`

**Step 1:** Move.
```bash
mkdir -p OldenEra.Generator/Models/Unfrozen
git mv "Olden Era - Template Editor/Models/Unfrozen/"*.cs OldenEra.Generator/Models/Unfrozen/
```

**Step 2:** In each moved file, change namespace.

From: `namespace OldenEraTemplateEditor.Models`
To:   `namespace OldenEra.Generator.Models.Unfrozen`

(Distinct sub-namespace because both moved sets currently coexist in `OldenEra.Generator.Models` and have type-name collisions risk; Unfrozen gets its own sub-namespace to keep the boundary clean.)

**Step 3:** Update consumers.

```bash
grep -rl "using OldenEraTemplateEditor.Models;" \
  "Olden Era - Template Editor" "Olden Era - Template Editor.Tests" "OldenEra.Generator" \
  | xargs sed -i '' 's|using OldenEraTemplateEditor\.Models;|using OldenEra.Generator.Models.Unfrozen;|g'
```

**Step 4:** Build, fix any remaining ambiguities (likely none, but watch for files that referenced both namespaces).

Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: build succeeds.

**Step 5:** Test.

Run: `dotnet test "Olden Era - Template Editor.slnx"`
Expected: all tests pass.

**Step 6:** Commit.
```bash
git add -A
git commit -m "refactor: move RmgTemplate DTOs into OldenEra.Generator.Models.Unfrozen"
```

---

### Task 1.5: Move `TemplateGenerator.cs` to library and renamespace **[macOS-OK]**

**Files:**
- Move: `Olden Era - Template Editor/Services/TemplateGenerator.cs` → `OldenEra.Generator/Services/TemplateGenerator.cs`

**Step 1:** Move.
```bash
mkdir -p OldenEra.Generator/Services
git mv "Olden Era - Template Editor/Services/TemplateGenerator.cs" OldenEra.Generator/Services/
```

**Step 2:** Change namespace.

From: `namespace Olden_Era___Template_Editor.Services`
To:   `namespace OldenEra.Generator.Services`

**Step 3:** Update `using` statements at the top of the moved file:
- Replace `using Olden_Era___Template_Editor.Models;` → `using OldenEra.Generator.Models;`
- Replace `using OldenEraTemplateEditor.Models;` → `using OldenEra.Generator.Models.Unfrozen;`

**Step 4:** Update consumers (WPF + Tests).

```bash
grep -rl "using Olden_Era___Template_Editor.Services;" \
  "Olden Era - Template Editor" "Olden Era - Template Editor.Tests" \
  | xargs sed -i '' 's|using Olden_Era___Template_Editor\.Services;|using OldenEra.Generator.Services;|g'
```

**Step 5:** Build.

Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: build succeeds.

**Step 6:** Test — this is the major checkpoint. The generator is now in the library; tests must still pass.

Run: `dotnet test "Olden Era - Template Editor.slnx"`
Expected: all tests pass.

**Step 7:** Commit.
```bash
git add -A
git commit -m "refactor: move TemplateGenerator into OldenEra.Generator.Services"
```

---

### Task 1.6: Move `TemplatePreviewPngWriter.cs` to library (still WPF-based) **[Windows-only verification]**

**Why:** Phase 2 will rewrite this with SkiaSharp. For now, move it as-is to verify the namespace plumbing works for a WPF-typed class. This means the **library temporarily depends on WPF**, which we accept transiently because Phase 2 removes it.

> **Skip-and-defer alternative:** If you want a clean macOS-buildable library *now*, leave `TemplatePreviewPngWriter.cs` in the WPF project for Phase 1 and let Phase 2 delete it directly when the SkiaSharp renderer lands. **Recommended path: skip this task entirely.** Phase 2 introduces `TemplatePreviewRenderer` in the library and Phase 2's final step deletes the old PNG writer. This gives us a library that builds on macOS at the end of Phase 1.

**Decision recorded:** Skip Task 1.6. Move on to 1.7.

---

### Task 1.7: Repoint test project to library only **[macOS-OK]**

**Why:** Tests should reference the library directly for the moved types. They currently reference the WPF project; after Phase 1, ideally they reference the library only. Defer the `<TargetFramework>` change (still `net10.0-windows`) until Phase 2 since `TemplatePreviewPngWriter` lives in the WPF app and tests may touch it.

**Files:**
- Modify: `Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj`

**Step 1:** Add a project reference to the library while keeping the WPF project reference (the WPF project currently still owns the PNG writer; tests need both).

```xml
<ItemGroup>
  <ProjectReference Include="..\Olden Era - Template Editor\Olden Era - Template Editor.csproj" />
  <ProjectReference Include="..\OldenEra.Generator\OldenEra.Generator.csproj" />
</ItemGroup>
```

**Step 2:** Build + test.

Run: `dotnet test "Olden Era - Template Editor.slnx"`
Expected: all tests pass.

**Step 3:** Commit.
```bash
git add "Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj"
git commit -m "test: reference OldenEra.Generator library directly from tests"
```

---

### Task 1.8: Run Phase 1 validation gate **[Windows-only verification]**

**Step 1:** `dotnet build "Olden Era - Template Editor.slnx"` — green.

**Step 2:** `dotnet test "Olden Era - Template Editor.slnx"` — green.

**Step 3:** Launch WPF app on Windows. Generate templates with the same three preset configurations as Task 1.0 (Random/default, Hub/default, Tournament 2-player). Diff each against `docs/plans/baselines/baseline-*.rmg.json`. Expect byte-identical output.

**Step 4:** If any diff is non-empty, investigate before continuing — Phase 1 must be regression-free.

**Step 5:** Commit a checkpoint note (no code change):
```bash
git commit --allow-empty -m "chore: Phase 1 validation gate passed"
```

---

## Phase 2 — SkiaSharp preview renderer

**Phase goal:** Replace `TemplatePreviewPngWriter.cs` (WPF) with a new `TemplatePreviewRenderer` in the library that uses SkiaSharp. After Phase 2, the library has zero WPF dependency. WPF app uses the library's renderer via a small adapter.

**Phase 2 validation gate:**
1. All existing tests pass.
2. New golden-image tests pass on macOS and Windows producing identical hashes.
3. **[Windows-only verification]** Manual visual diff: generate 6 representative templates (Random, Ring, Hub, Chain, Tournament, City Hold), eyeball PNGs side-by-side against pre-Phase-2 captures. No visual regressions.

### Task 2.1: Add SkiaSharp NuGet package to library

`OldenEra.Generator.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="SkiaSharp" Version="3.*" />
</ItemGroup>
```

Build, commit.

### Task 2.2: Bundle the preview font as embedded resource

- Pick an SIL OFL or Apache-licensed sans-serif (e.g. Inter, Noto Sans).
- Drop `.ttf` into `OldenEra.Generator/Assets/PreviewFont.ttf`.
- Add to csproj: `<EmbeddedResource Include="Assets/PreviewFont.ttf" />`.
- Add a small helper `LoadEmbeddedTypeface()` returning `SKTypeface`.

Test: a unit test that loads the typeface and asserts non-null.

### Task 2.3: Define `TemplatePreviewRenderer` public API and stub

Public surface:
```csharp
public static class TemplatePreviewRenderer
{
    public const int Width = 700;
    public const int Height = 700;
    public static byte[] RenderPng(RmgTemplate template, MapTopology topology = MapTopology.Default);
    public static IReadOnlyDictionary<string, (double X, double Y)> ComputeLayout(
        RmgTemplate template, MapTopology topology = MapTopology.Default);
}
```

Implement `ComputeLayout` first (port from existing — this is pure math, no rendering).

Test: a unit test on `ComputeLayout` with a known template fixture asserting positions match expected coordinates.

### Task 2.4: Port background, zone fills/borders, and labels

Port these regions of `TemplatePreviewPngWriter.cs` to SkiaSharp. After this task, a generated PNG should show all zones colored correctly with letter labels, but no connections.

Add a golden-image test for one Random-topology template at this checkpoint.

### Task 2.5: Port straight-line connections

Add gold/blue lines for direct/portal connections. Update the golden test.

### Task 2.6: Port curved connections + bridge gaps

The trickiest visual element. Port `PathGeometry` Bezier/Arc segments to `SKPath.QuadTo`/`CubicTo`/`ArcTo`. Update golden tests to cover at least one curved-connection case.

### Task 2.7: Port the hold-city golden house icon

If currently a font glyph, replace with an explicit `SKPath` so it's font-independent.

### Task 2.8: Add WPF adapter

`Olden Era - Template Editor/Services/WpfPreviewAdapter.cs`:
```csharp
public static BitmapImage Decode(byte[] png) { /* MemoryStream → BitmapImage */ }
```

Wire `MainWindow.xaml.cs` line 893 to call `TemplatePreviewRenderer.RenderPng(...)` then `WpfPreviewAdapter.Decode(...)`.

Wire `MainWindow.xaml.cs` line 953 to write the byte array to disk via `File.WriteAllBytes`.

### Task 2.9: Delete `TemplatePreviewPngWriter.cs` and remove WPF dependency from tests

- Delete the old PNG writer.
- Remove `<UseWPF>` if it was added to the library (it should not have been).
- Change `Olden Era - Template Editor.Tests.csproj` `<TargetFramework>` from `net10.0-windows` to `net10.0` if no remaining WPF references.
- Remove the test project's reference to the WPF project; keep only the library reference.

### Task 2.10: Run Phase 2 validation gate

- All tests pass on macOS (`dotnet test` from terminal).
- Golden-image hashes identical on macOS and Windows.
- [Windows-only] visual diff against pre-Phase-2 captures.

Commit a checkpoint.

---

## Phase 3 — Blazor WebAssembly frontend

**Phase goal:** A `OldenEra.Web` Blazor WASM project that lets the user configure settings, click Generate, see a preview, and download the `.rmg.json` + PNG. Runs entirely in the browser; no backend.

**Phase 3 validation gate:**
1. `dotnet run --project OldenEra.Web` on macOS — page loads in browser.
2. Generate flow works end-to-end: settings → JSON download + PNG preview.
3. Cross-platform parity test: same `GeneratorSettings` produces identical JSON hash from WPF and Web hosts.

### Task 3.1: Scaffold `OldenEra.Web` Blazor WASM project

```bash
dotnet new blazorwasm -n OldenEra.Web -o OldenEra.Web --framework net10.0
dotnet sln "Olden Era - Template Editor.slnx" add OldenEra.Web/OldenEra.Web.csproj
```

Add reference to `OldenEra.Generator`. Strip the default Counter/Weather demo content. Verify `dotnet run --project OldenEra.Web` serves a blank page locally on macOS. Commit.

### Task 3.2: Port theme to CSS

Translate `Themes/MedievalTheme.xaml` colors and styles to `wwwroot/app.css`. Apply to a placeholder layout.

### Task 3.3: Build `Index.razor` skeleton

Page structure mirroring `MainWindow.xaml`'s top-level sections: Template Name, Player Count, Map Size, Topology, Generate button. No advanced settings yet.

Bind to a `GeneratorSettings` instance held in the page's component state.

### Task 3.4: Wire Generate button to library

On click:
1. Call `TemplateGenerator.Generate(settings)`.
2. Serialize to JSON via `System.Text.Json` with `WriteIndented = true`, `DefaultIgnoreCondition = WhenWritingNull`.
3. Render PNG via `TemplatePreviewRenderer.RenderPng(...)`.
4. Display PNG inline as `<img src="data:image/png;base64,..."/>`.
5. Stash JSON bytes for download.

### Task 3.5: Implement `FileDownloader` JS interop

`wwwroot/js/download.js` + `Services/FileDownloader.cs` to push `byte[]` as a browser download with the user's chosen filename.

Wire a "Download .rmg.json" button to invoke it.

### Task 3.6: Add the rest of the settings UI in components

Split into `HeroSettingsPanel.razor`, `ZoneConfigPanel.razor`, `AdvancedPanel.razor`, `WinConditionsPanel.razor`, `TopologyPicker.razor`, `PreviewPanel.razor`. Each owns one section of bindings.

Iterate one component per task; commit after each.

### Task 3.7: Implement `BrowserSettingsStore`

`localStorage`-backed JSON serialization of `SettingsFile`. Save on every change (debounced 500ms). Load on app startup.

### Task 3.8: Implement `.oetgs` Import/Export

Buttons that trigger File System Access API where available, fall back to download/upload `<input type="file">`.

### Task 3.9: Implement `UpdateChecker`

Port `CheckForUpdateAsync` from `MainWindow.xaml.cs` lines 82–. Show a banner if a newer release tag exists. `HttpClient` works in WASM; CORS is a non-issue for `api.github.com`.

### Task 3.10: Add long-generation warning modal

Detect "large map" heuristically (e.g. map size ≥ X or zone count ≥ Y) and show "Generation may take several minutes" before kicking off.

### Task 3.11: Add cross-platform parity test

In the test project, write a test that constructs three fixed `GeneratorSettings`, calls `TemplateGenerator.Generate`, serializes with the same options the web project uses, and asserts on a known hash. The test runs from `dotnet test` and protects against any future divergence between hosts.

### Task 3.12: Run Phase 3 validation gate

`dotnet run --project OldenEra.Web` from macOS, click through every setting, generate a Random-default template, verify the downloaded JSON matches what the WPF app produces for the same settings. Commit checkpoint.

---

## Phase 4 — Deployment to GitHub Pages

**Phase goal:** Push to `main` auto-deploys the web app to `https://khandevelopsgames.github.io/Olden-Era---Template-Generator/`.

### Task 4.1: Add `.github/workflows/deploy-web.yml`

Standard Blazor + Pages workflow:
1. Checkout, setup .NET 10.
2. `dotnet publish OldenEra.Web -c Release -o publish`.
3. Rewrite base href in `publish/wwwroot/index.html` from `/` to `/Olden-Era---Template-Generator/`.
4. `touch publish/wwwroot/.nojekyll`.
5. `peaceiris/actions-gh-pages@v3` to push to `gh-pages` branch.

### Task 4.2: Configure GitHub Pages settings on the repo

Settings → Pages → Source: `gh-pages` branch, root.

(Manual one-time GUI step. Document it in the README.)

### Task 4.3: Cross-link distributions

- WPF: add a hyperlink "Try the web version" near the existing GitHub releases link.
- Web: footer link "Want Steam auto-detect? Download the Windows app."
- README: add "Web version" section with the Pages URL.

### Task 4.4: Trigger first deploy and verify

Push to `main`, watch Actions tab, open the Pages URL on iPhone Safari, generate a small template, AirDrop to a Windows machine, confirm it loads in-game.

---

## Done criteria

- Web users can open the URL on any platform, generate a template, and download `.rmg.json` + PNG.
- Windows users continue to use the WPF app with Steam auto-detect, unchanged in behavior.
- All tests pass; golden-image tests lock the renderer; cross-platform parity test locks JSON output.
- macOS dev workflow: `dotnet test` and `dotnet run --project OldenEra.Web` both work without a Windows machine.
- The WPF app, the web app, and the test project all consume `OldenEra.Generator` as their single source of truth for generation logic.
