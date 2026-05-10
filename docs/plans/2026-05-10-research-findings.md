# Research findings — 2026-05-10

Five parallel research agents looked into open questions raised during plan execution. Summary of actionable findings, plus what's still unanswered.

## Best community sources discovered

These all datamine the shipped `HeroesOldenEra_Data/StreamingAssets/Core.zip`:

- **alcaras/homm-olden** (https://github.com/alcaras/homm-olden) — full hero catalog with English-locale names, regenerated 2026-05-10. 108 heroes × 6 factions. **Primary source for Thread 2.4.**
- **randys006/HeroesOETool1** (https://github.com/randys006/HeroesOETool1/tree/master/Core/DB) — full `Core/DB/...` JSON dump.
- **twobob/mnmVis** (https://github.com/twobob/mnmVis/tree/master/StreamingAssets/Core/DB) — same dump + the seven stock `.rmg.json` templates.
- **laszlo-gilanyi/OldenEraExplorer** — units/heroes/spells/artifacts viewer (GPL-3.0). Useful as a UX reference if we add pickers.

## Findings by topic

### 1. Heroes & factions (Thread 2.4 unblocked)

**Plan assumption was wrong on SID format.** We assumed `hero_<faction>_<class>_<n>` (e.g. `hero_temple_paladin_01`). Actual format is `<unitKey>_hero_<n>` — for example `human_hero_1`, `necro_hero_12`, `unfrozen_hero_5`. No class token in the SID; indices are 1–18 per faction, not zero-padded.

**6 factions** with display name vs internal key mismatches (legacy):

| Display name | Internal `unitKey` (SID prefix) |
|---|---|
| Temple | `human` |
| Necropolis | `necro` |
| Grove | `nature` |
| Hive | `demon` |
| Schism | `unfrozen` |
| Dungeon | `dungeon` |

**108 heroes** = 18 per faction (indices 1–9 might, 10–18 magic). Full names available in `alcaras/homm-olden` `docs/data.js` → `window.OE_DATA.HEROES`. Campaign-only hero "Gunnar" not in the 108.

**Confidence: high** for faction names and SIDs (extracted from shipped JSON). Class label per hero is medium — inferred from `kind: might/magic` + faction class table.

**Action:** Thread 2.4 can move forward. Fetch and bundle a `heroes.json` derived from the alcaras catalog. Update Thread 2 plan's example-SID guidance from `hero_temple_paladin_01` to `human_hero_1`.

### 2. Faction Laws & Astrology (Thread 3.3 — wording verified)

**Faction Laws** is a new mechanic (not a HoMM3 carryover). Players earn **Law Points** from palace buildings, battle wins, and capturing map objects, then spend them on a tiered tree (Factional Laws + Army Laws). Effectively a civilization-wide talent tree.

**Astrology** is a separate currency system. Towns generate **Astrology Points** daily (notably from Mage Guild buildings). Points unlock or upgrade neutral spells (e.g. Town Portal 3 pts, Dimension Door 4 pts). The Stargazer skill grants +250/day, doubled with Scouting.

The slider's `factionLawsExpModifier` / `astrologyExpModifier` scale point gain rate, not progression generically.

**Suggested replacement hints (≤ 12 words):**
- Faction laws XP: `"Scales Law Point gain used to unlock faction-wide passive upgrades."`
- Astrology XP: `"Scales Astrology Point gain used to unlock and upgrade neutral spells."`

**Confidence: medium.** Mechanics descriptions cross-validated across official site, hoodedhorse wiki, community sites, and th.gl skill DB; verbatim in-game tooltip text could not be retrieved (primary wiki fetches returned 403). No higher-confidence wording available without running the game.

**Action:** replace the placeholder TODO hints in `WinConditionsPanel.razor` with the suggested wording. Keep a TODO note for later in-game-tooltip verification, but the new copy is substantively grounded in research.

### 3. Win condition labels (Thread 3 — internal inconsistency found)

External research blocked across the board (every wiki returned 401/403). **No external authoritative labels available.**

**However:** the agent flagged a real internal inconsistency. Our `KnownValues.VictoryConditionLabels` exposes only 4 conditions (`_1`, `_3`, `_5`, `_6`). `win_condition_4` is commented out in the array but is **referenced by `MainWindow.xaml.cs:596,637,647-649`** and **used by shipped templates** `Helltide.rmg.json` and `Symmetry.rmg.json`. A user opening one of those templates lands in an inconsistent UI state where the dropdown can't represent the loaded value.

**Action:** re-enable `win_condition_4` with a placeholder label (the original comment said "Gladiator Arena"). The agent's investigation of `MainWindow.xaml.cs` lines 596/637/647-649 — where checking `win_condition_4` triggers gladiator-arena rule logic — supports the "Gladiator Arena" name. **Confidence: low-medium** that this is the canonical name; high that it's our best guess without running the game.

### 4. Map size names (no findings)

Could not confirm any in-game tier names for sizes 96/128/144/160/192/240. No source pairs Olden Era tile counts with named tiers. The competitor editor (DerpcatMusic/homm-olden-era-rmg-editor-website) uses raw integer inputs, suggesting size names live only in client localization strings.

Experimental sizes 256–512 don't appear in any shipped template — community/editor headroom only.

**Action: none.** Keep current `MapSizeLabel` placeholders. Annotate as unverified in code if we want to flag for later.

### 5. SID and resource code spot-check

- **Building presets:** confirmed. `poor_buildings_construction`, `rich_buildings_construction`, `ultra_rich_buildings_construction` are all valid. Two not currently in our `KnownValues.BuildingsConstructionSids` worth adding: `extra_poor_buildings_construction`, `army_buildings_construction` (Shamrock template uses the latter).
- **Resources:** confirmed. `resource_gold/wood/ore/crystals/mercury/gemstones`. Also exists but unused: `resource_dust`. (Olden Era uses `mercury`, NOT `sulfur` like other HoMM games.)
- **Item tier SIDs:** `random_item_epic` and `random_item_legendary` confirmed in stock templates. `random_item_common` and `random_item_rare` plausible but not seen in the seven stock templates the agent scanned.
- **`add_bonus_hero_stat`:** confirmed. Initial agent flag of "movementBonus is not a valid stat name" was a false alarm — `movementBonus` IS used by shipped templates (Jebus Cross, etc.). The agent was looking at `defaultParameters` of a different bonus shape; our usage matches stock templates.
- **Faction SIDs:** the SID research agent reported only 4 factions (`human/undead/dungeon/unfrozen`) from an older dump. The dedicated heroes agent (against the 2026-05-10 alcaras dump) found all 6. **Use the 6-faction list above.**

**Action:**
- Add `extra_poor_buildings_construction` and `army_buildings_construction` to `KnownValues.BuildingsConstructionSids` (ships valid; helps if anyone adds a building-preset picker).
- Optional: add `resource_dust` to any future resource-bonus picker.
- No code change needed for `add_bonus_hero_stat`.

## Action items derived from research

| # | Item | Effort | Plan link |
|---|---|---|---|
| A | Re-enable `win_condition_4 = "Gladiator Arena"` in `KnownValues` | S | inconsistency, not in plans |
| B | Replace placeholder Faction Laws / Astrology hints with researched wording | S | Thread 3.3 |
| C | Add `extra_poor_buildings_construction` and `army_buildings_construction` to `KnownValues.BuildingsConstructionSids` | S | adjacent to Thread 2.2 |
| D | Update Thread 2.4 plan: SID format is `<unitKey>_hero_<n>`, not `hero_<faction>_<class>_<n>`. List the 6 factions. Point at `alcaras/homm-olden` as the data source. | S | Thread 2.4 |
| E | Bundle `heroes.json` derived from `alcaras/homm-olden` catalog | M | Thread 2.4 (was blocked) |

A–C are immediate quick wins. D is a plan update. E is a separate session — depends on choosing a license-compatible way to ship the data (alcaras/homm-olden license unknown from this research).
