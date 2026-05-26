
using System;
using System.Diagnostics.CodeAnalysis;
using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Specific content rule for solo encounter status, which can be applied to content items. */
public class RuleSoloEncounter : IContentRule
{
    public const string RuleName = "Solo Encounter";
    public const string RuleDescription = "Solo encounter means that the content item will be spawned without any additional content items around, enforcing consistent guard strength. Setting to false will make it more likely to be spawned with other content items, but will not always guarantee it.";
    public const string RuleMarker = "S";
    public string Name => RuleName;
    public string Description => RuleDescription;
    public string Marker => Value.isSoloEncounter ? RuleMarker : "!" + RuleMarker;
    /* Custom value type for solo encounter rule. */
    public sealed record SoloEncounterValue(bool isSoloEncounter) : IContentRule.RuleValue
    {
        public override object UntypedValue => isSoloEncounter;
    }
    /* Storage of the actual rule value. */
    public required SoloEncounterValue Value { get; set; }

    /* When rule is handled as IContentRule, use the explicit interface implementation to ensure type safety. */
    IContentRule.RuleValue IContentRule.Value
    {
        get => Value;
        set => Value = value is SoloEncounterValue soloEncounterValue
            ? soloEncounterValue
            /* Debugging helper, UI should not allow to set an invalid value */
            : throw new ArgumentException($"{nameof(RuleSoloEncounter)} requires a {nameof(SoloEncounterValue)}.", nameof(value));
    }
    /* Representation of the given rule in the UI when added as an individual rule */
    public string GetDisplayText() => $"{Name}: {Value.isSoloEncounter}";
    /* Need to expose the display name for UI binding. */
    public string DisplayName => GetDisplayText();
    
    [SetsRequiredMembers]
    public RuleSoloEncounter(bool? isSoloEncounter = null)
    {
        Value = new SoloEncounterValue(isSoloEncounter ?? false);
    }

    /* Required for saving settings! Rule variant constructor from serialized save data. */
    [SetsRequiredMembers]
    public RuleSoloEncounter(ContentRuleRowSave savedRule)
    {
        if (savedRule is null)
            throw new ArgumentNullException(nameof(savedRule));
        if (!savedRule.IsSoloEncounter.HasValue)
            throw new ArgumentException("IsSoloEncounter is required for RuleSoloEncounter.", nameof(savedRule));

        Value = new SoloEncounterValue(savedRule.IsSoloEncounter.Value);
    }

    public ContentRuleRowSave SerializeToRowSave()
    {
        var rowSave = new ContentRuleRowSave
        {
            Name = Name,
            IsSoloEncounter = Value.isSoloEncounter
        };
        return rowSave;
    }
}
}