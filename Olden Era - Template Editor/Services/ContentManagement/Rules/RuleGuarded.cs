
using System;
using System.Diagnostics.CodeAnalysis;
using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Specific content rule for guarded status, which can be applied to content items. */
public class RuleGuarded : IContentRule
{
    public const string RuleName = "Guarded";
    public string Name => RuleName;
    /* Custom value type for guarded rule. */
    public sealed record GuardedValue(bool isGuarded) : IContentRule.RuleValue
    {
        public override object UntypedValue => isGuarded;
    }
    /* Storage of the actual rule value. */
    public required GuardedValue Value { get; set; }

    /* When rule is handled as IContentRule, use the explicit interface implementation to ensure type safety. */
    IContentRule.RuleValue IContentRule.Value
    {
        get => Value;
        set => Value = value is GuardedValue guardedValue
            ? guardedValue
            /* Debugging helper, UI should not allow to set an invalid value */
            : throw new ArgumentException($"{nameof(RuleGuarded)} requires a {nameof(GuardedValue)}.", nameof(value));
    }
    /* Representation of the given rule in the UI when added as an individual rule */
    public string GetDisplayText() => $"{Name}: {Value.isGuarded}";
    /* Need to expose the display name for UI binding. */
    public string DisplayName => GetDisplayText();
    
    [SetsRequiredMembers]
    public RuleGuarded(bool? isGuarded = null)
    {
        Value = new GuardedValue(isGuarded ?? false);
    }
    public ContentRuleRowSave SerializeToRowSave()
    {
        var rowSave = new ContentRuleRowSave
        {
            Name = Name,
            IsGuarded = Value.isGuarded
        };
        return rowSave;
    }
}
}