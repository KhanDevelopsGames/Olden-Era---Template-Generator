# Map Borders & Roads — Design

**Date:** 2026-05-11
**Issue:** #23
**Branch:** `feature/borders-water-roads`

## Problem

Three Unfrozen-schema fields are hardcoded in the generator and not user-controllable:

- `Variant.Border.CornerRadius` (always `0.0`)
- `Variant.Border.ObstaclesWidth` (always `3`)
- `Variant.Border.WaterWidth` / `WaterType` (always `0` / `"water grass"`)
- `Road.Type` on every road (always `"Dirt"`)

These are written in `TemplateGenerator.MakeVariant` and `BuildConnectorZoneRoads` /
`BuildOuterZoneRoads`. Users cannot opt into a stone-road template, water-bordered
maps, or rounded-corner zones without editing the generated `.rmg.json` by hand.

## Goals

- Surface borders + water + road type as user-controllable settings.
- Defaults stay byte-identical to current output (no surprise diffs).
- Round-trip via `.oetgs` and the URL-share mechanism.
- Web + WPF feature parity.

## Non-goals

- Exposing `ObstaclesNoise` / `WaterNoise` lists — defaults are tuned, no user demand.
- Per-tier or per-connection overrides — explicit deferral, matches existing
  "Per-tier terrain density" deferral from the 2026-05-10 experimental rollout.
- SkiaSharp preview changes — the 700×700 preview is a zone-graph view; borders
  and roads are game-time render concerns and don't appear in it.

## Data model

New nested settings group on `GeneratorSettings`:

```csharp
public sealed class BordersRoadsSettings
{
    public double? CornerRadius { get; set; }      // null = leave default 0.0
    public int? ObstaclesWidth { get; set; }       // null = leave default 3
    public bool WaterBorderEnabled { get; set; }   // false = WaterWidth stays 0
    public int WaterWidth { get; set; } = 4;       // only used if enabled
    public string? RoadType { get; set; }          // null = "Dirt" (default)
}
```

`SettingsFile.BordersRoadsFile` mirrors the shape with `[JsonPropertyName]` tags.
All fields are nullable / default no-op so existing `.oetgs` files round-trip
without migration.

## Generator integration

Follows the established post-process pattern from the experimental rollout
(see `project_experimental_features.md`). `TemplateGenerator.Generate()` runs
unchanged. `ApplyExperimentalSettings(template, settings, tierByLetter)` gets a
new branch:

```csharp
ApplyBordersAndRoads(template, settings.BordersRoads);
```

Inside `ApplyBordersAndRoads`, for each `Variant`:

- If `CornerRadius.HasValue` → `variant.Border.CornerRadius = value`
- If `ObstaclesWidth.HasValue` → `variant.Border.ObstaclesWidth = value`
- If `WaterBorderEnabled` → `variant.Border.WaterWidth = WaterWidth` (WaterType unchanged)
- For each `zone.Roads[*]`, if `RoadType` non-null → `road.Type = RoadType`

**Why post-process for roads:** roads are constructed deep inside
`BuildSpawnZone`, `BuildNeutralZone`, `BuildHubZone`, and the helpers
`BuildConnectorZoneRoads` / `BuildOuterZoneRoads`. A single post-pass loop over
`variant.Zones[*].Roads[*]` is one localized change and keeps `BuildXxxZone`
signatures stable.

**Byte-identical default:** with all `BordersRoads` fields at defaults
(nulls + `WaterBorderEnabled=false`), `ApplyBordersAndRoads` writes nothing.
The hardcoded `MakeVariant` border block is unchanged. Existing tests stay green.

## UI

### Web

New component `OldenEra.Web/Components/MapBordersRoadsPanel.razor`, mounted in
the Experimental section of `Pages/Home.razor`. Card title:
"⚗ Map borders & roads".

Controls (using existing `ExperimentalCard` + `SliderRow` components):

| Field | Control | Range | Default |
|---|---|---|---|
| Corner radius | SliderRow | 0.0–1.0, step 0.05 | 0.0 |
| Obstacle width | SliderRow | 0–10, step 1 | 3 |
| Water border | Checkbox | — | off |
| Water width | SliderRow (disabled when checkbox off) | 1–10, step 1 | 4 |
| Road type | `<select>` | Dirt / Stone | Dirt |

Persistence: `BrowserSettingsStore` already serializes `SettingsFile` via
`SettingsMapper`. Once mapper is extended, the card auto-persists with the
existing 500 ms debounce.

### WPF

New `Expander` in `Views/ExperimentalPanel.xaml` mirroring the web layout.
Controls: two `Slider`s, `CheckBox` + dependent `Slider`, `ComboBox` bound to
`KnownValues.RoadTypes`. `MainWindow.xaml.cs` extends `GatherSettings` and
`ApplySettings` to round-trip the new fields.

No `Validate()` changes — defaults are valid; user-selected values are
constrained by control bounds.

## Tests

In `tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs`:

1. **Default no-op** — Generate with default settings. Assert
   `Border.CornerRadius==0`, `ObstaclesWidth==3`, `WaterWidth==0`, all roads
   `Type=="Dirt"`. Locks the byte-identical guarantee.
2. **CornerRadius override** — Set `0.5`, assert applied to every variant.
3. **ObstaclesWidth override** — Set `7`, assert applied to every variant.
4. **Water enabled** — Set `WaterBorderEnabled=true`, `WaterWidth=6`. Assert
   `Border.WaterWidth==6`; `WaterType=="water grass"` unchanged.
5. **Water disabled** — Set `WaterBorderEnabled=false`, `WaterWidth=6`. Assert
   `Border.WaterWidth==0` (slider value ignored).
6. **RoadType override** — Set `RoadType="Stone"`. Walk every
   `variant.Zones[*].Roads[*]` across Random and HubAndSpoke topologies; assert
   all `Type=="Stone"`. Covers `BuildConnectorZoneRoads` and `BuildOuterZoneRoads`
   paths.

`HostParityTests` covers settings round-trip once `SettingsMapper` is extended;
parameterize one fixture with the new fields.

## Files touched

- `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs` — `BordersRoadsSettings`
- `src/OldenEra.Generator/Models/Generator/SettingsFile.cs` — `BordersRoadsFile`
- `src/OldenEra.Generator/Services/TemplateGenerator.cs` — `ApplyBordersAndRoads`
- `src/OldenEra.Web/Services/SettingsMapper.cs` — bidirectional mapping
- `src/OldenEra.Web/Components/MapBordersRoadsPanel.razor` (new)
- `src/OldenEra.Web/Pages/Home.razor` — mount card
- `src/OldenEra.TemplateEditor/Views/ExperimentalPanel.xaml` + `.cs`
- `src/OldenEra.TemplateEditor/MainWindow.xaml.cs` — `GatherSettings`/`ApplySettings`
- `tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs` — 6 new tests
