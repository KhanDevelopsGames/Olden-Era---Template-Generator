# Thread 2 — GameData Catalog and ID-driven UI

Date: 2026-05-10
Scope: load real generator IDs from shipped `GameData/`, eliminate string-literal IDs in the generator, expose them in both UIs.

## Path-stability note for executor

A parallel branch is refactoring the codebase. Before each step, confirm current locations of:

- `OldenEra.Generator/Services/TemplateGenerator.cs`
- `OldenEra.Generator/Services/` directory (may be renamed)
- `Olden Era - Template Editor/GameData/GeneratorData/` (the canonical asset root)
- `OldenEra.Web/Components/AdvancedPanel.razor`, `PerTierOverridesPanel.razor`, `HeroSettingsPanel.razor`
- `OldenEra.Web/OldenEra.Web.csproj`
- `Olden Era - Template Editor/MainWindow.xaml(.cs)`

Use `git ls-files | grep -i <token>` if a path moved. The plan references files by intent; treat names as stable but locations as soft.

Assumption: each layer (`OldenEra.Generator`, `OldenEra.Web`, `Olden Era - Template Editor`) keeps its current root. If the refactor merges generator services into a different namespace, update `using` directives only — the public API of `GameDataCatalog` is the contract.

---

## Item 1 — `GameDataCatalog` service (foundation)

### Goal

One typed, immutable, fail-fast catalog of every ID the generator can emit, populated once at host startup, consumed by both WPF and Web. Replaces ad-hoc string literals.

### Files to create

- `OldenEra.Generator/Services/GameDataCatalog.cs` — service class.
- `OldenEra.Generator/Services/GameData/IGameDataSource.cs` — abstraction over file-system vs embedded asset access.
- `OldenEra.Generator/Services/GameData/FileSystemGameDataSource.cs` — used by WPF.
- `OldenEra.Generator/Services/GameData/EmbeddedGameDataSource.cs` — used by Web; reads JSON via `HttpClient` from `_content/OldenEra.Generator/GeneratorData/...` (see asset-shipping decision below).
- `OldenEra.Generator/Services/GameData/CatalogModels.cs` — record types.

### Models

```csharp
public sealed record PoolEntry(string Id, string SourceFile, int? Tier, bool IsRandom, bool IsUnguarded);
public sealed record ContentListEntry(string Id, string SourceFile);
public sealed record EncounterTemplateEntry(string Id, string SourceFile, int Width, int Height);
public sealed record ZoneLayoutEntry(string Id, string SourceFile);

public sealed class GameDataCatalog
{
    public IReadOnlyList<PoolEntry> Pools { get; }
    public IReadOnlyList<ContentListEntry> ContentLists { get; }
    public IReadOnlyList<EncounterTemplateEntry> EncounterTemplates { get; }
    public IReadOnlyList<ZoneLayoutEntry> ZoneLayouts { get; }
    public bool TryGetPool(string id, out PoolEntry entry);
    public bool TryGetContentList(string id, out ContentListEntry entry);
    // …
}
```

`Tier` is parsed from filename suffix (`_t0..t5`) or the `name` field; null when unknown. `IsRandom` and `IsUnguarded` come from `random_pools/` and the `_unguarded_` filename token.

### Asset-shipping decision (Web)

Two options:

A. **Embedded resource via `<EmbeddedResource>`**, read with `Assembly.GetManifestResourceStream`. Pros: deterministic, single binary. Cons: cannot lazy-load; bloats initial WASM download by the full GameData JSON (~MB-scale).

B. **`<Content>` items + `HttpClient.GetFromJsonAsync` from `_content/OldenEra.Generator/GeneratorData/...`**. Pros: cached by browser, parallelizable, easier diff/inspection. Cons: requires async init, requires `RazorClassLibrary` static-asset plumbing.

**Recommendation: B.** The Blazor host is already async-bootstrapped, the JSON is read-only, browser cache handles repeat loads, and binary size matters more than first-load latency for a static site.

Concretely: convert `OldenEra.Generator` into a project that exposes static web assets. Add to `OldenEra.Generator/OldenEra.Generator.csproj`:

```xml
<ItemGroup>
  <Content Include="..\Olden Era - Template Editor\GameData\GeneratorData\**\*.json"
           Link="GeneratorData\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest"
           Pack="true"
           PackagePath="staticwebassets/GeneratorData" />
</ItemGroup>
```

Verify with `dotnet publish OldenEra.Web -c Release` and inspect `wwwroot/_content/OldenEra.Generator/GeneratorData/`. If RCL static-asset behavior is unavailable for non-RCL projects, fall back to copying assets directly under `OldenEra.Web/wwwroot/GeneratorData/` and update the source class accordingly.

