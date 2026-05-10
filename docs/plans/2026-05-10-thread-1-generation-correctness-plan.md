# Thread 1: Generation Logic Correctness — Implementation Plan

**Date:** 2026-05-10
**Repo:** `/Users/rparn/Developer/personal-projects/Olden-Era---Template-Generator`
**Branch context:** A parallel refactor is in flight. Paths and line numbers below are pinned to current `HEAD`. Where a file is likely to move, the symbol/identifier the executor should grep for is noted instead of trusting line numbers.

## Cross-cutting assumptions

- The static entry `OldenEra.Generator.Services.TemplateGenerator.Generate(GeneratorSettings) → RmgTemplate` is stable. If the refactor splits `TemplateGenerator.cs` into multiple files, grep for `public static RmgTemplate Generate(GeneratorSettings settings)` to find the new home.
- `GeneratorSettings`, `MapTopology`, and the RMG model classes currently live under `OldenEra.Generator/Models/Generator/`. If they move, grep for the type name.
- Tests run on `net10.0-windows` in `Olden Era - Template Editor.Tests/`. New tests should be added there. If a non-Windows test project is introduced by the refactor, prefer adding the platform-agnostic tests there to keep them runnable on macOS/Linux CI.
- Game data is loaded relative to `Olden Era - Template Editor/GameData/GeneratorData/`. Tests should resolve via `AppContext.BaseDirectory` walk-up or a copy-to-output rule, mirroring whatever pattern existing tests use (check `HostParityTests.cs` and `TemplatePreviewRendererTests.cs` first).
- The hardcoded literals at `TemplateGenerator.cs:2441-2443` (`GeneralResourcesPoor/Medium/Rich`) are the canonical example of strings that must round-trip against shipped JSON. After the refactor, find them by grepping for `content_pool_general_resources_start_zone_poor`.

---

## Item 1 — Validate emitted IDs against shipped JSON

**Goal.** Catch every drift between hardcoded ID strings in `TemplateGenerator.cs` and the actual IDs shipped under `GameData/GeneratorData/`. Today, a typo in a literal like `content_pool_general_resources_start_zone_medium` produces a template the game silently rejects.

**Approach.**
1. Build a one-off test fixture `GameDataIdCatalog` that walks `GameData/GeneratorData/**/*.json`, parses each file with `System.Text.Json`, and harvests every top-level `id` field plus any nested `id` inside arrays. Bucket results by category derived from the parent folder (`content_pools`, `content_lists`, `encounter_templates`, `zone_layouts`). Expose `bool Contains(string id)` and `bool Contains(IdCategory cat, string id)`.
2. Add `EmittedIdValidationTests.cs`. Drive `TemplateGenerator.Generate` across a settings matrix:
   - PlayerCount ∈ {2, 4, 8}
   - Topology ∈ all `MapTopology` values
   - NeutralZone quality mix: all-low, all-medium, all-high, mixed
   - StartingResources ∈ {Poor, Medium, Rich} — exercises the `2441-2443` literals
   - Toggle CityHold, Tournament (with PlayerCount=2), RandomPortals
3. After each `Generate`, walk the returned `RmgTemplate`. Collect strings from: every `*ContentPool` array on each zone, `mandatoryContent`, `contentCountLimits` keys, `layout`, plus any neutral-profile pool fields. Assert each is present in the catalog. On mismatch, fail with the offending id, the source field, and the settings hash that produced it.
4. Add a focused fast assertion for the three start-zone resource pools so a regression there fails loudly with a clear message even if the matrix is later trimmed.

**Files to touch.**
- New: `Olden Era - Template Editor.Tests/EmittedIdValidationTests.cs`
- New: `Olden Era - Template Editor.Tests/Support/GameDataIdCatalog.cs`
- Read-only references: `OldenEra.Generator/Services/TemplateGenerator.cs` (lines 2441-2443 today; grep `content_pool_general_resources_start_zone`), `Olden Era - Template Editor/GameData/GeneratorData/`
- May require updating the test `.csproj` to copy `GameData` into the test output if it does not already (check first; `TemplatePreviewRendererTests.cs` likely already pulls it in).

