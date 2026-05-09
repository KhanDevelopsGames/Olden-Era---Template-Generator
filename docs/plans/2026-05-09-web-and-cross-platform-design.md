# Web + Cross-Platform Distribution Design

**Date:** 2026-05-09
**Status:** Approved, ready for implementation planning
**Scope:** Add a Blazor WebAssembly version of the template generator while preserving the existing WPF Windows app, by extracting shared code into a reusable .NET library.

## Goals

1. Make the template generator usable on macOS and other non-Windows platforms.
2. Enable development from macOS without requiring a Windows host or VM.
3. Keep the existing Windows UX intact, including Steam install detection and direct save into `map_templates`.
4. Share one generator codebase between desktop and web so feature work and bug fixes apply to both.

## Non-Goals

- Performance optimization of `TemplateGenerator`. Large maps currently take up to 15 minutes; that is accepted as a known issue and tracked separately. Web users may see roughly 1.5–2× longer due to WebAssembly overhead.
- Mobile-friendly responsive design.
- PWA / offline-first behavior beyond what the Blazor runtime caches by default.
- Internationalization.
- Replacing the WPF app. The desktop app remains the recommended option for Windows users who want Steam auto-detect.

## Architecture

One shared class library, two thin UI hosts.

```
Olden Era - Template Editor.slnx
├── OldenEra.Generator/                  [NEW] net10.0 class library
│   ├── Models/
│   │   ├── Generator/   (GeneratorSettings, MapTopology, SettingsFile)
│   │   └── Unfrozen/    (RmgTemplate, Zone, Variant, …)
│   ├── Services/
│   │   ├── TemplateGenerator.cs         ← unchanged logic
│   │   └── TemplatePreviewRenderer.cs   [REWRITTEN] uses SkiaSharp
│   └── GameData/                        ← embedded JSON resources
│
├── Olden Era - Template Editor/         [TRIMMED] WPF app, Windows only
│   ├── MainWindow.xaml(.cs)             ← UI only, calls into library
│   ├── Services/
│   │   ├── SteamInstallDetector.cs      ← Windows-specific, stays here
│   │   └── WpfPreviewAdapter.cs         ← thin: SKBitmap → BitmapImage
│   └── References: OldenEra.Generator
│
├── OldenEra.Web/                        [NEW] Blazor WASM
│   ├── Pages/Index.razor
│   ├── Components/                      (HeroSettings, ZoneConfig, etc.)
│   ├── Services/                        (BrowserSettingsStore, FileDownloader, UpdateChecker)
│   ├── wwwroot/
│   └── References: OldenEra.Generator
│
└── Olden Era - Template Editor.Tests/   ← references library, not WPF
```

### Key decisions

- **Library target: `net10.0`.** We control all consumers, so we don't need `netstandard2.x` portability. Lets us use modern C# language features in shared code.
- **Namespace consolidation.** Replace `Olden_Era___Template_Editor.Models` and `OldenEraTemplateEditor.Models` with a single `OldenEra.Generator.Models` namespace. The triple-underscore form is a project-name artifact; consolidating now makes the library publishable and code easier to read.
- **SkiaSharp for the preview renderer.** Works in WPF (via `SKBitmap` → `WriteableBitmap`), in Blazor WASM (via `SkiaSharp.Views.Blazor`), and on macOS (if Avalonia is added later). One renderer, every host.
- **Bundled font.** Ship an open-license font (e.g. Inter or Noto Sans) as an embedded resource so previews render identically on Windows and in the browser. Without this, system font differences would produce visibly different PNGs.
- **Library is pure.** No file I/O, no Steam detection, no update checks, no settings persistence. Inputs: `GeneratorSettings`. Outputs: `RmgTemplate` + `byte[]` PNG. Each host orchestrates everything else.
- **GameData becomes embedded resources** in the library so both UIs ship with identical generator data automatically.

## Implementation Phases

The phases are designed so each one is independently shippable. We can stop after any phase and still have improved the project.

### Phase 1 — Library extraction

Mechanical refactor. No logic changes; the WPF app must continue to behave byte-for-byte identically.

**Move list:**

