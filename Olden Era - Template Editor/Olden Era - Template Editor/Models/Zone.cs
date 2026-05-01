using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class Zone
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public double? Size { get; set; }

        [JsonPropertyName("layout")]
        public string? Layout { get; set; }

        [JsonPropertyName("guardCutoffValue")]
        public int? GuardCutoffValue { get; set; }

        [JsonPropertyName("guardRandomization")]
        public double? GuardRandomization { get; set; }

        [JsonPropertyName("guardMultiplier")]
        public double? GuardMultiplier { get; set; }

        [JsonPropertyName("guardWeeklyIncrement")]
        public double? GuardWeeklyIncrement { get; set; }

        [JsonPropertyName("guardReactionDistribution")]
        public List<int>? GuardReactionDistribution { get; set; }

        [JsonPropertyName("diplomacyModifier")]
        public double? DiplomacyModifier { get; set; }

        [JsonPropertyName("encounterHolesSettings")]
        public EncounterHolesSettings? EncounterHolesSettings { get; set; }

        [JsonPropertyName("guardedContentPool")]
        public List<string>? GuardedContentPool { get; set; }

        [JsonPropertyName("unguardedContentPool")]
        public List<string>? UnguardedContentPool { get; set; }

        [JsonPropertyName("resourcesContentPool")]
        public List<string>? ResourcesContentPool { get; set; }

        [JsonPropertyName("mandatoryContent")]
        public List<string>? MandatoryContent { get; set; }

        [JsonPropertyName("contentCountLimits")]
        public List<string>? ContentCountLimits { get; set; }

        [JsonPropertyName("guardedContentValue")]
        public int? GuardedContentValue { get; set; }

        [JsonPropertyName("guardedContentValuePerArea")]
        public int? GuardedContentValuePerArea { get; set; }

        [JsonPropertyName("unguardedContentValue")]
        public int? UnguardedContentValue { get; set; }

        [JsonPropertyName("unguardedContentValuePerArea")]
        public int? UnguardedContentValuePerArea { get; set; }

        [JsonPropertyName("resourcesValue")]
        public int? ResourcesValue { get; set; }

        [JsonPropertyName("resourcesValuePerArea")]
        public int? ResourcesValuePerArea { get; set; }

        [JsonPropertyName("mainObjects")]
        public List<MainObject>? MainObjects { get; set; }

        [JsonPropertyName("zoneBiome")]
        public BiomeSelector? ZoneBiome { get; set; }

        [JsonPropertyName("contentBiome")]
        public BiomeSelector? ContentBiome { get; set; }

        [JsonPropertyName("metaObjectsBiome")]
        public BiomeSelector? MetaObjectsBiome { get; set; }

        [JsonPropertyName("crossroadsPosition")]
        public int? CrossroadsPosition { get; set; }

        [JsonPropertyName("roads")]
        public List<Road>? Roads { get; set; }
    }

    public class EncounterHolesSettings
    {
        [JsonPropertyName("affectedEncounters")]
        public double? AffectedEncounters { get; set; }

        [JsonPropertyName("twoHoleEncounters")]
        public double? TwoHoleEncounters { get; set; }
    }

    public class BiomeSelector
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }
    }
}
