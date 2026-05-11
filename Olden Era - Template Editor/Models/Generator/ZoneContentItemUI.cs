using System.ComponentModel;
using System.Runtime.CompilerServices;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor.Models
{
    public class ZoneContentItemUI : INotifyPropertyChanged
    {
        private SidMapping? _sidMapping;
        private int _count;
        private bool _isGuarded;
        private bool _nearCastle;
        private string? _roadDistance;
        // Flag indicating if this is a single content item, or a group added via includeLists.
        private bool _isGroup;

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

        public bool IsGuarded
        {
            get => _isGuarded;
            set { _isGuarded = value; OnPropertyChanged(); }
        }

        public string? RoadDistance
        {
            get => _roadDistance;
            set { _roadDistance = value; OnPropertyChanged(); }
        }
        public bool NearCastle
        {
            get => _nearCastle;
            set { _nearCastle = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}