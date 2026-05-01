using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models
{
    public class ZoneLayout
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("obstaclesFill")]
        public double? ObstaclesFill { get; set; }

        [JsonPropertyName("obstaclesFillVoid")]
        public double? ObstaclesFillVoid { get; set; }

        [JsonPropertyName("lakesFill")]
        public double? LakesFill { get; set; }

        [JsonPropertyName("minLakeArea")]
        public int? MinLakeArea { get; set; }

        [JsonPropertyName("elevationClusterScale")]
        public double? ElevationClusterScale { get; set; }

        [JsonPropertyName("elevationModes")]
        public List<ElevationMode>? ElevationModes { get; set; }

        [JsonPropertyName("roadClusterArea")]
        public int? RoadClusterArea { get; set; }

        [JsonPropertyName("guardedEncounterResourceFractions")]
        public GuardedEncounterResourceFractions? GuardedEncounterResourceFractions { get; set; }

        [JsonPropertyName("ambientPickupDistribution")]
        public AmbientPickupDistribution? AmbientPickupDistribution { get; set; }
    }

    public class ElevationMode
    {
        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("minElevatedFraction")]
        public double MinElevatedFraction { get; set; }

        [JsonPropertyName("maxElevatedFraction")]
        public double MaxElevatedFraction { get; set; }
    }

    public class GuardedEncounterResourceFractions
    {
        [JsonPropertyName("countBounds")]
        public List<double>? CountBounds { get; set; }

        [JsonPropertyName("fractions")]
        public List<double>? Fractions { get; set; }
    }

    public class AmbientPickupDistribution
    {
        [JsonPropertyName("repulsion")]
        public double? Repulsion { get; set; }

        [JsonPropertyName("noise")]
        public double? Noise { get; set; }

        [JsonPropertyName("roadAttraction")]
        public double? RoadAttraction { get; set; }

        [JsonPropertyName("obstacleAttraction")]
        public double? ObstacleAttraction { get; set; }

        [JsonPropertyName("groupSizeWeights")]
        public List<int>? GroupSizeWeights { get; set; }
    }
}
