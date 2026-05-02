using OldenEraTemplateEditor.Models;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Models
{
    /// <summary>
    /// Persisted settings file (.oetgs) — all user-configurable UI state.
    /// </summary>
    public sealed class SettingsFile
    {
        [JsonPropertyName("templateName")]      public string  TemplateName           { get; set; } = "Custom Template";
        [JsonPropertyName("mapSize")]           public int     MapSize                { get; set; } = 160;
        [JsonPropertyName("playerCount")]       public int     PlayerCount            { get; set; } = 2;
        [JsonPropertyName("neutralZoneCount")]  public int     NeutralZoneCount       { get; set; } = 0;
        [JsonPropertyName("playerCastles")]     public int     PlayerZoneCastles      { get; set; } = 1;
        [JsonPropertyName("neutralCastles")]    public int     NeutralZoneCastles     { get; set; } = 1;
        [JsonPropertyName("heroMin")]           public int     HeroCountMin           { get; set; } = 10;
        [JsonPropertyName("heroMax")]           public int     HeroCountMax           { get; set; } = 10;
        [JsonPropertyName("heroIncrement")]     public int     HeroCountIncrement     { get; set; } = 0;
        [JsonPropertyName("topology")]          public MapTopology Topology           { get; set; } = MapTopology.Random;
        [JsonPropertyName("randomPortals")]     public bool    RandomPortals          { get; set; } = false;
        [JsonPropertyName("spawnFootholds")]    public bool    SpawnRemoteFootholds   { get; set; } = true;
        [JsonPropertyName("generateRoads")]     public bool    GenerateRoads          { get; set; } = true;
        [JsonPropertyName("isolateplayers")]    public bool    NoDirectPlayerConn     { get; set; } = false;
        [JsonPropertyName("contentDensity")]    public int     ContentDensityPercent  { get; set; } = 100;
    }
}
