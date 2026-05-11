using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SpellBansTests
{
    private static GeneratorSettings MakeSettings()
    {
        // Mirrors SmokeTests minimal-viable pattern.
        return new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 2
            },
            Topology = MapTopology.Default
        };
    }

    [Fact]
    public void BannedSpells_EmitsGlobalBansMagics()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string> { "spell.fly", "spell.town_portal" };

        var template = TemplateGenerator.Generate(s);

        Assert.NotNull(template.GlobalBans);
        Assert.NotNull(template.GlobalBans!.Magics);
        Assert.Contains("spell.fly", template.GlobalBans.Magics!);
        Assert.Contains("spell.town_portal", template.GlobalBans.Magics!);
    }

    [Fact]
    public void BannedSpells_Empty_DoesNotCreateMagicsArray()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string>();

        var template = TemplateGenerator.Generate(s);

        Assert.True(template.GlobalBans is null || template.GlobalBans.Magics is null);
    }

    [Fact]
    public void BannedSpells_DedupesIds()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string> { "spell.fly", "spell.fly", "spell.town_portal" };

        var template = TemplateGenerator.Generate(s);

        Assert.Equal(2, template.GlobalBans!.Magics!.Count);
        Assert.Equal(1, template.GlobalBans.Magics.Count(x => x == "spell.fly"));
    }

    [Fact]
    public void BannedSpells_IgnoresWhitespaceIds()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string> { "spell.fly", "   ", "" };

        var template = TemplateGenerator.Generate(s);

        Assert.Single(template.GlobalBans!.Magics!);
        Assert.Equal("spell.fly", template.GlobalBans.Magics![0]);
    }
}
