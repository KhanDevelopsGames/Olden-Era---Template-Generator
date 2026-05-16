using System.Collections.ObjectModel;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace OldenEraTemplateEditor.Services.ContentManagement
{

public class ZoneMandatoryContent
{
        public readonly ObservableCollection<ZoneContentItemUI> mines = new();
        public readonly ObservableCollection<ZoneContentItemUI> treasures = new();
        public readonly ObservableCollection<ZoneContentItemUI> unitRecruitment = new();
        public readonly ObservableCollection<ZoneContentItemUI> resourceBanks = new();
        public readonly ObservableCollection<ZoneContentItemUI> utilityStructures = new();
        public readonly ObservableCollection<ZoneContentItemUI> heroImprovementStructures = new();
        private IEnumerable<ObservableCollection<ZoneContentItemUI>> Collections
        {
            get
            {
                yield return mines;
                yield return treasures;
                yield return unitRecruitment;
                yield return resourceBanks;
                yield return utilityStructures;
                yield return heroImprovementStructures;
            }
        }

        public IEnumerable<ZoneContentItemUI> AllItems => Collections.SelectMany(collection => collection);

        /* Removes the specified item from any of the collections. Returns true if the item was found and removed, false otherwise. */
        public bool Remove(ZoneContentItemUI item)
        {
            return Collections.Any(collection => collection.Remove(item));
        }

        public void Clear()
        {
            foreach (var collection in Collections)
            {
                collection.Clear();
            }
        }
}

}