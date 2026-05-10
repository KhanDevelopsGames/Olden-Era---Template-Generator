# UI Rework Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Restyle both UIs (Blazor WASM + WPF) to a modern dark look with an amber accent, and restructure settings panels behind a left-nav stepper.

**Architecture:** Pure presentation change. No generator, validation, persistence, or file-I/O behavior changes. Web rolls first; WPF mirrors it after web is verified. See `docs/plans/2026-05-09-ui-rework-design.md` for the full design (palette, layout, component styling, file map).

**Tech Stack:** Blazor WebAssembly (`net10.0`), WPF (`net10.0-windows`), shared `OldenEra.Generator` library (`net10.0`), SkiaSharp 3.119, xUnit.

**Verification gates:**
- macOS: `dotnet build "Olden Era - Template Editor.slnx"` must succeed with 0 warnings, 0 errors after every commit.
- Tests: existing xUnit suite runs on CI (`windows-latest`); push the branch periodically and confirm `tests.yml` stays green. No new tests are introduced — UI changes have no automated test surface.
- Visual: each web task ends with `dotnet run --project OldenEra.Web` and a click-through of the affected surface in a browser.

**Conventions:**
- One commit per task. Keep diffs tight.
- Web-first (Phase A). WPF (Phase B) starts only after Phase A is merged or visually verified.
- Reference design tokens by their `--oe-*` CSS variable name (web) or `OeXxxBrush` resource key (WPF). No magic hex outside the token files.

---

## Phase A — Web (Blazor)

### Task A1: Replace CSS tokens (palette + typography)

**Files:**
- Modify: `OldenEra.Web/wwwroot/css/app.css` (the `:root { ... }` block at the top, ~lines 12–27, and the `html, body` block, ~29–38).

**Step 1:** Replace the `:root` block with the modern dark palette from the design doc (Section 1).

**Step 2:** Update `html, body` font stack to `Inter, "Segoe UI Variable", system-ui, sans-serif`, base size `13px`, line-height `1.5`, letter-spacing `0`.

**Step 3:** Update `h1`/`h2`/`h3`/`h4`/`h5` to use `--oe-text` (not `--oe-accent`), drop the `letter-spacing: 0.02em` rule, set sizes per design (h1 `20px`, h2 `15px`, h3 `13px`).

**Step 4:** Build.
Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: success, 0 warnings, 0 errors.

