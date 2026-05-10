# Thread 3: Text and UX Clarity — Implementation Plan

**Date:** 2026-05-10
**Mode:** Labels/hints/grouping only. No functional changes. No JSON schema or model field changes.

## Important assumptions (a parallel refactor branch may move files)

- WPF labels do **not** all live in `MainWindow.xaml`. They likely live in per-panel UserControls under `Olden Era - Template Editor/Views/` (e.g. `Views/HeroesPanel.xaml`, `Views/WinConditionsPanel.xaml`). Confirm before each edit.
- Before each edit, the executor should re-grep for the exact current text:
  - `grep -RIn "Initial hero cap" OldenEra.Web "Olden Era - Template Editor"`
  - `grep -RIn "Faction laws XP" OldenEra.Web "Olden Era - Template Editor"`
- E2e suite is at `tests/e2e/specs/smoke.spec.ts` (not `OldenEra.Web/e2e/`). It does **not** reference any label changed in this thread, so no test updates are needed. Re-grep before merging just in case.
- Hint styling already exists: Razor uses `<div class="oe-hint">…</div>`. WPF has no equivalent style — propose a small `TextBlock` with `Foreground="#888"`, `FontStyle="Italic"`, `TextWrapping="Wrap"`, `Margin="0,2,0,6"`. If a `HintText` style already exists in the WPF resources, prefer that; grep `App.xaml`/theme files for `Style x:Key="Hint"` first.

---

## Item 1 — Rename Hero panel labels (S)

**Razor — `OldenEra.Web/Components/HeroSettingsPanel.razor`:**

| Line | Current | Proposed |
|---|---|---|
| 6 | `Label="Initial hero cap"` | `Label="Heroes per player (min)"` |
| 11 | `Label="Max hero cap"` | `Label="Heroes per player (max)"` |
| 16 | `Label="Increase cap / castle"` | `Label="Bonus heroes per extra castle"` |

Add directly after the section header (`<div class="oe-section-header">Heroes</div>`, line 4):
```html
<div class="oe-hint">Maximum heroes a player may hire; increases with each captured castle.</div>
```

**WPF — `Olden Era - Template Editor/Views/HeroesPanel.xaml` (or wherever the labels live; grep first):**
- `Content="Initial hero cap"` → `Content="Heroes per player (min)"`
- `Content="Max hero cap"` → `Content="Heroes per player (max)"`
- `Content="Increase cap / castle"` → `Content="Bonus heroes per extra castle"`
- Insert a `TextBlock` between the section header and the controls grid:
  ```xml
  <TextBlock Text="Maximum heroes a player may hire; increases with each captured castle."
             TextWrapping="Wrap" Foreground="#888" FontStyle="Italic" Margin="5,2,5,8"/>
  ```
- The label column may be tight for "Bonus heroes per extra castle". Confirm visually; widen the column if the text truncates.

**Verification:**
- `dotnet build` both projects.
- Manual: launch web (`dotnet run --project OldenEra.Web`), open Heroes section, confirm three new labels and the hint render correctly. Repeat in WPF.
- `grep -RIn "hero cap"` should return zero hits across both UI projects.
- e2e: no change.

**Effort:** S.

---

## Item 2 — Group Win/Lose conditions (M)

**Razor — `OldenEra.Web/Components/WinConditionsPanel.razor`:**

Today the three checkboxes (lines 35–66) are siblings under the Game Rules section. Wrap them in two subgroups. Insert just before line 35:

```html
<div class="oe-subsection-header">Defeat conditions</div>
```

Then keep the two "Lose when…" checkboxes (lines 35–58). After the LostStartHero checkbox closes (line 58), insert:

```html
<div class="oe-subsection-header">Alternate victory</div>
```

Then keep the City Hold checkbox + sub-slider (lines 60–75).

For the auto-disable hint, the existing `disabled="@(isTournament || isLostCityWin)"` etc. pattern at lines 38, 55, 63 stays. Beneath each affected checkbox, render a hint when locked:

- After line 41 (closing LostStartCity):
  ```html
  @if (isLostCityWin)
  {
      <div class="oe-hint" style="margin-left:1.5rem;">Locked because primary win condition is Lost City.</div>
  }
  else if (isTournament)
  {
      <div class="oe-hint" style="margin-left:1.5rem;">Locked because primary win condition is Tournament.</div>
  }
  ```
- After line 58 (closing LostStartHero):
  ```html
  @if (isTournament)
  {
      <div class="oe-hint" style="margin-left:1.5rem;">Locked because primary win condition is Tournament.</div>
  }
  ```
- After line 66 (closing CityHold):
  ```html
  @if (isCityHoldWin)
  {
      <div class="oe-hint" style="margin-left:1.5rem;">Locked because primary win condition is City Hold.</div>
  }
  else if (isTournament)
  {
      <div class="oe-hint" style="margin-left:1.5rem;">Locked because primary win condition is Tournament.</div>
  }
  ```

