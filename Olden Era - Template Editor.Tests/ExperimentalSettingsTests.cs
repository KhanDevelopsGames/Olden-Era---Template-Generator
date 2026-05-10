using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace Olden_Era___Template_Editor.Tests;

public class ExperimentalSettingsTests
{
    [Fact]
    public void Defaults_DoNotEmitExperimentalFields()
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings());

        // Hero hire ban + bonuses + bans + custom limits should remain at the
        // pre-rollout shape: empty / null / single seed bonus.
        Assert.False(template.GameRules?.HeroHireBan ?? false);
        Assert.Single(template.GameRules!.Bonuses!); // movementBonus seed only
        Assert.Null(template.GlobalBans);

        var connections = template.Variants?[0].Connections;
        Assert.NotNull(connections);
        // Generator's hardcoded default is 0.15–0.20 — non-zero — so we just
        // assert the user override path didn't run by checking values stay
        // within the original distribution.
        Assert.All(connections!, c => Assert.True(c.GuardWeeklyIncrement is null or > 0));
    }

    [Fact]
    public void SingleHeroMode_ForcesHeroCountToOne()
    {
        var s = new GeneratorSettings
        {
            GameMode = "SingleHero",
            HeroSettings = new HeroSettings { HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.Equal(1, template.GameRules!.HeroCountMin);
        Assert.Equal(1, template.GameRules!.HeroCountMax);
        Assert.Equal(1, template.GameRules!.HeroCountIncrement);
    }

    [Fact]
    public void HeroHireBan_SetsGameRuleFlag()
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings { HeroHireBan = true });
        Assert.True(template.GameRules!.HeroHireBan);
    }

    [Fact]
    public void DesertionOverrides_UpdateWinConditions()
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings
        {
            DesertionDay = 7,
            DesertionValue = 5000,
        });
        Assert.Equal(7, template.GameRules!.WinConditions!.DesertionDay);
        Assert.Equal(5000, template.GameRules!.WinConditions!.DesertionValue);
    }

    [Fact]
    public void GlobalBans_AppearOnTemplate()
    {
        var s = new GeneratorSettings();
        s.Content.GlobalBans.Add("dragon_utopia");
        s.Content.GlobalBans.Add("pandora_box");

        var template = TemplateGenerator.Generate(s);
        Assert.NotNull(template.GlobalBans);
        Assert.Contains("dragon_utopia", template.GlobalBans!.Items!);
        Assert.Contains("pandora_box", template.GlobalBans!.Items!);
    }

    [Fact]
    public void ContentCountLimits_AppendUserEntries()
    {
        var s = new GeneratorSettings();
        s.Content.ContentCountLimits.Add(new ContentLimit { Sid = "mine_gold", MaxPerPlayer = 2 });

        var template = TemplateGenerator.Generate(s);
        Assert.Contains(template.ContentCountLimits!,
            l => l.Limits is not null
                 && l.Limits.Exists(x => x.Sid == "mine_gold" && x.MaxCount == 2));
    }

    [Fact]
    public void StartingBonuses_EmitOnePerSetField()
    {
        var s = new GeneratorSettings();
        s.Bonuses.Resources["gold"] = 5;
        s.Bonuses.HeroAttack = 2;
        s.Bonuses.ItemSid = "some_item";
        s.Bonuses.SpellSid = "some_spell";
        s.Bonuses.UnitMultiplier = 1.5;

        var template = TemplateGenerator.Generate(s);
        var bonuses = template.GameRules!.Bonuses!;
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_res");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_stat" && b.Parameters![0] == "attack");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_item");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_spell");
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_unit_multipler");
    }

    [Fact]
    public void StartHeroOnly_FlipsReceiverFilter()
    {
        var s = new GeneratorSettings();
        s.Bonuses.HeroAttack = 1;
        s.Bonuses.HeroStatStartHeroOnly = true;

        var template = TemplateGenerator.Generate(s);
        var stat = template.GameRules!.Bonuses!.Find(b => b.Sid == "add_bonus_hero_stat" && b.Parameters![0] == "attack");
        Assert.NotNull(stat);
        Assert.Equal("start_hero", stat!.ReceiverFilter);
    }

    [Fact]
    public void GuardProgressionOverride_StampsZonesAndConnections()
    {
        var s = new GeneratorSettings
        {
            GuardProgression = new GuardProgressionSettings
            {
                ZoneGuardWeeklyIncrement = 0.25,
                ConnectionGuardWeeklyIncrement = 0.30,
            },
        };
        var template = TemplateGenerator.Generate(s);
        var variant = template.Variants![0];
        Assert.All(variant.Zones!, z => Assert.Equal(0.25, z.GuardWeeklyIncrement));
        Assert.All(variant.Connections!, c => Assert.Equal(0.30, c.GuardWeeklyIncrement));
    }

    [Fact]
    public void TerrainOverrides_ApplyToZoneLayouts()
    {
        var s = new GeneratorSettings
        {
            Terrain = new TerrainSettings { ObstaclesFill = 0.4, LakesFill = 0.2 },
        };
        var template = TemplateGenerator.Generate(s);
        Assert.All(template.ZoneLayouts!, l => Assert.Equal(0.4, l.ObstaclesFill));
        Assert.All(template.ZoneLayouts!, l => Assert.Equal(0.2, l.LakesFill));
    }

    [Fact]
    public void BuildingPresetOverride_AppliesToCities()
    {
        var s = new GeneratorSettings
        {
            BuildingPresets = new BuildingPresetSettings
            {
                PlayerZonePreset = "rich_buildings_construction",
                NeutralZonePreset = "poor_buildings_construction",
            },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 1, NeutralZoneCastles = 1 },
        };
        var template = TemplateGenerator.Generate(s);
        // Player-zone Spawn objects pick up the player preset.
        var spawns = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "Spawn")
            .ToList();
        Assert.NotEmpty(spawns);
        Assert.All(spawns, m => Assert.Equal("rich_buildings_construction", m.BuildingsConstructionSid));

        // Neutral City objects pick up the neutral preset.
        var neutralCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(neutralCities);
        Assert.All(neutralCities, m => Assert.Equal("poor_buildings_construction", m.BuildingsConstructionSid));
    }

    [Fact]
    public void TierOverride_BuildingPresetWinsOverGlobalForMatchingTier()
    {
        var s = new GeneratorSettings
        {
            BuildingPresets = new BuildingPresetSettings { NeutralZonePreset = "poor_buildings_construction" },
        };
        s.ZoneCfg.Advanced.Enabled = true;
        s.ZoneCfg.Advanced.NeutralHighCastleCount = 1; // Spawn one High-tier neutral zone with a castle.
        s.ZoneCfg.Advanced.HighTier.BuildingPreset = "ultra_rich_buildings_construction";

        var template = TemplateGenerator.Generate(s);
        var allCities = template.Variants![0].Zones!
            .SelectMany(z => z.MainObjects ?? new List<MainObject>())
            .Where(m => m.Type == "City" && string.IsNullOrEmpty(m.Spawn))
            .ToList();
        Assert.NotEmpty(allCities);
        // High tier zone(s) must use the ultra_rich override; everything else stays on the global poor.
        Assert.Contains(allCities, m => m.BuildingsConstructionSid == "ultra_rich_buildings_construction");
    }

    [Fact]
    public void TierOverride_GuardWeeklyIncrementWinsForMatchingTier()
    {
        var s = new GeneratorSettings
        {
            GuardProgression = new GuardProgressionSettings { ZoneGuardWeeklyIncrement = 0.10 },
        };
        s.ZoneCfg.Advanced.Enabled = true;
        s.ZoneCfg.Advanced.NeutralLowNoCastleCount = 1;
        s.ZoneCfg.Advanced.LowTier.GuardWeeklyIncrement = 0.40;

        var template = TemplateGenerator.Generate(s);
        var lowZones = template.Variants![0].Zones!.Where(z => z.GuardWeeklyIncrement == 0.40).ToList();
        Assert.NotEmpty(lowZones);
    }

    [Fact]
    public void SettingsMapper_RoundTripsExperimentalFields()
    {
        var g = new GeneratorSettings
        {
            GameMode = "SingleHero",
            HeroHireBan = true,
            DesertionDay = 5,
            DesertionValue = 4500,
            Terrain = new TerrainSettings { ObstaclesFill = 0.3, LakesFill = 0.1 },
            BuildingPresets = new BuildingPresetSettings { PlayerZonePreset = "rich_buildings_construction" },
            GuardProgression = new GuardProgressionSettings { ZoneGuardWeeklyIncrement = 0.2 },
            NeutralCities = new NeutralCitySettings { GuardChance = 0.75, GuardValuePercent = 150 },
        };
        g.Content.GlobalBans.Add("dragon_utopia");
        g.Content.ContentCountLimits.Add(new ContentLimit { Sid = "mine_gold", MaxPerPlayer = 3 });
        g.Bonuses.Resources["wood"] = 4;
        g.Bonuses.HeroSpellpower = 3;

        var file = SettingsMapper.ToFile(g, advancedMode: false, experimentalMapSizes: false);
        var (back, _, _, _) = SettingsMapper.FromFile(file);

        Assert.Equal("SingleHero", back.GameMode);
        Assert.True(back.HeroHireBan);
        Assert.Equal(5, back.DesertionDay);
        Assert.Equal(4500, back.DesertionValue);
        Assert.Equal(0.3, back.Terrain.ObstaclesFill);
        Assert.Equal(0.1, back.Terrain.LakesFill);
        Assert.Equal("rich_buildings_construction", back.BuildingPresets.PlayerZonePreset);
        Assert.Equal(0.2, back.GuardProgression.ZoneGuardWeeklyIncrement);
        Assert.Equal(0.75, back.NeutralCities.GuardChance);
        Assert.Equal(150, back.NeutralCities.GuardValuePercent);
        Assert.Contains("dragon_utopia", back.Content.GlobalBans);
        Assert.Single(back.Content.ContentCountLimits);
        Assert.Equal(3, back.Content.ContentCountLimits[0].MaxPerPlayer);
        Assert.Equal(4, back.Bonuses.Resources["wood"]);
        Assert.Equal(3, back.Bonuses.HeroSpellpower);
    }
}
