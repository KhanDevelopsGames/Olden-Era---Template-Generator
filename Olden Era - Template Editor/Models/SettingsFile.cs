using OldenEraTemplateEditor.Models;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Models
{
    /// <summary>
    /// Persisted settings file (.oetgs) — all user-configurable UI state.
    /// </summary>
    public sealed class SettingsFile
    {
        [JsonPropertyName("templateName")]      public string  TemplateName           { get; set; } = "Custom Template";
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
        [JsonPropertyName("guardRandomization")] public double GuardRandomization { get; set; } = 0.05;
        [JsonPropertyName("heroMin")]           public int     HeroCountMin           { get; set; } = 10;
        [JsonPropertyName("heroMax")]           public int     HeroCountMax           { get; set; } = 10;
        [JsonPropertyName("heroIncrement")]     public int     HeroCountIncrement     { get; set; } = 0;
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
        [JsonPropertyName("tournamentFirstTournamentDay")] public int TournamentFirstTournamentDay { get; set; } = 8;
        [JsonPropertyName("tournamentAnnouncementLeadDays")] public int TournamentAnnouncementLeadDays    { get; set; } = 3;
        [JsonPropertyName("tournamentInterval")] public int TournamentInterval    { get; set; } = 7;
        [JsonPropertyName("tournamentPointsToWin")] public int TournamentPointsToWin        { get; set; } = 2;

        // Legacy setting from v0.2 and earlier; when present, it seeds both split density sliders.
        [JsonPropertyName("contentDensity")]    public int?    ContentDensityPercent        { get; set; }

        [JsonIgnore] public int EffectiveResourceDensityPercent  => ResourceDensityPercent  ?? ContentDensityPercent ?? 100;
        [JsonIgnore] public int EffectiveStructureDensityPercent => StructureDensityPercent ?? ContentDensityPercent ?? 100;
    }
}
