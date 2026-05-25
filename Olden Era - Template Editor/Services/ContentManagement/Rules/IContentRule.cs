using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Interface for content rules that can be applied to content items. */
public interface IContentRule
{
    /* Name of the rule to be displayed in the UI */
    public string Name { get; }
    /* Description of the rule to be displayed in the UI */
    public string Description { get; }
    /* Value of the rule, which can be of different types based on the rule type. */
    public abstract record RuleValue
    {
        public abstract object UntypedValue { get; }
    }
    public RuleValue Value { get; set; }
    
    /* Get a user-friendly display text for the rule */
    public string GetDisplayText();
    /* Bind-friendly projection for UI controls that cannot call methods directly. */
    public string DisplayName => GetDisplayText();
    /* Serialize rule data to match the settings data structure */
    public ContentRuleRowSave SerializeToRowSave();
}

}
