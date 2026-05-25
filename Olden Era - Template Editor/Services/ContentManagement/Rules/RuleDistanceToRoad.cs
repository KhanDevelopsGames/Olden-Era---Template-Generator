
using System;
using System.Diagnostics.CodeAnalysis;
using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Specific content rule for distance to road, which can be applied to content items. */
public class RuleDistanceToRoad : IContentRule
{
    public const string RuleName = "Distance to road";
    public string Name => RuleName;
    /* Custom value type for distance to road rule. */
    public sealed record DistanceValue(DistanceVariation distanceVariation) : IContentRule.RuleValue
    {
        public override object UntypedValue => distanceVariation;
    }
    /* Storage of the actual rule value. */
    public required DistanceValue Value { get; set; }

    /* When rule is handled as IContentRule, use the explicit interface implementation to ensure type safety. */
    IContentRule.RuleValue IContentRule.Value
    {
        get => Value;
        set => Value = value is DistanceValue distanceValue
            ? distanceValue
            /* Debugging helper, UI should not allow to set an invalid value */
            : throw new ArgumentException($"{nameof(RuleDistanceToRoad)} requires a {nameof(DistanceValue)}.", nameof(value));
    }
    /* Representation of the given rule in the UI when added as an individual rule. */
    public string GetDisplayText() => $"{Name}: {Value.distanceVariation.Name}";
    /* Need to expose the display name for UI binding. */
    public string DisplayName => GetDisplayText();

    [SetsRequiredMembers]
    public RuleDistanceToRoad(DistanceVariation? value = null)
    {
        Value = new DistanceValue(value ?? DistancePresets.Medium);
    }
    
    [SetsRequiredMembers]
    public RuleDistanceToRoad(ContentRuleRowSave savedRule)
    {
            Value = new DistanceValue(DistancePresets.GetDistanceVariationByName(savedRule.DistanceName));
    }
    public ContentRuleRowSave SerializeToRowSave()
    {
        var rowSave = new ContentRuleRowSave
        {
            Name = Name,
            DistanceName = Value.distanceVariation.Name
        };
        return rowSave;
    }
}
}