using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Models
{
    public sealed class ContentLimitFile
    {
        [JsonPropertyName("sid")] public string Sid { get; set; } = "";
        [JsonPropertyName("maxPerPlayer")] public int MaxPerPlayer { get; set; } = 1;
    }

    public sealed class TierOverrideFile
    {
        [JsonPropertyName("obstaclesFill")] public double ObstaclesFill { get; set; } = 0.0;
        [JsonPropertyName("lakesFill")] public double LakesFill { get; set; } = 0.0;
        [JsonPropertyName("buildingPreset")] public string BuildingPreset { get; set; } = "";
        [JsonPropertyName("guardWeeklyIncrement")] public double GuardWeeklyIncrement { get; set; } = 0.0;
    }

    /// <summary>
    /// Persisted settings file (.oetgs) — all user-configurable UI state.
    /// </summary>
    public sealed class SettingsFile
    {
        [JsonPropertyName("templateName")]      public string  TemplateName           { get; set; } = "Custom Template";
        [JsonPropertyName("seed")]              public int?    Seed                   { get; set; } = null;
        [JsonPropertyName("mapSize")]           public int     MapSize                { get; set; } = 160;
        [JsonPropertyName("playerCount")]       public int     PlayerCount            { get; set; } = 2;
        [JsonPropertyName("neutralZoneCount")]  public int     NeutralZoneCount       { get; set; } = 0;
        [JsonPropertyName("playerCastles")]     public int     PlayerZoneCastles      { get; set; } = 1;
        [JsonPropertyName("neutralCastles")]    public int     NeutralZoneCastles     { get; set; } = 1;
        [JsonPropertyName("advancedMode")]      public bool    AdvancedMode           { get; set; } = false;
        [JsonPropertyName("neutralLowNoCastle")]    public int NeutralLowNoCastleCount    { get; set; } = 0;
        [JsonPropertyName("neutralLowCastle")]      public int NeutralLowCastleCount      { get; set; } = 0;
        [JsonPropertyName("neutralMediumNoCastle")] public int NeutralMediumNoCastleCount { get; set; } = 0;
        [JsonPropertyName("neutralMediumCastle")]   public int NeutralMediumCastleCount   { get; set; } = 0;
        [JsonPropertyName("neutralHighNoCastle")]   public int NeutralHighNoCastleCount   { get; set; } = 0;
        [JsonPropertyName("neutralHighCastle")]     public int NeutralHighCastleCount     { get; set; } = 0;
        [JsonPropertyName("matchPlayerCastleFactions")] public bool MatchPlayerCastleFactions { get; set; } = false;
        [JsonPropertyName("minNeutralZonesBetweenPlayers")] public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        [JsonPropertyName("experimentalBalancedZonePlacement")] public bool ExperimentalBalancedZonePlacement { get; set; } = false;
        [JsonPropertyName("experimentalMapSizes")] public bool ExperimentalMapSizes { get; set; } = false;
        [JsonPropertyName("playerZoneSize")]  public double  PlayerZoneSize       { get; set; } = 1.0;
        [JsonPropertyName("neutralZoneSize")] public double  NeutralZoneSize      { get; set; } = 1.0;
        [JsonPropertyName("hubZoneSize")]     public double  HubZoneSize          { get; set; } = 1.0;
        [JsonPropertyName("hubCastles")]      public int     HubZoneCastles       { get; set; } = 0;
        [JsonPropertyName("guardRandomization")] public double GuardRandomization { get; set; } = 0.05;
        [JsonPropertyName("heroMin")]           public int     HeroCountMin           { get; set; } = 4;
        [JsonPropertyName("heroMax")]           public int     HeroCountMax           { get; set; } = 8;
        [JsonPropertyName("heroIncrement")]     public int     HeroCountIncrement     { get; set; } = 1;
        [JsonPropertyName("heroBans")]          public List<string> HeroBans          { get; set; } = new();
        [JsonPropertyName("fixedStartingHeroByFaction")] public Dictionary<string, string?> FixedStartingHeroByFaction { get; set; } = new();
        [JsonPropertyName("topology")]          public MapTopology Topology           { get; set; } = MapTopology.Random;
        [JsonPropertyName("randomPortals")]     public bool    RandomPortals          { get; set; } = false;
        [JsonPropertyName("maxPortalConns")]    public int     MaxPortalConnections   { get; set; } = 32;
        [JsonPropertyName("spawnFootholds")]    public bool    SpawnRemoteFootholds   { get; set; } = true;
        [JsonPropertyName("generateRoads")]     public bool    GenerateRoads          { get; set; } = true;
        [JsonPropertyName("isolateplayers")]    public bool    NoDirectPlayerConn     { get; set; } = false;
        [JsonPropertyName("resourceDensity")]   public int?    ResourceDensityPercent       { get; set; }
        [JsonPropertyName("structureDensity")]  public int?    StructureDensityPercent      { get; set; }
        [JsonPropertyName("neutralStackStrength")] public int  NeutralStackStrengthPercent  { get; set; } = 100;
        [JsonPropertyName("borderGuardStrength")]  public int  BorderGuardStrengthPercent   { get; set; } = 100;
        [JsonPropertyName("victoryCondition")]  public string  VictoryCondition             { get; set; } = "win_condition_1";
        [JsonPropertyName("factionLawsExp")]    public int     FactionLawsExpPercent        { get; set; } = 100;
        [JsonPropertyName("astrologyExp")]      public int     AstrologyExpPercent          { get; set; } = 100;
        [JsonPropertyName("lostStartCity")]     public bool    LostStartCity                { get; set; } = false;
        [JsonPropertyName("lostStartCityDay")]  public int     LostStartCityDay             { get; set; } = 3;
        [JsonPropertyName("lostStartHero")]     public bool    LostStartHero                { get; set; } = false;
        [JsonPropertyName("cityHold")]          public bool    CityHold                     { get; set; } = false;
        [JsonPropertyName("cityHoldDays")]      public int     CityHoldDays                 { get; set; } = 6;
        [JsonPropertyName("gladiatorArena")]    public bool    GladiatorArena               { get; set; } = false;
        [JsonPropertyName("gladiatorArenaDaysDelayStart")] public int GladiatorArenaDaysDelayStart { get; set; } = 30;
        [JsonPropertyName("gladiatorArenaCountDay")] public int GladiatorArenaCountDay       { get; set; } = 3;
        [JsonPropertyName("tournament")]        public bool    Tournament                   { get; set; } = false;
        [JsonPropertyName("tournamentFirstTournamentDay")] public int TournamentFirstTournamentDay { get; set; } = 14;
        [JsonPropertyName("tournamentInterval")] public int TournamentInterval    { get; set; } = 7;
        [JsonPropertyName("tournamentPointsToWin")] public int TournamentPointsToWin        { get; set; } = 2;
        [JsonPropertyName("tournamentSaveArmy")] public bool TournamentSaveArmy             { get; set; } = true;

        // ── Experimental ────────────────────────────────────────────────────────
        /// <summary>Master toggle. When false, the UI hides every experimental control.</summary>
        [JsonPropertyName("experimentalEnabled")] public bool  ExperimentalEnabled          { get; set; } = false;
        [JsonPropertyName("gameMode")]          public string  GameMode                     { get; set; } = "Classic";
        [JsonPropertyName("heroHireBan")]       public bool    HeroHireBan                  { get; set; } = false;
        [JsonPropertyName("desertionDay")]      public int     DesertionDay                 { get; set; } = 0;
        [JsonPropertyName("desertionValue")]    public int     DesertionValue               { get; set; } = 0;

        [JsonPropertyName("terrainObstaclesFill")] public double TerrainObstaclesFill       { get; set; } = 0.0;
        [JsonPropertyName("terrainLakesFill")]     public double TerrainLakesFill           { get; set; } = 0.0;

        [JsonPropertyName("buildingPresetPlayer")]  public string BuildingPresetPlayer      { get; set; } = "";
        [JsonPropertyName("buildingPresetNeutral")] public string BuildingPresetNeutral     { get; set; } = "";

        [JsonPropertyName("zoneGuardWeeklyIncrement")]       public double ZoneGuardWeeklyIncrement       { get; set; } = 0.0;
        [JsonPropertyName("connectionGuardWeeklyIncrement")] public double ConnectionGuardWeeklyIncrement { get; set; } = 0.0;

        [JsonPropertyName("neutralCityGuardChance")]        public double NeutralCityGuardChance        { get; set; } = 0.0;
        [JsonPropertyName("neutralCityGuardValuePercent")]  public int    NeutralCityGuardValuePercent  { get; set; } = 100;

        [JsonPropertyName("globalBans")]            public List<string> GlobalBans                = new();
        [JsonPropertyName("contentCountLimits")]    public List<ContentLimitFile> ContentCountLimits = new();

        [JsonPropertyName("bonusResources")]        public Dictionary<string,int> BonusResources = new();
        [JsonPropertyName("bonusHeroAttack")]       public int BonusHeroAttack       { get; set; } = 0;
        [JsonPropertyName("bonusHeroDefense")]      public int BonusHeroDefense      { get; set; } = 0;
        [JsonPropertyName("bonusHeroSpellpower")]   public int BonusHeroSpellpower   { get; set; } = 0;
        [JsonPropertyName("bonusHeroKnowledge")]    public int BonusHeroKnowledge    { get; set; } = 0;
        [JsonPropertyName("bonusHeroStatStartHeroOnly")] public bool BonusHeroStatStartHeroOnly { get; set; } = false;
        [JsonPropertyName("bonusItemSid")]          public string BonusItemSid       { get; set; } = "";
        [JsonPropertyName("bonusItemStartHeroOnly")] public bool BonusItemStartHeroOnly { get; set; } = false;
        [JsonPropertyName("bonusSpellSid")]         public string BonusSpellSid      { get; set; } = "";
        [JsonPropertyName("bonusSpellStartHeroOnly")] public bool BonusSpellStartHeroOnly { get; set; } = false;
        [JsonPropertyName("bonusUnitMultiplier")]   public double BonusUnitMultiplier { get; set; } = 0.0;
        [JsonPropertyName("bonusUnitMultiplierStartHeroOnly")] public bool BonusUnitMultiplierStartHeroOnly { get; set; } = false;

        [JsonPropertyName("tierLow")]    public TierOverrideFile TierLow    { get; set; } = new();
        [JsonPropertyName("tierMedium")] public TierOverrideFile TierMedium { get; set; } = new();
        [JsonPropertyName("tierHigh")]   public TierOverrideFile TierHigh   { get; set; } = new();

        // Legacy setting from v0.2 and earlier; when present, it seeds both split density sliders.
        [JsonPropertyName("contentDensity")]    public int?    ContentDensityPercent        { get; set; }

        [JsonIgnore] public int EffectiveResourceDensityPercent  => ResourceDensityPercent  ?? ContentDensityPercent ?? 100;
        [JsonIgnore] public int EffectiveStructureDensityPercent => StructureDensityPercent ?? ContentDensityPercent ?? 100;
    }
}
