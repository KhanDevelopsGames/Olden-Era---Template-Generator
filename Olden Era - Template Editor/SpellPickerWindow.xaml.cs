using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Olden_Era___Template_Editor
{
    public partial class SpellPickerWindow : Window
    {
        public string?      SelectedId  => SelectedIds.Count > 0 ? SelectedIds[0] : null;
        public List<string> SelectedIds { get; private set; } = [];
        public bool         MakeFree    { get; private set; }

        private readonly HashSet<string>       _alreadyPicked;
        private readonly HashSet<string>       _collapsedSchools = [];
        private readonly HashSet<TreeViewItem> _checkedLeaves    = [];

        private static readonly SolidColorBrush CheckedBrush   = new(Color.FromRgb(0x5A, 0x4A, 0x28));
        private static readonly SolidColorBrush CheckMarkBrush = new(Color.FromRgb(0xC9, 0xA8, 0x4C));

        private static readonly Dictionary<string, Color> SchoolColors = new()
        {
            ["neutral"] = Color.FromRgb(0xA0, 0xA0, 0xA0),
            ["day"]     = Color.FromRgb(0xC8, 0xB8, 0x7A),
            ["night"]   = Color.FromRgb(0x9B, 0x7E, 0xD4),
            ["space"]   = Color.FromRgb(0x7E, 0xC9, 0xD0),
            ["primal"]  = Color.FromRgb(0xD4, 0x7A, 0x48),
        };

        private static readonly Dictionary<string, string> SchoolDisplayNames = new()
        {
            ["neutral"] = "Neutral",
            ["day"]     = "Day",
            ["night"]   = "Night",
            ["space"]   = "Space",
            ["primal"]  = "Primal",
        };

        private static readonly Dictionary<string, int> SchoolOrder = new()
        {
            ["neutral"] = 0, ["day"] = 1, ["night"] = 2, ["space"] = 3, ["primal"] = 4,
        };

        public SpellPickerWindow(IEnumerable<string>? alreadyPicked = null, bool showMakeFree = true)
        {
            InitializeComponent();
            _alreadyPicked = alreadyPicked?.ToHashSet() ?? [];
            if (!showMakeFree)
                ChkMakeFree.Visibility = Visibility.Collapsed;
            RefreshTree(string.Empty);
            TxtSearch.Focus();
        }

        // ── Tree building ─────────────────────────────────────────────────────────

        private void RefreshTree(string filter)
        {
            foreach (TreeViewItem si in TvSpells.Items)
                if (si.Tag is string school)
                {
                    if (si.IsExpanded) _collapsedSchools.Remove(school);
                    else               _collapsedSchools.Add(school);
                }

            _checkedLeaves.Clear();
            TvSpells.Items.Clear();

            var groups = KnownValues.KnownSpells
                .Where(s => !_alreadyPicked.Contains(s.Id))
                .Where(s => string.IsNullOrEmpty(filter)
                         || s.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                         || s.Id.Contains(filter, System.StringComparison.OrdinalIgnoreCase)
                         || s.School.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                .GroupBy(s => s.School)
                .OrderBy(g => SchoolOrder.GetValueOrDefault(g.Key, 99));

            foreach (var group in groups)
            {
                var schoolKey = group.Key;
                var displayName = SchoolDisplayNames.GetValueOrDefault(schoolKey, schoolKey);
                var schoolNode = new TreeViewItem
                {
                    Tag        = schoolKey,
                    IsExpanded = !_collapsedSchools.Contains(schoolKey),
                    Header     = BuildSchoolHeader(schoolKey, displayName, group.Count()),
                };

                foreach (var spell in group.OrderBy(s => s.Tier).ThenBy(s => s.Name))
                    schoolNode.Items.Add(BuildLeafItem(spell));

                TvSpells.Items.Add(schoolNode);
            }

            UpdateAddButton();
        }

        private static FrameworkElement BuildSchoolHeader(string schoolKey, string displayName, int count)
        {
            var color = SchoolColors.GetValueOrDefault(schoolKey, Color.FromRgb(0xC9, 0xA8, 0x4C));
            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text              = displayName,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = new SolidColorBrush(color),
                Margin            = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text              = $"({count})",
                Foreground        = new SolidColorBrush(Color.FromRgb(0x9A, 0x8A, 0x6A)),
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return sp;
        }

        private static TreeViewItem BuildLeafItem(KnownValues.SpellEntry spell)
        {
            var chk = new TextBlock
            {
                Name              = "ChkMark",
                Text              = "",
                Width             = 16,
                Foreground        = CheckMarkBrush,
                FontWeight        = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 4, 0),
            };

            var tier = new TextBlock
            {
                Text              = $"[T{spell.Tier}]",
                Width             = 32,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x9A, 0x8A, 0x6A)),
                FontFamily        = new FontFamily("Consolas"),
                FontSize          = 11,
                Margin            = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var name = new TextBlock
            {
                Text              = spell.Name,
                Foreground        = new SolidColorBrush(Color.FromRgb(0xE8, 0xD5, 0xA3)),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(chk);
            sp.Children.Add(tier);
            sp.Children.Add(name);

            return new TreeViewItem { Header = sp, Tag = spell };
        }

        // ── Check-toggle helpers ──────────────────────────────────────────────────

        private static TextBlock? GetCheckMark(TreeViewItem item)
            => item.Header is StackPanel sp && sp.Children.Count > 0
                ? sp.Children[0] as TextBlock
                : null;

        private void ToggleLeaf(TreeViewItem item)
        {
            if (_checkedLeaves.Contains(item))
            {
                _checkedLeaves.Remove(item);
                item.ClearValue(TreeViewItem.BackgroundProperty);
                if (GetCheckMark(item) is { } chk) chk.Text = "";
            }
            else
            {
                _checkedLeaves.Add(item);
                item.Background = CheckedBrush;
                if (GetCheckMark(item) is { } chk) chk.Text = "✓";
            }
            UpdateAddButton();
        }

        private void UpdateAddButton()
        {
            int n = _checkedLeaves.Count;
            BtnAdd.Content   = n > 1 ? $"Add Selected ({n})" : "Add Selected";
            BtnAdd.IsEnabled = n > 0;
        }

        private static TreeViewItem? FindLeafFromSource(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is TreeViewItem tvi && tvi.Tag is KnownValues.SpellEntry)
                    return tvi;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshTree(TxtSearch.Text);

        private void TvSpells_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var leaf = FindLeafFromSource(e.OriginalSource);
            if (leaf == null) return;
            ToggleLeaf(leaf);
            e.Handled = true;
        }

        private void TvSpells_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var leaf = FindLeafFromSource(e.OriginalSource);
            if (leaf == null) return;
            if (!_checkedLeaves.Contains(leaf)) ToggleLeaf(leaf);
            CommitAll();
        }

        private void CommitAll()
        {
            SelectedIds = _checkedLeaves
                .Select(tvi => ((KnownValues.SpellEntry)tvi.Tag!).Id)
                .ToList();
            MakeFree = ChkMakeFree.IsChecked == true;
            if (SelectedIds.Count > 0)
                DialogResult = true;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
            => CommitAll();

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
