# PNG Preview Rework — Olden Era Replica

**Date:** 2026-05-10
**Status:** Design
**Scope:** `OldenEra.Generator/Services/TemplatePreviewRenderer.cs` only. Layout math
(`ComputeLayout`, connection routing, untangling) does not change. WPF and Blazor
hosts are unaffected — they consume the same `byte[]` PNG.

## Why

The current 700×700 PNG reads as a UI panel, not a map: flat dark-teal background,
bright green Discord-style spawn discs, thin yellow connection lines. Side-by-side
with the in-game Olden Era preview card it looks generic.

The renderer should produce a **map**, not a status pane. Same data, same layout,
new skin.

## Reference

In-game Olden Era preview card (the user's "OctoJebus" reference):

- Parchment field with subtle stains and a soft vignette
- Dark navy chrome around the parchment (handled by hosts, not the PNG)
- Round bronze tokens with a metallic rim and a serif numeral
- Hold-city as a small crest at the map center
- Connections drawn as faded ink lines
- Single restrained inner border on the parchment itself

We are replicating only the parchment area — title bar and info table are drawn by
the WPF and Blazor hosts, not by the renderer.

## Design

### 1. Background — parchment with texture

Replace the flat `BackgroundColor = #1c1610` with three layered draws:

1. **Base radial gradient** centered slightly above middle:
   `#e7d6a8` (0%) → `#cdb685` (60%) → `#9c8757` (100%)
   `SKShader.CreateRadialGradient` filling the full canvas.
2. **Stains:** 6–10 large soft brown blobs (`#3a2010`, alpha 0.10–0.35), radii
   60–140 px, drawn with a `SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 35)`.
   Positions and radii seeded from `template.Name.GetHashCode()` so the same
   template always renders the same stain pattern (avoids visual jitter when the
   user re-renders without changing settings).
3. **Vignette:** radial overlay, transparent at 60% radius → `#000` α 0.45 at
   100%. One full-canvas rect with a radial gradient shader.

We are intentionally not adding per-pixel grain. Skia procedural noise is
expensive and the stains plus vignette already kill the "flat panel" look.

### 2. Frame

Three concentric strokes inside the canvas, all in warm parchment-ink tones:

- Outer: 1.0 px stroke at inset 8 px, color `#8f733f` — keep current behavior.
- Inner-1: 2.5 px stroke at inset 22 px, color `#5a3a14`.
- Inner-2: 0.8 px stroke at inset 30 px, color `#5a3a14`.

Skip corner ornaments for now — they look fussy at 700 px and are easy to add
later if wanted. YAGNI.

### 3. Zone tokens — bronze coins

Every zone (spawn, hub, neutral, hold-city) renders as the same coin silhouette.
Type is encoded in **rim color**, not fill. This is the biggest visual change.

**Coin construction (`DrawCoin(canvas, center, radius, rimColor)`):**

1. Drop shadow: `DrawOval` 22×6 at `+3y`, `#000` α 0.35, blurred 1.5 px.
2. Base fill: 2-stop radial gradient `#7a5530` (35% offset, 30% center) →
   `#1f130a` at the rim. Built once per call, not per zone (reused with
   translation).
3. Inner engraved ring: 0.8 px stroke at `0.77 × radius`, `#1c0f06` α 0.6.
4. Rim: 2.5 px stroke at radius, two-stop linear gradient `#d8a85a` top →
   `#5a3a1a` bottom. For non-default rim colors, swap the top color.

**Rim colors by zone type:**

| Zone type             | Rim color   |
|-----------------------|-------------|
| Spawn (player)        | `#d8a85a`   |
| Hub                   | `#cfa86b`   |
| Neutral, bronze tier  | `#cd7f32`   |
| Neutral, silver tier  | `#c0c0c0`   |
| Neutral, gold tier    | `#ffd232`   |
| Hold-city (any)       | `#ffd700` (rim 3.5 px) |

`SpawnFill` (green), `HubFill` (teal), `BronzeFill`, `SilverFill`, `GoldFill`
become unused — delete them.

### 4. Numerals

Current: `SKTypeface.Default` (system sans), Embolden, color `#a0e6a8` (green).
New: serif numerals in warm cream `#f1d990`, no embolden.

Font: bundle **Cinzel-SemiBold.ttf** (SIL Open Font License) as an embedded
resource in `OldenEra.Generator`. Load once into a static `SKTypeface` via
`SKTypeface.FromStream(GetManifestResourceStream(...))`. Falls back to
`SKTypeface.Default` if loading fails (defensive — should not happen).

Cinzel is ~85 KB; acceptable wasm payload cost for the visual win.

### 5. Connections

- `DirectLineColor`: `#b4913c` → `#2a1a0a` (dark ink brown).
- `PortalLineColor`: `#5aaad2 α180` → `#2c4f6a α200` (deep teal-ink, still
  distinguishable but no longer glowing).
- Stroke width and curving logic unchanged.
- Add `StrokeCap = SKStrokeCap.Round` to all connection paints.

### 6. Hold-city icon

Current: gold house silhouette + star sub-badge top-right.

New: same house geometry, filled with the coin's bronze gradient instead of
solid `#ffd700`. Add a small chevron/pediment (3-point triangle) above the roof
ridge, also bronze-gradient filled. **Remove the star sub-badge** — the gold rim
already encodes "this is the hold city".

### 7. Decorative compass rose

Top-right inner area, fixed position `(560, 130)`, radius 38, color `#3a200a`,
opacity 0.25:

- Outer thin circle, 1 px
- Inner thin circle at 0.7 × radius, 0.6 px
- Vertical "N" diamond: 4-point path, `#3a200a` α 0.55
- Horizontal "E–W" diamond: same, α 0.40
- A serif "N" glyph just outside the top of the outer circle

Purely decorative. Does not move when zones change. Acceptable because the
top-right corner is empty in every topology that uses `LayoutZonesRing` /
`LayoutZonesMultiHub`. For tournament's two-cluster layout the rose lands in
relative empty space and remains unobtrusive.

### 8. Things deliberately *not* changing

- Canvas size stays 700×700 (game requirement).
- `ComputeLayout` is untouched.
- `DrawConnections` math (curving, untangling, bridge gaps, crossings) untouched.
- Public API is unchanged: same `RenderPng`, same `ComputeLayout`, same
  `GetLastZoneRadius`.
- `DrawCastleBadge` and `DrawNeutralCastleContent` keep their current geometry;
  only their color paints get retuned to the parchment palette.
- WPF host's "save preview alongside template" feature unaffected.

## Implementation outline

1. Add `OldenEra.Generator/Resources/Cinzel-SemiBold.ttf` (download from Google
   Fonts / SIL OFL release, attribute in `LICENSE.txt` if not already covered).
   Mark as `<EmbeddedResource>` in `OldenEra.Generator.csproj`.
2. Add a static `SerifTypeface` lazy-loader in `TemplatePreviewRenderer`.
3. Add private helpers:
   - `DrawParchmentBackground(SKCanvas, int seed)`
   - `DrawFrame(SKCanvas)`
   - `DrawCompassRose(SKCanvas)`
   - `DrawCoin(SKCanvas, SKPoint center, float radius, SKColor rimTopColor, float rimWidth)`
4. Rewrite the head of `DrawPreview` to call (in order): parchment → frame →
   compass rose → connections → zones.
5. Rewrite `DrawZone` to call `DrawCoin` and pick the rim color from a small
   switch on zone type / neutral tier / hold-city.
6. Retune `DrawPlayerNumber`, `DrawCastleBadge`, `DrawNeutralCastleContent`,
   `DrawHoldCityIcon` to use `SerifTypeface` and the new color tokens.
7. Delete now-unused fill constants (`SpawnFill`, `HubFill`, `BronzeFill`,
   `SilverFill`, `GoldFill`).

## Test plan

Existing tests:

- `TemplatePreviewRendererTests.cs` — 4 tests. Layout assertions are unaffected
  (geometry unchanged). PNG-magic-byte check still passes.
- `TemplateGeneratorTests.cs` — uses `PngBitmapDecoder` to sample pixels at
  specific coords. **Will likely fail** because background and zone colors
  change. We will update those assertions to match the new palette: parchment
  cream at the canvas center-empty area, dark coin fill at zone centers, gold
  rim at hold-city zone outlines.

New test:

- One `RenderPng` test that decodes the PNG and asserts the pixel at `(20, 20)`
  (well inside the parchment, outside any token) has R > 200, G > 180, B < 180
  — i.e., is parchment-cream, not the old `#1c1610`. This locks in the
  background change.

We will not test stain placement determinism beyond "running twice on the same
template produces byte-identical PNGs", which is already implicit because the
seed is derived from `template.Name`.

## Risks

- **Font in browser-wasm:** `SKTypeface.FromStream` works in
  `SkiaSharp.NativeAssets.WebAssembly` (verified in `feedback_skiasharp_blazor_wasm.md`
  context — Skia loads natively). Adds ~85 KB to the published wasm payload.
- **Pixel-sample tests:** WPF-typed tests in `TemplateGeneratorTests.cs` will
  need updates. Plan to update them in the same change so CI stays green.
- **Visual regressions on edge topologies:** the compass rose at fixed
  `(560, 130)` could overlap a zone in unusual layouts. Mitigation: the rose is
  drawn *under* connection lines and zones (first thing after the parchment),
  so any overlap is a faint texture behind the active content rather than a
  conflict.

## Out of scope

- Title bar, map name typography, info table — those belong to the hosts.
- Stylized icons for individual zone contents (mines, dwellings, etc.).
- Animated or interactive previews.
- A second "high-detail" rendering tier — current 700×700 is what the game
  consumes; no reason to add a second size.
