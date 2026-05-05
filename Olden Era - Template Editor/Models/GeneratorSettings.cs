namespace Olden_Era___Template_Editor.Models
{
    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public int PlayerCount { get; set; } = 2;
        public int HeroCountMin { get; set; } = 4;
        public int HeroCountMax { get; set; } = 8;
        public int HeroCountIncrement { get; set; } = 1;
        public int NeutralZoneCount { get; set; } = 0;
        public int MapSize { get; set; } = 160;
        public string VictoryCondition { get; set; } = "win_condition_1";
        public int PlayerZoneCastles { get; set; } = 1;
        public int NeutralZoneCastles { get; set; } = 1;
        public bool AdvancedMode { get; set; } = false;
        public int NeutralLowNoCastleCount { get; set; } = 0;
        public int NeutralLowCastleCount { get; set; } = 0;
        public int NeutralMediumNoCastleCount { get; set; } = 0;
        public int NeutralMediumCastleCount { get; set; } = 0;
        public int NeutralHighNoCastleCount { get; set; } = 0;
        public int NeutralHighCastleCount { get; set; } = 0;
        public bool MatchPlayerCastleFactions { get; set; } = false;
        public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        public bool ExperimentalBalancedZonePlacement { get; set; } = false;
        public double PlayerZoneSize { get; set; } = 1.0;
        public double NeutralZoneSize { get; set; } = 1.0;
        public double HubZoneSize { get; set; } = 1.0;
        public double GuardRandomization { get; set; } = 0.05;
        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public int MaxPortalConnections { get; set; } = 32;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public bool GenerateRoads { get; set; } = true;
        public MapTopology Topology { get; set; } = MapTopology.Random;
        public int ResourceDensityPercent { get; set; } = 100;
        public int StructureDensityPercent { get; set; } = 100;
        public int NeutralStackStrengthPercent { get; set; } = 100;
        public int BorderGuardStrengthPercent { get; set; } = 100;
        public int FactionLawsExpPercent { get; set; } = 100;
        public int AstrologyExpPercent { get; set; } = 100;
        public bool LostStartCity { get; set; } = false;
        public int LostStartCityDay { get; set; } = 3;
        public bool LostStartHero { get; set; } = false;
        public bool CityHold { get; set; } = false;
        public int CityHoldDays { get; set; } = 6;
        public bool GladiatorArena { get; set; } = false;
        public int GladiatorArenaDaysDelayStart { get; set; } = 30;
        public int GladiatorArenaCountDay { get; set; } = 3;
        public bool Tournament { get; set; } = false;
        public int TournamentFirstTournamentDay { get; set; } = 8;
        public int TournamentAnnouncementLeadDays { get; set; } = 3;
        public int TournamentInterval { get; set; } = 7;
        public int TournamentPointsToWin { get; set; } = 2;
    }

    public enum NeutralZoneQuality
    {
        Low,
        Medium,
        High
    }
}
