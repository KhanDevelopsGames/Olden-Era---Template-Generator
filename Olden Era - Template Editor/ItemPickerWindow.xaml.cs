using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Olden_Era___Template_Editor
{
    public partial class ItemPickerWindow : Window
    {
        public string? SelectedId { get; private set; }

        private readonly List<BanEntry> _allEntries;
        private readonly HashSet<string> _alreadyBanned;
        // Categories that the user has manually collapsed — preserved across searches.
        private readonly HashSet<string> _collapsedCategories = [];

        public ItemPickerWindow(IEnumerable<BanEntry> entries, IEnumerable<string> alreadyBanned, string windowTitle)
        {
            InitializeComponent();
            Title = windowTitle;
            _allEntries    = [.. entries];
            _alreadyBanned = [.. alreadyBanned];
            RefreshTree(string.Empty);
            TxtSearch.Focus();
        }

        // ── Tree building ─────────────────────────────────────────────────────────

        private void RefreshTree(string filter)
        {
            // Remember which categories the user collapsed between refreshes.
            foreach (TreeViewItem ci in TvItems.Items)
                if (ci.Tag is string cat)
                {
                    if (ci.IsExpanded) _collapsedCategories.Remove(cat);
                    else               _collapsedCategories.Add(cat);
                }

            TvItems.Items.Clear();

            var groups = _allEntries
                .Where(e => !_alreadyBanned.Contains(e.Id))
                .Where(e => string.IsNullOrEmpty(filter)
                         || e.DisplayName.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                         || e.Category.Contains(filter,    System.StringComparison.OrdinalIgnoreCase)
                         || e.Id.Contains(filter,          System.StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Category)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var catNode = new TreeViewItem
                {
                    Tag        = group.Key,
                    IsExpanded = !_collapsedCategories.Contains(group.Key),
                    Header     = BuildCategoryHeader(group.Key, group.Count()),
                };

                foreach (var entry in group.OrderBy(e => e.DisplayName))
                    catNode.Items.Add(BuildLeafItem(entry));

                TvItems.Items.Add(catNode);
            }

            BtnAdd.IsEnabled = SelectedLeaf() != null;
        }

        private static FrameworkElement BuildCategoryHeader(string category, int count)
        {
            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text       = category,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xA8, 0x4C)),
                Margin     = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text      = $"({count})",
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8A, 0x6A)),
                FontSize  = 11,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return sp;
        }

        private static TreeViewItem BuildLeafItem(BanEntry entry)
        {
            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new Ellipse
            {
                Width  = 9, Height = 9,
                Fill   = entry.CategoryBrush,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text   = entry.DisplayName,
                Width  = 220,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xD5, 0xA3)),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text       = entry.Id,
                FontFamily = new FontFamily("Consolas"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8A, 0x6A)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            return new TreeViewItem { Header = sp, Tag = entry };
        }

        private BanEntry? SelectedLeaf()
            => TvItems.SelectedItem is TreeViewItem { Tag: BanEntry entry } ? entry : null;

        private void Commit(string id)
        {
            SelectedId   = id;
            DialogResult = true;
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshTree(TxtSearch.Text);

        private void TvItems_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
            => BtnAdd.IsEnabled = SelectedLeaf() != null;

        private void TvItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedLeaf() is BanEntry entry)
                Commit(entry.Id);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedLeaf() is BanEntry entry)
                Commit(entry.Id);
        }

        private void BtnAddCustom_Click(object sender, RoutedEventArgs e)
        {
            var id = TxtCustomId.Text.Trim();
            if (!string.IsNullOrEmpty(id))
                Commit(id);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
