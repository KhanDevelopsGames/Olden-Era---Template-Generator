using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
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

        [JsonPropertyName("magics")]
        public List<string>? Magics { get; set; }
    }
}