CSS: if `oe-subsection-header` does not already exist (grep `OldenEra.Web/wwwroot/css` for it), add a small rule mirroring the existing section header at one step smaller weight. Example: `font-weight:600; font-size:0.9rem; margin-top:0.75rem; padding-bottom:0.2rem; border-bottom:1px solid var(--oe-border, #2a2a2a);`. Place next to `.oe-section-header`.

**WPF — `Olden Era - Template Editor/Views/WinConditionsPanel.xaml`:**

Insert two `TextBlock` headers around the three checkboxes:
- Before the first "Lose when…" checkbox:
  ```xml
  <TextBlock Text="Defeat conditions" FontWeight="SemiBold" Margin="0,8,0,2"/>
  ```
- Before the City Hold checkbox:
  ```xml
  <TextBlock Text="Alternate victory" FontWeight="SemiBold" Margin="0,12,0,2"/>
  ```

For the locked hints, the WPF code-behind already toggles `IsEnabled` on these checkboxes via `MainWindow.xaml.cs`. Re-grep:
```
grep -n "ChkLostStartCity\|ChkCityHold\|ChkLostStartHero" "Olden Era - Template Editor/MainWindow.xaml.cs"
```
For each checkbox, add a sibling `TextBlock` (initially `Visibility="Collapsed"`, `x:Name="HntLostStartCityLocked"` etc.) immediately below, and update the same code-behind that flips `IsEnabled` to also flip `Visibility` and set `Text`. Pure C# is simpler than `DataTrigger` plumbing here.

**Verification:**
- Web: switch the Primary win condition dropdown to City Hold, Lost City, and Tournament — confirm correct checkboxes go disabled and the right hint appears.
- WPF: same checklist.
- `grep -RIn "Lose when starting"` should still return three hits (one Razor, one WPF, plus any existing tests — confirm none break).

**Effort:** M (mostly because of the WPF code-behind plumbing for the lock hints).

---

## Item 3 — XP modifier tooltips (S, with a verification flag)

**Razor — `WinConditionsPanel.razor`** lines 7–14 are the two sliders.

After each `<SliderRow … />`, add a `<div class="oe-hint">…</div>`. Proposed wording (canonical source not verifiable from the repo — see flag below):

- Faction laws XP: `"Speed at which factions unlock new laws and tier bonuses. 100% is the in-game baseline."`
- Astrology XP: `"Speed at which the weekly astrological event progresses. 100% is the in-game baseline."`

**FLAG FOR EXECUTOR:** these two systems are Olden Era player-facing mechanics. The above wording is plausible but **not** verified against the in-game tooltip text or the official wiki. Per the sourcing rules below (Item 5), player-facing wording must match the game's vocabulary. Before merging, the executor should:
1. Open Olden Era and read the in-game tooltip on the Faction Laws and Astrology UI.
2. Cross-check the Olden Era wiki entries for "Faction Laws" and "Astrology".
3. If wording differs, replace the strings above. Until verified, leave a `<!-- TODO: verify wording against in-game tooltip -->` comment beside each hint.

A safer fallback, if in-game text cannot be obtained quickly:
- `"Adjusts the rate at which Faction Laws progress. 100% = standard pace."`
- `"Adjusts the rate at which Astrology progresses. 100% = standard pace."`

**WPF — `Views/WinConditionsPanel.xaml`:**
After each `Slider` (SldFactionLawsExp, SldAstrologyExp), add a Grid row with a hint `TextBlock` spanning both columns. Extend the grid with two new rows for hints; shift `Grid.Row` indices accordingly. Since the executor will be touching this XAML for Item 2, batch the work.

**Effort:** S (text) + the wording verification step.

---

## Item 4 — Audit advanced labels (M)

Files audited: `AdvancedPanel.razor`, `ZoneConfigPanel.razor`, `PerTierOverridesPanel.razor`, `ExperimentalZonePanel.razor`.

Most cryptic terms already have an `oe-hint` nearby — the audit is targeted, not blanket.

### `AdvancedPanel.razor`
- Existing top hint: `"Relative region weights and guard variance used by the game layout."` — too vague. Replace with: `"Per-zone size multipliers and randomization for guard strength. Affects layout but not what's inside."`
- "Player zone size" / "Neutral zone size" — `0.5x–2x`. Add a single combined hint under both sliders: `"Multiplier applied to the base zone area. 1x is the generator's default."`
- "Guard strength randomization" (`guardRandomization`, 0–50%) — add hint: `"How much each guard's strength varies from the zone's target. 0% = identical, 50% = up to half-strength swing either way."`
- Six "Low/Medium/High neutral, [no] castle" sliders — already covered by the section hint. No change.
- "Min neutrals between players" — already has a contextual hint below it. No change.

