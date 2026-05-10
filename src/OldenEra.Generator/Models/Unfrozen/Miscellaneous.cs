using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEra.Generator.Models.Unfrozen
{
    public class ValueOverride
    {
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = string.Empty;

        [JsonPropertyName("variant")]
        public int? Variant { get; set; }

        [JsonPropertyName("guardValue")]
        public int? GuardValue { get; set; }
    }

    public class GlobalBans
    {
        [JsonPropertyName("items")]
        public List<string>? Items { get; set; }

        /// <summary>Banned hero SIDs. Matches the shape used by Arcade.rmg.json.</summary>
        [JsonPropertyName("heroes")]
        public List<string>? Heroes { get; set; }

        /// <summary>Banned neutral-magic SIDs. Schema sibling of <see cref="Items"/> / <see cref="Heroes"/>.</summary>
        [JsonPropertyName("magics")]
        public List<string>? Magics { get; set; }
    }
}
