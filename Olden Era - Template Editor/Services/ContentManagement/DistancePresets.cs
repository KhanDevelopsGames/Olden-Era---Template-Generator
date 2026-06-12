using System.ComponentModel.DataAnnotations;
using OldenEraTemplateEditor.Models;
using System.Linq;
using System.Reflection;

namespace OldenEraTemplateEditor.Services.ContentManagement
{

public struct DistanceVariation
{
    public string Name { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
}
public static class DistancePresets
{
    public static DistanceVariation NextTo = new DistanceVariation { Name = "Next To", Min = 0.05, Max = 0.1 };
    public static DistanceVariation Near = new DistanceVariation { Name = "Near", Min = 0.1, Max = 0.25 };
    public static DistanceVariation Medium = new DistanceVariation { Name = "Medium", Min = 0.25, Max = 0.5 };
    public static DistanceVariation Far = new DistanceVariation { Name = "Far", Min = 0.5, Max = 0.75 };
    public static DistanceVariation VeryFar = new DistanceVariation { Name = "Very Far", Min = 0.75, Max = 0.9 };

    /* Get the display names of all distance variations */
    public static string[] GetDisplayNames()
    {
        return typeof(DistancePresets)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetValue(null) is DistanceVariation dv ? dv.Name : f.Name)
            .ToArray();
    }
    /* Get a distance variation by its display name */
    public static DistanceVariation GetDistanceVariationByName(string? name)
    {
        return typeof(DistancePresets)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetValue(null))
            .OfType<DistanceVariation>()
            .FirstOrDefault(dv => dv.Name == name);
    }
}

}