| From | To |
|---|---|
| `Olden Era - Template Editor/Models/Generator/*` | `OldenEra.Generator/Models/Generator/` |
| `Olden Era - Template Editor/Models/Unfrozen/*` | `OldenEra.Generator/Models/Unfrozen/` |
| `Olden Era - Template Editor/Services/TemplateGenerator.cs` | `OldenEra.Generator/Services/` |
| `Olden Era - Template Editor/GameData/GeneratorData/**` | `OldenEra.Generator/GameData/` (embedded) |

**Stays in WPF project:**

- `MainWindow.xaml(.cs)`, `App.xaml(.cs)`, `Themes/MedievalTheme.xaml`
- A new `SteamInstallDetector.cs` extracted from inline code currently in `MainWindow.xaml.cs`
- `CheckForUpdateAsync` (UI concern)
- `.oetgs` settings file load/save (UI concern)
- `TemplatePreviewPngWriter.cs` is deleted in Phase 2

**Code changes:**

- Update `using` statements to point at the new namespace.
- Switch JSON asset loading from `File.ReadAllText` to `Assembly.GetManifestResourceStream`. Localized to whichever methods currently load `generator_config.json`, content lists, etc.
- Repoint `Olden Era - Template Editor.Tests` from the WPF project to the new library.

**Validation gate before Phase 2:**

1. `dotnet build "Olden Era - Template Editor.slnx"` succeeds.
2. `dotnet test "Olden Era - Template Editor.slnx"` — all existing tests pass against the library.
3. Manual: launch WPF app, generate one template, diff JSON output against a known-good baseline. Byte-identical expected.

### Phase 2 — SkiaSharp preview renderer

Replace `TemplatePreviewPngWriter.cs` (~1,700 lines, `System.Windows.Media`) with `TemplatePreviewRenderer` in the library, using SkiaSharp.

**Public API:**

```csharp
public static class TemplatePreviewRenderer
{
    public static byte[] RenderPng(RmgTemplate template, GeneratorSettings settings);
}
```

Pure function: data in, PNG bytes out. WPF saves to disk and decodes for the in-app preview Image control via a small adapter (~10 lines: `byte[]` → `MemoryStream` → `BitmapImage`).

**Translation rules (the easy 80%):**

| WPF | SkiaSharp |
|---|---|
| `DrawingContext.DrawEllipse` | `canvas.DrawOval` |
| `DrawingContext.DrawLine` | `canvas.DrawLine` |
| `DrawingContext.DrawText` | `canvas.DrawText` |
| `DrawingContext.DrawGeometry` | `canvas.DrawPath` |
| `Color.FromRgb` | `new SKColor(r, g, b)` |
| `new Pen(brush, thickness)` | `SKPaint { Style = Stroke, StrokeWidth = thickness, … }` |
| `RenderTargetBitmap` + `PngBitmapEncoder` | `surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100)` |

**The 20% that needs care:**

1. **Curved connections and bridge gaps.** Recently implemented and visually load-bearing. Port `PathGeometry` + Bezier/Arc segments to `SKPath.QuadTo` / `CubicTo` / `ArcTo`. Easy to flip a control-point convention; needs visual diff testing.
2. **Text rendering.** Bundle the font and load via `SKTypeface.FromStream` so output matches across hosts.
3. **Hold-city golden house icon.** If the current implementation uses an emoji or font glyph, switch to a small vector path drawn directly. More reliable across platforms.
4. **Anti-aliasing.** Set `paint.IsAntialias = true` everywhere. Skia's default is off.
5. **WPF preview integration.** WPF currently writes a PNG to disk, then loads it. New flow: library returns `byte[]`; WPF saves and decodes via the adapter.

**Validation:**

1. **Golden image tests.** xUnit tests that generate a small fixed-seed template, render the PNG, hash it, and assert the hash. Lock in the output once it looks right; any future regression breaks the test instantly. Target representative cases: each topology (Random, Ring, Hub, Chain), Tournament mode, City Hold.
2. **Manual visual diff.** Generate the same 5–6 templates before and after; eyeball side-by-side. Bundled font + explicit AA settings should make them near-identical.
3. **Cross-platform test runs.** SkiaSharp tests work on macOS via `dotnet test`. No more "wait for a Windows machine to verify."

### Phase 3 — Blazor WASM frontend

XAML → Razor port of `MainWindow.xaml`. The 945-line XAML maps to roughly six component files of 100–200 lines each.

**Project shape:**