**Step 5:** Visually verify.
Run: `dotnet run --project OldenEra.Web` and load `http://localhost:5XXX`.
Expected: page loads with new dark/amber palette and modern type. Existing layout still intact (everything else still old-styled — that's fine).

**Step 6:** Commit.
```bash
git add OldenEra.Web/wwwroot/css/app.css
git commit -m "style(web): swap palette and typography tokens to modern dark"
```

---

### Task A2: Restyle button / input / select / checkbox primitives

**Files:**
- Modify: `OldenEra.Web/wwwroot/css/app.css` (the `.oe-btn`, `.oe-btn.primary`, `input[type=...]`, `select`, `.oe-check` rules).

**Step 1:** Rewrite `.oe-btn` to default variant (`--oe-surface-2` bg, 1px `--oe-border`, `--oe-text`, `4px` radius, `8px 14px` padding); add `.oe-btn.primary` (amber bg, near-black text); add `.oe-btn.ghost` (no bg, no border, underline on hover). Add `:disabled` state.

**Step 2:** Restyle `input[type=text]`, `input[type=number]`, `select`, `textarea` to share one ruleset (`--oe-surface-2` bg, 1px `--oe-border`, `4px` radius, `8px 10px`). Add `:focus-visible` 2px amber outline.

**Step 3:** Restyle `<select>` with a custom caret via `background-image` (inline SVG data URL) and `appearance: none`.

**Step 4:** Restyle `.oe-check` so the checkbox renders as a custom 16px square with amber-fill checked state. Use `appearance: none` on the underlying input and overlay a `::before` pseudo-element for the check.

**Step 5:** Build, run, click around.
Expected: every button, input, select, checkbox renders in new style. No regressions.

**Step 6:** Commit.
```bash
git add OldenEra.Web/wwwroot/css/app.css
git commit -m "style(web): restyle buttons, inputs, selects, checkboxes"
```

---

### Task A3: Restyle slider

**Files:**
- Modify: `OldenEra.Web/wwwroot/css/app.css` (`.oe-slider-row` and `input[type=range]` rules).

**Step 1:** Rewrite the `input[type=range]` rule with `appearance: none`, custom 4px track (`--oe-surface-3`), filled portion via gradient using `--oe-accent` and a CSS custom property (`--value-pct`) set inline from Razor.

**Step 2:** Restyle the thumb (`::-webkit-slider-thumb`, `::-moz-range-thumb`) — 14px round, `--oe-text` fill, 1px `--oe-border-strong` border.

**Step 3:** In `Components/SliderRow.razor`, set `style="--value-pct: @Pct%"` on the `<input type=range>`, where `Pct = (Value - Min) / (Max - Min) * 100`.

**Step 4:** Restyle the value badge: monospace, tabular-nums, `--oe-text-dim`, right-aligned next to label.

**Step 5:** Build + visually verify a slider drags smoothly with the amber fill following the thumb.

**Step 6:** Commit.
```bash
git add OldenEra.Web/wwwroot/css/app.css OldenEra.Web/Components/SliderRow.razor
git commit -m "style(web): restyle slider with amber fill and modern thumb"
```

---

### Task A4: Restyle banners, modal, spinner

**Files:**
- Modify: `OldenEra.Web/wwwroot/css/app.css` (`.oe-warning`, `.oe-update-banner`, `.oe-error`, `.oe-modal*`, `.oe-spinner`).

**Step 1:** Banners: 3px amber left border, `--oe-warn-bg` background, `13px` text, no decorative borders. `.oe-error` swaps left border to red.

**Step 2:** Modal overlay: `rgba(0,0,0,0.6)` scrim. Modal panel: `--oe-surface-1`, 1px `--oe-border`, `8px` radius, max-width `420px`, `20px` padding.

**Step 3:** Spinner: 2px ring with amber arc on `--oe-border` track. Animation unchanged.

**Step 4:** Build + click `Generate` on a small map to confirm the spinner modal renders cleanly; test the large-map confirm modal.

**Step 5:** Commit.
```bash
git add OldenEra.Web/wwwroot/css/app.css
git commit -m "style(web): restyle banners, modal, spinner"
```

---

### Task A5: Add three-column shell layout

**Files:**
- Modify: `OldenEra.Web/wwwroot/css/app.css` (`.oe-app`, `.oe-grid`, `.oe-title-bar`, `.oe-toolbar`, `.oe-preview-col`).
- Modify: `OldenEra.Web/Pages/Home.razor` (the markup outside `@code`, lines 9–132).

**Step 1:** Update `.oe-app` to `max-width: 1440px`. Add new `.oe-shell` class with `display: grid; grid-template-columns: 220px 1fr 360px; gap: 24px; align-items: start;`.

**Step 2:** Restructure `Home.razor`: keep the `<div class="oe-app">` wrapper. Add a `<header class="oe-header">` that contains the title, version badge, and (eventually) the Open/Save buttons. Below it, replace `oe-grid` with `oe-shell` containing three direct children: `<aside class="oe-nav">` (placeholder, empty for now), `<section class="oe-content">` (all the existing settings panels move here, unchanged), `<aside class="oe-preview-col">` (existing preview column, unchanged).

**Step 3:** Add `position: sticky; top: 16px;` to `.oe-nav` and `.oe-preview-col`.

**Step 4:** Build, run, eyeball.
Expected: page now has three columns. Empty left rail; settings stack in the middle; preview pinned right. No regressions in functionality.

**Step 5:** Commit.
```bash
git add OldenEra.Web/wwwroot/css/app.css OldenEra.Web/Pages/Home.razor
git commit -m "feat(web): introduce three-column shell layout"
```

---

### Task A6: Extract Map settings into its own panel component

**Files:**
- Create: `OldenEra.Web/Components/MapSettingsPanel.razor`
- Modify: `OldenEra.Web/Pages/Home.razor` (lines ~42–79: the inline "TEMPLATE" section).

**Step 1:** Create `MapSettingsPanel.razor` with a `[Parameter] public GeneratorSettings Settings { get; set; } = default!;`, `[Parameter] public bool ExperimentalMapSizes { get; set; }`, `[Parameter] public EventCallback<bool> ExperimentalMapSizesChanged { get; set; }`, `[Parameter] public EventCallback OnChanged { get; set; }`. Move the markup for Template Name, Map Size, Experimental toggle, Players slider into this component. The component computes `AvailableMapSizes` from `ExperimentalMapSizes`.

**Step 2:** In `Home.razor`, replace the inline Template section with `<MapSettingsPanel Settings="Settings" ExperimentalMapSizes="ExperimentalMapSizes" ExperimentalMapSizesChanged="OnExperimentalToggled" OnChanged="OnChanged" />`. Remove `MapSize` property, `AvailableMapSizes`, `FormatMapSize`, `OnTemplateNameInput`, `OnExperimentalToggled` from `Home.razor` if they're now exclusively used by the new component (move them to the component); leave `ExperimentalMapSizes` field in `Home.razor` since it's also passed to the settings mapper.

**Step 3:** Build.
Expected: 0 errors. Settings file open/save and persistence still work because `ExperimentalMapSizes` state lives in the parent.

**Step 4:** Run, manually verify Map controls still bind correctly (template name typing, map size dropdown, experimental toggle, players slider).

**Step 5:** Commit.
```bash
git add OldenEra.Web/Components/MapSettingsPanel.razor OldenEra.Web/Pages/Home.razor
git commit -m "refactor(web): extract Map settings into MapSettingsPanel"
```

---

### Task A7: Add SectionNav and active-section switching

**Files:**
- Create: `OldenEra.Web/Components/SectionNav.razor`
- Modify: `OldenEra.Web/wwwroot/css/app.css` (add `.oe-nav`, `.oe-nav-item`, `.oe-nav-item.active`, `.oe-nav-dot`).
- Modify: `OldenEra.Web/Pages/Home.razor`.

**Step 1:** Define an enum `enum Section { Map, Heroes, Topology, Zones, WinConditions, Advanced }` inside `Home.razor` `@code`. Add `private Section ActiveSection = Section.Map;`.

**Step 2:** Create `SectionNav.razor`:
- `[Parameter] public Section Active { get; set; }`
- `[Parameter] public EventCallback<Section> ActiveChanged { get; set; }`
- `[Parameter] public string? ValidationMessage { get; set; }` (rendered as a small footer area in the nav)
- Renders 6 buttons (one per `Section`), each calling `ActiveChanged.InvokeAsync(s)`. Active item gets `.active` class.

**Step 3:** In `Home.razor`, render `<SectionNav>` inside `<aside class="oe-nav">`. In `<section class="oe-content">`, switch on `ActiveSection` and render exactly one panel: `MapSettingsPanel`, `HeroSettingsPanel`, `TopologyPicker`, `ZoneConfigPanel`+`AdvancedPanel` together (Zones owns advanced toggle), or `WinConditionsPanel`. The "Advanced" nav item shows the `AdvancedPanel` only when `AdvancedMode == true`; otherwise its nav item is dimmed/disabled. *Decision:* keep it always selectable; if `AdvancedMode == false`, the panel renders a hint pointing to the Zones tab to enable advanced mode.

**Step 4:** CSS: `.oe-nav-item` is a button with no border, full-width, `8px 12px` padding, `--oe-text-dim` color. `.active` swaps to `--oe-text`, adds 3px amber `border-left`. Hover: `--oe-surface-2` background.

**Step 5:** Build + click through every nav item; confirm the corresponding panel renders and bindings still work.

**Step 6:** Commit.
```bash
git add OldenEra.Web/Components/SectionNav.razor OldenEra.Web/wwwroot/css/app.css OldenEra.Web/Pages/Home.razor
git commit -m "feat(web): add section nav with active-panel switching"
```

---

### Task A8: Restyle TopologyPicker as a tile grid

**Files:**
- Modify: `OldenEra.Web/Components/TopologyPicker.razor`
- Modify: `OldenEra.Web/wwwroot/css/app.css` (add `.oe-topo-grid`, `.oe-topo-tile`, `.oe-topo-tile.selected`).

**Step 1:** Rewrite the markup: a `<div class="oe-topo-grid">` containing one `<button class="oe-topo-tile">` per topology (Default/Ring, HubAndSpoke, Chain, Random). `SharedWeb` is unused per the codebase map — skip it. Each tile contains an inline SVG schematic (~64×64 viewBox) and a `<span>` label.

**Step 2:** Inline SVGs (suggest tiny stroked-line schematics):
- Ring: 4 circles arranged in a ring connected by a center loop.
- HubAndSpoke: central node, 4 spokes.
- Chain: 4 nodes in a line.
- Random: 4 nodes scattered with criss-cross lines.

**Step 3:** CSS: `.oe-topo-grid` is `display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px`. `.oe-topo-tile` is `--oe-surface-2`, 1px `--oe-border`, `6px` radius, `12px` padding, hover lifts to `--oe-surface-3`. `.selected`: 1px `--oe-accent` border + `--oe-surface-3` background.

**Step 4:** Build + verify each tile selects correctly and the bound `Settings.Topology` enum updates (existing logic — no behavior change).

**Step 5:** Commit.
```bash
git add OldenEra.Web/Components/TopologyPicker.razor OldenEra.Web/wwwroot/css/app.css
git commit -m "feat(web): restyle topology picker as tile grid with schematics"
```

---

### Task A9: Move Open/Save into header bar; remove standalone toolbar

**Files:**
- Modify: `OldenEra.Web/Pages/Home.razor` (header area + delete `.oe-toolbar` block at lines ~30–36).
- Modify: `OldenEra.Web/wwwroot/css/app.css` (delete `.oe-toolbar` rule; tighten `.oe-header` so it lays out title left, action buttons right).

**Step 1:** Move the `<button>Open settings…</button>` and `<button>Save settings…</button>` into the header next to the version badge. Use the `.oe-btn.ghost` variant. Drop the `📂` and `💾` emoji prefixes.

**Step 2:** Delete the `.oe-toolbar` `<div>` and its CSS rule.

**Step 3:** Build + click both buttons; confirm Open prompts for file, Save downloads `.oetgs`.

**Step 4:** Commit.
```bash
git add OldenEra.Web/Pages/Home.razor OldenEra.Web/wwwroot/css/app.css
git commit -m "refactor(web): move open/save into header bar"
```

---

### Task A10: Remove emoji from remaining buttons; final CSS cleanup

**Files:**
- Modify: `OldenEra.Web/Pages/Home.razor` (Generate, Download buttons).
- Modify: `OldenEra.Web/wwwroot/css/app.css` (delete any unused `.oe-*` rules left over from the medieval theme — search for selectors not referenced by any `.razor`/`.html` file).

**Step 1:** Strip emoji from `Generate Template`, `Download .rmg.json`, `Download Preview PNG`. Keep wording as in the design doc.

**Step 2:** Search-and-delete: `grep -rn "oe-" OldenEra.Web/wwwroot OldenEra.Web/Components OldenEra.Web/Pages` to find which `.oe-*` classes are referenced; remove any from `app.css` that aren't.

**Step 3:** Build + visual sweep of every section.

**Step 4:** Commit.
```bash
git add OldenEra.Web/Pages/Home.razor OldenEra.Web/wwwroot/css/app.css
git commit -m "style(web): drop button emoji and prune unused CSS"
```

---

### Task A11: Responsive breakpoints

**Files:**
- Modify: `OldenEra.Web/wwwroot/css/app.css` (add `@media (max-width: 900px)` and `@media (max-width: 600px)` blocks).
- Modify: `OldenEra.Web/Components/SectionNav.razor` (render an alternate layout under a CSS class set responsively).

**Step 1:** ≤ 900px: `.oe-shell` becomes a single column (`grid-template-columns: 1fr`). `.oe-nav` becomes a horizontal scroll strip (`display: flex; overflow-x: auto`); items become pills. `.oe-preview-col` flows to the bottom.

**Step 2:** ≤ 600px: hide pill nav (`display: none`); show a `<select class="oe-nav-select">` instead. Bind to `ActiveSection` via standard Blazor `@bind`. Add the `<select>` to `SectionNav.razor` and toggle visibility purely with CSS — no JS, no resize listener.

**Step 3:** Build + resize browser to confirm both breakpoints behave.

**Step 4:** Commit.
```bash
git add OldenEra.Web/wwwroot/css/app.css OldenEra.Web/Components/SectionNav.razor
git commit -m "feat(web): responsive nav for narrow viewports"
```

---

### Task A12: Final web pass + push

**Step 1:** Run a complete click-through:
- Open the app at default settings.
- Visit each nav section.
- Toggle advanced mode in Zones → confirm Advanced section becomes meaningful.
- Pick each topology tile → preview canvas refreshes after Generate.
- Toggle experimental map sizes → larger sizes appear.
- Save settings → `.oetgs` file downloads. Open it back → settings restore.
- Generate on a small map → preview renders, downloads work.
- Trigger validation error (e.g., empty template name) → red error sits under Generate.
- Resize to 800px and 500px → nav adapts.

**Step 2:** Build cleanly.
Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: 0 warnings, 0 errors.

**Step 3:** Push the branch.
```bash
git push -u origin feature/ui-rework
```
Watch `tests.yml` on GitHub Actions; expect green (no generator changes).

**Step 4:** *Stop here* and confirm with the user that the web rework is acceptable before starting Phase B.

---

## Phase B — WPF

### Task B1: Create ModernDarkTheme.xaml with color resources

**Files:**
- Create: `Olden Era - Template Editor/Themes/ModernDarkTheme.xaml`

**Step 1:** Create a new `ResourceDictionary` declaring `SolidColorBrush` resources mirroring the CSS tokens (`OeBgBrush`, `OeSurface1Brush`, `OeSurface2Brush`, `OeSurface3Brush`, `OeBorderBrush`, `OeBorderStrongBrush`, `OeTextBrush`, `OeTextDimBrush`, `OeTextMuteBrush`, `OeAccentBrush`, `OeAccentHiBrush`, `OeAccentDimBrush`, `OeErrorBrush`, `OeWarnBgBrush`). Use the same hex values as the design doc.

**Step 2:** Add a default font: `<FontFamily x:Key="OeFont">Segoe UI Variable, Segoe UI</FontFamily>`. (Inter isn't shipped with Windows; Segoe UI Variable on Win11 is the closest equivalent.)

**Step 3:** Build.
Run: `dotnet build "Olden Era - Template Editor.slnx"`
Expected: 0 errors. Theme isn't referenced yet, so no visible change.

**Step 4:** Commit.
```bash
git add "Olden Era - Template Editor/Themes/ModernDarkTheme.xaml"
git commit -m "feat(wpf): add ModernDarkTheme resource dictionary"
```

---

### Task B2: Swap App.xaml merged dictionaries to ModernDarkTheme

**Files:**
- Modify: `Olden Era - Template Editor/App.xaml`

**Step 1:** Replace the merged-dictionary entry pointing at `MedievalTheme.xaml` with `ModernDarkTheme.xaml`. Leave `MedievalTheme.xaml` on disk for now — deleted in B9.

**Step 2:** Build.
Expected: 0 errors. Existing controls referencing old resource keys (`OeGoldBrush`, etc.) will likely fail — that's expected; this commit may *not* run, but it should *build*. If build fails because old keys don't resolve at compile time (XAML in WPF mostly resolves at runtime), proceed; if fails at compile, alias the missing keys to new brushes in `ModernDarkTheme.xaml` as a temporary bridge.

**Step 3:** Commit.
```bash
git add "Olden Era - Template Editor/App.xaml" "Olden Era - Template Editor/Themes/ModernDarkTheme.xaml"
git commit -m "refactor(wpf): point App.xaml at ModernDarkTheme"
```

> **Note:** App is unrunnable on macOS. Visual verification of WPF lands when CI builds and a Windows reviewer runs the binary. Document this in the PR.

---

### Task B3: Restyle Button / TextBox / ComboBox / CheckBox styles

**Files:**
- Modify: `Olden Era - Template Editor/Themes/ModernDarkTheme.xaml`

**Step 1:** Add implicit `Style TargetType="Button"` (default surface-2 bg, 1px border, 4px radius via `Border` in `ControlTemplate`, padding `8,4`). Add `Style x:Key="OePrimaryButton"` (amber bg, near-black text). Add `Style x:Key="OeGhostButton"` (transparent, underline-on-hover via trigger).

**Step 2:** Add implicit styles for `TextBox`, `ComboBox`, `CheckBox` matching the web equivalents. ComboBox needs a `ControlTemplate` override for the toggle button arrow; CheckBox needs a `ControlTemplate` to render the custom 16px square.

**Step 3:** Build.
Expected: 0 errors.

**Step 4:** Commit.
```bash
git add "Olden Era - Template Editor/Themes/ModernDarkTheme.xaml"
git commit -m "style(wpf): restyle button, textbox, combobox, checkbox"
```

---

### Task B4: Restyle Slider with ControlTemplate

**Files:**
- Modify: `Olden Era - Template Editor/Themes/ModernDarkTheme.xaml`

**Step 1:** Add a `Style TargetType="Slider"` with a `ControlTemplate` that renders a 4px track in `OeSurface3Brush`, a filled portion in `OeAccentBrush` driven by a `Track` element, and a 14px round thumb in `OeTextBrush` with 1px `OeBorderStrongBrush`.

**Step 2:** Build + commit.
```bash
git add "Olden Era - Template Editor/Themes/ModernDarkTheme.xaml"
git commit -m "style(wpf): restyle slider with amber fill"
```

---

### Tasks B5–B10: Extract per-section UserControls

**For each section, repeat the pattern below:**

| # | Section | UserControl path |
|---|---------|------------------|
| B5 | Map | `Views/MapPanel.xaml(.cs)` |
| B6 | Heroes | `Views/HeroesPanel.xaml(.cs)` |
| B7 | Topology | `Views/TopologyPanel.xaml(.cs)` |
| B8 | Zones | `Views/ZonesPanel.xaml(.cs)` |
| B9a | WinConditions | `Views/WinConditionsPanel.xaml(.cs)` |
| B9b | Advanced | `Views/AdvancedPanel.xaml(.cs)` |

**Files (per section):**
- Create: `Olden Era - Template Editor/Views/<Name>Panel.xaml`
- Create: `Olden Era - Template Editor/Views/<Name>Panel.xaml.cs`
- Modify: `Olden Era - Template Editor/MainWindow.xaml` (cut the section's controls; replace with `<views:<Name>Panel x:Name="..."/>`).
- Modify: `Olden Era - Template Editor/MainWindow.xaml.cs` (rewire control references — most accessors become `MapPanel.TplName.Text`, etc.).

**Step 1:** Create the UserControl. Move the section's XAML markup verbatim. Expose any controls referenced from `MainWindow.xaml.cs` via `x:Name` so the parent can still reach them with `<panelName>.<ctrlName>`.

**Step 2:** Update `MainWindow.xaml.cs` references — `BuildSettings`, `ApplySettings`, `Validate` all need their member-access paths updated. Keep the methods in `MainWindow.xaml.cs`; they just walk through the new panel hierarchy.

**Step 3:** Build.
Expected: 0 errors.

**Step 4:** Commit.
```bash
git add "Olden Era - Template Editor/Views/<Name>Panel.xaml" "Olden Era - Template Editor/Views/<Name>Panel.xaml.cs" "Olden Era - Template Editor/MainWindow.xaml" "Olden Era - Template Editor/MainWindow.xaml.cs"
git commit -m "refactor(wpf): extract <Name>Panel UserControl"
```

> One commit per panel — six commits total. If a binding regresses, bisect lands on a single panel.

---

### Task B11: MainWindow shell with ListBox nav + ContentControl

**Files:**
- Modify: `Olden Era - Template Editor/MainWindow.xaml`
- Modify: `Olden Era - Template Editor/MainWindow.xaml.cs`

**Step 1:** Replace the existing settings-stack `Grid` with a 3-column grid (`220, *, 360`). Left column: a `ListBox` with `ItemsPanel=StackPanel` and items "Map", "Heroes", "Topology", "Zones", "Win Conditions", "Advanced". Strip ListBox/ListBoxItem chrome with a `Style` that adopts the new active-item left-edge amber bar. Middle column: a `ContentControl` whose content is set by the active nav item — bind via a switch in code-behind that swaps which `<Panel>` is `Visible`/`Collapsed`.

**Step 2:** Move Open/Save and the version badge into a top header `Grid.Row=0`. Use the `OeGhostButton` style.

**Step 3:** Build + commit.
```bash
git add "Olden Era - Template Editor/MainWindow.xaml" "Olden Era - Template Editor/MainWindow.xaml.cs"
git commit -m "feat(wpf): three-column shell with section nav"
```

---

### Task B12: Restyle topology tiles in TopologyPanel

**Files:**
- Modify: `Olden Era - Template Editor/Views/TopologyPanel.xaml`

**Step 1:** Replace the radio-button row with a `ListBox` whose `ItemsPanel` is `UniformGrid Columns=2`. Each item is a tile rendered via `ItemTemplate` with a small `Path` element drawing the schematic and a `TextBlock` label. Selection: 1px `OeAccentBrush` border + `OeSurface3Brush` background via `ItemContainerStyle` triggers.

**Step 2:** Bind `SelectedItem` (or `SelectedValue`) to whatever `MainWindow.xaml.cs` currently uses for topology selection.

**Step 3:** Build + commit.
```bash
git add "Olden Era - Template Editor/Views/TopologyPanel.xaml"
git commit -m "style(wpf): restyle topology picker as tile grid"
```

---

### Task B13: Delete MedievalTheme.xaml

**Files:**
- Delete: `Olden Era - Template Editor/Themes/MedievalTheme.xaml`
- Modify: `Olden Era - Template Editor/Olden Era - Template Editor.csproj` (remove any `<Resource Include=...MedievalTheme.xaml />` entry, if present).

**Step 1:** Confirm no remaining `<ResourceDictionary Source=...MedievalTheme.xaml/>` references via `grep -rn MedievalTheme "Olden Era - Template Editor/"`.

**Step 2:** Delete the file. Build.

**Step 3:** Commit.
```bash
git add -A
git commit -m "chore(wpf): remove obsolete MedievalTheme"
```

---

### Task B14: Final WPF push and CI verification

**Step 1:** Push the branch and watch CI.
```bash
git push
```
Expected: `tests.yml` green on `windows-latest`.

**Step 2:** Coordinate with a Windows reviewer for visual sign-off. The reviewer runs the WPF binary and clicks through every panel + topology tile + Open/Save + Generate.

**Step 3:** When approved, finish the development branch using `superpowers:finishing-a-development-branch`.

---

## Out of scope (do not do)

- No icon library, no SVG asset pipeline beyond the four inline topology schematics (web) and four `Path` schematics (WPF).
- No animation/transition system. `transition: background-color 80ms` (web) and the existing default WPF transitions are it.
- No theme switcher. Dark only.
- No layout breakpoint below 600px on WPF (desktop-only host).
- No MVVM framework migration on WPF — keep code-behind, just push it into the per-section UserControls.
- No automated UI tests. Visual verification stays manual; the `xUnit` suite covers generator and renderer correctness only.
