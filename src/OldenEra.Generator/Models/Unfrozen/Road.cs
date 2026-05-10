using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Models.Unfrozen
{
    public class Road
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("from")]
        public RoadEndpoint? From { get; set; }

        [JsonPropertyName("to")]
        public RoadEndpoint? To { get; set; }
    }

    public class RoadEndpoint
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }
    }
}
