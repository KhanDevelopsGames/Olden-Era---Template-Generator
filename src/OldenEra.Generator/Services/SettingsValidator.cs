using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Pre-generation validation shared by the WPF and Blazor hosts.
    /// </summary>
    /// <remarks>
    /// Mirrors the rules in MainWindow.Validate(). Blockers prevent generation;
    /// warnings advise the user but do not block.
    /// </remarks>
    public static class SettingsValidator
    {
        public const int DefaultMaxZones = 32;

        public sealed record Result(IReadOnlyList<string> Blockers, IReadOnlyList<string> Warnings)
        {
            public bool IsValid => Blockers.Count == 0;
        }

        public static Result Validate(GeneratorSettings settings, int? maxZonesOverride = null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var blockers = new List<string>();
            var warnings = new List<string>();

            int players = settings.PlayerCount;
            int neutral = TotalNeutralZones(settings);
            int maxZones = maxZonesOverride ?? DefaultMaxZones;

            if (settings.HeroSettings.HeroCountMin > settings.HeroSettings.HeroCountMax)
            {
                blockers.Add("Min Heroes cannot be greater than Max Heroes.");
            }

            if (players + neutral > maxZones)
            {
                blockers.Add($"Total zones (players + neutral) cannot exceed {maxZones}.");
            }

            if (string.IsNullOrWhiteSpace(settings.TemplateName))
            {
                blockers.Add("Template name cannot be empty.");
            }

            bool cityHoldActive = settings.GameEndConditions.CityHold
                || settings.GameEndConditions.VictoryCondition == "win_condition_5";
            if (cityHoldActive && settings.Topology != MapTopology.HubAndSpoke && neutral == 0)
            {
                blockers.Add("City Hold requires at least one neutral zone to place the hold city. Add a neutral zone or switch to the Hub layout.");
            }

            if (settings.NoDirectPlayerConnections && neutral == 0)
            {
                blockers.Add("\"Connect via neutral zones only\" requires at least one neutral zone. Add a neutral zone or disable this option.");
            }

            if (settings.GameEndConditions.VictoryCondition == "win_condition_6" && players != 2)
            {
                blockers.Add("Tournament mode only supports exactly 2 players.");
            }

            if (settings.TemplateName.Trim().Equals("Custom Template", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("⚠️ The template is still using the default name \"Custom Template\". Consider renaming it before saving.");
            }

            int totalZones = players + neutral;
            int totalZonesIncludingHub = settings.Topology == MapTopology.HubAndSpoke ? totalZones + 1 : totalZones;
            int mapSize = settings.MapSize;
            if (totalZonesIncludingHub > 0 && (mapSize * mapSize) / totalZonesIncludingHub < 1024)
            {
                warnings.Add("⚠️ Estimated zone size is too small. The game may freeze when loading the map. Increase the map size or reduce the number of zones.");
            }

            if (mapSize > KnownValues.MaxOfficialMapSize)
            {
                warnings.Add("Experimental map sizes above 240x240 are not confirmed by official templates; generated maps may fail, freeze, or behave unpredictably in game.");
            }

            if (totalZones > 10)
            {
                int playerCastles = settings.ZoneCfg.PlayerZoneCastles;
                int neutralCastles = settings.ZoneCfg.Advanced.Enabled ? 0 : settings.ZoneCfg.NeutralZoneCastles;
                if (playerCastles > 1 || neutralCastles > 1)
                {
                    warnings.Add("⚠️ Using more than 1 castle per zone with more than 10 total zones may cause the game to freeze when generating the map. Consider reducing the number of castles.");
                }
            }

            if (settings.ZoneCfg.Advanced.Enabled
                && settings.MinNeutralZonesBetweenPlayers > 0
                && !TemplateGenerator.CanHonorNeutralSeparation(settings, neutral))
            {
                warnings.Add("Minimum neutral separation cannot be guaranteed with the current layout, neutral zone total, or portal setting; generation will ignore that option.");
            }

            return new Result(blockers, warnings);
        }

        public static int TotalNeutralZones(GeneratorSettings settings)
        {
            if (!settings.ZoneCfg.Advanced.Enabled)
                return settings.ZoneCfg.NeutralZoneCount;

            var a = settings.ZoneCfg.Advanced;
            return a.NeutralLowNoCastleCount + a.NeutralLowCastleCount
                 + a.NeutralMediumNoCastleCount + a.NeutralMediumCastleCount
                 + a.NeutralHighNoCastleCount + a.NeutralHighCastleCount;
        }
    }
}
