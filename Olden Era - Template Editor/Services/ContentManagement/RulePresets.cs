using System.ComponentModel.DataAnnotations;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{

/* Some commonly found rulesets, to avoid repetition and ensure consistency across templates. */
public static class RulePresets
{
    public static ContentPlacementRule AtCrossroads(double min, double max, int weight = 1)=> 
        new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = min, TargetMax = max, Weight = weight };

    public static ContentPlacementRule NearRoad(double min, double max, int weight = 1) => 
        new ContentPlacementRule { Type = "Road", Args = [], TargetMin = min, TargetMax = max, Weight = weight };
    
}
}