namespace Olden_Era___Template_Editor.Models
{
    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public int PlayerCount { get; set; } = 2;
        public int HeroCountMin { get; set; } = 10;
        public int HeroCountMax { get; set; } = 10;
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
        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public bool GenerateRoads { get; set; } = true;
        public MapTopology Topology { get; set; } = MapTopology.Random;
        public int ResourceDensityPercent { get; set; } = 100;
        public int StructureDensityPercent { get; set; } = 100;
        public int NeutralStackStrengthPercent { get; set; } = 100;
        public int BorderGuardStrengthPercent { get; set; } = 100;
    }

    public enum NeutralZoneQuality
    {
        Low,
        Medium,
        High
    }
}
