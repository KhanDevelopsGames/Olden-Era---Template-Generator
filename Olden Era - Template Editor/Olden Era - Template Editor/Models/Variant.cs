using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class Variant
    {
        [JsonPropertyName("orientation")]
        public Orientation? Orientation { get; set; }

        [JsonPropertyName("border")]
        public Border? Border { get; set; }

        [JsonPropertyName("zones")]
        public List<Zone>? Zones { get; set; }

        [JsonPropertyName("connections")]
        public List<Connection>? Connections { get; set; }
    }

    public class Orientation
    {
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("zeroAngleZone")]
        public string? ZeroAngleZone { get; set; }

        [JsonPropertyName("baseAngleMin")]
        public double? BaseAngleMin { get; set; }

        [JsonPropertyName("baseAngleMax")]
        public double? BaseAngleMax { get; set; }

        [JsonPropertyName("randomAngleAmplitude")]
        public double? RandomAngleAmplitude { get; set; }

        [JsonPropertyName("randomAngleStep")]
        public double? RandomAngleStep { get; set; }
    }

    public class Border
    {
        [JsonPropertyName("cornerRadius")]
        public double? CornerRadius { get; set; }

        [JsonPropertyName("obstaclesWidth")]
        public int? ObstaclesWidth { get; set; }

        [JsonPropertyName("obstaclesNoise")]
        public List<NoiseEntry>? ObstaclesNoise { get; set; }

        [JsonPropertyName("waterWidth")]
        public int? WaterWidth { get; set; }

        [JsonPropertyName("waterNoise")]
        public List<NoiseEntry>? WaterNoise { get; set; }

        [JsonPropertyName("waterType")]
        public string? WaterType { get; set; }
    }

    public class NoiseEntry
    {
        [JsonPropertyName("amp")]
        public double Amp { get; set; }

        [JsonPropertyName("freq")]
        public double Freq { get; set; }
    }
}
