using System.Windows;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor
{
    public partial class AddZoneContentRuleWindow : Window
    {
        private readonly SidMapping _contentItem;
        public IContentRule? CreatedRule { get; private set; }

        private ref readonly IContentRule GetSelectedRule()
        {
            return ref _contentRulePresets[CmbRuleType.SelectedIndex];
        }

        private IContentRule[] _contentRulePresets = ContentRuleManager.GetRules();

        public AddZoneContentRuleWindow(SidMapping contentItem)
        {
            InitializeComponent();

            _contentItem = contentItem;

            CmbRuleType.ItemsSource = _contentRulePresets.Select(rule => rule.Name);

            CmbDistance.ItemsSource = DistancePresets.GetDisplayNames();
            CmbDistance.SelectedIndex = 0;

            CmbRuleType.SelectedIndex = 0;
            UpdateRuleSpecificControls();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /* Builds a new content rule based on the selected rule and user input. */
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var selectedRule = GetSelectedRule();
            
            switch(selectedRule)
            {
                case RuleDistanceToRoad:
                    CreatedRule = new RuleDistanceToRoad(DistancePresets.GetDistanceVariationByName(CmbDistance.SelectedItem as string));
                    break;
                case RuleDistanceToTown:
                    CreatedRule = new RuleDistanceToTown(DistancePresets.GetDistanceVariationByName(CmbDistance.SelectedItem as string));
                    break;
                case RuleGuarded:
                    CreatedRule = new RuleGuarded(ChkGuarded.IsChecked ?? false);
                    break;
                case RuleVariant:
                    CreatedRule = new RuleVariant(CmbVariant.SelectedItem is int variantId ? variantId : 0);
                    break;
                default:
                    // We never should reach this state. (assuming the UI only allows valid rules to be added).
                    break;
            }
            DialogResult = true;
            Close();
        }

        private void CmbRuleType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateRuleSpecificControls();
        }

        /* Handle rule-specific control visibility and updates */
        private void UpdateRuleSpecificControls()
        {
            var selectedRule = GetSelectedRule();
            if (FindName("TxtRuleDescription") is System.Windows.Controls.TextBlock ruleDescription)
                ruleDescription.Text = selectedRule?.Description ?? string.Empty;

            /* Controls visibility of controls per rule */
            PnlDistance.Visibility = (selectedRule is RuleDistanceToRoad || selectedRule is RuleDistanceToTown) ? Visibility.Visible : Visibility.Collapsed;
            PnlGuarded.Visibility = (selectedRule is RuleGuarded) ? Visibility.Visible : Visibility.Collapsed;
            PnlVariant.Visibility = (selectedRule is RuleVariant) ? Visibility.Visible : Visibility.Collapsed;

            if (selectedRule is RuleVariant)
            {
                RefreshVariantOptions();
            }
        }
        /* Variants differ between content items. Refresh the options based on the parent content item. */
        private void RefreshVariantOptions()
        {
            var variants = GetVariantValuesForParent();

            CmbVariant.ItemsSource = variants;
            CmbVariant.IsEnabled = variants.Count > 0;
            CmbVariant.Visibility = variants.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtVariantEmpty.Visibility = variants.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtVariantEmpty.Text = $"No known variants for {_contentItem.Name}";

            if (variants.Count > 0)
            {
                CmbVariant.SelectedIndex = 0;
            }
        }
        /* Function to handle possible variant values for the content item. */
        private List<int> GetVariantValuesForParent()
        {
            if(_contentItem.Sid == ContentIds.MineWood.Sid)
            {
                return new List<int>();
            }
            return new List<int> { 0, 1, 2 };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
