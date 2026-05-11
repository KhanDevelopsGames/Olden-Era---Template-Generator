using System.Data;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
public static class ContentPresets
{
    /* Foothold rules derived from example templates. Might need adjustment. */
    private static List<ContentPlacementRule> FootholdRules(int castleCount)
    {
        var rules = new List<ContentPlacementRule>
        {
            new() { Type = "Crossroads", Args = [], TargetMin = 0.2, TargetMax = 0.3, Weight = 0 },
        };
        if (castleCount > 0)
            // Not sure about what exactly weight == 0 does (doesn't it mean no impact?), but it's present in the original templates.
            rules.Add(new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.2, TargetMax = 0.4, Weight = 0 });
        if (castleCount > 1)
            rules.Add(new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.5, TargetMax = 0.5, Weight = 2 });
        return rules;
    }

    public static ContentItem RemoteFoothold(int castleCount)
    { 
        var rules = FootholdRules(castleCount);

        return ContentItemBuilder.Create(ContentIds.RemoteFoothold.Sid)
            .WithName("name_remote_foothold_1") // Think about uniqueness of names... It's duplicated in some templates, but might be important.
            .SoloEncounter()
            .Guarded(false)
            .AddRules(rules)
            .Build();
    }
    
    /* Basic wood mine, guarded & anchored near the player castle */
    public static ContentItem MineWood_Anchored() => 
        ContentItemBuilder.Create(ContentIds.MineWood.Sid)
            .WithName("name_mine_wood")
            .Mine()
            .Guarded()
            .AddRules([
                RulePresets.NearCastle(),
                RulePresets.CrossroadsDistance(DistancePresets.Near)
            ])
            .Build();
    /* Basic ore mine, guarded & anchored near the player castle */
    public static ContentItem MineOre_Anchored() => 
        ContentItemBuilder.Create(ContentIds.MineOre.Sid)
            .WithName("name_mine_ore")
            .Mine()
            .Guarded()
            .AddRules([
                RulePresets.NearCastle(),
                RulePresets.CrossroadsDistance(DistancePresets.Near)
            ])
            .Build();
    /* Gold mine near crossroads */
    public static ContentItem MineGold_NearCrossroads() => 
        ContentItemBuilder.Create(ContentIds.MineGold.Sid)
            .Mine()
            .AddRules([
                RulePresets.CrossroadsDistance(DistancePresets.Near)
            ])
            .Build();
    
    /* Mines near roads */
    public static ContentItem MineCrystals_NextToRoad() =>
        ContentItemBuilder.Create(ContentIds.MineCrystals.Sid)
            .WithName("name_mine_crystals")
            .Mine()
            .RoadDistance(DistancePresets.NextTo)
            .Build();
    public static ContentItem MineMercury_NextToRoad() =>
        ContentItemBuilder.Create(ContentIds.MineMercury.Sid)
            .WithName("name_mine_mercury")
            .Mine()
            .RoadDistance(DistancePresets.NextTo)
            .Build();
    public static ContentItem MineGemstones_NextToRoad() =>
        ContentItemBuilder.Create(ContentIds.MineGemstones.Sid)
            .WithName("name_mine_gemstones")
            .Mine()
            .RoadDistance(DistancePresets.NextTo)
            .Build();
    public static ContentItem AlchemyLab_NearRoad() =>
        ContentItemBuilder.Create(ContentIds.AlchemyLab.Sid)
            .WithName("name_alchemy_lab")
            .Mine()
            .RoadDistance(DistancePresets.Near)
            .Build();

}

}