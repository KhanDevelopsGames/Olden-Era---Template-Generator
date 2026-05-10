# TODO: validation, UX/IDs, and text clarity

Brainstorm output 2026-05-10. Three independent threads. Each item is sized to be tackled on its own; pick any one and run it.

---

## Thread 1 — Generation logic correctness

- [ ] **1.1 Validate emitted IDs against shipped JSON.** Write a test that:
  1. Loads every `*ContentPool` / `mandatoryContent` / `contentCountLimits` ID emitted by `TemplateGenerator.Generate(...)` across a matrix of representative settings.
  2. Loads every pool name defined in `Olden Era - Template Editor/GameData/GeneratorData/content_pools/**/*.json` and the equivalents under `content_lists/`, `encounter_templates/`, `zone_layouts/`.
  3. Asserts every emitted ID exists in the shipped data.

  Touch points: `TemplateGenerator.cs:2441-2443` (hardcoded pool IDs), `Olden Era - Template Editor.Tests/`. Currently no such test — `HostParityTests` only round-trips JSON and `TemplatePreviewRendererTests` only checks PNG bytes.

- [ ] **1.2 Port WPF `Validate()` to the web host.** `MainWindow.xaml.cs:379` produces blockers + warnings (default name, zone too small, >240 map size, >1 castle/zone with >10 zones, neutral-separation impossibility). The web host shows none of these. Extract validation into `OldenEra.Generator/Services/SettingsValidator.cs` returning `(blockers[], warnings[])`, call from both hosts.

- [ ] **1.3 Reference-template parity test.** Pick 3–5 example templates (Jebus Cross, Anarchy, Hallway, Diamond) from `GameData/ExampleTemplates/`. Reverse-engineer `GeneratorSettings` that should reproduce their *shape* (zone count, topology, win condition). Generate, then assert structural parity (zone count matches, win-condition flags match, hold-city present iff expected). Catches regressions in `BuildVariant*` dispatch.

- [ ] **1.4 Codify topology invariants.**
  - Tournament mode requires exactly 2 players — assert and surface as a blocker, not silent fallback.
  - HubAndSpoke + City Hold: confirm hub becomes hold city (codebase map says it does; lock with a test).
  - Tier-by-zone-letter (recent commit `59b98ce`): add a regression test using the exact failure case.

---

## Thread 2 — UX/UI: load IDs from `GameData/`

- [ ] **2.1 Add `GameDataCatalog` service.** New file `OldenEra.Generator/Services/GameDataCatalog.cs`. At startup, parse:
  - `content_pools/**/*.json` → list of `{id, displayName?, sourceFile}` pool entries.
  - `content_lists/*.json` → content list IDs.
  - `encounter_templates/*.json`, `zone_layouts/*.json` likewise.

  Expose `IReadOnlyList<PoolEntry> Pools`, etc. WPF and Blazor both consume the same catalog. (Web embeds the JSON via `Content` items in `OldenEra.Web.csproj`; WPF reads from disk.)

- [ ] **2.2 Replace hardcoded pool literals with catalog lookups.** `TemplateGenerator.cs:2441-2443` and any other `string[] = ["content_pool_..."]` lines. Lookup happens once; if missing, fail loudly at startup rather than silently emitting bad templates.

- [ ] **2.3 Expose pool/content pickers in advanced UI.** New panel section: "Override content pool for zone X" → dropdown populated from catalog. Defaults remain the generator's choice. This unlocks user customization without touching generation logic.

- [ ] **2.4 Hero settings: real options.** `HeroSettingsPanel.razor` currently has only 3 sliders. Add:
  - Hero ban list (multi-select from heroes catalog — needs hero IDs from game data; check what's in `GameData/` first, may need to add a hero list file).
  - Optional fixed starting hero per faction.
  - Faction filter for content lists.

  *Pre-req:* confirm hero IDs are derivable from shipped data. If not, this item is blocked until a hero list is added to the repo.

---

## Thread 3 — Text clarity (UI labels and hints)

- [ ] **3.1 Rename Hero panel labels.** `OldenEra.Web/Components/HeroSettingsPanel.razor`:
  - "Initial hero cap" → "Heroes per player (min)"
  - "Max hero cap" → "Heroes per player (max)"
  - "Increase cap / castle" → "Bonus heroes per extra castle"

  Mirror in WPF (`MainWindow.xaml`). These describe `heroCountMin`/`heroCountMax`/`heroCountIncrement` in the `.rmg.json` — the current names suggest leveling caps, which is wrong.

- [ ] **3.2 Group Win/Lose conditions.** `OldenEra.Web/Components/WinConditionsPanel.razor`: split the three checkboxes into two sections — **Defeat conditions** (Lose when starting city / hero lost) and **Alternate victory** (Win by holding neutral city). Add a hint when a checkbox is auto-disabled by the primary win condition: `"Locked because primary win condition is City Hold"`.

- [ ] **3.3 Add tooltips for XP modifiers.** `Faction laws XP` and `Astrology XP` (25–200%): add a one-line `oe-hint` explaining what the system does in-game and what 100% baseline means. Use the Olden Era wiki for canonical wording.

- [ ] **3.4 Audit remaining advanced labels.** Skim `AdvancedPanel.razor`, `ZoneConfigPanel.razor`, `PerTierOverridesPanel.razor`, `ExperimentalZonePanel.razor` for cryptic terms (`guardCutoff`, `guardMultiplier`, `guardWeeklyIncrement`). Either add a hint per slider or accept these as expert-level and add one panel-level explainer at the top.

- [ ] **3.5 Sourcing canonical names.** For **template-author** terms (guard randomization, content pools), no canonical source exists — these are internal generator concepts; write our own clear hints. For **player-facing** terms (heroes, factions, skills, win conditions), use the in-game label / Olden Era wiki to match the game's vocabulary. Don't invent new names for things players already see in-game.

---

## Quick-win order (recommended)

1. **3.1** — 10-line cosmetic fix, immediate clarity gain.
2. **1.1** — automated test, no UI work, prevents silent breakage.
3. **2.1 + 2.2** — foundation that makes both validation (1) and pickers (2.3) easy.
4. **1.2** — copy `Validate()` into shared lib once you're already touching it.

Everything else can follow opportunistically.
