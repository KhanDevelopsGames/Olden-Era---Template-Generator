using System.Collections.Generic;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;

namespace OldenEra.Generator.Services;

/// <summary>
/// Translates between the persisted <see cref="SettingsFile"/> shape and the
/// in-memory <see cref="GeneratorSettings"/> shape used by the panels. This is
/// the web equivalent of MainWindow's GatherSettings/ApplySettings/BuildSettings
/// trio — there's only one in-memory model in the WASM app, so we map directly.
/// </summary>
public static class SettingsMapper
{
    /// <summary>
    /// Build a fresh <see cref="GeneratorSettings"/> from a loaded file.
    /// Returns the new settings and the reconstructed advanced/experimental flags.
    /// </summary>
    public static (GeneratorSettings Settings, bool AdvancedMode, bool ExperimentalMapSizes, bool ExperimentalEnabled) FromFile(SettingsFile s)
    {
        bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001
                                || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
        bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
        bool advanced = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;

        var settings = new GeneratorSettings
        {
            TemplateName = string.IsNullOrEmpty(s.TemplateName) ? "Custom Template" : s.TemplateName,
            GameMode = string.IsNullOrEmpty(s.GameMode) ? "Classic" : s.GameMode,
            MapSize = s.MapSize,
            PlayerCount = s.PlayerCount,
            HeroHireBan = s.HeroHireBan,
            DesertionDay = s.DesertionDay,
            DesertionValue = s.DesertionValue,
            Terrain = new TerrainSettings
            {
                ObstaclesFill = s.TerrainObstaclesFill,
                LakesFill = s.TerrainLakesFill,
            },
            BuildingPresets = new BuildingPresetSettings
            {
                PlayerZonePreset = s.BuildingPresetPlayer ?? "",
                NeutralZonePreset = s.BuildingPresetNeutral ?? "",
            },
            GuardProgression = new GuardProgressionSettings
            {
                ZoneGuardWeeklyIncrement = s.ZoneGuardWeeklyIncrement,
                ConnectionGuardWeeklyIncrement = s.ConnectionGuardWeeklyIncrement,
            },
            NeutralCities = new NeutralCitySettings
            {
                GuardChance = s.NeutralCityGuardChance,
                GuardValuePercent = s.NeutralCityGuardValuePercent <= 0 ? 100 : s.NeutralCityGuardValuePercent,
            },
            Content = new ContentControlSettings
            {
                GlobalBans = s.GlobalBans is null ? new() : new List<string>(s.GlobalBans),
                ContentCountLimits = s.ContentCountLimits is null
                    ? new()
                    : s.ContentCountLimits.ConvertAll(l => new ContentLimit { Sid = l.Sid, MaxPerPlayer = l.MaxPerPlayer }),
            },
            Bonuses = new StartingBonusSettings
            {
                Resources = s.BonusResources is null ? new() : new Dictionary<string,int>(s.BonusResources),
                HeroAttack = s.BonusHeroAttack,
                HeroDefense = s.BonusHeroDefense,
                HeroSpellpower = s.BonusHeroSpellpower,
                HeroKnowledge = s.BonusHeroKnowledge,
                HeroStatStartHeroOnly = s.BonusHeroStatStartHeroOnly,
                ItemSid = s.BonusItemSid ?? "",
                ItemStartHeroOnly = s.BonusItemStartHeroOnly,
                SpellSid = s.BonusSpellSid ?? "",
                SpellStartHeroOnly = s.BonusSpellStartHeroOnly,
                UnitMultiplier = s.BonusUnitMultiplier,
                UnitMultiplierStartHeroOnly = s.BonusUnitMultiplierStartHeroOnly,
            },
            HeroSettings = new HeroSettings
            {
                HeroCountMin = s.HeroCountMin,
                HeroCountMax = s.HeroCountMax,
                HeroCountIncrement = s.HeroCountIncrement,
                HeroBans = s.HeroBans is null ? new() : new List<string>(s.HeroBans),
                FixedStartingHeroByFaction = s.FixedStartingHeroByFaction is null
                    ? new()
                    : new Dictionary<string, string?>(s.FixedStartingHeroByFaction),
            },
            Topology = s.Topology,
            RandomPortals = s.RandomPortals,
            MaxPortalConnections = Math.Clamp(s.MaxPortalConnections, 1, 32),
            SpawnRemoteFootholds = s.SpawnRemoteFootholds,
            GenerateRoads = s.GenerateRoads,
            NoDirectPlayerConnections = s.NoDirectPlayerConn,
            MatchPlayerCastleFactions = s.MatchPlayerCastleFactions,
            MinNeutralZonesBetweenPlayers = s.MinNeutralZonesBetweenPlayers,
            ExperimentalBalancedZonePlacement = s.ExperimentalBalancedZonePlacement,
            FactionLawsExpPercent = Math.Clamp(s.FactionLawsExpPercent, 25, 200),
            AstrologyExpPercent = Math.Clamp(s.AstrologyExpPercent, 25, 200),
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = s.NeutralZoneCount,
                PlayerZoneCastles = s.PlayerZoneCastles,
                NeutralZoneCastles = s.NeutralZoneCastles,
                ResourceDensityPercent = s.EffectiveResourceDensityPercent,
                StructureDensityPercent = s.EffectiveStructureDensityPercent,
                NeutralStackStrengthPercent = s.NeutralStackStrengthPercent,
                BorderGuardStrengthPercent = s.BorderGuardStrengthPercent,
                HubZoneSize = Math.Clamp(s.HubZoneSize, 0.25, 3.0),
                HubZoneCastles = Math.Clamp(s.HubZoneCastles, 0, 4),
                Advanced = new AdvancedSettings
                {
                    Enabled = advanced,
                    NeutralLowNoCastleCount = s.NeutralLowNoCastleCount,
                    NeutralLowCastleCount = s.NeutralLowCastleCount,
                    NeutralMediumNoCastleCount = s.NeutralMediumNoCastleCount,
                    NeutralMediumCastleCount = s.NeutralMediumCastleCount,
                    NeutralHighNoCastleCount = s.NeutralHighNoCastleCount,
                    NeutralHighCastleCount = s.NeutralHighCastleCount,
                    PlayerZoneSize = Math.Clamp(s.PlayerZoneSize, 0.1, 2.0),
                    NeutralZoneSize = Math.Clamp(s.NeutralZoneSize, 0.1, 2.0),
                    GuardRandomization = s.GuardRandomization,
                    LowTier = TierFromFile(s.TierLow),
                    MediumTier = TierFromFile(s.TierMedium),
                    HighTier = TierFromFile(s.TierHigh),
                },
            },
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = string.IsNullOrEmpty(s.VictoryCondition) ? "win_condition_1" : s.VictoryCondition,
                LostStartCity = s.LostStartCity,
                LostStartCityDay = Math.Clamp(s.LostStartCityDay, 1, 30),
                LostStartHero = s.LostStartHero,
                CityHold = s.CityHold,
                CityHoldDays = Math.Clamp(s.CityHoldDays, 1, 30),
            },
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = s.GladiatorArena,
                DaysDelayStart = Math.Clamp(s.GladiatorArenaDaysDelayStart, 1, 60),
                CountDay = Math.Clamp(s.GladiatorArenaCountDay, 1, 30),
            },
            TournamentRules = new TournamentRules
            {
                Enabled = s.Tournament,
                FirstTournamentDay = Math.Clamp(s.TournamentFirstTournamentDay, 1, 60),
                Interval = Math.Clamp(s.TournamentInterval, 1, 30),
                PointsToWin = Math.Clamp(s.TournamentPointsToWin, 1, 10),
                SaveArmy = s.TournamentSaveArmy,
            },
        };

        return (settings, advanced, needsExperimentalMapSizes, s.ExperimentalEnabled);
    }

    /// <summary>
    /// Capture the current in-memory state into a <see cref="SettingsFile"/>
    /// so it can be JSON-serialized.
    /// </summary>
    public static SettingsFile ToFile(GeneratorSettings g, bool advancedMode, bool experimentalMapSizes, bool experimentalEnabled = false)
    {
        var a = g.ZoneCfg.Advanced;
        return new SettingsFile
        {
            TemplateName = g.TemplateName,
            MapSize = g.MapSize,
            PlayerCount = g.PlayerCount,
            NeutralZoneCount = g.ZoneCfg.NeutralZoneCount,
            PlayerZoneCastles = g.ZoneCfg.PlayerZoneCastles,
            NeutralZoneCastles = g.ZoneCfg.NeutralZoneCastles,
            AdvancedMode = advancedMode,
            NeutralLowNoCastleCount = a.NeutralLowNoCastleCount,
            NeutralLowCastleCount = a.NeutralLowCastleCount,
            NeutralMediumNoCastleCount = a.NeutralMediumNoCastleCount,
            NeutralMediumCastleCount = a.NeutralMediumCastleCount,
            NeutralHighNoCastleCount = a.NeutralHighNoCastleCount,
            NeutralHighCastleCount = a.NeutralHighCastleCount,
            MatchPlayerCastleFactions = g.MatchPlayerCastleFactions,
            MinNeutralZonesBetweenPlayers = g.MinNeutralZonesBetweenPlayers,
            ExperimentalBalancedZonePlacement = g.ExperimentalBalancedZonePlacement,
            ExperimentalMapSizes = experimentalMapSizes,
            PlayerZoneSize = a.PlayerZoneSize,
            NeutralZoneSize = a.NeutralZoneSize,
            HubZoneSize = g.ZoneCfg.HubZoneSize,
            HubZoneCastles = g.ZoneCfg.HubZoneCastles,
            GuardRandomization = a.GuardRandomization,
            HeroCountMin = g.HeroSettings.HeroCountMin,
            HeroCountMax = g.HeroSettings.HeroCountMax,
            HeroCountIncrement = g.HeroSettings.HeroCountIncrement,
            HeroBans = new List<string>(g.HeroSettings.HeroBans),
            FixedStartingHeroByFaction = new Dictionary<string, string?>(g.HeroSettings.FixedStartingHeroByFaction),
            Topology = g.Topology,
            RandomPortals = g.RandomPortals,
            MaxPortalConnections = g.MaxPortalConnections,
            SpawnRemoteFootholds = g.SpawnRemoteFootholds,
            GenerateRoads = g.GenerateRoads,
            NoDirectPlayerConn = g.NoDirectPlayerConnections,
            ResourceDensityPercent = g.ZoneCfg.ResourceDensityPercent,
            StructureDensityPercent = g.ZoneCfg.StructureDensityPercent,
            NeutralStackStrengthPercent = g.ZoneCfg.NeutralStackStrengthPercent,
            BorderGuardStrengthPercent = g.ZoneCfg.BorderGuardStrengthPercent,
            VictoryCondition = g.GameEndConditions.VictoryCondition,
            FactionLawsExpPercent = g.FactionLawsExpPercent,
            AstrologyExpPercent = g.AstrologyExpPercent,
            LostStartCity = g.GameEndConditions.LostStartCity,
            LostStartCityDay = g.GameEndConditions.LostStartCityDay,
            LostStartHero = g.GameEndConditions.LostStartHero,
            CityHold = g.GameEndConditions.CityHold,
            CityHoldDays = g.GameEndConditions.CityHoldDays,
            GladiatorArena = g.GladiatorArenaRules.Enabled,
            GladiatorArenaDaysDelayStart = g.GladiatorArenaRules.DaysDelayStart,
            GladiatorArenaCountDay = g.GladiatorArenaRules.CountDay,
            Tournament = g.TournamentRules.Enabled,
            TournamentFirstTournamentDay = g.TournamentRules.FirstTournamentDay,
            TournamentInterval = g.TournamentRules.Interval,
            TournamentPointsToWin = g.TournamentRules.PointsToWin,
            TournamentSaveArmy = g.TournamentRules.SaveArmy,
            ExperimentalEnabled = experimentalEnabled,
            GameMode = g.GameMode,
            HeroHireBan = g.HeroHireBan,
            DesertionDay = g.DesertionDay,
            DesertionValue = g.DesertionValue,
            TerrainObstaclesFill = g.Terrain.ObstaclesFill,
            TerrainLakesFill = g.Terrain.LakesFill,
            BuildingPresetPlayer = g.BuildingPresets.PlayerZonePreset,
            BuildingPresetNeutral = g.BuildingPresets.NeutralZonePreset,
            ZoneGuardWeeklyIncrement = g.GuardProgression.ZoneGuardWeeklyIncrement,
            ConnectionGuardWeeklyIncrement = g.GuardProgression.ConnectionGuardWeeklyIncrement,
            NeutralCityGuardChance = g.NeutralCities.GuardChance,
            NeutralCityGuardValuePercent = g.NeutralCities.GuardValuePercent,
            GlobalBans = new List<string>(g.Content.GlobalBans),
            ContentCountLimits = g.Content.ContentCountLimits.ConvertAll(
                l => new ContentLimitFile { Sid = l.Sid, MaxPerPlayer = l.MaxPerPlayer }),
            BonusResources = new Dictionary<string,int>(g.Bonuses.Resources),
            BonusHeroAttack = g.Bonuses.HeroAttack,
            BonusHeroDefense = g.Bonuses.HeroDefense,
            BonusHeroSpellpower = g.Bonuses.HeroSpellpower,
            BonusHeroKnowledge = g.Bonuses.HeroKnowledge,
            BonusHeroStatStartHeroOnly = g.Bonuses.HeroStatStartHeroOnly,
            BonusItemSid = g.Bonuses.ItemSid,
            BonusItemStartHeroOnly = g.Bonuses.ItemStartHeroOnly,
            BonusSpellSid = g.Bonuses.SpellSid,
            BonusSpellStartHeroOnly = g.Bonuses.SpellStartHeroOnly,
            BonusUnitMultiplier = g.Bonuses.UnitMultiplier,
            BonusUnitMultiplierStartHeroOnly = g.Bonuses.UnitMultiplierStartHeroOnly,
            TierLow = TierToFile(a.LowTier),
            TierMedium = TierToFile(a.MediumTier),
            TierHigh = TierToFile(a.HighTier),
        };
    }

    private static TierOverrides TierFromFile(TierOverrideFile? f) =>
        f is null ? new TierOverrides() : new TierOverrides
        {
            ObstaclesFill = f.ObstaclesFill,
            LakesFill = f.LakesFill,
            BuildingPreset = f.BuildingPreset ?? "",
            GuardWeeklyIncrement = f.GuardWeeklyIncrement,
        };

    private static TierOverrideFile TierToFile(TierOverrides t) => new()
    {
        ObstaclesFill = t.ObstaclesFill,
        LakesFill = t.LakesFill,
        BuildingPreset = t.BuildingPreset,
        GuardWeeklyIncrement = t.GuardWeeklyIncrement,
    };
}
