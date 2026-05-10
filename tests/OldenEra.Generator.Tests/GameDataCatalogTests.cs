using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class GameDataCatalogTests
{
    private static GameDataCatalog Load() => GameDataCatalog.LoadFromDirectory(RepoPaths.GeneratorDataRoot());

    [Fact]
    public void LoadFromDirectory_PopulatesEachCategory()
    {
        var catalog = Load();
        Assert.NotEmpty(catalog.Pools);
        Assert.NotEmpty(catalog.ContentLists);
        Assert.NotEmpty(catalog.ZoneLayouts);
    }

    [Fact]
    public void Contains_KnownStartZonePoolIds()
    {
        var catalog = Load();
        Assert.True(catalog.Contains(GameDataCategory.ContentPool, KnownIds.GeneralResourcePools.StartZonePoor));
        Assert.True(catalog.Contains(GameDataCategory.ContentPool, KnownIds.GeneralResourcePools.StartZoneMedium));
        Assert.True(catalog.Contains(GameDataCategory.ContentPool, KnownIds.GeneralResourcePools.StartZoneRich));
    }

    [Fact]
    public void Contains_ReturnsFalseForUnknownId()
    {
        var catalog = Load();
        Assert.False(catalog.Contains(GameDataCategory.ContentPool, "definitely_missing_pool_xyz"));
    }

    [Fact]
    public void LoadFromDirectory_Throws_WhenRootMissing()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            GameDataCatalog.LoadFromDirectory("/this/path/does/not/exist"));
    }

    [Fact]
    public void KnownIds_ValidateAgainst_ReturnsEmpty_ForShippedCatalog()
    {
        var catalog = Load();
        var missing = KnownIds.ValidateAgainst(catalog);
        Assert.Empty(missing);
    }
}
