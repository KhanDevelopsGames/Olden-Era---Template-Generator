using System.ComponentModel.DataAnnotations;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{

public struct DistanceVariation
{
    public double Min { get; set; }
    public double Max { get; set; }
}
public static class DistancePresets
{
    public static DistanceVariation NextTo = new DistanceVariation { Min = 0.05, Max = 0.1 };
    public static DistanceVariation Near = new DistanceVariation { Min = 0.1, Max = 0.25 };
    public static DistanceVariation Medium = new DistanceVariation { Min = 0.25, Max = 0.5 };
    public static DistanceVariation Far = new DistanceVariation { Min = 0.5, Max = 0.75 };
    public static DistanceVariation VeryFar = new DistanceVariation { Min = 0.75, Max = 0.9 };
}

}