using OldenEraTemplateEditor.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class BonusPickerWindow : Window
    {
        public BonusEntry? Result { get; private set; }

        public BonusPickerWindow()
        {
            InitializeComponent();
            CmbType.SelectedIndex     = 0;
            CmbReceiver.SelectedIndex = 0;
            CmbSpell.SelectedIndex    = 0;
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
            var picker = new ItemPickerWindow(entries, [], "Pick Starting Item") { Owner = this };
            if (picker.ShowDialog() == true && picker.SelectedId is { } id)
                TxtItem.Text = id;
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

            Result = new BonusEntry
            {
                PresetType     = type,
                ReceiverFilter = receiver,
                Param          = param,
                Param2         = param2,
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
