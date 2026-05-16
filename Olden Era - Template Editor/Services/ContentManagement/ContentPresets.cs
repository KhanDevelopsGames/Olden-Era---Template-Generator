using System.Data;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Some commonly found content items with rules, to use throughout the application. */
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
}

}