### Data flow

```
Host startup
  ├─ WPF: new GameDataCatalog(new FileSystemGameDataSource("GameData/GeneratorData"))
  │      → synchronous; read all JSON via File.ReadAllText.
  └─ Web: await GameDataCatalog.LoadAsync(new EmbeddedGameDataSource(httpClient, "_content/OldenEra.Generator/GeneratorData"))
         → async; HttpClient.GetFromJsonAsync per file, parallelized with Task.WhenAll.

Both register the singleton in DI (Web: builder.Services.AddSingleton; WPF: container or static).
TemplateGenerator receives it via constructor.
```

For both sources, parse files with `System.Text.Json` using `JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }` (the shipped JSON uses tab indents but is otherwise standard).

### Edge cases

- Duplicate pool IDs across files: keep first, log a warning, expose `Catalog.Diagnostics`.
- Unknown tier filename suffix: `Tier = null`, do not crash.
- Empty/malformed JSON: throw at startup with file path in the message.
- `template_maneuvers_pools copy.json`: filename has a space; ensure glob picks it up but flag in diagnostics as suspicious.
- Web `HttpClient.BaseAddress` differences in dev vs publish: use relative URLs only.

### Test strategy

- New test project file `Olden Era - Template Editor.Tests/GameDataCatalogTests.cs`:
  - Load real `GameData/GeneratorData` from disk, assert non-empty `Pools`, `ContentLists`, `ZoneLayouts`.
  - Assert specific known IDs exist: `content_pool_general_resources_start_zone_poor`, `template_pool_default`, `zone_layout_default`, `zone_layout_sides`, `zone_layout_treasure_zone`, `zone_layout_center`, `zone_layout_spawns`.
  - Assert `TryGetPool` returns false for `"definitely_missing_pool_xyz"`.
- Round-trip: build a `FakeGameDataSource` returning fixed JSON, assert parser tier extraction.

### Effort: M

### Dependencies: none.

---

## Item 2 — Replace hardcoded literals in `TemplateGenerator.cs`

### Goal

Every ID in `TemplateGenerator.cs` is either:
(a) resolved through `GameDataCatalog` at construction time, or
(b) an explicit constant grouped in a single, top-of-file `KnownIds` static class with one-line documentation.

If a referenced ID is missing from the catalog, the generator's constructor throws — failure is visible at app launch, not buried in generated output.

### Audit (from `grep` against the current file)

Hot spots:

- Lines 17–20: `SpawnLayoutName`, `SideLayoutName`, `TreasureLayoutName`, `CenterLayoutName` zone layout constants.
- Lines ~2400-2500: pool arrays `T0..T5GuardedPools`, `T0..T5UnguardedPools`, `GeneralResourcesPoor/Medium/Rich`.
- Lines 2842-2999+: many `IncludeLists = ["basic_content_list_..."]` literals inside content-list builders.

### Files to modify

- `OldenEra.Generator/Services/TemplateGenerator.cs` — change constructor to take `GameDataCatalog`, replace `string[]` arrays with `IReadOnlyList<string>` resolved from the catalog.
- `OldenEra.Generator/Services/KnownIds.cs` (new) — central record of ID groups, e.g.:

```csharp
internal static class KnownIds
{
    public static class ZoneLayouts { public const string Spawn = "zone_layout_spawns"; /* … */ }
    public static class GeneralResources { public const string Poor = "content_pool_general_resources_start_zone_poor"; /* … */ }
    public static IEnumerable<string> AllReferenced() => /* yield every constant */;
}
```

- `TemplateGenerator` constructor body:

```csharp
public TemplateGenerator(GameDataCatalog catalog)
{
    _catalog = catalog;
    var missing = KnownIds.AllReferenced()
        .Where(id => !catalog.TryGetPool(id, out _)
                  && !catalog.TryGetContentList(id, out _)
                  && !catalog.TryGetZoneLayout(id, out _))
        .ToList();
    if (missing.Count > 0)
        throw new InvalidOperationException(
            $"GameDataCatalog is missing required IDs: {string.Join(", ", missing)}");
}
```

- Update WPF `MainWindow.xaml.cs` and `OldenEra.Web/Program.cs` to construct `TemplateGenerator` with the catalog.

### Mapping table (representative)

