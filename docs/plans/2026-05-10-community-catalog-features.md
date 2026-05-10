# Features enabled by `CommunityCatalog`

Date: 2026-05-10. The bundled datamine in `src/OldenEra.Generator/CommunityData/` exposes 108 heroes, 152 units, 103 spells, 30 skills, 24 subclasses, and 6 factions. This file inventories what we can build on top of it, sized so each can be picked up independently.

## Cross-cutting decisions

These apply to every item below. Decide once.

1. **Where do dropdowns get the catalog from?**
   - **Option A (recommended)**: inject `CommunityCatalog.Default` into Razor components via DI. Mirror in WPF by importing `CommunityCatalog.Default` directly (it's a static singleton).
   - Option B: pass the catalog as a `[Parameter]` on each component. More plumbing, no real benefit.
2. **What happens to existing free-text fields when a catalog dropdown replaces them?**
   - `Settings.Bonuses.ItemSid`, `SpellSid` are persisted in `.oetgs` settings files and embedded in shared URLs. **Do not break legacy values.** When loading a saved value that is not in the catalog, render it as a "(unknown SID — keep as-is?)" option at the top of the dropdown. Same for hero ban lists if any user has a stale ID.
3. **Faction display**: prefer `FactionEntry.Name` (display) over `FactionEntry.Id` (internal). The catalog already has both.
4. **Search/filter**: any picker exposing >30 items needs a text filter. Heroes (108), units (152), spells (103), skills (30) all qualify. Use a single shared `<CatalogFilter>` Razor component.

## Quick-win order (recommended)

1. **F1 — Spell-bonus picker** (S, no UX risk, replaces a confusing free-text field)
2. **F2 — Banned-objects upgrade** (S, swaps the hardcoded list for the catalog's units, properly grouped)
3. **F3 — Hero ban list** (M, a real new feature, the most asked-for outcome of having heroes data)
4. **F4 — Fixed starting hero per faction** (M, depends on F3's plumbing)
5. **F5 — Subclass / skill awareness** (M+, mostly informational; only build if a use case emerges)

Each item below is sized so you can stop at any point and have shipped value.

---

## F1 — Spell-bonus picker

### Goal

Replace the free-text "Bonus spell SID" input with a dropdown grouped by school and tier. Same for the parallel ItemSid (deferred until items are in the datamine).

### Where it lives today

- Setting: `GeneratorSettings.Bonuses.SpellSid` (`src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs:146`).
- UI: `src/OldenEra.Web/Components/StartingBonusesPanel.razor:58-68`.
- WPF mirror: corresponding TextBox in `src/OldenEra.TemplateEditor/Views/ExperimentalPanel.xaml` (or wherever starting bonuses live; grep first).
- Generator usage: `TemplateGenerator.cs` emits the SID into `gameRules.bonuses[].parameters` when non-empty.

### Plan

1. Create a shared `SpellPicker.razor` component:
   - Takes `string? Value` and `EventCallback<string?> ValueChanged`.
   - Loads `CommunityCatalog.Default.Spells`.
   - Renders a `<select>` with `<optgroup>` per school (day/night/arcane/primal). Within each, sort by tier then name.
   - Top option: `<option value="">(no bonus spell)</option>`.
   - If `Value` is non-empty and not in the catalog, prepend `<option>{Value} (legacy/unknown)</option>` to preserve the saved value.
2. Replace the text input in `StartingBonusesPanel.razor:58-68` with `<SpellPicker @bind-Value="Settings.Bonuses.SpellSid" />`.
3. Mirror in WPF: `<ComboBox>` with `ItemTemplate` showing `{Tier}. {Name}` and a `GroupStyle` keyed on `School`.
4. Tooltip / hint: when a spell is selected, show `desc` and `manaCost[0]` underneath. Optional but high-leverage.

### Schema/data risk

None. The setting type doesn't change. Saved `.oetgs` files load unchanged.

### Tests

- bUnit (or simple compile check) on `SpellPicker` rendering all spells.
- Existing `HostParityTests` continues to pass — JSON shape unchanged.

### Effort: S.

### Out of scope

Item picker. Items aren't in the datamine yet (the agent confirmed). When/if `items.json` ships upstream, mirror this component as `ItemPicker` and wire to `Settings.Bonuses.ItemSid` the same way. Spells are doing 80% of the cleanup value alone.

---

## F2 — Banned-objects upgrade

### Goal

The current "Banned objects" panel renders chips from a hardcoded `KnownValues.ObjectSids` list. Replace with a catalog-derived multi-select grouped by faction × tier, so users see real units rather than a flat string list.

### Where it lives today

- UI: `src/OldenEra.Web/Components/ExperimentalZonePanel.razor:77-86` (chip grid) and `:88-108` (per-player count limits).
- Source list: `KnownValues.ObjectSids` in `src/OldenEra.Generator/Models/Unfrozen/KnownValues.cs:109`.
- Setting: `Settings.Content.GlobalBans` (`List<string>`).

### Plan

1. New `UnitBanGrid.razor` component:
   - Loads `CommunityCatalog.Default.Units`.
   - Renders a section per faction (using `FactionEntry.Name`), each containing chips per tier (1–7).
   - Selected state binds to `Settings.Content.GlobalBans` (still `List<string>`; the list now holds unit SIDs from the catalog, plus any legacy strings the user previously banned).
2. Keep `KnownValues.ObjectSids` as a **fallback**: if the SID isn't in the catalog, show it in a "Legacy bans" section so the user can clear it.
3. Optional: per-faction "Ban all tier-1" / "Ban all tier-7" shortcut buttons.
4. Mirror in WPF.

### Schema/data risk

None — `GlobalBans` stays `List<string>`. Existing values pass through.

### Tests

- Catalog test asserting unit IDs sort cleanly into faction × tier groups.
- Manual smoke: ban a Tier 5 unit, generate, confirm the SID lands in the emitted JSON.

### Effort: S.

### Out of scope

Validating that banned SIDs make sense for the chosen template (e.g. banning all Necropolis units when no Necromancy heroes exist). That's pre-generation validation, deferred.

---

## F3 — Hero ban list

### Goal

Add per-faction multi-select hero ban lists. The most-asked-for feature once we have hero data. Drives `Settings.HeroBans` (a new field) into the emitted template's hero-pool restrictions.

### Where it lives today

Doesn't. This is net-new feature work.

### Plan

1. **Settings model**:
   - Add `public List<string> HeroBans { get; set; } = new();` to `HeroSettings` in `GeneratorSettings.cs`.
   - Persist in `SettingsFile` mapper (round-trip for `.oetgs`).
2. **Generator wiring**:
   - At template assembly time, emit banned hero IDs into the appropriate `.rmg.json` field. **Open question**: which field? Inspect shipped templates with hero bans (search `Olden Era - Template Editor/GameData/ExampleTemplates/*.rmg.json` for `bannedHeroes` / `heroExclusions` / similar keys) before adding the emit logic. If no shipped template uses such a field, this item is **blocked on understanding the JSON contract** — don't invent fields.
3. **UI** (`HeroSettingsPanel.razor`):
   - Below the existing min/max/increment sliders, add a "Ban specific heroes" expandable section.
   - Per-faction tabs (using `CommunityCatalog.Default.Factions`).
   - Within each, a chip per hero (`HeroEntry.Name`) with `HeroEntry.Specialty` as a tooltip.
   - Clicking a chip toggles its presence in `Settings.HeroSettings.HeroBans`.
4. **Mirror in WPF**.
5. **Validation rule** (extend `SettingsValidator`): warn if all heroes of a faction are banned and the player count requires that faction (e.g. on `MatchPlayerCastleFactions`).

### Schema/data risk

Medium. The JSON-schema research at step 2 is the gating risk. If the game's `.rmg.json` doesn't support per-template hero exclusions, the entire feature is implemented client-side only — useful for the "starting hero" sub-feature (F4) but no effect on guard heroes that spawn from pools. Document the limitation explicitly.

### Tests

- Unit test on the settings round-trip.
- If JSON emit lands: an `EmittedIdValidationTests` extension confirming banned hero IDs match real catalog IDs.

### Effort: M (rises to L if the JSON schema needs reverse-engineering).

### Dependencies

`CommunityCatalog` (✅), settings round-trip plumbing (✅).

---

## F4 — Fixed starting hero per faction

### Goal

When a player picks a faction, optionally pin which hero they start with. Mirrors a common HoMM3 random-template feature.

### Where it lives today

Doesn't. Net-new.

### Plan

1. **Settings model**:
   - `Dictionary<string, string?> FixedStartingHeroByFaction` keyed by faction id (e.g. `"temple" → "human_hero_3"`). `null` or missing = random.
2. **Generator wiring**: emit the fixed hero ID into the player's starting `MainObject` of type `Spawn`. Inspect shipped templates that pin starting heroes — `Jebus Cross.rmg.json` and `Helltide.rmg.json` likely do.
3. **UI** (in `HeroSettingsPanel.razor`, below F3's ban list):
   - One row per faction.
   - Each row: faction label + hero dropdown (default `Random`).
   - Dropdown options come from `CommunityCatalog.Default.HeroesByFaction(factionId)`, filtered to exclude any hero in `HeroBans`.
4. **Mirror in WPF**.
5. **Validation**: blocker if a fixed hero is also in the ban list.

### Schema/data risk

Lower than F3 — fixed starting heroes are a known existing JSON pattern in shipped templates. The audit step is shorter.

### Effort: M.

### Dependencies

F3 (the UI sits next to it; the ban-list-vs-fixed-hero validation depends on both being present).

---

## F5 — Subclass / skill awareness

### Goal

Catch-all for features that consume `subclasses.json` and `skills.json`. None are urgent; build only if a specific use case appears.

### Possible expressions

- **Subclass preview**: when a user pins a hero in F4, show what subclass that hero is eligible for and the required skill 5-tuple.
- **Skill-based content control**: allow the user to skew the generator toward "more daylight magic" or "no nightshade", influencing pool weights.
- **Subclass-aware bans**: if all heroes that can become "Paragon" are banned, warn the user.

These are all **research-mode features**. They don't move the core "generate a playable map template" outcome — they're for advanced users who want to design balanced templates.

### Plan

Not yet.  Treat F5 as a placeholder; promote individual items to their own section once a real user asks for one.

### Effort: M+ per individual feature.

---

## Items intentionally NOT in this plan

- **Item picker for `Settings.Bonuses.ItemSid`**: blocked on `items.json` not shipping in the datamine. Add when upstream covers it.
- **Spell-pool tier balancing**: the generator's pool selection is mechanical (T0..T5 pools with fixed multipliers). Letting users tilt that balance is a deeper refactor than F1–F4.
- **Per-zone hero placement**: requires the user to think in zones, not factions. Not commonly requested.
- **Generation algorithm changes**: the catalog only feeds inputs/outputs; it doesn't change the core algorithm. Anything that touches `BuildVariant*` is out of scope here.

## Suggested next session

If you want a single small item to land: **F1 (spell picker)**. It's the smallest deliverable, replaces a clearly broken UX element (free-text SID), and its plumbing pattern (DI-loaded singleton + bUnit-style component test) becomes a template the bigger items reuse.

If you want to start the bigger work: **F3 (hero ban list)**, *with* an upfront 30-minute audit pass on shipped templates first to determine whether per-template hero exclusions are even supported by the game's JSON schema. Without that audit the feature can be designed but not actually shipped end-to-end.
