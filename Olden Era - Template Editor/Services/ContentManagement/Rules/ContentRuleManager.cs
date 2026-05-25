using System.Reflection;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Content rule manager for processing rules from the UI to the underlying data model.
* Not every "rule" coming from the UI will be a rule type of content items in final templates,
* but all could be handled logically as rules within the system. */
public static class ContentRuleManager
{
    public static RuleDistanceToRoad _ruleDistanceToRoad = new RuleDistanceToRoad();
    public static RuleDistanceToTown _ruleDistanceToTown = new RuleDistanceToTown();
    public static RuleGuarded _ruleGuarded = new RuleGuarded();
    public static RuleVariant _ruleVariant = new RuleVariant();
    
    /* Retrieve all defined Content Rules */
    public static IContentRule[] GetRules()
    {
        return typeof(ContentRuleManager)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => typeof(IContentRule).IsAssignableFrom(field.FieldType))
            .Select(field => (IContentRule?)field.GetValue(null))
            .Where(rule => rule is not null)
            .Cast<IContentRule>()
            .ToArray();
    }

    /* Apply the rules from the UI data storage to the final JSON content item. */
    public static void ApplyRulesToFinalContentItem(ContentItem contentItem, ZoneContentItemUI itemUIData)
    {
        List<ContentPlacementRule> ContentPlacementRules = new List<ContentPlacementRule>();
        bool? isGuarded = null;
        foreach(IContentRule Rule in itemUIData.Rules)
        {
            switch(Rule)
            {
                case RuleDistanceToRoad rule:
                    ContentPlacementRules.Add(RulePresets.RoadDistance(rule.Value.distanceVariation));
                    break;
                case RuleDistanceToTown rule:
                    ContentPlacementRules.Add(RulePresets.TownDistance(rule.Value.distanceVariation));
                    break;
                case RuleGuarded rule:
                    isGuarded = rule.Value.isGuarded;
                    break;
                default:
                    // We never should reach this state. (assuming the UI only allows valid rules to be added).
                    continue;
            }
        }
        /* isGuarded can be null and that's fine - if rule is not set, do not force it in the final ContentItem. */
        contentItem.IsGuarded = isGuarded;
        if (ContentPlacementRules.Count > 0)
        {
            contentItem.Rules ??= new List<ContentPlacementRule>();
            contentItem.Rules.AddRange(ContentPlacementRules);
        }
    }

    public static IContentRule? CreateRuleFromSavedRule(ContentRuleRowSave savedRule)
    {
        if (string.IsNullOrWhiteSpace(savedRule.Name))
            return null;

        if (string.Equals(savedRule.Name, RuleDistanceToRoad.RuleName, StringComparison.OrdinalIgnoreCase))
        {
            return new RuleDistanceToRoad(savedRule);
        }

        if (string.Equals(savedRule.Name, RuleDistanceToTown.RuleName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(savedRule.DistanceName))
                return null;
            return new RuleDistanceToTown(DistancePresets.GetDistanceVariationByName(savedRule.DistanceName));
        }

        if (string.Equals(savedRule.Name, RuleGuarded.RuleName, StringComparison.OrdinalIgnoreCase) && savedRule.IsGuarded.HasValue)
        {
            return new RuleGuarded(savedRule.IsGuarded.Value);
        }

        if (string.Equals(savedRule.Name, RuleVariant.RuleName, StringComparison.OrdinalIgnoreCase) && savedRule.VariantId.HasValue)
        {
            return new RuleVariant(savedRule.VariantId.Value);
        }

        return null;
    }
}

}