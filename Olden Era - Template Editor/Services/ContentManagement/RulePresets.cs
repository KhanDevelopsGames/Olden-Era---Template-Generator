using System.ComponentModel.DataAnnotations;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{

/* Some commonly found rulesets, to avoid repetition and ensure consistency across templates. */
public static class RulePresets
{
    public static ContentPlacementRule RoadDistance(DistanceVariation distance, int weight = 1) => 
        new ContentPlacementRule { Type = "Road", Args = [], TargetMin = distance.Min, TargetMax = distance.Max, Weight = weight };

    public static ContentPlacementRule CrossroadsDistance(DistanceVariation distance, int weight = 1)=> 
        new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = distance.Min, TargetMax = distance.Max, Weight = weight };

}
}