```
OldenEra.Web/                    .NET 10 Blazor WASM
├── Pages/
│   └── Index.razor              ← main page, all settings + generate button
├── Components/
│   ├── HeroSettingsPanel.razor
│   ├── ZoneConfigPanel.razor
│   ├── AdvancedPanel.razor
│   ├── TopologyPicker.razor
│   ├── WinConditionsPanel.razor
│   └── PreviewPanel.razor
├── Services/
│   ├── BrowserSettingsStore.cs  ← localStorage-backed implicit persistence
│   ├── FileDownloader.cs        ← JS interop: byte[] → browser download
│   └── UpdateChecker.cs         ← GitHub release check (HttpClient works in WASM)
├── wwwroot/
│   ├── index.html
│   ├── app.css                  ← port of MedievalTheme.xaml
│   └── favicon.ico
└── References: OldenEra.Generator
```

**Settings persistence:**

- Implicit (remembered between visits) → `localStorage`, JSON-serialized `SettingsFile`.
- Explicit `.oetgs` Import/Export → File System Access API where supported, with download/upload fallback for Firefox/Safari.

**Generate flow:**

1. User clicks Generate.
2. `TemplateGenerator.Generate(settings)` runs in the WASM thread → `RmgTemplate`.
3. `JsonSerializer.Serialize(template)` → `byte[]`.
4. `TemplatePreviewRenderer.RenderPng(template, settings)` → `byte[]`.
5. JS interop triggers a download, plus inline preview rendering.

**Long-generation UX:**

WASM is single-threaded. A 15-minute generation will freeze the tab. Mitigation:

- Show a "Generating, this may take several minutes for large maps" modal with progress indicator before generation begins.
- Accept that the tab is unresponsive during generation.
- Defer Web Worker offload to a follow-up if real users hit the browser "page not responding" dialog often enough to matter.

**What is intentionally not in this phase:**

- Steam detection (impossible in browser; remains a Windows-only WPF feature)
- Mobile-responsive layout
- PWA / offline manifest
- i18n

### Phase 4 — Deployment

GitHub Actions workflow on push to `main`:

1. `dotnet publish OldenEra.Web -c Release -o publish`
2. Set base href to `/Olden-Era---Template-Generator/` to match the repo subpath.
3. Add `.nojekyll` so GitHub Pages serves Blazor's `_framework/` folder.
4. Push `publish/wwwroot` to the `gh-pages` branch.
5. Pages serves the site at `https://khandevelopsgames.github.io/Olden-Era---Template-Generator/`.

Workflow is ~30 lines of standard Blazor + Pages template. Free hosting; auto-deploys on every push to `main`.

**Cross-linking between distributions:**

- WPF app: add a "Try the web version" link next to the existing GitHub releases link.
- Web app: footer link reading "Want Steam auto-detect? Download the Windows app."

## Testing Strategy

- **Existing xUnit tests** continue to run against the library and must pass at every phase boundary.
- **Golden image tests** lock in PNG renderer output across platforms.
- **Cross-platform parity test:** one test that runs the same `GeneratorSettings` through `TemplateGenerator.Generate` and hashes the resulting JSON. Both hosts must produce the same hash.
- **Manual end-to-end on web:** deploy to Pages, open on iPhone Safari, generate a small template, AirDrop to a Windows machine, confirm it loads in-game.

## Trade-offs and Accepted Limitations

| Trade-off | Accepted because |
|---|---|
| Web users have a manual "drop file into `map_templates`" step | Browsers cannot read the registry or write to arbitrary folders. Friends already do something similar with shared templates. |
| Web generation is ~1.5–2× slower than WPF | WASM overhead. Generator is the same; perf optimization is tracked separately. |
| Long generations freeze the browser tab | Single-threaded WASM. Modal warning communicates expectations; Web Workers deferred. |
| Two UI codebases to maintain | The shared library carries 90% of the surface area. UIs are thin and rarely change in lockstep. |
| Mac users won't have Steam auto-detect even when Olden Era ships on Mac | Browser sandbox limitation. If Mac users ask for it later, an Avalonia desktop build can be added on top of the same library. |

## Out of Scope (Future Work)

- Performance optimization of `TemplateGenerator`, including the 15-minute large-map case.
- Avalonia desktop port for native macOS app with Steam detection.
- Web Worker offload for long generations.
- Multi-language UI.
- Mobile-friendly layout.
