using System.Windows.Media;

namespace OldenEraTemplateEditor.Models
{
    /// <summary>
    /// UI view-model for a single row in the banned-items or banned-spells ListBox.
    /// </summary>
    public class BanEntry
    {
        private static readonly Brush MovementCategoryBrush  = CreateFrozenBrush(Color.FromRgb(100, 149, 237)); // cornflower blue
        private static readonly Brush DiplomacyCategoryBrush = CreateFrozenBrush(Color.FromRgb(218, 165,  32)); // goldenrod
        private static readonly Brush CombatCategoryBrush    = CreateFrozenBrush(Color.FromRgb(205,  92,  92)); // indian red
        private static readonly Brush MagicCategoryBrush     = CreateFrozenBrush(Color.FromRgb(147, 112, 219)); // medium purple
        private static readonly Brush SetCategoryBrush       = CreateFrozenBrush(Color.FromRgb(186,  85, 211)); // medium orchid
        private static readonly Brush DefaultCategoryBrush   = CreateFrozenBrush(Color.FromRgb(150, 150, 150)); // gray

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public string Id          { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category    { get; set; } = "";

        public Brush CategoryBrush => Category switch
        {
            "Movement"  => MovementCategoryBrush,
            "Diplomacy" => DiplomacyCategoryBrush,
            "Combat"    => CombatCategoryBrush,
            "Magic"     => MagicCategoryBrush,
            "Set"       => SetCategoryBrush,
            "Spell"     => MagicCategoryBrush,
            _           => DefaultCategoryBrush,
        };
    }
}
