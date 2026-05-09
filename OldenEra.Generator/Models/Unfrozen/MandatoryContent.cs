using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Models.Unfrozen
{
    public class MandatoryContentGroup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<ContentItem>? Content { get; set; }
    }

    public class ContentItem
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sid")]
        public string? Sid { get; set; }

        [JsonPropertyName("variant")]
        public int? Variant { get; set; }

        [JsonPropertyName("isGuarded")]
        public bool? IsGuarded { get; set; }

        [JsonPropertyName("isMine")]
        public bool? IsMine { get; set; }

        [JsonPropertyName("soloEncounter")]
        public bool? SoloEncounter { get; set; }

        [JsonPropertyName("includeLists")]
        public List<string>? IncludeLists { get; set; }

        [JsonPropertyName("rules")]
        public List<ContentPlacementRule>? Rules { get; set; }
    }

    public class ContentPlacementRule
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }

        [JsonPropertyName("targetMin")]
        public double? TargetMin { get; set; }

        [JsonPropertyName("targetMax")]
        public double? TargetMax { get; set; }

        [JsonPropertyName("weight")]
        public double? Weight { get; set; }
    }

    public class ContentCountLimit
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("playerMin")]
        public int? PlayerMin { get; set; }

        [JsonPropertyName("playerMax")]
        public int? PlayerMax { get; set; }

        [JsonPropertyName("limits")]
        public List<ContentSidLimit>? Limits { get; set; }
    }

    public class ContentSidLimit
    {
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = string.Empty;

        [JsonPropertyName("maxCount")]
        public int MaxCount { get; set; }
    }
}
