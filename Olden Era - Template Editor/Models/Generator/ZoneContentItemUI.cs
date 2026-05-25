using System.ComponentModel;
using System.Runtime.CompilerServices;
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
            set { _sidMapping = value; OnPropertyChanged(); }
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