| Literal | Replace with |
|---|---|
| `"zone_layout_spawns"` | `KnownIds.ZoneLayouts.Spawn` (validated against catalog) |
| `"content_pool_general_resources_start_zone_poor"` | `KnownIds.GeneralResources.Poor` |
| `T0GuardedPools = [...]` | Catalog filter only if intent is "all t0 guarded pools". If intent is "this curated subset", keep as constants in `KnownIds.Pools.T0Guarded` and validate. **Prefer constants** to preserve current generation behavior; the catalog's role here is validation, not membership inference. |
| `IncludeLists = ["basic_content_list_..."]` | `KnownIds.ContentLists.<Name>` |

### Edge cases

- A constant references an ID present in shipped JSON only when the user has selected a specific template family (e.g. `custom_content_lists_helltide.json`). Catalog must load all files unconditionally; the validator must accept any file as a valid source.
- Some IDs (`classic_template_pool_random_unguarded_t5_*`) live under `random_pools/classic_version/`. Confirm directory is recursively scanned.
- Renaming literals to constants must not change generator output. Diff a sample generated `.rmg.json` before/after.

### Test strategy

- Snapshot test: run the generator with a fixed seed and a known settings object pre- and post-refactor; output must be byte-identical.
- Add a unit test that constructs `TemplateGenerator` with a catalog missing one known ID and asserts the constructor throws with the missing ID in the message.

### Effort: L (mechanical but voluminous; ~50+ literals).

### Dependencies: Item 1.

---

## Item 3 — Per-zone pool override pickers in the UI

### Goal

Advanced users can override which content pool drives a zone's guarded/unguarded encounters and which content list drives starting resources, choosing from catalog-derived dropdowns. Default = "generator's choice" (current behavior preserved).

### Settings model changes

- `OldenEra.Generator/Models/TemplateSettings.cs` (or wherever per-zone overrides currently live — confirm path; `PerTierOverridesPanel.razor` suggests a `PerTierOverrides` model exists):
  - Add `string? GuardedPoolId`, `string? UnguardedPoolId`, `string? ResourcePoolId` to the per-zone or per-tier override record.
  - `null` = inherit generator default; non-null = catalog ID, validated at generation time.

### Files to create / modify

- `OldenEra.Web/Components/AdvancedPanel.razor` — add a new `<section>` "Per-zone pool overrides" with three `<select>` controls per zone slot, populated from `GameDataCatalog.Pools` filtered by tier and guarded/unguarded.
- `OldenEra.Web/Components/PerTierOverridesPanel.razor` — same dropdown component for per-tier overrides if that is the chosen surface.
- `OldenEra.Web/Components/PoolPicker.razor` (new shared component) — `<select>` with optgroups by tier, options labeled `Id` (and `SourceFile` as `title` for hover).
- `OldenEra.Web/Program.cs` — inject `GameDataCatalog` into the relevant settings page.
- `Olden Era - Template Editor/MainWindow.xaml` — add equivalent ComboBoxes; `MainWindow.xaml.cs` binds `ItemsSource` to `Catalog.Pools`.
- `OldenEra.Generator/Services/SettingsMapper.cs` — map override IDs into the generated template; if the override ID is unknown to the catalog at generation, throw with a clear message (this is a user-data integrity issue, not a startup issue).

### Data flow

```
Catalog (singleton)
  ↓ injected
PoolPicker.razor   → binds <select> to Pools filtered by (tier, guarded?)
  ↓ @bind-Value
TemplateSettings.PerZoneOverrides[i].GuardedPoolId
  ↓
TemplateGenerator → uses override when non-null, else default behavior
```

### Edge cases

- User loads a saved settings JSON that references a pool ID no longer in the catalog (game patch removed it): show a non-blocking warning in the UI ("Override 'foo' not found, using default") and clear the setting on next save.
- Empty pool list (catalog failed to load): disable the picker with a tooltip "GameData not loaded".
- WPF and Web must share the same option ordering. Sort by `(Tier ?? 99, Id)` in `PoolPicker` and the WPF ComboBox `SortDescription`.

### Test strategy

- bUnit test for `PoolPicker.razor`: renders all catalog pools, calls `OnChange` with the selected ID.
- Integration test: a `TemplateSettings` with `GuardedPoolId = "template_pool_default"` produces a generated `.rmg.json` whose corresponding zone references that pool string.

### Effort: M.

### Dependencies: Item 1.

---

## Item 4 — Real hero settings

### Status (updated 2026-05-10)

**Item 4(a) — structural metadata: SHIPPED** in `src/OldenEra.Generator/Services/HeroCatalog.cs`. Six factions, SID-pattern helper (`BuildSid("Temple", 1) → "human_hero_1"`), might/magic split. No hero names — see licence note below.

**Item 4(b) — UI for hero ban list / fixed starting hero / faction filter: NOT STARTED.** Net-new feature work, depends on UX decisions.

