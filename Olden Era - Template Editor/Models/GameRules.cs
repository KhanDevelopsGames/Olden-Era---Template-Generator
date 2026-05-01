using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class GameRules
    {
        [JsonPropertyName("heroCountMin")]
        public int? HeroCountMin { get; set; }

        [JsonPropertyName("heroCountMax")]
        public int? HeroCountMax { get; set; }

        [JsonPropertyName("heroCountIncrement")]
        public int? HeroCountIncrement { get; set; }

        [JsonPropertyName("heroHireBan")]
        public bool? HeroHireBan { get; set; }

        [JsonPropertyName("encounterHoles")]
        public bool? EncounterHoles { get; set; }

        [JsonPropertyName("tournamentRules")]
        public bool? TournamentRules { get; set; }

        [JsonPropertyName("factionLawsExpModifier")]
        public double? FactionLawsExpModifier { get; set; }

        [JsonPropertyName("astrologyExpModifier")]
        public double? AstrologyExpModifier { get; set; }

        [JsonPropertyName("bonuses")]
        public List<Bonus>? Bonuses { get; set; }

        [JsonPropertyName("winConditions")]
        public WinConditions? WinConditions { get; set; }
    }

    public class Bonus
    {
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = string.Empty;

        [JsonPropertyName("receiverSide")]
        public int? ReceiverSide { get; set; }

        [JsonPropertyName("receiverFilter")]
        public string? ReceiverFilter { get; set; }

        [JsonPropertyName("parameters")]
        public List<string>? Parameters { get; set; }
    }

    public class WinConditions
    {
        [JsonPropertyName("classic")]
        public bool? Classic { get; set; }

        [JsonPropertyName("desertion")]
        public bool? Desertion { get; set; }

        [JsonPropertyName("desertionDay")]
        public int? DesertionDay { get; set; }

        [JsonPropertyName("desertionValue")]
        public int? DesertionValue { get; set; }

        [JsonPropertyName("heroLighting")]
        public bool? HeroLighting { get; set; }

        [JsonPropertyName("heroLightingDay")]
        public int? HeroLightingDay { get; set; }

        [JsonPropertyName("lostStartCity")]
        public bool? LostStartCity { get; set; }

        [JsonPropertyName("lostStartCityDay")]
        public int? LostStartCityDay { get; set; }

        [JsonPropertyName("lostStartHero")]
        public bool? LostStartHero { get; set; }

        [JsonPropertyName("cityHold")]
        public bool? CityHold { get; set; }

        [JsonPropertyName("cityHoldDays")]
        public int? CityHoldDays { get; set; }

        [JsonPropertyName("gladiatorArena")]
        public bool? GladiatorArena { get; set; }

        [JsonPropertyName("gladiatorArenaRegistrationStartWork")]
        public bool? GladiatorArenaRegistrationStartWork { get; set; }

        [JsonPropertyName("gladiatorArenaRegistrationStartFight")]
        public bool? GladiatorArenaRegistrationStartFight { get; set; }

        [JsonPropertyName("gladiatorArenaDaysDelayStart")]
        public int? GladiatorArenaDaysDelayStart { get; set; }

        [JsonPropertyName("gladiatorArenaCountDay")]
        public int? GladiatorArenaCountDay { get; set; }

        [JsonPropertyName("championSelectRule")]
        public string? ChampionSelectRule { get; set; }
    }
}
