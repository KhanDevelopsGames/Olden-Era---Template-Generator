using System.Collections.Generic;

namespace OldenEra.Generator.Models
{
    public class TournamentRules
    {
        public bool Enabled { get; set; } = false;
        public int FirstTournamentDay { get; set; } = 14;
        public int Interval { get; set; } = 7;
        public int PointsToWin { get; set; } = 2;
        public bool SaveArmy { get; set; } = true;
    }
    public class GladiatorArenaRules
    {
        public bool Enabled { get; set; } = false;
        public int DaysDelayStart { get; set; } = 30;
        public int CountDay { get; set; } = 3;
    }

    public class GameEndConditions
    {
        public string VictoryCondition { get; set; } = "win_condition_1";
        public bool LostStartCity { get; set; } = false;
        public int LostStartCityDay { get; set; } = 3;
        public bool LostStartHero { get; set; } = false;
        public bool CityHold { get; set; } = false;
        public int CityHoldDays { get; set; } = 6;
    }

    public class HeroSettings
    {
        public int HeroCountMin { get; set; } = 4;
        public int HeroCountMax { get; set; } = 8;
        public int HeroCountIncrement { get; set; } = 1;

        /// <summary>
        /// Hero IDs (e.g. <c>"human_hero_3"</c>) banned from the template.
        /// Emitted into <c>globalBans.heroes</c>; matches the schema seen in
        /// shipped templates (Arcade.rmg.json).
        /// </summary>
        public List<string> HeroBans { get; set; } = new();

        /// <summary>
        /// Spell IDs (e.g. <c>"spell.fly"</c>) banned from the template.
        /// Emitted into <c>globalBans.magics</c>; sibling of <see cref="HeroBans"/>.
        /// </summary>
        public List<string> BannedSpells { get; set; } = new();

        /// <summary>
        /// Pinned starting hero per faction. Key is the faction id
        /// (e.g. <c>"temple"</c>), value is a hero id from the catalog,
        /// or <c>null</c>/missing for "random".
        /// </summary>
        /// <remarks>
        /// UI-only at this time: the .rmg.json schema does not declare a
        /// pinned-hero shape on player Spawn MainObjects (audit of 232
        /// Spawn entries across shipped templates: zero references). The
        /// setting round-trips in <c>.oetgs</c> so it is preserved when the
        /// schema later supports it; for now no JSON is emitted.
        /// </remarks>
        public Dictionary<string, string?> FixedStartingHeroByFaction { get; set; } = new();
    }

    public class AdvancedSettings
    {
        public bool Enabled { get; set; } = false;
        public int NeutralLowNoCastleCount { get; set; } = 0;
        public int NeutralLowCastleCount { get; set; } = 0;
        public int NeutralMediumNoCastleCount { get; set; } = 0;
        public int NeutralMediumCastleCount { get; set; } = 0;
        public int NeutralHighNoCastleCount { get; set; } = 0;
        public int NeutralHighCastleCount { get; set; } = 0;
        public double PlayerZoneSize { get; set; } = 1.0;
        public double NeutralZoneSize { get; set; } = 1.0;
        public double GuardRandomization { get; set; } = 0.05;

        // Per-tier experimental overrides. Empty / 0 = fall through to the global value.
        public TierOverrides LowTier { get; set; } = new TierOverrides();
        public TierOverrides MediumTier { get; set; } = new TierOverrides();
        public TierOverrides HighTier { get; set; } = new TierOverrides();
    }

    /// <summary>
    /// Per-neutral-tier overrides that win over the global Terrain / BuildingPresets /
    /// GuardProgression values when set. All defaults are no-op.
    /// </summary>
    public class TierOverrides
    {
        public double ObstaclesFill { get; set; } = 0.0;
        public double LakesFill { get; set; } = 0.0;
        public string BuildingPreset { get; set; } = "";
        public double GuardWeeklyIncrement { get; set; } = 0.0;
    }
    public class ZoneConfiguration
    {
        public int NeutralZoneCount { get; set; } = 0;
        public int PlayerZoneCastles { get; set; } = 1;
        public int NeutralZoneCastles { get; set; } = 1;
        public int ResourceDensityPercent { get; set; } = 100;
        public int StructureDensityPercent { get; set; } = 100;
        public int NeutralStackStrengthPercent { get; set; } = 100;
        public int BorderGuardStrengthPercent { get; set; } = 100;
        public double HubZoneSize { get; set; } = 1.0;
        public int HubZoneCastles { get; set; } = 0;
        public AdvancedSettings Advanced { get; set; } = new AdvancedSettings();
    }

    // ── Experimental capabilities ───────────────────────────────────────────────
    // All fields default to "unset" and are no-ops in the generator output. The
    // generator only emits non-default values, so existing fixtures stay byte-
    // identical until a user changes something.