**Test strategy.** This *is* the test. Validate the catalog itself in a `Sanity` fact: load it and assert the count is non-zero per category. Run the matrix as `[Theory]` with `MemberData` so failures point at specific settings.

**Dependencies.** None on other items. Independent of refactor — only depends on `Generate` signature, the most stable surface in the project.

**Effort.** M. Most cost is reflection-walking the `RmgTemplate` graph. If `RmgTemplate` exposes a clean shape, this is closer to S.

---

## Item 2 — Port WPF `Validate()` to shared lib

**Goal.** Extract validation logic out of `MainWindow.xaml.cs` so the Blazor host (`OldenEra.Web`) has the same pre-generation guardrails as WPF. Today only WPF validates; the web host can submit nonsense settings.

**Source of truth.** `Olden Era - Template Editor/MainWindow.xaml.cs:379-479` (find by symbol `private bool Validate()`). The method currently mixes UI concerns (pulling values from sliders, toggling `BtnPreview.IsEnabled`, calling `SetValidationText`) with pure logic. Extract the logic.

**New API.**

```csharp
namespace OldenEra.Generator.Services;

public static class SettingsValidator
{
    public readonly record struct Result(
        IReadOnlyList<string> Blockers,
        IReadOnlyList<string> Warnings)
    {
        public bool IsValid => Blockers.Count == 0;
    }

    public static Result Validate(GeneratorSettings settings);
}
```

**Rules to migrate (in order).** Each rule's classification (blocker vs warning) matches today's WPF behavior so user-visible semantics do not change.

Blockers:
1. `HeroMin > HeroMax` → `"Min Heroes cannot be greater than Max Heroes."`
2. `players + neutral > maxZones` (24 advanced / 16 simple — surface the threshold via a parameter or a new constant on `GeneratorSettings`) → existing message.
3. Empty/whitespace template name → `"Template name cannot be empty."`
4. CityHold + non-HubAndSpoke + zero neutrals → existing message.
5. `NoDirectPlayerConnection` + zero neutrals → existing message.
6. `VictoryCondition == "win_condition_6"` (Tournament) + `PlayerCount != 2` → `"Tournament mode only supports exactly 2 players."` (Coordinates with Item 4.)

Warnings:
7. Default name `"Custom Template"` (case-insensitive) → existing message.
8. Map-area-per-zone < 1024 (account for HubAndSpoke adding +1 zone) → existing message.
9. `MapSize > KnownValues.MaxOfficialMapSize` (240) → existing experimental message.
10. `totalZones > 10` AND (player castles > 1 OR neutral castles > 1 in simple mode) → existing castle-freeze warning.
11. Advanced + `MinNeutralZonesBetweenPlayers > 0` + `!TemplateGenerator.CanHonorNeutralSeparation(...)` → existing message.

**Things that move with the rules.**
- The `AdvancedModeMaxZones` / `SimpleModeMaxZones` constants currently live in `MainWindow.xaml.cs`. Move them into `OldenEra.Generator/Constants/` (folder already exists) so the validator can reference them without depending on WPF.
- `KnownValues.MaxOfficialMapSize` and `KnownValues.VictoryConditionIds` already live in shared territory (verify via grep); if not, move them too.
- `TotalNeutralZonesFromUi()` is a UI helper. The validator instead expects a fully-populated `GeneratorSettings` whose neutral counts are already aggregated (this is how `Generate` already consumes them).

**Caller updates.**
- `MainWindow.xaml.cs:379` → reduce to: gather settings, call `SettingsValidator.Validate`, then translate `Result` into the WPF UI side-effects (set text, toggle `BtnPreview.IsEnabled`). Preserve current visual output by joining warnings with `"\n\n"` exactly as today.
- `OldenEra.Web` (find the form-submit / Generate button handler — likely in a `Pages/*.razor` or `Pages/*.razor.cs`) → call validator before invoking `Generate`, surface `Blockers` as inline errors and `Warnings` as a non-blocking notice.

