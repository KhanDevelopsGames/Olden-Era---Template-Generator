# UI Rework — Modern Dark Restyle + Stepper Layout

**Date:** 2026-05-09
**Scope:** Both UI surfaces — `OldenEra.Web` (Blazor WASM) and `Olden Era - Template Editor` (WPF) — kept in sync.
**Driver:** Visual style feels dated. Drop the medieval framing for a modern dark UI; restructure settings panels into a left-nav stepper.

## Goals

- Modern dark UI: neutral near-black surfaces, single amber accent, flat surfaces, sans-serif type.
- Replace the long stacked-panels layout with a left-nav stepper that focuses one section at a time.
- Keep Blazor and WPF visually and structurally aligned.
- No behavior changes to generator, validation, persistence, or file I/O.

## Non-goals (YAGNI)

- No icon library or SVG asset pipeline beyond 5 inline topology schematics.
- No animation system. Single `transition: background-color 80ms` on hover is the only motion.
- No theme switcher. Dark only.
- No mobile breakpoint below 600px on WPF (desktop-only host).
- No MVVM framework migration on WPF — keep code-behind, just push it into per-section UserControls.

## Section 1 — Visual language & tokens

Palette:

```
--oe-bg:            #0B0D10
--oe-surface-1:     #14171C
--oe-surface-2:     #1B1F26
--oe-surface-3:     #232831
--oe-border:        #2A2F39
--oe-border-strong: #3A4150
--oe-text:          #E6E8EB
--oe-text-dim:      #9098A3
--oe-text-mute:     #5F6671
--oe-accent:        #F59E0B
--oe-accent-hi:     #FBBF24
--oe-accent-dim:    #B45309
--oe-success:       #10B981
--oe-error:         #EF4444
--oe-warn-bg:       #2A1F0A
```

Typography: `Inter, "Segoe UI Variable", system-ui, sans-serif`. Tabular numerals on sliders. Scale: `13px` base, `12px` label, `15px` section title, `20px` page title. Letter-spacing `0`. Headings use `--oe-text`; amber is reserved for affordance.

Surfaces: flat, no glow. 1px `--oe-border` hairlines. `border-radius: 6px` panels, `4px` inputs/buttons. No drop shadows except modal overlay.

Focus: 2px amber outline via `:focus-visible`. Hover lifts surface one step.

## Section 2 — Layout & navigation

Three-column shell on web (≥ 1024px): `220px nav | 1fr content | 360px preview`. Max width `1440px`, centered. Nav and preview are sticky; content scrolls.

Nav items: Map · Heroes · Topology · Zones · Win Conditions · Advanced. Each shows a status dot for validation issues (red) or pending changes (amber). Active item: left-edge amber bar + brighter text. Text-only, no icons.

Section panel: title + one-line description, then controls. Single column. `gap: 14px` between rows, `24px` between subsections. Sections demarcated by spacing and a hairline rule, not nested cards.

Preview column: `Generate` button at top, 700×700 canvas letterboxed, downloads below. Validation message and warnings sit directly under `Generate`.

Toolbar: Open/Save move into the top-right of the header bar as ghost buttons. The current standalone `.oe-toolbar` row goes away.

Responsive (web only):
- ≤ 900px — nav becomes a horizontal pill strip; preview drops below content.
- ≤ 600px — pill strip becomes `<select>`.

WPF mirror: `Grid` with three columns; `ListBox` (chrome stripped) drives a `ContentControl` swapping `DataTemplate`s per section. `MainWindow.xaml` splits into one `UserControl` per section (mirrors `OldenEra.Web/Components/`).

## Section 3 — Component styling

Buttons (three variants):
- *Primary* — amber bg, near-black text, `padding 10px 16px`, no border. Hover `--oe-accent-hi`. Disabled → `--oe-surface-3` bg + `--oe-text-mute` text.
- *Default* — `--oe-surface-2` bg, 1px border, `--oe-text`. Hover lifts to `--oe-surface-3`.
- *Ghost* — no bg, no border; underline on hover. Used for header Open/Save.

Drop emoji prefixes (`📂 💾 ⚔ 🖼`) — read as cluttered against the modern palette. Buttons read `Generate`, `Download .rmg.json`, `Download preview PNG`, `Open settings…`, `Save settings…`.