    public class TerrainSettings
    {
        /// <summary>0 = unset, otherwise overrides the per-zone obstaclesFill (0..1).</summary>
        public double ObstaclesFill { get; set; } = 0.0;
        /// <summary>0 = unset, otherwise overrides the per-zone lakesFill (0..1).</summary>
        public double LakesFill { get; set; } = 0.0;
    }

    public class BuildingPresetSettings
    {
        /// <summary>Empty = use generator default. Otherwise a value from KnownValues.BuildingsConstructionSids.</summary>
        public string PlayerZonePreset { get; set; } = "";
        public string NeutralZonePreset { get; set; } = "";
    }

    public class GuardProgressionSettings
    {
        /// <summary>0 = use generator default per zone. Otherwise the value to stamp on every zone.</summary>
        public double ZoneGuardWeeklyIncrement { get; set; } = 0.0;
        /// <summary>0 = use generator default per connection. Otherwise the value to stamp on every connection.</summary>
        public double ConnectionGuardWeeklyIncrement { get; set; } = 0.0;
    }

    public class NeutralCitySettings
    {
        /// <summary>0 = use generator default. Otherwise the chance any neutral city is guarded (0..1).</summary>
        public double GuardChance { get; set; } = 0.0;
        /// <summary>100 = unmodified. Otherwise scales the per-city guardValue.</summary>
        public int GuardValuePercent { get; set; } = 100;
    }

    public class ContentLimit
    {
        public string Sid { get; set; } = "";
        public int MaxPerPlayer { get; set; } = 1;
    }

    public class ContentControlSettings
    {
        /// <summary>SIDs the engine should ban globally.</summary>
        public List<string> GlobalBans { get; set; } = new();
        /// <summary>Extra count caps appended to the generator's defaults.</summary>
        public List<ContentLimit> ContentCountLimits { get; set; } = new();
    }

    public class StartingBonusSettings
    {
        // Bonus resources, keyed by Olden Era resource code (e.g. "gold", "wood").
        public Dictionary<string, int> Resources { get; set; } = new();

        // Hero stat bonuses. 0 = unset.
        public int HeroAttack { get; set; } = 0;
        public int HeroDefense { get; set; } = 0;
        public int HeroSpellpower { get; set; } = 0;
        public int HeroKnowledge { get; set; } = 0;
        public bool HeroStatStartHeroOnly { get; set; } = false;

        public string ItemSid { get; set; } = "";
        public bool ItemStartHeroOnly { get; set; } = false;

        public string SpellSid { get; set; } = "";
        public bool SpellStartHeroOnly { get; set; } = false;

        /// <summary>0 = unset. Otherwise a multiplier applied to starting unit counts.</summary>
        public double UnitMultiplier { get; set; } = 0.0;
        public bool UnitMultiplierStartHeroOnly { get; set; } = false;
    }

    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public int PlayerCount { get; set; } = 2;
        public int MapSize { get; set; } = 160;

        /// <summary>
        /// Optional deterministic seed. Null = non-deterministic (system random).
        /// </summary>
        public int? Seed { get; set; } = null;
        public HeroSettings HeroSettings { get; set; } = new HeroSettings();

        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public int MaxPortalConnections { get; set; } = 32;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public bool GenerateRoads { get; set; } = true;
        public bool ExperimentalBalancedZonePlacement { get; set; } = false;
        public bool MatchPlayerCastleFactions { get; set; } = false;
        public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        public MapTopology Topology { get; set; } = MapTopology.Random;
        public ZoneConfiguration ZoneCfg { get; set; } = new ZoneConfiguration();
        public int FactionLawsExpPercent { get; set; } = 100;
        public int AstrologyExpPercent { get; set; } = 100;
        public GameEndConditions GameEndConditions { get; set; } = new GameEndConditions();
        public GladiatorArenaRules GladiatorArenaRules { get; set; } = new GladiatorArenaRules();
        public TournamentRules TournamentRules { get; set; } = new TournamentRules();

        // ── Experimental ────────────────────────────────────────────────────────

        /// <summary>Bans hero hiring at taverns. Softer variant of SingleHero mode.</summary>
        public bool HeroHireBan { get; set; } = false;
        /// <summary>Override desertion victory-condition day. 0 = generator default (3).</summary>
        public int DesertionDay { get; set; } = 0;
        /// <summary>Override desertion victory-condition value. 0 = generator default (3000).</summary>
        public int DesertionValue { get; set; } = 0;

        public TerrainSettings Terrain { get; set; } = new TerrainSettings();
        public BuildingPresetSettings BuildingPresets { get; set; } = new BuildingPresetSettings();
        public GuardProgressionSettings GuardProgression { get; set; } = new GuardProgressionSettings();
        public NeutralCitySettings NeutralCities { get; set; } = new NeutralCitySettings();
        public ContentControlSettings Content { get; set; } = new ContentControlSettings();
        public StartingBonusSettings Bonuses { get; set; } = new StartingBonusSettings();
    }

    public enum NeutralZoneQuality
    {
        Low,
        Medium,
        High
    }
}
