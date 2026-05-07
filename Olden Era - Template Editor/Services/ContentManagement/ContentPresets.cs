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

        return ContentItemBuilder.Create(ContentIds.RemoteFoothold)
            .WithName("name_remote_foothold_1") // Think about uniqueness of names... It's duplicated in some templates, but might be important.
            .SoloEncounter()
            .Guarded(false)
            .AddRules(rules)
            .Build();
    }
    
    /* Basic wood mine, guarded & anchored near the player castle */
    public static ContentItem MineWood_Anchored() => 
        ContentItemBuilder.Create(ContentIds.MineWood)
            .WithName("name_mine_wood")
            .Mine()
            .Guarded()
            .AddRules([
                new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.35, Weight = 1 },
                RulePresets.AtCrossroads(0.15, 0.30)
            ])
            .Build();
    /* Basic ore mine, guarded & anchored near the player castle */
    public static ContentItem MineOre_Anchored() => 
        ContentItemBuilder.Create(ContentIds.MineOre)
            .WithName("name_mine_ore")
            .Mine()
            .Guarded()
            .AddRules([
                new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.35, Weight = 1 },
                RulePresets.AtCrossroads(0.15, 0.30)
            ])
            .Build();
    /* Gold mine near crossroads */
    public static ContentItem MineGold_Crossroads() => 
        ContentItemBuilder.Create(ContentIds.MineGold)
            .Mine()
            .AddRules([
                RulePresets.AtCrossroads(0.10, 0.30)
            ])
            .Build();
    
    /* Mines near roads */
    public static ContentItem MineCrystals_Road() =>
        ContentItemBuilder.Create(ContentIds.MineCrystals)
            .WithName("name_mine_crystals")
            .Mine()
            .AddRules([
                RulePresets.NearRoad(0.05, 0.10)
            ])
            .Build();
    public static ContentItem MineMercury_Road() =>
        ContentItemBuilder.Create(ContentIds.MineMercury)
            .WithName("name_mine_mercury")
            .Mine()
            .AddRules([
                RulePresets.NearRoad(0.05, 0.10)
            ])
            .Build();
    public static ContentItem MineGemstones_Road() =>
        ContentItemBuilder.Create(ContentIds.MineGemstones)
            .WithName("name_mine_gemstones")
            .Mine()
            .AddRules([
                RulePresets.NearRoad(0.05, 0.10)
            ])
            .Build();
    public static ContentItem AlchemyLab_Road() =>
        ContentItemBuilder.Create(ContentIds.AlchemyLab)
            .WithName("name_alchemy_lab")
            .Mine()
            .AddRules([
                RulePresets.NearRoad(0.20, 0.30)
            ])
            .Build();

    

}

}