Inputs: `--oe-surface-2` bg, 1px border, `4px` radius, `8px 10px` padding, `13px` text. Focus → `--oe-border-strong` + amber ring. Native `<select>` chrome with custom caret via background-image; no JS dropdown library.

Sliders: 4px track in `--oe-surface-3`, filled portion amber, 14px round thumb in `--oe-text` with `--oe-border-strong` ring. Value badge right-aligned next to the label, monospace tabular-num, `--oe-text-dim`. `SliderRow` API unchanged — CSS-only rewrite.

Checkboxes / radios: custom 16px square / circle, `--oe-surface-2` bg, 1px border. Checked → amber fill with 10px black checkmark.

Topology picker: 2×3 grid of ~88×88px tiles, each with a tiny inline SVG schematic (ring / hub / chain / random / shared-web placeholder), label below. Selected tile: 1px amber border + `--oe-surface-3` fill.

Banners:
- *Warning* — `--oe-warn-bg`, 3px amber left border, no emoji.
- *Update available* — same shape, contains link + dismiss "✕".
- *Validation error* — red left border, sits under `Generate`.

Modals: `--oe-surface-1` panel, `8px` radius, 1px border, max-width `420px`, centered over `rgba(0,0,0,0.6)` scrim. Spinner: 2px ring with amber arc on `--oe-border` track.

WPF mirror: a new `Themes/ModernDarkTheme.xaml` replaces `MedievalTheme.xaml`. Slider and checkbox need `ControlTemplate` overrides; everything else is property setters. Topology tiles become a `ListBox` with `UniformGrid` items panel and a custom `ItemTemplate`.

## Section 4 — File-level changes

### Web (`OldenEra.Web/`)

- `wwwroot/css/app.css` — full rewrite. New palette, new typography, restyle every `.oe-*` class. Roughly the same line count.
- `Pages/Home.razor` — restructure shell to three-column `header / nav / content / preview`. Add `ActiveSection` state field. Move Open/Save into the header. Validation, persistence, generation logic stays put.
- New `Components/SectionNav.razor` — vertical nav with status dots; binds `ActiveSection` and accepts a list of sections.
- New `Components/MapSettingsPanel.razor` — extracts the inline "Template" block from `Home.razor` so all nav targets are panels.
- `Components/TopologyPicker.razor` — rewrite markup to the 2×3 tile grid with inline SVG schematics. Logic unchanged.
- `Components/SliderRow.razor`, `HeroSettingsPanel.razor`, `ZoneConfigPanel.razor`, `AdvancedPanel.razor`, `WinConditionsPanel.razor`, `PreviewPanel.razor` — markup tweaks only for new label/spacing conventions.

### WPF (`Olden Era - Template Editor/`)

- New `Themes/ModernDarkTheme.xaml`. `App.xaml` merged dictionaries swap to it. `MedievalTheme.xaml` stays on disk for one commit, then is deleted.
- `MainWindow.xaml` splits into shell + 6 `UserControl`s under `Views/`: `MapPanel`, `HeroesPanel`, `TopologyPanel`, `ZonesPanel`, `WinConditionsPanel`, `AdvancedPanel`. Mirrors `OldenEra.Web/Components/` 1:1.
- `MainWindow.xaml.cs` — `BuildSettings` / `ApplySettings` / `Validate` stay in the shell. Control references migrate into the new UserControls with `DependencyProperty` bindings against a shared `GeneratorSettings` instance. Steam detect, Open/Save dialogs, update banner stay in the shell.

## Rollout

1. **Web first.** Feature branch. Click through every section. Ship to GitHub Pages. Eyeball end-to-end.
2. **WPF second.** Port theme + structure. Run `tests.yml` on `windows-latest` — should stay green (no generator changes).
3. Delete `MedievalTheme.xaml` after WPF verification.

## Risks

- WPF `MainWindow.xaml.cs` split is the largest mechanical change. Plan: one commit per panel extraction so a binding regression bisects cleanly.
- No automated UI tests. Visual verification is manual on both surfaces.
- No pre-rework screenshots captured for visual diffing — if pixel-level parity ever matters, capture them on the feature branch before the CSS rewrite lands.
