using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class SettingsValidatorTests
{
    private static GeneratorSettings ValidBaseline() => new()
    {
        TemplateName = "My Template",
        PlayerCount = 4,
        MapSize = 160,
        Topology = MapTopology.Default,
        ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 4 },
    };

    [Fact]
    public void Baseline_HasNoBlockersOrWarnings()
    {
        var result = SettingsValidator.Validate(ValidBaseline());
        Assert.True(result.IsValid);
        Assert.Empty(result.Blockers);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Blocker_WhenHeroMinExceedsMax()
    {
        var s = ValidBaseline();
        s.HeroSettings.HeroCountMin = 9;
        s.HeroSettings.HeroCountMax = 8;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Blockers, b => b.Contains("Min Heroes"));
    }

    [Fact]
    public void Blocker_WhenTotalZonesExceedsMax()
    {
        var s = ValidBaseline();
        s.PlayerCount = 8;
        s.ZoneCfg.NeutralZoneCount = 30;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Blockers, b => b.Contains("cannot exceed"));
    }

    [Fact]
    public void Blocker_WhenTemplateNameEmpty()
    {
        var s = ValidBaseline();
        s.TemplateName = "   ";
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Blockers, b => b.Contains("Template name"));
    }

    [Fact]
    public void Blocker_CityHold_WithoutNeutrals_NotHubAndSpoke()
    {
        var s = ValidBaseline();
        s.ZoneCfg.NeutralZoneCount = 0;
        s.GameEndConditions.CityHold = true;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Blockers, b => b.Contains("City Hold"));
    }

    [Fact]
    public void NoBlocker_CityHold_WithHubAndSpoke()
    {
        var s = ValidBaseline();
        s.ZoneCfg.NeutralZoneCount = 0;
        s.Topology = MapTopology.HubAndSpoke;
        s.GameEndConditions.CityHold = true;
        var result = SettingsValidator.Validate(s);
        Assert.DoesNotContain(result.Blockers, b => b.Contains("City Hold"));
    }

    [Fact]
    public void Blocker_NoDirectPlayerConnections_WithoutNeutrals()
    {
        var s = ValidBaseline();
        s.ZoneCfg.NeutralZoneCount = 0;
        s.NoDirectPlayerConnections = true;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Blockers, b => b.Contains("neutral zones only"));
    }

    [Fact]
    public void Blocker_Tournament_RequiresExactlyTwoPlayers()
    {
        var s = ValidBaseline();
        s.PlayerCount = 4;
        s.GameEndConditions.VictoryCondition = "win_condition_6";
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Blockers, b => b.Contains("Tournament"));
    }

    [Fact]
    public void NoBlocker_Tournament_WithTwoPlayers()
    {
        var s = ValidBaseline();
        s.PlayerCount = 2;
        s.GameEndConditions.VictoryCondition = "win_condition_6";
        var result = SettingsValidator.Validate(s);
        Assert.DoesNotContain(result.Blockers, b => b.Contains("Tournament"));
    }

    [Fact]
    public void Warning_DefaultTemplateName()
    {
        var s = ValidBaseline();
        s.TemplateName = "Custom Template";
        var result = SettingsValidator.Validate(s);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("default name"));
    }

    [Fact]
    public void Warning_ExperimentalMapSize()
    {
        var s = ValidBaseline();
        s.MapSize = 320;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Warnings, w => w.Contains("Experimental"));
    }

    [Fact]
    public void Warning_ZoneSizeTooSmall()
    {
        var s = ValidBaseline();
        s.MapSize = 96; // 9216 / (4+4) = 1152, still ok... need smaller
        s.PlayerCount = 8;
        s.ZoneCfg.NeutralZoneCount = 8;
        s.MapSize = 96;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Warnings, w => w.Contains("zone size"));
    }

    [Fact]
    public void Warning_TooManyCastles_WithManyZones()
    {
        var s = ValidBaseline();
        s.PlayerCount = 6;
        s.ZoneCfg.NeutralZoneCount = 6;
        s.ZoneCfg.PlayerZoneCastles = 2;
        var result = SettingsValidator.Validate(s);
        Assert.Contains(result.Warnings, w => w.Contains("castle per zone"));
    }

    [Fact]
    public void Warning_WhenAllHeroesOfAFactionAreBanned()
    {
        var s = ValidBaseline();
        // Pick the first faction and ban every one of its heroes.
        var faction = CommunityCatalog.Default.Factions.First();
        var heroes = CommunityCatalog.Default.HeroesByFaction(faction.Id).ToList();
        Assert.NotEmpty(heroes);
        s.HeroSettings.HeroBans = heroes.Select(h => h.Id).ToList();

        var result = SettingsValidator.Validate(s);
        Assert.True(result.IsValid); // warning, not blocker
        Assert.Contains(result.Warnings, w => w.Contains(faction.Name) && w.Contains("banned"));
    }

    [Fact]
    public void TotalNeutralZones_AdvancedMode_SumsTierBuckets()
    {
        var s = ValidBaseline();
        s.ZoneCfg.NeutralZoneCount = 99; // ignored in advanced mode
        s.ZoneCfg.Advanced.Enabled = true;
        s.ZoneCfg.Advanced.NeutralLowCastleCount = 2;
        s.ZoneCfg.Advanced.NeutralMediumNoCastleCount = 3;
        s.ZoneCfg.Advanced.NeutralHighCastleCount = 1;
        Assert.Equal(6, SettingsValidator.TotalNeutralZones(s));
    }
}
