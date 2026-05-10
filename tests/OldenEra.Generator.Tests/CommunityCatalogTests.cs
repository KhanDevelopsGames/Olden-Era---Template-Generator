using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class CommunityCatalogTests
{
    private static CommunityCatalog Catalog => CommunityCatalog.Default;

    [Fact]
    public void LoadsAllCollectionsNonEmpty()
    {
        Assert.NotEmpty(Catalog.Heroes);
        Assert.NotEmpty(Catalog.Units);
        Assert.NotEmpty(Catalog.Spells);
        Assert.NotEmpty(Catalog.Skills);
        Assert.NotEmpty(Catalog.Subclasses);
        Assert.NotEmpty(Catalog.Factions);
    }

    [Fact]
    public void HeroCount_Matches108()
    {
        Assert.Equal(108, Catalog.Heroes.Count);
    }

    [Fact]
    public void FactionCount_Matches6()
    {
        Assert.Equal(6, Catalog.Factions.Count);
    }

    [Fact]
    public void Heroes_HaveExpectedSidFormat()
    {
        // SID is "<unitKey>_hero_<n>" per the alcaras dump.
        // Example: "human_hero_1", "necro_hero_12".
        var sample = Catalog.Heroes.First(h => h.Faction == "temple");
        Assert.Matches(@"^[a-z_]+_hero_\d+$", sample.Id);
    }

    [Fact]
    public void HeroesByFaction_PartitionsAllHeroes()
    {
        // Every hero belongs to a known faction; sum across factions == total.
        var factionIds = Catalog.Factions.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(Catalog.Heroes, h => Assert.Contains(h.Faction, factionIds));

        int sum = factionIds.Sum(fid => Catalog.HeroesByFaction(fid).Count());
        Assert.Equal(Catalog.Heroes.Count, sum);
    }

    [Fact]
    public void Spells_HaveSchoolAndTier()
    {
        Assert.All(Catalog.Spells, s =>
        {
            Assert.False(string.IsNullOrEmpty(s.School), $"Spell {s.Id} has no school.");
            Assert.True(s.Tier >= 1, $"Spell {s.Id} has invalid tier {s.Tier}.");
        });
    }

    [Fact]
    public void SpellsBySchool_FindsExpectedSchools()
    {
        // Game has day, night, arcane, primal magic schools (per skill-columns.json).
        var schools = Catalog.Spells.Select(s => s.School).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("day", schools);
        Assert.Contains("night", schools);
    }

    [Fact]
    public void Default_IsCachedSingleton()
    {
        Assert.Same(CommunityCatalog.Default, CommunityCatalog.Default);
    }
}
