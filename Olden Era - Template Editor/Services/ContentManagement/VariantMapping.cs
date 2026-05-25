
namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Simple class for storing variant information for relevant content. */
public class VariantMapping
{
    public SidMapping content { get; set; }
    /* Dictionary of possible variant values and their descriptions */
    public Dictionary<int, string> variants { get; set; } = new();
    public string DisplayText => variants.Count > 0
        ? $"{variants.First().Key} ({variants.First().Value})"
        : content.Name;

    public VariantMapping(SidMapping content, Dictionary<int, string> variants)
    {
        this.content = content;
        this.variants = variants;
    }

    public override string ToString() => DisplayText;
}

public static class VariantMappingManager
{
    public static readonly VariantMapping utopiaVariants = new VariantMapping(ContentIds.DragonUtopia, new Dictionary<int, string>
    {
        { 0, "WIP 0" },
        { 1, "WIP 1" },
        { 2, "WIP 2" }
    });

    public static readonly Dictionary<SidMapping, VariantMapping> contentVariantMappings = new Dictionary<SidMapping, VariantMapping>
    {
        { ContentIds.DragonUtopia, utopiaVariants },
        /* Add more content items with variants here as needed. */
    };

    public static List<VariantMapping> GetVariantsForContent(SidMapping content)
    {
        if (contentVariantMappings.TryGetValue(content, out VariantMapping? mapping) && mapping is not null)
        {
            return mapping.variants
                .Select(variant => new VariantMapping(content, new Dictionary<int, string>
                {
                    { variant.Key, variant.Value }
                }))
                .ToList();
        }

        return new List<VariantMapping>();
    }
    public static VariantMapping? GetVariantForContentById(SidMapping content, int variantId)
    {
        return GetVariantsForContent(content)
            .FirstOrDefault(variant => variant.variants.ContainsKey(variantId));
    }
}

}