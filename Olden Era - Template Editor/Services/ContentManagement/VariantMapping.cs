
using System.Reflection;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Simple class for storing variant information for relevant content. */
public class VariantMapping
{
    public SidMapping content { get; set; }
    /* Dictionary of possible variant values and their descriptions */
    public Dictionary<int, string> variants { get; set; } = new();
    public string DisplayText => variants.Count > 0
        ? $"{variants.First().Value}"
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
        { 0, "Small Guard" },
        { 1, "Medium Guard" },
        { 2, "Large Guard" },
        { 3, "Maximum Guard" },
    });
    public static readonly VariantMapping pandoraBoxVariants = new VariantMapping(ContentIds.PandoraBox, new Dictionary<int, string>
    {
        { 0, "Gold T1 (Low)" },
        { 1, "Gold T2" },
        { 2, "Gold T3" },
        { 3, "Gold T4 (High)" },
        { 4, "Experience T1 (Low)" },
        { 5, "Experience T2" },
        { 6, "Experience T3" },
        { 7, "Experience T4 (High)" },
        { 8, "Units T1 (Low)" },
        { 9, "Units T2" },
        { 10, "Units T3" },
        { 11, "Units T4" },
        { 12, "Units T5" },
        { 13, "Units T6" },
        { 14, "Units T7 (High)" },
        { 15, "All Stats T1 (Low)" },
        { 16, "All Stats T2" },
        { 17, "All Stats T3" },
        { 18, "All Stats T4 (High)" },
        { 19, "Magic School Spells: Daylight" },
        { 20, "Magic School Spells: Nightshade" },
        { 21, "Magic School Spells: Arcane" },
        { 22, "Magic School Spells: Primal" },
        { 23, "Spells T1" },
        { 24, "Spells T2" },
        { 25, "Spells T3" },
        { 26, "Spells T4" },
        { 27, "Spells T5" },
    });

    public static readonly VariantMapping montyHallVariants = new VariantMapping(ContentIds.MontyHall, new Dictionary<int, string>
    {
        { 0, "Common Artifact" },
        { 1, "Rare Artifact" },
        { 2, "Epic Artifact" },
        { 3, "Legendary Artifact" },
    });

    /* Retrieve all defined Content Rules */
    public static VariantMapping[] GetAllVariantMappings()
    {
        return typeof(VariantMappingManager)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(VariantMapping))
            .Select(field => (VariantMapping?)field.GetValue(null))
            .Where(mapping => mapping is not null)
            .Cast<VariantMapping>()
            .ToArray();
    }

    public static List<VariantMapping> GetVariantsForContent(SidMapping content)
    {
        var mapping = GetAllVariantMappings().FirstOrDefault(vm => vm.content == content);
        if (mapping is not null)
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