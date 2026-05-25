using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Olden_Era___Template_Editor.Models
{
    /// <summary>
    /// Lightweight serialization record for a single mandatory-content UI row.
    /// Preserves the row exactly as the user configured it, including the Count
    /// slider — so two separate sawmill rows stay as two rows after a round-trip.
    /// </summary>
    public sealed class ZoneContentRowSave
    {
        /// <summary>SID of the content item or include-list.</summary>
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = string.Empty;

        /// <summary>Spinner / Count value shown in the UI row.</summary>
        [JsonPropertyName("count")]
        public int Count { get; set; } = 1;

        /// <summary>True when the SID is an include-list group rather than a concrete item.</summary>
        [JsonPropertyName("isGroup")]
        public bool IsGroup { get; set; }

        /// <summary>Whether the content is guarded.</summary>
        /// Deprecated, left in for backward-compatibility with old saves. New settings files should use the Rules list instead.
        [JsonPropertyName("isGuarded")]
        public bool IsGuarded { get; set; }

        /// <summary>Whether the Near Castle placement rule is active.</summary>
        /// Deprecated, left in for backward-compatibility with old saves. New settings files should use the Rules list instead.
        [JsonPropertyName("nearCastle")]
        public bool NearCastle { get; set; }

        /// <summary>Road-distance label: "Any", "Next To", "Near", "Medium", "Far", "Very Far".</summary>
        /// Deprecated, left in for backward-compatibility with old saves. New settings files should use the Rules list instead.
        [JsonPropertyName("roadDistance")]
        public string RoadDistance { get; set; } = "Any";

        /// <summary>True when this row lives in the Mines collection (affects IsMine on the generated ContentItem).</summary>
        [JsonPropertyName("isMine")]
        public bool IsMine { get; set; }

        /// <summary>
        /// Serialized content rules for the row. New settings files should use this.
        /// </summary>
        [JsonPropertyName("rules")]
        public List<ContentRuleRowSave>? Rules { get; set; }
    }

    /// <summary>
    /// Lightweight, JSON-friendly representation of a single IContentRule value.
    /// </summary>
    public sealed class ContentRuleRowSave
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("distanceName")]
        public string? DistanceName { get; set; }

        [JsonPropertyName("isGuarded")]
        public bool? IsGuarded { get; set; }

        [JsonPropertyName("variantId")]
        public int? VariantId { get; set; }
    }
}
