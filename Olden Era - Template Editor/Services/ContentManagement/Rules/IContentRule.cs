using Olden_Era___Template_Editor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Interface for content rules that can be applied to content items.
*  @note - The implementing class should provide a constructor that accepts a ContentRuleRowSave for integrating with the save data structure.
* If the data depends on SidMappings, the fallback for constructor that accepts both of these parameters is implemented.
* This is not enforced by the compiler, and missing it will cause undefined behavior.
* Checklist for adding a new rule:
* Part 1 - Class implementation requirements:
* 1. Create a new class implementing IContentRule. Define unique Name, and add a Description for UI display.
* 2. Implement the Value sealed record to encapsulate the specific data for the rule, ensuring it inherits from IContentRule.RuleValue. 
*    See the already implemented rules for examples.
* 3. Implement the GetDisplayText method to provide a user-friendly display text.
* 4. Implement the SerializeToRowSave method to serialize the rule data & update the ContentRuleRowSave to reflect the rule's state.
* 5. Add a constructor that accepts a ContentRuleRowSave (see RuleVariant for exception as it requires additional parameters) to initialize the rule from saved data.
* Part 2 - Integration requirements:
* 1. Add the new rule to the ContentRuleManager as a static field for it to be visible in the UI.
* 2. Update the AddZoneContentRulesWindow to handle the new rule type and its specific controls & fields visibility.
*/
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
