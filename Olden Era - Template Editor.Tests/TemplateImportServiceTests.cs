using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateImportServiceTests
{
    [Fact]
    public void ImportToSettings_FromGeneratedChainTemplate_RecoversCoreEditableFields()
    {
        var source = new GeneratorSettings
        {
            TemplateName = "Import Roundtrip Chain",
            GameMode = "SingleHero",
            PlayerCount = 4,
            MapSize = 192,
            Topology = MapTopology.Chain,
            NoDirectPlayerConnections = true,
            RandomPortals = true,
            MaxPortalConnections = 3,
            SpawnRemoteFootholds = false,
            GenerateRoads = false,
            MatchPlayerCastleFactions = true,
            HeroSettings = new HeroSettings
            {
                HeroCountMin = 8,
                HeroCountMax = 11,
                HeroCountIncrement = 2
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 3,
                PlayerZoneCastles = 2,
                NeutralZoneCastles = 1,
                ResourceDensityPercent = 150,
                StructureDensityPercent = 75,
                NeutralStackStrengthPercent = 120,
                BorderGuardStrengthPercent = 80,
            },
            FactionLawsExpPercent = 125,
            AstrologyExpPercent = 90,
            BannedItems = "item_a\nitem_b",
            BannedMagics = "spell_a",
            ValueOverridesText = "sid_x=111\nsid_y=222",
            Bonuses =
            [
                new BonusEntry
                {
                    PresetType = BonusPresetType.Spell,
                    ReceiverFilter = "start_hero",
                    Param = "neutral_magic_haste",
                    Param2 = "1"
                },
                new BonusEntry
                {
                    PresetType = BonusPresetType.StartingGold,
                    ReceiverFilter = "start_hero",
                    Param = "1500",
                    Param2 = "0"
                }
            ],
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_5",
                CityHold = true,
                CityHoldDays = 9,
                LostStartCity = true,
                LostStartCityDay = 6,
                LostStartHero = true,
            }
        };

        RmgTemplate generated = TemplateGenerator.Generate(source);

        SettingsFile imported = TemplateImportService.ImportToSettings(generated);

        Assert.Equal(source.TemplateName, imported.TemplateName);
        Assert.Equal(source.MapSize, imported.MapSize);
        Assert.Equal(source.PlayerCount, imported.PlayerCount);
        Assert.Equal(MapTopology.Chain, imported.Topology);
        Assert.Equal(source.ZoneCfg.NeutralZoneCount, imported.NeutralZoneCount);
        Assert.Equal(source.ZoneCfg.PlayerZoneCastles, imported.PlayerZoneCastles);
        Assert.Equal(source.ZoneCfg.NeutralZoneCastles, imported.NeutralZoneCastles);

        Assert.Equal(source.HeroSettings.HeroCountMin, imported.HeroCountMin);
        Assert.Equal(source.HeroSettings.HeroCountMax, imported.HeroCountMax);
        Assert.Equal(source.HeroSettings.HeroCountIncrement, imported.HeroCountIncrement);

        Assert.True(imported.RandomPortals);
        Assert.Equal(3, imported.MaxPortalConnections);
        Assert.True(imported.NoDirectPlayerConn);
        Assert.False(imported.SpawnRemoteFootholds);
        Assert.False(imported.GenerateRoads);
        Assert.True(imported.MatchPlayerCastleFactions);

        Assert.Equal(source.FactionLawsExpPercent, imported.FactionLawsExpPercent);
        Assert.Equal(source.AstrologyExpPercent, imported.AstrologyExpPercent);
        Assert.Equal(source.BannedItems, imported.BannedItems);
        Assert.Equal(source.BannedMagics, imported.BannedMagics);
        Assert.Equal(source.ValueOverridesText, imported.ValueOverridesText);

        Assert.Equal("win_condition_5", imported.VictoryCondition);
        Assert.True(imported.CityHold);
        Assert.Equal(source.GameEndConditions.CityHoldDays, imported.CityHoldDays);
        Assert.True(imported.LostStartCity);
        Assert.Equal(source.GameEndConditions.LostStartCityDay, imported.LostStartCityDay);
        Assert.True(imported.LostStartHero);

        Assert.Equal(source.ZoneCfg.ResourceDensityPercent, imported.ResourceDensityPercent);
        Assert.Equal(source.ZoneCfg.StructureDensityPercent, imported.StructureDensityPercent);
        Assert.Equal(source.ZoneCfg.NeutralStackStrengthPercent, imported.NeutralStackStrengthPercent);
        Assert.Equal(source.ZoneCfg.BorderGuardStrengthPercent, imported.BorderGuardStrengthPercent);

        Assert.Contains(((int)BonusPresetType.Spell).ToString(), imported.BonusesJson);
        Assert.Contains(((int)BonusPresetType.StartingGold).ToString(), imported.BonusesJson);
    }

    [Fact]
    public void ImportToSettings_FromGeneratedTournamentTemplate_RecoversTournamentSettings()
    {
        var source = new GeneratorSettings
        {
            TemplateName = "Import Roundtrip Tournament",
            PlayerCount = 2,
            Topology = MapTopology.Default,
            TournamentRules = new TournamentRules
            {
                Enabled = true,
                FirstTournamentDay = 16,
                Interval = 6,
                PointsToWin = 3,
                SaveArmy = true,
            },
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_6"
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 4,
                NeutralZoneCastles = 1,
            }
        };

        RmgTemplate generated = TemplateGenerator.Generate(source);

        SettingsFile imported = TemplateImportService.ImportToSettings(generated);

        Assert.True(imported.Tournament);
        Assert.Equal(source.TournamentRules.PointsToWin, imported.TournamentPointsToWin);
        Assert.Equal(source.TournamentRules.FirstTournamentDay, imported.TournamentFirstTournamentDay);
        Assert.Equal(source.TournamentRules.Interval, imported.TournamentInterval);
        Assert.True(imported.TournamentSaveArmy);

        Assert.Equal("win_condition_6", imported.VictoryCondition);
        Assert.Equal(MapTopology.Default, imported.Topology);
    }

    [Fact]
    public void ImportToSettings_FromGameNativeSingleHeroTemplate_HeroCountMinIsOne()
    {
        // Game-native templates store heroCountMin as the actual value (not uiMin - increment).
        // A single-hero template has heroCountMin=1, heroCountMax=1, heroCountIncrement=1.
        // The importer must not return heroCountMin=2 by blindly adding the increment.
        var template = new RmgTemplate
        {
            GameRules = new OldenEraTemplateEditor.Models.GameRules
            {
                HeroCountMin = 1,
                HeroCountMax = 1,
                HeroCountIncrement = 1
            }
        };

        SettingsFile imported = TemplateImportService.ImportToSettings(template);

        Assert.Equal(1, imported.HeroCountMin);
        Assert.Equal(1, imported.HeroCountMax);
    }
}
