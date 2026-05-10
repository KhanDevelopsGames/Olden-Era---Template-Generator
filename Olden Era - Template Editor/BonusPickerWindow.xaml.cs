using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class BonusPickerWindow : Window
    {
        public BonusEntry?       Result  { get; private set; }
        public List<BonusEntry>  Results { get; private set; } = [];

        private readonly HashSet<string> _existingKeys;
        private readonly HashSet<string> _existingItemIds;

        public BonusPickerWindow(IEnumerable<BonusEntry>? existingBonuses = null)
        {
            InitializeComponent();
            CmbType.SelectedIndex     = 0;
            CmbReceiver.SelectedIndex = 0;
            CmbSpell.SelectedIndex    = 0;

            var existing = existingBonuses?.ToList() ?? [];
            _existingKeys    = existing.Select(b => b.ToString()).ToHashSet();
            _existingItemIds = existing
                .Where(b => b.PresetType == BonusPresetType.StartingItem)
                .Select(b => b.Param)
                .ToHashSet();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private BonusPresetType SelectedType =>
            (BonusPresetType)int.Parse((string)((ComboBoxItem)CmbType.SelectedItem).Tag);

        private string SelectedReceiver =>
            (string)((ComboBoxItem)CmbReceiver.SelectedItem).Tag;

        // ── Event handlers ────────────────────────────────────────────────────────

        private void CmbType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            var type = SelectedType;

            PnlTownPortal.Visibility = type == BonusPresetType.TownPortalFree    ? Visibility.Visible : Visibility.Collapsed;
            PnlSpell.Visibility      = type == BonusPresetType.Spell              ? Visibility.Visible : Visibility.Collapsed;
            PnlMultiplier.Visibility = type == BonusPresetType.UnitMultiplier     ? Visibility.Visible : Visibility.Collapsed;
            PnlMovement.Visibility   = type == BonusPresetType.MovementBonus      ? Visibility.Visible : Visibility.Collapsed;
            PnlItem.Visibility       = type == BonusPresetType.StartingItem       ? Visibility.Visible : Visibility.Collapsed;
            PnlResources.Visibility  = type is BonusPresetType.StartingGold
                                           or BonusPresetType.StartingGems
                                           or BonusPresetType.StartingCrystals
                                           or BonusPresetType.StartingMercury
                                       ? Visibility.Visible : Visibility.Collapsed;

            if (PnlResources.Visibility == Visibility.Visible)
            {
                (LblResourceAmount.Text, TxtResourceAmount.Text) = type switch
                {
                    BonusPresetType.StartingGold     => ("Gold amount",     "10000"),
                    BonusPresetType.StartingGems     => ("Gems amount",     "15"),
                    BonusPresetType.StartingCrystals => ("Crystals amount", "15"),
                    BonusPresetType.StartingMercury  => ("Mercury amount",  "15"),
                    _                                => ("Amount",          "10000"),
                };
            }
        }

        private void CmbSpell_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            var tag = (string)((ComboBoxItem)CmbSpell.SelectedItem).Tag;
            TxtSpellCustom.Visibility = string.IsNullOrEmpty(tag) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnPickItem_Click(object sender, RoutedEventArgs e)
        {
            var entries = KnownValues.BannableItems
                .Select(b => new BanEntry { Id = b.Id, DisplayName = b.DisplayName, Category = b.Category });
            var picker = new ItemPickerWindow(entries, _existingItemIds, "Pick Starting Item") { Owner = this };
            if (picker.ShowDialog() != true) return;

            if (picker.SelectedIds.Count > 1)
            {
                // Multi-selection: build one StartingItem bonus per artifact and close immediately
                var receiver = SelectedReceiver;
                Results = picker.SelectedIds
                    .Select(id => new BonusEntry
                    {
                        PresetType     = BonusPresetType.StartingItem,
                        ReceiverFilter = receiver,
                        Param          = id,
                    })
                    .ToList();
                DialogResult = true;
            }
            else if (picker.SelectedIds.Count == 1)
            {
                TxtItem.Text = picker.SelectedIds[0];
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var type     = SelectedType;
            var receiver = SelectedReceiver;
            string param  = "";
            string param2 = "0";

            switch (type)
            {
                case BonusPresetType.TownPortalFree:
                    break;

                case BonusPresetType.Spell:
                    var spellTag = (string)((ComboBoxItem)CmbSpell.SelectedItem).Tag;
                    param = string.IsNullOrEmpty(spellTag) ? TxtSpellCustom.Text.Trim() : spellTag;
                    if (string.IsNullOrEmpty(param))
                    {
                        MessageBox.Show("Enter a spell ID.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    param2 = ChkMakeFree.IsChecked == true ? "1" : "0";
                    break;

                case BonusPresetType.UnitMultiplier:
                    param = TxtMultiplier.Text.Trim();
                    if (string.IsNullOrEmpty(param)) param = "2";
                    break;

                case BonusPresetType.MovementBonus:
                    param = TxtMovement.Text.Trim();
                    if (string.IsNullOrEmpty(param)) param = "0";
                    break;

                case BonusPresetType.StartingItem:
                    param = TxtItem.Text.Trim();
                    if (string.IsNullOrEmpty(param))
                    {
                        MessageBox.Show("Enter or pick an artifact ID.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    break;

                default: // StartingGold, StartingGems, StartingCrystals, StartingMercury
                    param = TxtResourceAmount.Text.Trim();
                    if (string.IsNullOrEmpty(param)) param = "0";
                    break;
            }

            var candidate = new BonusEntry
            {
                PresetType     = type,
                ReceiverFilter = receiver,
                Param          = param,
                Param2         = param2,
            };

            if (_existingKeys.Contains(candidate.ToString()))
            {
                MessageBox.Show("This bonus has already been added.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Result       = candidate;
            Results      = [Result];
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