**Files to touch.**
- New: `OldenEra.Generator/Services/SettingsValidator.cs`
- Edit: `Olden Era - Template Editor/MainWindow.xaml.cs` (replace body of `Validate()`)
- Edit: relevant Blazor page in `OldenEra.Web/` (executor will need to grep for `TemplateGenerator.Generate` to locate it)
- Possibly move: WPF-local constants into `OldenEra.Generator/Constants/`
- New: `Olden Era - Template Editor.Tests/SettingsValidatorTests.cs` — one fact per rule, plus a "happy path produces empty lists" fact.

**Test strategy.** Pure unit tests against `SettingsValidator.Validate`. Each rule gets a positive case (rule fires) and at least one negative case (does not fire). The Tournament+1-player case is shared with Item 4.

**Dependencies.** Item 4's tournament rule is asserted both here (validator) and at the generator level (Item 4 makes generator throw rather than silently fall through). Implement validator first; Item 4's blocker behavior at the generator level is the second line of defense.

**Risk from refactor.** If the refactor introduces a settings DTO/projection layer between WPF and `GeneratorSettings`, the validator should still take `GeneratorSettings` (the post-projection type). The WPF caller change is the only place that may need re-anchoring.

**Effort.** M.

---

## Item 3 — Reference-template parity test

**Goal.** Lock in that representative shipped templates can be re-derived. Catches regressions where a generator change quietly breaks a known-good template's structure.

**Approach.**
1. Pick 4 reference templates in `Olden Era - Template Editor/GameData/ExampleTemplates/`:
   - `Jebus Cross.rmg.json` (HubAndSpoke, City Hold)
   - `Anarchy.rmg.json` (Random topology, many neutrals)
   - `Hallway.rmg.json` (Chain topology)
   - `Diamond.rmg.json` (Default/Ring with even player count)
2. For each, hand-author a `GeneratorSettings` instance in the test (PlayerCount, Topology, neutral counts and qualities, victory condition, CityHold, etc.) that *should* reproduce the template's shape. Document the choices inline.
3. Assert *structural* parity — never byte-for-byte, never RNG-sensitive content:
   - Zone count (players + neutrals + hub if applicable)
   - Topology / connection pattern: same number of edges, same connectivity class (chain / ring / star / mesh). Compute from the emitted `connections` and from the reference template's `connections`.
   - `winConditions` flag set: `tournament`, `cityHold`, primary `victoryCondition` id all match.
   - Hold-city presence: when CityHold is on, exactly one zone has the hold-city marker (today encoded on a neutral letter or the hub — see `PickHoldCityNeutralLetter` and `hubIsHoldCity` at `TemplateGenerator.cs:993-1010`).
4. Build a small helper `RmgTopologyFingerprint` that reduces an `RmgTemplate` to `(zoneCount, edgeCount, sortedDegreeSequence, hasHub)` so reference and emitted templates can be compared with one equality assertion.

**Files to touch.**
- New: `Olden Era - Template Editor.Tests/ReferenceTemplateParityTests.cs`
- New: `Olden Era - Template Editor.Tests/Support/RmgTopologyFingerprint.cs`
- Read-only: `Olden Era - Template Editor/GameData/ExampleTemplates/{Jebus Cross,Anarchy,Hallway,Diamond}.rmg.json`

**Test strategy.** `[Theory]` over the four templates. If hand-authored settings cannot reproduce the shape, that is itself a finding — record it as `Skip` with a TODO and an explanation of which shipped template uses a hand-tuned shape the generator cannot currently emit. Do not silently weaken the assertion.

**Dependencies.** Helps validate Item 4 (City Hold on Hub, Tournament=2-player). Independent of Item 1 and Item 2.

**Risk from refactor.** Low. Operates on the public `Generate` output and on shipped JSON — neither moves.

**Effort.** L. Most cost is iteration to find settings that reproduce each template's shape. Budget for two of the four to need adjustment after first run.

---

## Item 4 — Codify topology invariants