### `ZoneConfigPanel.razor`
- "Resource spawn rate" (20–400%) — already plain. No change.
- "Structure spawn rate" (20–200%) — already plain. No change.
- "Zone neutral strength" / "Border/portal strength" — values are 25–300%. Add a single panel-level hint at the top: `"All percentages compare to the generator's baseline (100%). Lower = sparser/weaker, higher = denser/stronger."`
- "Castles / Player Zone" / "Castles / Neutral Zone" — clear from label. No change.
- Checkboxes — most already have conditional hints. No change.

### `PerTierOverridesPanel.razor`
- Top hint already explains overrides. Keep.
- "Guard / week" (`GuardWeeklyIncrement`) — add hint: `"Weekly increase in guard strength for this tier. 0% = use the global value above."`
- "Building preset" — `<select>` with a "Use global / generator default" option already self-documents. No change.

### `ExperimentalZonePanel.razor`
- Terrain card hint `"0 % = generator default. Higher values create more chokepoints."` is fine, but applies more clearly to obstacles than lakes. Tweak to: `"0% = generator default. Obstacles add chokepoints; lakes add water cover."`
- Building presets — fine.
- Guard progression — hint already explains defaults. Fine.
- Neutral cities — `Guard chance` and `Guard value` have no hints. Add card-level hint: `"Chance that a neutral city gets a guarding stack, and how strong it is relative to the zone baseline (100%)."`
- Content control — hint exists. Fine.

### Items intentionally NOT renamed
The user instructs not to invent new names for internal generator concepts. Do not rename `GuardWeeklyIncrement`, `GuardRandomization`, `GuardCutoff`, `GuardMultiplier`, `DiplomacyModifier`, etc. — explain via hints only. Note: `guardCutoff`, `guardMultiplier`, and `diplomacyModifier` from the original requirements list are **not present** in the current Razor files. They may exist in the JSON schema/model but are not yet exposed in the UI panels audited, or were renamed during the parallel refactor. Re-grep before declaring this item complete: `grep -RIn "GuardCutoff\|GuardMultiplier\|DiplomacyModifier" OldenEra.Web`.

### WPF mirror
All hints proposed above must be added to the matching WPF UserControls under `Olden Era - Template Editor/Views/`. Likely files (re-grep to confirm names in the refactor branch):
- `Views/AdvancedPanel.xaml`
- `Views/ZoneConfigPanel.xaml`
- `Views/PerTierOverridesPanel.xaml`
- `Views/ExperimentalZonePanel.xaml`

If any don't exist as separate UserControls, the markup will be inline in `MainWindow.xaml`. Grep before editing.

**Effort:** M.

---

## Item 5 — Sourcing policy (S, documentation only)

This is the one item with no UI change. Document the policy at the top of this plan and, if the project keeps a contributor doc, append it there too.

**Policy text:**

> **Label and hint sourcing rules**
>
> - **Player-facing terms** (heroes, factions, skills, win conditions, faction laws, astrology, gladiator arena, tournament): match the in-game label or the Olden Era wiki. Do not invent synonyms. When in doubt, copy the game's tooltip wording verbatim.
> - **Template-author terms** (guard randomization, content pools, tiers, building presets, neutral zone counts, weekly guard increment, etc.): no canonical source exists. Write plain-English hints that describe what the slider/toggle does. Do not invent new names for the underlying concept; only explain it.
> - **Reviewer flag:** any new player-facing string added without verifying the in-game wording must be marked with `<!-- TODO: verify -->` (Razor) or an XAML comment. Do not merge with TODOs unresolved.

**File to update:** if `AGENTS.md` or `README.md` already sets project conventions, append the policy as a new section "UI text sourcing". Otherwise embed only in this plan document.

**Effort:** S.

---

## Sequencing

1. Item 5 first (policy) — anchors the wording decisions.
2. Item 1 (Hero labels) — small, isolated, builds confidence.
3. Item 3 (XP hints) — depends on policy verification step.
4. Item 2 (Win/Lose grouping) — touches the same Razor file as Item 3, batch.
5. Item 4 (Advanced audit) — largest surface area, do last.

## Cross-cutting verification checklist

- `dotnet build` for `OldenEra.Web` and `Olden Era - Template Editor` (both must build clean).
- `dotnet test` for `Olden Era - Template Editor.Tests`.
- Run e2e: `cd tests/e2e && npx playwright test` — should still be all green; no test text changed.
- Manual smoke: open every panel in web, then in WPF. No truncated labels, all hints visible, all locked-state hints triggered by switching the win condition dropdown through all five values.
- `grep -RIn "hero cap\|Initial hero\|Increase cap"` returns zero in both UI trees.

## Risk / parallel-refactor mitigation

- Each step starts with `grep -RIn "<exact current text>"` to locate the file. If zero hits in Razor, the refactor renamed the component — search by component name (`HeroSettings`, `WinConditions`) and re-derive paths.
- If the WPF UserControl files were merged back into `MainWindow.xaml`, the labels will be at similar positions but with a different parent — visual inspection still applies.
- If `oe-hint` was renamed in CSS, search for `.oe-hint` and update class names consistently.
