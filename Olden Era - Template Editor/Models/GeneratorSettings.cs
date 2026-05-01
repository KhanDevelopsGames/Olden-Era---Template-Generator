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
        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public MapTopology Topology { get; set; } = MapTopology.Random;
    }
}
