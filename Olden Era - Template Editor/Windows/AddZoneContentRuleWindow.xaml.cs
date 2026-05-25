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
        private readonly List<IContentRule> _appliedRules;
        public IContentRule? CreatedRule { get; private set; }

        private IContentRule? GetSelectedRule()
        {
            int selectedIndex = CmbRuleType.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _contentRulePresets.Length)
                return null;

            return _contentRulePresets[selectedIndex];
        }

        private IContentRule[] _contentRulePresets = ContentRuleManager.GetRules();

        public AddZoneContentRuleWindow(SidMapping contentItem, List<IContentRule>? appliedRules = null)
        {
            InitializeComponent();

            _contentItem = contentItem;
            _appliedRules = appliedRules ?? new List<IContentRule>();

            CmbRuleType.ItemsSource = _contentRulePresets.Select(rule => rule.Name);
            CmbVariant.DisplayMemberPath = nameof(VariantMapping.DisplayText);

            CmbDistance.ItemsSource = DistancePresets.GetDisplayNames();
            CmbDistance.SelectedIndex = 0;

            CmbRuleType.SelectedIndex = 0;
            UpdateRuleSpecificControls();
        }

        public AddZoneContentRuleWindow(SidMapping contentItem, IContentRule existingRule, List<IContentRule>? appliedRules = null)
            : this(contentItem, appliedRules)
        {
            TxtWindowTitle.Text = "Edit rule";
            PrepopulateFromRule(existingRule);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /* Builds a new content rule based on the selected rule and user input. */
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var selectedRule = GetSelectedRule();
            if (selectedRule is null)
                return;
            
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
                    VariantMapping? selectedVariant = GetSelectedVariantMapping();
                    if (selectedVariant is null)
                        return;

                    CreatedRule = new RuleVariant(selectedVariant);
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

        private IContentRule? GetAppliedRuleOfSelectedType()
        {
            var selectedRule = GetSelectedRule();
            if (selectedRule is null)
                return null;

            return _appliedRules.FirstOrDefault(rule => rule.GetType() == selectedRule.GetType());
        }

        private VariantMapping? GetSelectedVariantMapping()
            => CmbVariant.SelectedItem as VariantMapping;

        private void PrepopulateFromRule(IContentRule existingRule)
        {
            int selectedIndex = Array.FindIndex(_contentRulePresets, rule => rule.GetType() == existingRule.GetType());
            if (selectedIndex >= 0)
            {
                CmbRuleType.SelectedIndex = selectedIndex;
            }

            switch (existingRule)
            {
                case RuleDistanceToRoad roadRule:
                    CmbDistance.SelectedItem = roadRule.Value.distanceVariation.Name;
                    break;
                case RuleDistanceToTown townRule:
                    CmbDistance.SelectedItem = townRule.Value.distanceVariation.Name;
                    break;
                case RuleGuarded guardedRule:
                    ChkGuarded.IsChecked = guardedRule.Value.isGuarded;
                    break;
                case RuleVariant variantRule:
                    if (CmbVariant.ItemsSource is IEnumerable<VariantMapping> variants)
                    {
                        VariantMapping? matchingVariant = variants
                            .FirstOrDefault(variant => variant.variants.ContainsKey(variantRule.Value.variantId));

                        if (matchingVariant is not null)
                        {
                            CmbVariant.SelectedItem = matchingVariant;
                        }
                    }
                    break;
            }
        }

        /* Handle rule-specific control visibility and updates */
        private void UpdateRuleSpecificControls()
        {
            var selectedRule = GetSelectedRule();
            if (selectedRule is null)
                return;

            if (FindName("TxtRuleDescription") is System.Windows.Controls.TextBlock ruleDescription)
                ruleDescription.Text = selectedRule.Description;

            /* Controls visibility of controls per rule */
            PnlDistance.Visibility = (selectedRule is RuleDistanceToRoad || selectedRule is RuleDistanceToTown) ? Visibility.Visible : Visibility.Collapsed;
            PnlGuarded.Visibility = (selectedRule is RuleGuarded) ? Visibility.Visible : Visibility.Collapsed;
            PnlVariant.Visibility = (selectedRule is RuleVariant) ? Visibility.Visible : Visibility.Collapsed;

            if (selectedRule is RuleVariant)
            {
                RefreshVariantOptions();
            }

            IContentRule? existingRule = GetAppliedRuleOfSelectedType();
            if (existingRule is not null)
            {
                TxtWindowTitle.Text = "Edit rule";
                PrepopulateFromRule(existingRule);
            }
            else
            {
                TxtWindowTitle.Text = "Add rule";
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
        private List<VariantMapping> GetVariantValuesForParent()
            => VariantMappingManager.GetVariantsForContent(_contentItem);

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
