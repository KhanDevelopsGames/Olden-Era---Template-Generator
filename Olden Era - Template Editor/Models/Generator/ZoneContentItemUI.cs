using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor.Models
{
    public class ZoneContentItemUI : INotifyPropertyChanged
    {
        private SidMapping? _sidMapping;
        private int _count;
        // Flag indicating if this is a single content item, or a group added via includeLists.
        private bool _isGroup;
        // Abstract representation of "content rules" from the UI, to be converted to proper ContentItem fields during its construction.
        public List<IContentRule> Rules { get; set; } = new List<IContentRule>();

        public SidMapping? SidMapping
        {
            get => _sidMapping;
            set
            {
                _sidMapping = value;
                OnPropertyChanged();
                /* Separate update handling for name changes (variant selection via the rules) */
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string DisplayName
        {
            get
            {
                if (SidMapping is null)
                    return string.Empty;

                RuleVariant? variantRule = Rules.OfType<RuleVariant>().FirstOrDefault();
                if (variantRule is null)
                    return SidMapping.Name;

                return $"{SidMapping.Name} ({variantRule.Value.variantMapping})";
            }
        }

        public string RuleMarkers
        {
            get
            {
                string guardedMarker = string.Empty;
                RuleGuarded? guardedRule = Rules.OfType<RuleGuarded>().FirstOrDefault();
                if (guardedRule is not null)
                {
                    guardedMarker = guardedRule.Value.isGuarded ? "G" : "!G";
                }

                bool hasRoadRule = Rules.OfType<RuleDistanceToRoad>().Any();
                bool hasTownRule = Rules.OfType<RuleDistanceToTown>().Any();

                var markers = new List<string>();
                if (!string.IsNullOrEmpty(guardedMarker))
                    markers.Add(guardedMarker);
                if (hasRoadRule)
                    markers.Add("R");
                if (hasTownRule)
                    markers.Add("T");

                return string.Join(" ", markers);
            }
        }

        public void NotifyRulesChanged()
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(RuleMarkers));
        }

        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }
        public bool IsGroup
        {
            get => _isGroup;
            /* Just a flag, no need to notify UI */
            set { _isGroup = value; }
        }
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}