# Possible Settings Catalog

Reference of all template capabilities that *could* be exposed as user settings.
Use this when planning which features to surface in the redesigned UI.

---

## High-Value Shortlist

If the redesign budget is tight, these are the capabilities most likely worth
exposing — high player impact, distinct from existing settings:

1. **Global object bans** — let users blacklist specific content (e.g., no Dragon Utopias, no Pandora's Boxes). Largest single jump in template expressiveness.
2. **Building presets per zone** — poor / rich / ultra_rich / massacre / army / siege / arcade. Direct gameplay-pacing lever.
3. **Starting bonuses** — resources, hero stats, items, spells, unit multipliers. Doubles as a handicap/asymmetry system.
4. **Guard progression over time** — `guardWeeklyIncrement` on zones and connections. Changes mid/late-game pacing dramatically.
5. **Single Hero mode / hero hire ban** — flips the whole game format. Low UX cost, high impact.
6. **Terrain density** — obstacles, lakes, elevation per zone. Affects pathing and feel.
7. **Content count limits** — cap occurrences per player (e.g., max 2 gold mines). Closes a known balance gap.
8. **Neutral city guard chance & strength** — control how aggressive neutral cities feel.

---

## Currently Exposed Settings

Source of truth: `OldenEra.Generator/Models/Generator/GeneratorSettings.cs`.

**Top-level**: TemplateName, GameMode, PlayerCount, MapSize, ExperimentalMapSizes,
NoDirectPlayerConnections, RandomPortals, MaxPortalConnections, SpawnRemoteFootholds,
GenerateRoads, ExperimentalBalancedZonePlacement, MatchPlayerCastleFactions,
MinNeutralZonesBetweenPlayers, Topology, FactionLawsExpPercent, AstrologyExpPercent.

**HeroSettings**: HeroCountMin, HeroCountMax, HeroCountIncrement.

**ZoneConfiguration**: NeutralZoneCount, PlayerZoneCastles, NeutralZoneCastles,
ResourceDensityPercent, StructureDensityPercent, NeutralStackStrengthPercent,
BorderGuardStrengthPercent, HubZoneSize, HubZoneCastles.

**AdvancedSettings** (gated by Advanced.Enabled): PlayerZoneSize, NeutralZoneSize,
GuardRandomization, plus 6 NeutralZone counts (Low/Medium/High × Castle/NoCastle).

**GameEndConditions**: VictoryCondition (win_condition_1..6), LostStartCity +
LostStartCityDay, LostStartHero, CityHold + CityHoldDays.

**GladiatorArenaRules**: Enabled, DaysDelayStart, CountDay.

**TournamentRules**: Enabled, FirstTournamentDay, Interval, PointsToWin, SaveArmy.

---

## Unexposed Capabilities

### Terrain & Map Shape

| Capability | Template Field | Range | Player Value |
|---|---|---|---|
| Obstacle density per zone | `zoneLayout.obstaclesFill` | 0.0–1.0 | **High** — pathing and feel |
| Void density in obstacles | `zoneLayout.obstaclesFillVoid` | 0.0–1.0 | Low — cosmetic |
| Lake coverage per zone | `zoneLayout.lakesFill` | 0.0–1.0 | Medium |
| Minimum lake area | `zoneLayout.minLakeArea` | int | Low — cosmetic |
| Elevation cluster scale | `zoneLayout.elevationClusterScale` | double | Low |
| Elevation modes | `zoneLayout.elevationModes` | weighted array | Medium — flat vs. hilly maps |
| Map border water/obstacle width | `variant.waterWidth`, `obstaclesWidth` | double | Medium |
| Border noise | `variant.waterNoise`, `obstaclesNoise` | amp+freq | Low — cosmetic |

### Content & Object Control

| Capability | Template Field | Player Value |
|---|---|---|
| Global object bans | `globalBans` | **High** — biggest unexplored surface |
| Guard value overrides | `valueOverrides[]` | Medium — power-user lever |
| Content count limits | `contentCountLimits[]` | **High** — closes balance gaps |
| Mandatory content | `mandatoryContent[]` | Medium — scenario-style design |
| Content placement rules | `contentItem.rules[]` | Low — likely better as preset |

The full SID list lives in `OldenEra.Generator/Models/Template/ObjectSids.cs` (177+ entries).

### City & Building Configuration

| Capability | Template Field | Player Value |
|---|---|---|
| Building preset | `mainObject.buildingsConstructionSid` (19 variants) | **High** |
| Neutral city guard chance | `mainObject.guardChance` (0.0–1.0) | **High** |
| Neutral city guard value | `mainObject.guardValue` | Medium |
| Abandoned outposts | `mainObject.type = "AbandonedOutpost"` | Medium — niche |
| City placement mode | `mainObject.placement` | Low — usually generator's call |

**Building presets:** poor, rich, ultra_rich, massacre, army, siege, arcade,
chosen_one, plus massacre upgraded variants (_up_1.._up_3).

### Connection Tuning

| Capability | Template Field | Player Value |
|---|---|---|
| Guard weekly growth | `connection.guardWeeklyIncrement` | **High** — late-game pacing |
| Guard escape | `connection.guardEscape` | Medium |
| Guard match groups | `connection.guardMatchGroup` | Low — internal grouping |

### Zone Guard Progression

| Capability | Template Field | Player Value |
|---|---|---|
| Guard weekly increment | `zone.guardWeeklyIncrement` | **High** |
| Guard reaction distribution | `zone.guardReactionDistribution` (int[6]) | Medium — needs UI design |
| Encounter holes (fraction affected) | `zone.encounterHolesSettings.affectedEncounters` | Medium |
| Encounter holes (double holes) | `zone.encounterHolesSettings.twoHoleEncounters` | Low |

### Starting Bonuses

| Capability | Template Field | Player Value |
|---|---|---|
| Bonus resources | `gameRules.bonuses` sid `add_bonus_res` | **High** |
| Bonus hero stats | sid `add_bonus_hero_stat` | **High** |
| Bonus hero item | sid `add_bonus_hero_item` | Medium |
| Bonus hero spell | sid `add_bonus_hero_spell` | Medium |
| Bonus unit multiplier | sid `add_bonus_hero_unit_multipler` | Medium |

Receivers: `all_heroes` or `start_hero` only.

### Game Mode

| Capability | Template Field | Player Value |
|---|---|---|
| Single Hero mode | `gameMode = "SingleHero"` | **High** |
| Hero hire ban | `gameRules.heroHireBan = true` | Medium — softer variant of above |

### Miscellaneous (potential)

| Capability | Template Field | Player Value |
|---|---|---|
| Desertion day/value | `gameRules.winConditions.desertionDay/Value` | Medium — AFK protection |
| Ambient pickup distribution | `zoneLayout.ambientPickupDistribution` | Low |
| Road cluster area | `zoneLayout.roadClusterArea` | Low |

---

## Don't Expose (Internal Plumbing)

These template fields exist but are generator-internal. Surfacing them as
settings would add UI noise without meaningful player choice:

- `connection.length` — distance hint, used internally for layout solving.
- `connection.gatePlacement` — only `"Center"` is a valid value today.
- `gameRules.winConditions.championSelectRule` — only `"StartHero"` is used.
- `gameRules.winConditions.heroLightingDay` — always day 1; no gameplay reason to vary.
- `variant.mode` — `BoundingCircle` vs. `MinimalBoundingSquare` is a generator math detail.
- `zoneLayout.guardCutoffValue` — internal threshold; generator computes it.
- Per-zone biome selectors — generated from faction matching, not a freeform choice.

---

## Interactions to Watch For

- **Bans vs. mandatory content** — banning an SID that's also mandatory should error or auto-clear.
- **Bans vs. value overrides** — overriding a banned SID is meaningless; UI should hide.
- **Content count limits vs. resource density** — both shape mine counts; need consistent semantics.
- **Guard overrides vs. `BorderGuardStrengthPercent`** — global percent applies first, then per-SID override.
- **Encounter holes ↔ guard progression** — holes only make sense with non-trivial guards.
- **Building presets vs. `MatchPlayerCastleFactions`** — preset is faction-agnostic; behavior when factions are forced needs spec.
- **Single Hero mode vs. `HeroCountMax`** — mode forces 1; UI should disable hero count when active.
- **Starting bonuses vs. handicap** — multiple bonuses per player would need an asymmetry editor.

---

## Open Questions

- Does `globalBans` actually take effect in the current Olden Era build? Needs an in-game smoke test before designing UI around it.
- Are experimental map sizes >256 stable, or does the game crash/misbehave?
- Is `gameMode = "SingleHero"` fully supported, or template-only?
- Can `guardWeeklyIncrement` be set to 0 safely (no progression), or does the engine assume a minimum?
- Do `mandatoryContent` placement rules (Road, NearObject, ZoneEdge) all work, or are some no-ops?
- What's the canonical mapping `win_condition_1..6` → human names? The current dropdown labels need verification against the game's localized strings.

---

## Notes for Design Alignment

- Settings are grouped by conceptual area, not UI panel — panel assignment is a design decision.
- Player-value labels are rough; reorder freely once the redesign has a target user persona.
- "Content control" is the largest unexplored surface area and the hardest UX challenge — likely needs a dedicated screen, not a panel.
- Starting bonuses lend themselves to a per-player table, which doesn't fit the current "one form for all players" UX.
