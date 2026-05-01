namespace Olden_Era___Template_Editor.Models
{
    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public int PlayerCount { get; set; } = 2;
        public int HeroCountMin { get; set; } = 5;
        public int HeroCountMax { get; set; } = 10;
        public int NeutralZoneCount { get; set; } = 0;
        public int MapSize { get; set; } = 160;
        public string VictoryCondition { get; set; } = "win_condition_5";
    }
}
