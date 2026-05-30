
using System;
using System.Diagnostics.CodeAnalysis;
using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Specific content rule for guarded status, which can be applied to content items. */
public class RuleVariant : IContentRule
{
    public const string RuleName = "Variant";
    public const string RuleDescription = "Forces the content item to spawn a specific variant.";
    public const string RuleMarker = "";
    public string Name => RuleName;
    public string Description => RuleDescription;
    public string Marker => RuleMarker;
    /* Custom value type for variant rule. */
    public sealed record VariantValue(VariantMapping variantMapping, int variantId) : IContentRule.RuleValue
    {
        public override object UntypedValue => variantMapping;
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
    public string GetDisplayText()
    {
        if (Value.variantMapping.variants.TryGetValue(Value.variantId, out string? description))
            return $"{Name}: {description}";

        return $"{Name}: ERROR - please show this on template generator discord :)";
    }
    /* Need to expose the display name for UI binding. */
    public string DisplayName => GetDisplayText();
    
    [SetsRequiredMembers]
    public RuleVariant(VariantMapping? variantMapping = null, int? variantId = null)
    {
        // Dummy mapping to avoid null issues. 
        VariantMapping dummyVariant = VariantMappingManager.utopiaVariants;
        VariantMapping resolvedMapping = variantMapping ?? dummyVariant;

        int resolvedVariantId = variantId
            ?? resolvedMapping.variants.Keys.FirstOrDefault();

        if (!resolvedMapping.variants.ContainsKey(resolvedVariantId))
            throw new ArgumentException("Selected variant ID is not present in the provided variant mapping.", nameof(variantId));

        Value = new VariantValue(resolvedMapping, resolvedVariantId);
    }

    /* Required for saving settings! Rule variant constructor from serialized save data. */
    [SetsRequiredMembers]
    public RuleVariant(ContentRuleRowSave savedRule, SidMapping contentItem)
    {
        /* Sanity checks. Shouldn't be possible to reach exception throw during regular user interaction. */
        if (savedRule is null)
            throw new ArgumentNullException(nameof(savedRule));
        if (contentItem is null)
            throw new ArgumentNullException(nameof(contentItem));
        if (!savedRule.VariantId.HasValue)
            throw new ArgumentException("VariantId is required for RuleVariant.", nameof(savedRule));

        VariantMapping? variantMapping = VariantMappingManager.GetVariantForContentById(contentItem, savedRule.VariantId.Value);
        if (variantMapping is null)
            throw new ArgumentException("VariantId does not match any known variant for the content item.", nameof(savedRule));

        Value = new VariantValue(variantMapping, savedRule.VariantId.Value);
    }

    public ContentRuleRowSave SerializeToRowSave()
    {
        var rowSave = new ContentRuleRowSave
        {
            Name = Name,
            VariantId = Value.variantId
        };
        return rowSave;
    }
}
}