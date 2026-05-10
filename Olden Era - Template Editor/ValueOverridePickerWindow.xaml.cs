using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Olden_Era___Template_Editor
{
    public partial class ValueOverridePickerWindow : Window
    {
        /// <summary>Lines in "sid=guardValue" format to append to the overrides text box.</summary>
        public List<string> ResultLines { get; private set; } = [];

        private readonly HashSet<ListBoxItem> _checkedItems = [];
        private List<string> _filtered = [];

        private static readonly SolidColorBrush CheckedBrush = new(Color.FromRgb(0x5A, 0x4A, 0x28));

        public ValueOverridePickerWindow(IEnumerable<string> alreadyOverridden)
        {
            InitializeComponent();
            var existing = new HashSet<string>(alreadyOverridden);
            _filtered = KnownValues.ObjectSids
                .Where(s => !existing.Contains(s))
                .OrderBy(s => s)
                .ToList();
            LbSids.ItemsSource = _filtered;
            UpdateAddButton();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static TextBlock? GetCheckMark(ListBoxItem lbi)
        {
            if (lbi.ContentTemplate?.LoadContent() is not null) { }
            // Walk visual tree to find the ChkMark TextBlock
            if (VisualTreeHelper.GetChildrenCount(lbi) == 0) return null;
            return FindCheckMark(lbi);
        }

        private static TextBlock? FindCheckMark(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBlock tb && tb.Name == "ChkMark") return tb;
                var found = FindCheckMark(child);
                if (found != null) return found;
            }
            return null;
        }

        private void ToggleItem(ListBoxItem lbi)
        {
            if (_checkedItems.Contains(lbi))
            {
                _checkedItems.Remove(lbi);
                lbi.ClearValue(ListBoxItem.BackgroundProperty);
                if (FindCheckMark(lbi) is { } chk) chk.Text = "";
            }
            else
            {
                _checkedItems.Add(lbi);
                lbi.Background = CheckedBrush;
                if (FindCheckMark(lbi) is { } chk) chk.Text = "✓";
            }
            UpdateAddButton();
        }

        private static ListBoxItem? FindListBoxItem(object originalSource)
        {
            var dep = originalSource as DependencyObject;
            while (dep != null)
            {
                if (dep is ListBoxItem lbi) return lbi;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        private void UpdateAddButton()
        {
            int n = _checkedItems.Count;
            BtnAdd.Content   = n > 1 ? $"Add Selected ({n})" : "Add Selected";
            BtnAdd.IsEnabled = n > 0;
        }

        private void RefreshList(string filter)
        {
            _checkedItems.Clear();
            _filtered = KnownValues.ObjectSids
                .Where(s => string.IsNullOrEmpty(filter)
                         || s.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();
            LbSids.ItemsSource = null;
            LbSids.ItemsSource = _filtered;
            UpdateAddButton();
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => RefreshList(TxtSearch.Text);

        private void LbSids_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var lbi = FindListBoxItem(e.OriginalSource);
            if (lbi == null) return;
            lbi.ApplyTemplate(); // ensure visual tree is materialised
            ToggleItem(lbi);
            e.Handled = true;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtGuardValue.Text.Trim(), out int gv)) gv = 5000;
            ResultLines = _checkedItems
                .Select(lbi => lbi.Content is string sid ? $"{sid}={gv}" : null)
                .Where(l => l != null)
                .Select(l => l!)
                .ToList();
            if (ResultLines.Count > 0)
                DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
