using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Models
{
    public sealed class VisualTemplateDocument
    {
        public const int CurrentVersion = 1;

        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentVersion;

        [JsonPropertyName("templateName")]
        public string TemplateName { get; set; } = "Visual Template";

        [JsonPropertyName("description")]
        public string? Description { get; set; } = "Created with the Olden Era visual template designer.";

        [JsonPropertyName("gameMode")]
        public string GameMode { get; set; } = "Classic";

        [JsonPropertyName("mapSize")]
        public int MapSize { get; set; } = 160;

        [JsonPropertyName("victoryCondition")]
        public string VictoryCondition { get; set; } = "win_condition_1";

        [JsonPropertyName("heroMin")]
        public int HeroCountMin { get; set; } = 4;

        [JsonPropertyName("heroMax")]
        public int HeroCountMax { get; set; } = 8;

        [JsonPropertyName("heroIncrement")]
        public int HeroCountIncrement { get; set; } = 1;

        [JsonPropertyName("factionLawsExp")]
        public int FactionLawsExpPercent { get; set; } = 100;

        [JsonPropertyName("astrologyExp")]
        public int AstrologyExpPercent { get; set; } = 100;

        [JsonPropertyName("borderGuardStrength")]
        public int BorderGuardStrengthPercent { get; set; } = 100;

        [JsonPropertyName("lostStartCity")]
        public bool LostStartCity { get; set; }

        [JsonPropertyName("lostStartCityDay")]
        public int LostStartCityDay { get; set; } = 3;

        [JsonPropertyName("lostStartHero")]
        public bool LostStartHero { get; set; }

        [JsonPropertyName("cityHold")]
        public bool CityHold { get; set; }

        [JsonPropertyName("cityHoldDays")]
        public int CityHoldDays { get; set; } = 6;

        [JsonPropertyName("gladiatorArena")]
        public bool GladiatorArena { get; set; }

        [JsonPropertyName("gladiatorArenaDaysDelayStart")]
        public int GladiatorArenaDaysDelayStart { get; set; } = 30;

        [JsonPropertyName("gladiatorArenaCountDay")]
        public int GladiatorArenaCountDay { get; set; } = 3;

        [JsonPropertyName("tournament")]
        public bool Tournament { get; set; }

        [JsonPropertyName("tournamentFirstTournamentDay")]
        public int TournamentFirstTournamentDay { get; set; } = 8;

        [JsonPropertyName("tournamentAnnouncementLeadDays")]
        public int TournamentAnnouncementLeadDays { get; set; } = 3;

        [JsonPropertyName("tournamentInterval")]
        public int TournamentInterval { get; set; } = 7;

        [JsonPropertyName("tournamentPointsToWin")]
        public int TournamentPointsToWin { get; set; } = 2;

        [JsonPropertyName("generateRoads")]
        public bool GenerateRoads { get; set; } = true;

        [JsonPropertyName("spawnRemoteFootholds")]
        public bool SpawnRemoteFootholds { get; set; } = true;

        [JsonPropertyName("zones")]
        public List<VisualZone> Zones { get; set; } = [];

        [JsonPropertyName("connections")]
        public List<VisualConnection> Connections { get; set; } = [];
    }

    public sealed class VisualZone
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("exportLetter")]
        public string ExportLetter { get; set; } = string.Empty;

        [JsonPropertyName("zoneType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VisualZoneType ZoneType { get; set; } = VisualZoneType.NeutralMedium;

        [JsonPropertyName("playerSlot")]
        public int? PlayerSlot { get; set; }

        [JsonPropertyName("canvasX")]
        public double CanvasX { get; set; } = 350;

        [JsonPropertyName("canvasY")]
        public double CanvasY { get; set; } = 350;

        [JsonPropertyName("zoneSize")]
        public double ZoneSize { get; set; } = 1.0;

        [JsonPropertyName("guardRandomization")]
        public double GuardRandomization { get; set; } = 0.05;

        [JsonPropertyName("neutralStackStrength")]
        public int NeutralStackStrengthPercent { get; set; } = 100;

        [JsonPropertyName("resourceDensity")]
        public int ResourceDensityPercent { get; set; } = 100;

        [JsonPropertyName("structureDensity")]
        public int StructureDensityPercent { get; set; } = 100;

        [JsonPropertyName("castleCount")]
        public int CastleCount { get; set; } = 1;

        [JsonPropertyName("generateRoads")]
        public bool GenerateRoads { get; set; } = true;

        [JsonPropertyName("spawnRemoteFoothold")]
        public bool SpawnRemoteFoothold { get; set; } = true;

        [JsonPropertyName("castles")]
        public List<VisualCastle> Castles { get; set; } = [new()];
    }

    public sealed class VisualCastle
    {
        [JsonPropertyName("factionMode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VisualCastleFactionMode FactionMode { get; set; } = VisualCastleFactionMode.Unrestricted;

        [JsonPropertyName("allowedFactions")]
        public List<string> AllowedFactions { get; set; } = [];

        [JsonPropertyName("matchPlayerSlot")]
        public int? MatchPlayerSlot { get; set; }
    }

    public sealed class VisualConnection
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("fromZoneId")]
        public string FromZoneId { get; set; } = string.Empty;

        [JsonPropertyName("toZoneId")]
        public string ToZoneId { get; set; } = string.Empty;

        [JsonPropertyName("connectionType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VisualConnectionType ConnectionType { get; set; } = VisualConnectionType.Direct;

        [JsonPropertyName("guardStrength")]
        public int GuardStrengthPercent { get; set; } = 100;
    }

    public enum VisualZoneType
    {
        PlayerSpawn,
        NeutralLow,
        NeutralMedium,
        NeutralHigh
    }

    public enum VisualConnectionType
    {
        Direct,
        Portal
    }

    public enum VisualCastleFactionMode
    {
        Unrestricted,
        Restricted,
        MatchPlayer
    }
}
