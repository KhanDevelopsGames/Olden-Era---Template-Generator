# Possible Settings Catalog

Reference of all template capabilities that *could* be exposed as user settings.
Organized by category. Use this when planning which features to surface in the redesigned UI.

---

## Currently Exposed Settings (summary)

- Template name, map size, player count
- Hero count min/max/increment
- Map topology (Ring, Random, HubAndSpoke, Chain)
- Zone counts, castle counts, resource/structure density
- Neutral stack & border guard strength
- Roads, portals, remote footholds
- Faction matching, balanced zone placement
- Advanced zone size multipliers, guard randomization
- Game rules: faction laws XP, astrology XP
- Victory conditions: Classic, Timed, Lost Start City, Arena, City Hold, Tournament

---

## Unexposed Capabilities

### Terrain & Map Shape

| Capability | Template Field | Range | Notes |
|---|---|---|---|
| Obstacle density per zone | `zoneLayout.obstaclesFill` | 0.0–1.0 | How cluttered terrain is with impassable objects |
| Void density in obstacles | `zoneLayout.obstaclesFillVoid` | 0.0–1.0 | Empty pockets within obstacle clusters |
| Lake coverage per zone | `zoneLayout.lakesFill` | 0.0–1.0 | Water body coverage within zones |
| Minimum lake area | `zoneLayout.minLakeArea` | int | Smallest lake that can generate |
| Elevation cluster scale | `zoneLayout.elevationClusterScale` | double | How grouped height differences are |
| Elevation modes | `zoneLayout.elevationModes` | weighted array | Flat vs. hilly terrain distribution |
| Map border water width | `variant.waterWidth` | double | Thickness of water at map edges |
| Map border obstacle width | `variant.obstaclesWidth` | double | Thickness of obstacles at map edges |
| Map corner radius | `variant.cornerRadius` | double | Sharp rectangular vs. rounded map edges |
| Border noise amplitude/frequency | `variant.waterNoise`, `obstaclesNoise` | amp+freq | Irregularity of map borders |

### Content & Object Control

| Capability | Template Field | Notes |
|---|---|---|
| Global object bans | `globalBans` | Remove specific objects from generation entirely (e.g., no Dragon Utopias) |
| Guard value overrides | `valueOverrides[]` | Override how heavily a specific object type is guarded |
| Content count limits | `contentCountLimits[]` | Cap occurrences per player (e.g., max 2 gold mines) |
| Mandatory content | `mandatoryContent[]` | Force specific objects to always appear in certain zones |
| Content placement rules | `contentItem.rules[]` | Control where content spawns: near roads, near objects, zone edges |

There are 177+ known object SIDs including mines, structures, encounters, treasures, and special locations.

### City & Building Configuration

| Capability | Template Field | Range | Notes |
|---|---|---|---|
| Building preset | `mainObject.buildingsConstructionSid` | 19 variants | Controls city starting buildings |
| Neutral city guard chance | `mainObject.guardChance` | 0.0–1.0 | Probability a neutral city spawns guarded |
| Neutral city guard value | `mainObject.guardValue` | int | Strength of city guards |
| Abandoned outposts | `mainObject.type = "AbandonedOutpost"` | — | Alternative to full cities in neutral zones |
| City placement mode | `mainObject.placement` | Center, Connection, NearZone, Uniform | Where in the zone the city sits |

**Building preset variants:**
- Wealth tiers: poor, rich, ultra_rich
- Combat focus: massacre, army, siege
- Specialty: arcade, chosen_one
- Upgraded variants: massacre_up_1 through _up_3

### Connection Tuning

| Capability | Template Field | Range | Notes |
|---|---|---|---|
| Guard weekly growth | `connection.guardWeeklyIncrement` | double | Border guards get stronger each week |
| Guard escape | `connection.guardEscape` | bool | Whether armies can flee border guards |
| Connection length | `connection.length` | double | Hint for physical distance between zones |
| Gate placement | `connection.gatePlacement` | "Center" | Where the gate object sits on the connection |
| Guard match groups | `connection.guardMatchGroup` | string | Group connections to have same guard strength |

### Zone Guard Progression

| Capability | Template Field | Range | Notes |
|---|---|---|---|
| Guard weekly increment | `zone.guardWeeklyIncrement` | double | Zone guards grow stronger over time |
| Guard reaction distribution | `zone.guardReactionDistribution` | int[6] | How aggressively guards patrol/respond |
| Encounter holes (fraction affected) | `zone.encounterHolesSettings.affectedEncounters` | 0.0–1.0 | Skip some encounters randomly |
| Encounter holes (double holes) | `zone.encounterHolesSettings.twoHoleEncounters` | 0.0–1.0 | Fraction with two free paths |

### Starting Bonuses

| Capability | Template Field | Notes |
|---|---|---|
| Bonus resources | `gameRules.bonuses` with sid `add_bonus_res` | Extra gold/resources at start |
| Bonus hero stats | `gameRules.bonuses` with sid `add_bonus_hero_stat` | Starting stat boosts |
| Bonus hero item | `gameRules.bonuses` with sid `add_bonus_hero_item` | Start with an item |
| Bonus hero spell | `gameRules.bonuses` with sid `add_bonus_hero_spell` | Start with a spell |
| Bonus unit multiplier | `gameRules.bonuses` with sid `add_bonus_hero_unit_multipler` | Scale starting army |

Bonuses can target "all_heroes" or "start_hero" only.

### Game Mode

| Capability | Template Field | Notes |
|---|---|---|
| Single Hero mode | `gameMode = "SingleHero"` | One hero only, hiring disabled |
| Hero hire ban | `gameRules.heroHireBan = true` | Disable tavern hiring without changing game mode |

### Miscellaneous

| Capability | Template Field | Notes |
|---|---|---|
| Desertion day/value | `gameRules.winConditions.desertionDay/Value` | AFK protection threshold |
| Hero lighting day | `gameRules.winConditions.heroLightingDay` | When starting hero gets a lantern |
| Champion select rule | `gameRules.winConditions.championSelectRule` | Tournament champion selection |
| Ambient pickup distribution | `zoneLayout.ambientPickupDistribution` | Scattered small pickups on the ground |
| Road cluster area | `zoneLayout.roadClusterArea` | How tightly road-adjacent content clusters |
| Map orientation mode | `variant.mode` | BoundingCircle vs. MinimalBoundingSquare |

---

## Notes for Design Alignment

- Settings above are grouped by conceptual area, not by UI panel — panel assignment is a design decision.
- Some settings interact: e.g., encounter holes only matter if guard progression is active.
- "Content control" (bans, limits, mandatory) is the largest unexplored surface area and the most complex UX challenge.
- Starting bonuses could serve as a handicap/asymmetry system.
- Terrain settings (obstacles, lakes, elevation) are cosmetic but affect gameplay pathing significantly.
