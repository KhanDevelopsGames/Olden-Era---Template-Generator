
using System;
using System.Diagnostics.CodeAnalysis;
using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Specific content rule for distance to town, which can be applied to content items. */
    public class RuleDistanceToTown : IContentRule
{
    public const string RuleName = "Distance to town";
    public string Name => RuleName;
    /* Custom value type for distance to town rule. */
    public sealed record DistanceToTownValue(DistanceVariation distanceVariation) : IContentRule.RuleValue
    {
        public override object UntypedValue => distanceVariation;
    }
    /* Storage of the actual rule value. */
    public required DistanceToTownValue Value { get; set; }

    /* When rule is handled as IContentRule, use the explicit interface implementation to ensure type safety. */
    IContentRule.RuleValue IContentRule.Value
    {
        get => Value;
        set => Value = value is DistanceToTownValue distanceToTownValue
            ? distanceToTownValue
            /* Debugging helper, UI should not allow to set an invalid value */
            : throw new ArgumentException($"{nameof(RuleDistanceToTown)} requires a {nameof(DistanceToTownValue)}.", nameof(value));
    }
    /* Representation of the given rule in the UI when added as an individual rule */
    public string GetDisplayText() => $"{Name}: {Value.distanceVariation.Name}";
    /* Need to expose the display name for UI binding. */
    public string DisplayName => GetDisplayText();
    
    [SetsRequiredMembers]
    public RuleDistanceToTown(DistanceVariation? value = null)
    {
        Value = new DistanceToTownValue(value ?? DistancePresets.Medium);
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