using OldenEraTemplateEditor.Models;
namespace Olden_Era___Template_Editor.Models
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

    public class ZoneContent
    {
        public List<ContentItem> PlayerZoneMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> LowNeutralMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> MediumNeutralMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> HighNeutralMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> HubZoneMandatoryContent { get; set; } = new List<ContentItem>();
    }

    public class ZoneMainObjectOverride
    {
        public string Type { get; set; } = string.Empty;
        public string? Spawn { get; set; }
        public string? Owner { get; set; }
        public double? GuardChance { get; set; }
        public int? GuardValue { get; set; }
        public double? GuardWeeklyIncrement { get; set; }
        public bool? RemoveGuardIfHasOwner { get; set; }
        public string? BuildingsConstructionSid { get; set; }
        public TypedSelector? Faction { get; set; }
        public string? Placement { get; set; }
        public List<string>? PlacementArgs { get; set; }
        public bool? HoldCityWinCon { get; set; }
    }

    public class ZoneOverrideSettings
    {
        public string ZoneName { get; set; } = string.Empty;
        public double? Size { get; set; }
        public string? Layout { get; set; }
        public int? GuardCutoffValue { get; set; }
        public double? GuardRandomization { get; set; }
        public double? GuardMultiplier { get; set; }
        public double? GuardWeeklyIncrement { get; set; }
        public List<int>? GuardReactionDistribution { get; set; }
        public double? DiplomacyModifier { get; set; }
        public List<string>? GuardedContentPool { get; set; }
        public List<string>? UnguardedContentPool { get; set; }
        public List<string>? ResourcesContentPool { get; set; }
        public List<string>? MandatoryContent { get; set; }
        public List<string>? ContentCountLimits { get; set; }
        public int? GuardedContentValue { get; set; }
        public int? GuardedContentValuePerArea { get; set; }
        public int? UnguardedContentValue { get; set; }
        public int? UnguardedContentValuePerArea { get; set; }
        public int? ResourcesValue { get; set; }
        public int? ResourcesValuePerArea { get; set; }
        public List<ZoneMainObjectOverride>? MainObjects { get; set; }
    }

    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public bool SingleHeroMode { get; set; } = false;
        public int PlayerCount { get; set; } = 2;
        public int MapSize { get; set; } = 160;
        public HeroSettings HeroSettings { get; set; } = new HeroSettings();
        
        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public int MaxPortalConnections { get; set; } = 32;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public bool GenerateRoads { get; set; } = true;
        public bool MatchPlayerCastleFactions { get; set; } = false;
        public bool PlayerStartsWithCastles { get; set; } = false;
        public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        public MapTopology Topology { get; set; } = MapTopology.Balanced;
        public ZoneConfiguration ZoneCfg { get; set; } = new ZoneConfiguration();
        public int FactionLawsExpPercent { get; set; } = 100;
        public int AstrologyExpPercent { get; set; } = 100;
        public string BannedItems { get; set; } = "";
        public string BannedMagics { get; set; } = "";
        public string ValueOverridesText { get; set; } = "";
        public System.Collections.Generic.List<OldenEraTemplateEditor.Models.BonusEntry> Bonuses { get; set; } = new List<OldenEraTemplateEditor.Models.BonusEntry>();
        public ZoneContent ZoneContent { get; set; } = new ZoneContent();
        public List<ZoneOverrideSettings> ZoneOverrides { get; set; } = new List<ZoneOverrideSettings>();
        public GameEndConditions GameEndConditions { get; set; } = new GameEndConditions();
        public GladiatorArenaRules GladiatorArenaRules { get; set; } = new GladiatorArenaRules();
        public TournamentRules TournamentRules { get; set; } = new TournamentRules();
    }

    public enum NeutralZoneQuality
    {
        Low,
        Medium,
        High
    }
}
