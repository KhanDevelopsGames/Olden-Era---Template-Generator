using System.Windows.Media;

namespace OldenEraTemplateEditor.Models
{
    /// <summary>
    /// UI view-model for a single row in the banned-items or banned-spells ListBox.
    /// </summary>
    public class BanEntry
    {
        public string Id          { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Category    { get; set; } = "";

        public Brush CategoryBrush => Category switch
        {
            "Movement"  => new SolidColorBrush(Color.FromRgb(100, 149, 237)), // cornflower blue
            "Diplomacy" => new SolidColorBrush(Color.FromRgb(218, 165,  32)), // goldenrod
            "Combat"    => new SolidColorBrush(Color.FromRgb(205,  92,  92)), // indian red
            "Magic"     => new SolidColorBrush(Color.FromRgb(147, 112, 219)), // medium purple
            "Set"       => new SolidColorBrush(Color.FromRgb(186,  85, 211)), // medium orchid
            "Spell"     => new SolidColorBrush(Color.FromRgb(147, 112, 219)), // medium purple
            _           => new SolidColorBrush(Color.FromRgb(150, 150, 150)), // gray
        };
    }
}