**Goal.** Pin down three topology rules that have either silently misbehaved or recently regressed. Two are assertions; one is a behavior change in `TemplateGenerator`.

**Sub-items.**

**4a. Tournament must be exactly 2 players (blocker, not silent fallback).**
Today, `TemplateGenerator.cs:998-1000`:
```
bool isTournament = settings.TournamentRules.Enabled || settings.GameEndConditions.VictoryCondition == "win_condition_6";
if (isTournament && playerLetters.Count == 2)
    return BuildVariantTournament(...);
```
If a caller sets Tournament with 3+ players, the generator silently falls through to whatever the topology switch picks — confusing, content-incorrect map. Change:
- Inside `Generate` (or its `BuildVariant` callee), when `isTournament && playerLetters.Count != 2`, throw `ArgumentException` with a clear message. The validator (Item 2) catches this earlier; the throw is defense-in-depth.
- Add a test that asserts the throw.

**4b. HubAndSpoke + CityHold → hub becomes hold city.**
Existing code at `TemplateGenerator.cs:38-43` and `:1004` already does this via `hubIsHoldCity`. Add a regression test that:
- Generates with `Topology = HubAndSpoke`, `CityHold = true`. Asserts the hub zone (find by name `"Hub"` or by structural position — degree = neutralCount, only zone with no `-letter` suffix) carries the hold-city flag, and no other zone does.
- Generates with `Topology = HubAndSpoke`, `CityHold = false`. Asserts no zone carries the flag.
- Generates with `Topology = Chain`, `CityHold = true`. Asserts the flag is on a neutral zone, not on a player.

**4c. Tier-by-zone-letter regression test.**
Lock in commit `59b98ce` (the bare-letter vs `Neutral-{letter}` lookup fix). The fix is in `TemplateGenerator.cs` — find by grepping `ExtractZoneLetter`. Test:
- Build advanced-mode `GeneratorSettings` with at least one Low-tier and one High-tier neutral letter override that differ in `GuardWeeklyIncrement` (or another easily-asserted tier-only field).
- Generate.
- For each emitted neutral zone named `Neutral-{letter}`, assert the zone's tier-derived field matches the override the user set for that letter — proving the `Neutral-A` → `A` extraction works.
- Bonus: also assert an unprefixed zone name (e.g. `Hub`) skips tier resolution gracefully (does not crash, falls back to global).

**Files to touch.**
- Edit: `OldenEra.Generator/Services/TemplateGenerator.cs` — add the `ArgumentException` throw at line ~999. (May move during refactor; grep `BuildVariantTournament`.)
- New: `Olden Era - Template Editor.Tests/TopologyInvariantTests.cs` — one `[Fact]` per sub-item, except 4c which is a `[Theory]` over a couple of letter combinations.

**Test strategy.** All assertions operate on the public `Generate` output. 4c specifically guards the historical bug — name the test `Neutral_Tier_Override_Resolves_By_Letter_Suffix` so a future regression's failure message tells the next person what they broke.

**Dependencies.** 4a is the generator-side counterpart to Item 2 rule #6. Implement Item 2 first so end users see the friendly validator message before hitting the generator-level throw. 4b is exercised incidentally by Item 3's Jebus Cross case but should still have its own focused test.

**Risk from refactor.** 4a's throw site moves with `BuildVariantTournament`. 4c's `ExtractZoneLetter` may be inlined or renamed; the test does not call it directly, so the test is robust as long as zone names keep the `Neutral-{letter}` convention. If the refactor changes that convention, the test will fail loudly — the correct outcome.

**Effort.** M.

---

## Suggested execution order

1. Item 2 (validator extraction) — establishes the boundary the other items rely on.
2. Item 4 — short, sharp invariants; builds confidence in the test infrastructure.
3. Item 1 — broadest coverage; benefits from Items 2 and 4 having stabilized expected behavior.
4. Item 3 — most exploratory; do last so failures land against a known-stable baseline.

## Total effort

~1 S, 3 M, 1 L → roughly 2–3 focused days for one engineer, including iteration on Item 3.
