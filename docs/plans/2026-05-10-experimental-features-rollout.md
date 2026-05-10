# Experimental Features Rollout

Adds the unexposed template capabilities from
`docs/plans/2026-05-10-possible-settings-catalog.md` to the generator and
the redesigned web UI. Every new feature ships behind an `[EXPERIMENTAL]`
badge so users know the surface is unproven against the live game build.

## Goals

- Expose the full unexposed-capabilities surface from the catalog except
  the two flagged as not fitting the panel-based UI:
  - **Deferred:** per-player asymmetric starting bonuses (catalog flags
    "doesn't fit one form for all players"). Bonuses ship as
    "applied to all players", with a per-bonus `start_hero` toggle.
  - **Deferred:** mandatory content placement rules (catalog flags as low
    value, niche, scenario-style).
- Keep the panel-based shell intact (Map / Heroes / Topology / Zones /
  Win Conditions). No new top-level sections.
- Mark everything new with a reusable `ExperimentalBadge` component.
  Retrofit it onto `ExperimentalMapSizes` and
  `ExperimentalBalancedZonePlacement`.
- Default values must produce byte-identical output to today, so existing
  fixtures and `HostParityTests` stay green.

## Non-goals

- Per-zone editor screen. Zones remain auto-named; per-zone authoring is
  expressed through the existing global + Low/Med/High tier model.
- Polished dedicated content-control screen. Content controls live as a
  collapsible block inside the Zones section.
- Per-player starting bonus asymmetry / handicap editor.
- Mandatory content with placement rules.
- Internal-plumbing fields listed under "Don't Expose" in the catalog.

---

## Section 1 — Data model

All additions live in `OldenEra.Generator/Models/Generator/GeneratorSettings.cs`.
Defaults must be neutral (no-op when serialized through the generator).

**`GeneratorSettings` additions:**

- `GameMode` — already a string. Add `"SingleHero"` as a recognized value.
- `bool HeroHireBan = false`
- `int? DesertionDay = null`
- `int? DesertionValue = null`

**New nested classes on `GeneratorSettings`:**

- `TerrainSettings Terrain { get; set; }`
  - `double ObstaclesFill = 0.0` (0 = use generator default)
  - `double LakesFill = 0.0`
  - `string ElevationMode = "default"` (`flat` / `default` / `hilly`)
- `BuildingPresets BuildingPresets { get; set; }`
  - `string PlayerZonePreset = ""` (empty = generator default)
  - `string NeutralZonePreset = ""`
- `GuardProgression GuardProgression { get; set; }`
  - `double ZoneGuardWeeklyIncrement = 0.0`
  - `double ConnectionGuardWeeklyIncrement = 0.0`
- `NeutralCitySettings NeutralCities { get; set; }`
  - `double GuardChance = 0.0` (0 = unset)
  - `int GuardValuePercent = 100`
- `ContentControl Content { get; set; }`
  - `List<string> GlobalBans = new()`
  - `List<ContentLimit> ContentCountLimits = new()`
  - record `ContentLimit { string Sid; int MaxPerPlayer; }`
- `StartingBonuses Bonuses { get; set; }`
  - `Dictionary<string,int> Resources = new()` (sid → amount)
  - `int HeroAttack`, `HeroDefense`, `HeroSpellpower`, `HeroKnowledge` (each int, 0 = unset)
  - `string ItemSid = ""`
  - `string SpellSid = ""`
  - `double UnitMultiplier = 0.0`
  - per-bonus `bool StartHeroOnly` flags (one per non-resource bonus)

**`AdvancedSettings` additions (per-tier overrides):**

For each of `BuildingPreset`, `GuardWeeklyIncrement`, `ObstaclesFill`,
`LakesFill`, expose six fields keyed by `Low/Medium/High × Castle/NoCastle`.
Empty / 0 means "fall through to the global value".

This adds ~24 fields to `AdvancedSettings`. Acceptable because the panel
already groups by tier — see Section 4.

---

## Section 2 — Generator wiring

All edits in `OldenEra.Generator/Services/TemplateGenerator.cs`.

- **`Generate()` top:** when `Settings.GameMode == "SingleHero"`, set the
  same on the `RmgTemplate` and force `HeroSettings.HeroCountMin/Max = 1`
  before zone build. Set `gameRules.heroHireBan` from settings.
- **Win conditions:** if `DesertionDay`/`DesertionValue` set, write to
  `gameRules.winConditions`.
- **Bonuses:** for each non-default bonus field, append a
  `gameRules.bonuses` entry with the catalog-defined `sid`, value, and
  receiver = `all_heroes` (or `start_hero` if the per-bonus flag is set).
- **`BuildSpawnZone`:** apply player-zone `BuildingPreset` to the main
  object's `buildingsConstructionSid`. Apply
  `Terrain.ObstaclesFill/LakesFill/ElevationMode` to the zone's layout.
  Stamp `Zone.GuardWeeklyIncrement`.
- **`BuildNeutralZone`:** same, plus per-tier override resolution. The
  zone already knows its `NeutralZoneQuality`; thread castle/no-castle
  via the existing plan. Helper:
  ```csharp
  static T ResolveTier<T>(T global, T tierValue, Func<T,bool> isUnset)
      => isUnset(tierValue) ? global : tierValue;
  ```
  Apply `mainObject.guardChance` and `mainObject.guardValue` from
  `NeutralCities`.
