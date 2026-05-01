using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class Connection
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("connectionType")]
        public string? ConnectionType { get; set; }

        [JsonPropertyName("guardZone")]
        public string? GuardZone { get; set; }

        [JsonPropertyName("guardEscape")]
        public bool? GuardEscape { get; set; }

        [JsonPropertyName("simTurnSquad")]
        public bool? SimTurnSquad { get; set; }

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }

        [JsonPropertyName("guardWeeklyIncrement")]
        public double? GuardWeeklyIncrement { get; set; }

        [JsonPropertyName("guardMatchGroup")]
        public string? GuardMatchGroup { get; set; }

        [JsonPropertyName("gatePlacement")]
        public string? GatePlacement { get; set; }

        [JsonPropertyName("length")]
        public double? Length { get; set; }
    }
}
