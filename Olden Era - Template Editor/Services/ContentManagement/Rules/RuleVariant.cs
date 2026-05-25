
using System;
using System.Diagnostics.CodeAnalysis;
using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Specific content rule for guarded status, which can be applied to content items. */
public class RuleVariant : IContentRule
{
    public const string RuleName = "Variant";
    public string Name => RuleName;
    /* Custom value type for variant rule. */
    public sealed record VariantValue(int variant) : IContentRule.RuleValue
    {
        public override object UntypedValue => variant;
    }
    /* Storage of the actual rule value. */
    public required VariantValue Value { get; set; }

    /* When rule is handled as IContentRule, use the explicit interface implementation to ensure type safety. */
    IContentRule.RuleValue IContentRule.Value
    {
        get => Value;
        set => Value = value is VariantValue variantValue
            ? variantValue
            /* Debugging helper, UI should not allow to set an invalid value */
            : throw new ArgumentException($"{nameof(RuleVariant)} requires a {nameof(VariantValue)}.", nameof(value));
    }
    /* Representation of the given rule in the UI when added as an individual rule */
    public string GetDisplayText() => $"{Name}: {Value.variant}";
    /* Need to expose the display name for UI binding. */
    public string DisplayName => GetDisplayText();
    
    [SetsRequiredMembers]
    public RuleVariant(int? variant = null)
    {
        Value = new VariantValue(variant ?? 0);
    }
    public ContentRuleRowSave SerializeToRowSave()
    {
        var rowSave = new ContentRuleRowSave
        {
            Name = Name,
            VariantId = Value.variant
        };
        return rowSave;
    }
}
}