### What research found

The `find ... -iname "*hero*"` precondition still returns zero — the shipped `GameData/` does not contain a hero list. However, datamining communities have published the data:

- **alcaras/homm-olden** (https://github.com/alcaras/homm-olden) — full 108-hero catalog regenerated 2026-05-10 from the game's shipped `Core.zip`. **License: none (i.e. all rights reserved)**, so we cannot bundle their JSON without permission.
- The game's **own** SID format is `<unitKey>_hero_<n>`, not the speculative `hero_<faction>_<class>_<n>` this plan originally assumed. There is no class token.

Faction display names vs internal SID prefixes:

| Display | SID prefix |
|---|---|
| Temple | `human` |
| Necropolis | `necro` |
| Grove | `nature` |
| Hive | `demon` |
| Schism | `unfrozen` |
| Dungeon | `dungeon` |

Each faction has 18 stock heroes (indices 1–9 might, 10–18 magic).

### What ships

`HeroCatalog` exposes only the parts that are facts (faction list, SID format, count, might/magic split). Hero **names** are localized strings the game owns; bundling them would need either:

- An explicit licence from Unfrozen Studios, or
- A licence on a derived dataset (alcaras/homm-olden currently has none), or
- A run-time extraction from the user's own `HeroesOldenEra_Data/StreamingAssets/Core.zip` (no redistribution).

### Item 4(b) — when ready

### Goal

`HeroSettingsPanel.razor` exposes:

- Faction filter (multi-select of factions present in heroes.json).
- Hero ban list (multi-select of hero IDs; banned heroes excluded from generation).
- Fixed starting hero per faction (one-of dropdown per faction, plus "random").
- Existing min/max/increment sliders remain.

### Files to create / modify

- `OldenEra.Generator/Services/GameData/HeroCatalog.cs` (or fold into `GameDataCatalog` as an optional `Heroes` collection that is empty when the file is absent).
- `OldenEra.Generator/Models/TemplateSettings.cs` — extend hero settings:
  ```csharp
  public sealed record HeroSettings(
      int Min, int Max, int Increment,
      IReadOnlyList<string> AllowedFactions,
      IReadOnlyList<string> BannedHeroIds,
      IReadOnlyDictionary<string, string?> FixedStartingHeroByFaction);
  ```
- `OldenEra.Web/Components/HeroSettingsPanel.razor` — add three new sub-sections; reuse existing `SliderRow.razor` for the numeric inputs.
- `Olden Era - Template Editor/MainWindow.xaml(.cs)` — mirror controls.
- `OldenEra.Generator/Services/SettingsMapper.cs` — translate ban list, fixed heroes, and faction filter into the generated `.rmg.json`. Confirm output schema with the example templates under `Olden Era - Template Editor/GameData/ExampleTemplates/`.

### Data flow

```
heroes.json → HeroCatalog (Faction → IReadOnlyList<HeroEntry>)
  ↓ injected
HeroSettingsPanel.razor → multi-selects bound to HeroSettings record
  ↓
SettingsMapper → emits ban / fixed-hero fields in the .rmg.json
```

### Edge cases

- Heroes file missing at runtime: `HeroCatalog.Heroes` is empty; UI sub-sections render a "Hero data not available" notice and the existing sliders still work.
- Duplicate hero IDs across factions: log warning, keep first.
- Fixed-hero ID also in ban list: validation error in the UI; do not allow save.

### Test strategy

- Unit tests on `HeroCatalog` parsing.
- bUnit: rendering with empty catalog shows the fallback notice; rendering with stub catalog shows dropdowns.
- Snapshot test: `.rmg.json` output reflects ban list and fixed heroes.

### Effort: M (once data is available); +S to author/extract heroes.json.

### Dependencies: Item 1 (catalog plumbing); plus a one-time data deliverable (heroes.json).

---

## Sequencing

1. Item 1 — must land first. All others import the catalog.
2. Item 2 — refactor under green snapshot tests.
3. Item 3 — UI layer; can be developed in parallel with Item 4 once Item 1 is merged.
4. Item 4 — only after the heroes.json precondition is met.

## Risk summary

- **Asset-shipping in Web**: confirm the static-web-asset path actually appears under `wwwroot/_content/...` after publish. If not, switch to copying assets into `OldenEra.Web/wwwroot/GeneratorData/` directly.
- **Path drift from parallel refactor**: re-resolve paths at the start of every item.
- **Snapshot stability for Item 2**: a bug-for-bug-equal refactor requires fixed seeds and identical iteration order — keep `string[]` arrays as ordered constants, do not switch to `HashSet`.