- **`BuildHubZone`:** apply player-zone preset and terrain (hub is the
  player home in HubAndSpoke).
- **Connection builder:** stamp `connection.guardWeeklyIncrement` from
  `GuardProgression.ConnectionGuardWeeklyIncrement` on every emitted
  connection.
- **Top-level template assembly:**
  - Populate `template.GlobalBans` from `Content.GlobalBans`.
  - Populate `template.ContentCountLimits[]` from `Content.ContentCountLimits`.

**Invariant:** if every new setting is at its default, the emitted JSON
must be identical to today. Verified by an additive test (Section 6).

---

## Section 3 — Settings persistence

`OldenEra.Generator/Models/Generator/SettingsFile.cs` mirrors every new
field with `[JsonPropertyName]` snake_case keys and default values that
match Section 1.

`OldenEra.Web/Services/SettingsMapper.cs` round-trips them in both
`ToFile` and `FromFile`. New fields are additive; the legacy
`contentDensity` migration stays untouched.

`SettingsShareCodec` keeps working (it serializes `SettingsFile`). Add a
clearer share-toast message when a user's bans/limits push the encoded
length past `MaxEncodedLength` ("Settings too large to share — fewer
bans / limits would help").

---

## Section 4 — UI

### New component

`OldenEra.Web/Components/ExperimentalBadge.razor` — inline chip:

```razor
<span class="oe-exp-badge" title="Experimental — may behave unpredictably in game">
    ⚗ EXPERIMENTAL
</span>
```

CSS in `wwwroot/css/app.css`: amber background (`#c47e2c`-ish to fit the
medieval palette), small caps, ~10px font, rounded.

### Panel changes

- **`MapSettingsPanel`:** add Single Hero toggle (badge), Hero hire ban
  (badge, disabled when Single Hero is on), Desertion day/value
  (badge). Retrofit badge onto existing experimental map sizes toggle.
- **`ZoneConfigPanel`:** new collapsible sub-sections (each carrying the
  badge):
  - "Terrain" — obstacles density, lakes density, elevation mode.
  - "Buildings" — player-zone preset, neutral-zone preset.
  - "Guard progression" — zone weekly %, connection weekly %.
  - "Neutral cities" — guard chance, guard value %.
  - "Content control" — bans multi-select chips (driven by
    `ObjectSids`), count-limits table.
- **`AdvancedPanel`:** per-tier override rows under each existing
  Low/Med/High × Castle/NoCastle group. Empty inputs = "use global".
- **`HeroSettingsPanel`:** new "Starting bonuses" sub-section (badge):
  resources row, hero stats row, item dropdown, spell dropdown, unit
  multiplier. Each non-resource bonus has a "Apply to start hero only"
  checkbox.

### Validation interactions

Non-blocking yellow warnings (existing `Validate()` pattern):

- Single Hero on while HeroCountMax > 1 → warn, payload still forces 1/1.
- Banned SID also present in count-limits → warn + hide that row.
- `obstaclesFill` and `lakesFill` summing > 0.9 → warn (zone may be
  unplayable).
- `Tournament` win condition unchanged (already 2-player blocker).

---

## Section 5 — Tests

Add to `Olden Era - Template Editor.Tests/`:

- **`TemplateGeneratorTests` — defaults parity:** generate with a fresh
  `GeneratorSettings()` and compare the serialized JSON to a checked-in
  fixture from before this change. Locks the "default = no-op" invariant.
- **`TemplateGeneratorTests` — feature reach-through:** one test per
  feature group asserting the field appears in the output JSON when set
  (single hero → game mode + hero count, ban list → globalBans array,
  bonus → gameRules.bonuses entry, guard increment → zone +
  connection fields, building preset → mainObject preset).
- **`HostParityTests`:** add a third fixture exercising a handful of new
  fields (single hero, two bans, one resource bonus, zone guard
  increment). Round-trip serialize/deserialize.
- **`SettingsMapperTests`** (new): one round-trip per new top-level
  block to lock `SettingsFile` ↔ `GeneratorSettings` symmetry.

CI runs only on Windows; macOS dev parity is manual via
`dotnet build OldenEra.Generator` + `dotnet test`.

---

## Rollout order

1. Section 1 (data model) + minimal `SettingsFile` mirror.
2. Section 2 (generator wiring) one feature group at a time, each
   landing with its reach-through test.
3. Section 4 panels (one PR-sized chunk each: Map, Zones,
   Advanced, Heroes).
4. Section 5 parity + mapper tests.
5. Section 3 share-codec error message + length verification.

Each step keeps the build and existing tests green. The
`ExperimentalBadge` ships in step 3's first chunk so subsequent panels
can reuse it.

## Open follow-ups (not in this rollout)

- Per-player asymmetric bonuses / handicap editor.
- Mandatory content with placement rules.
- Per-zone editor screen (if the global + tier model proves too coarse
  in playtesting).
- In-game smoke test for `globalBans` and `gameMode = "SingleHero"`
  (catalog open questions) — informs whether the badges can come off
  later.
