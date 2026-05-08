using OldenEraTemplateEditor.Models;
namespace OldenEraTemplateEditor.Services.ContentManagement
{

/* Generic ContentItem builder for managing the content within zones. */
public class ContentItemBuilder
{
    private readonly ContentItem _item = new();

    public static ContentItemBuilder Create(string sid)
    {
        return new ContentItemBuilder().WithSid(sid);
    }

    public ContentItemBuilder WithSid(string sid)
    {
        _item.Sid = sid;
        return this;
    }

    public ContentItemBuilder WithName(string name)
    {
        _item.Name = name;
        return this;
    }

    public ContentItemBuilder WithVariant(int variant)
    {
        _item.Variant = variant;
        return this;
    }

    public ContentItemBuilder Guarded(bool value = true)
    {
        _item.IsGuarded = value;
        return this;
    }

    public ContentItemBuilder Mine(bool value = true)
    {
        _item.IsMine = value;
        return this;
    }

    public ContentItemBuilder SoloEncounter(bool value = true)
    {
        _item.SoloEncounter = value;
        return this;
    }
    public ContentItemBuilder AddRule(ContentPlacementRule rule)
    {
        _item.Rules ??= new List<ContentPlacementRule>();
        _item.Rules.Add(rule);
        return this;
    }
    public ContentItemBuilder AddRules(IEnumerable<ContentPlacementRule> rules)
    {
        _item.Rules ??= new List<ContentPlacementRule>();
        _item.Rules.AddRange(rules);
        return this;
    }

    // Helper for common rule for content items regarding distance from roads.
    public ContentItemBuilder RoadDistance(DistanceVariation distance, int weight = 1) => AddRule(RulePresets.RoadDistance(distance, weight));

    public ContentItemBuilder AddIncludeList(string list)
    {
        _item.IncludeLists ??= new List<string>();
        _item.IncludeLists.Add(list);
        return this;
    }
    public ContentItemBuilder AddIncludeLists(IEnumerable<string> lists)
    {
        _item.IncludeLists ??= new List<string>();
        _item.IncludeLists.AddRange(lists);
        return this;
    }
    public ContentItem Build() => _item;
}
}
