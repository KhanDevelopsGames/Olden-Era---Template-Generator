using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class HeroCatalogTests
{
    [Fact]
    public void Factions_ContainsAllSixCanonicalFactions()
    {
        var displayNames = HeroCatalog.Factions.Select(f => f.DisplayName).ToList();
        Assert.Contains("Temple", displayNames);
        Assert.Contains("Necropolis", displayNames);
        Assert.Contains("Grove", displayNames);
        Assert.Contains("Hive", displayNames);
        Assert.Contains("Schism", displayNames);
        Assert.Contains("Dungeon", displayNames);
        Assert.Equal(6, displayNames.Count);
    }

    [Theory]
    [InlineData("Temple", 1, "human_hero_1")]
    [InlineData("Necropolis", 12, "necro_hero_12")]
    [InlineData("Grove", 18, "nature_hero_18")]
    [InlineData("Hive", 5, "demon_hero_5")]
    [InlineData("Schism", 7, "unfrozen_hero_7")]
    [InlineData("Dungeon", 10, "dungeon_hero_10")]
    public void BuildSid_FormatsCorrectly(string faction, int index, string expected)
    {
        Assert.Equal(expected, HeroCatalog.BuildSid(faction, index));
    }

    [Fact]
    public void BuildSid_IsCaseInsensitiveOnFactionName()
    {
        Assert.Equal("human_hero_1", HeroCatalog.BuildSid("temple", 1));
        Assert.Equal("human_hero_1", HeroCatalog.BuildSid("TEMPLE", 1));
    }

    [Fact]
    public void BuildSid_RejectsUnknownFaction()
    {
        Assert.Throws<ArgumentException>(() => HeroCatalog.BuildSid("Inferno", 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    [InlineData(-1)]
    public void BuildSid_RejectsOutOfRangeIndex(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeroCatalog.BuildSid("Temple", index));
    }

    [Fact]
    public void IsMightHero_AndIsMagicHero_PartitionTheRange()
    {
        // Indices 1..9 might, 10..18 magic. Mutually exclusive within 1..18.
        for (int i = 1; i <= HeroCatalog.HeroesPerFaction; i++)
        {
            Assert.NotEqual(HeroCatalog.IsMightHero(i), HeroCatalog.IsMagicHero(i));
        }
        Assert.True(HeroCatalog.IsMightHero(1));
        Assert.True(HeroCatalog.IsMightHero(9));
        Assert.True(HeroCatalog.IsMagicHero(10));
        Assert.True(HeroCatalog.IsMagicHero(18));
    }
}
