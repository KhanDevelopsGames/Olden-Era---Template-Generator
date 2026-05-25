using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using OldenEraTemplateEditor.Services.ContentManagement;


namespace Olden_Era___Template_Editor
{
    public partial class ManageZoneContentRulesWindow : Window
    {
        private readonly List<IContentRule> _rules;
        private SidMapping _sidMapping;

        public ManageZoneContentRulesWindow(SidMapping contentItem, List<IContentRule> appliedRules)
        {
            InitializeComponent();
            _sidMapping = contentItem;
            TxtManagingRulesFor.Text = $"Managing rules for: {_sidMapping.Name}";
            _rules = appliedRules;
            LbRules.DisplayMemberPath = nameof(IContentRule.DisplayName);
            LbRules.ItemsSource = _rules;
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var addRuleWindow = new AddZoneContentRuleWindow(_sidMapping, _rules)
            {
                Owner = this
            };

            if (addRuleWindow.ShowDialog() == true && addRuleWindow.CreatedRule != null)
            {
                int existingIndex = _rules.FindIndex(rule => rule.GetType() == addRuleWindow.CreatedRule.GetType());
                if (existingIndex >= 0)
                {
                    _rules[existingIndex] = addRuleWindow.CreatedRule;
                }
                else
                {
                    _rules.Add(addRuleWindow.CreatedRule);
                }
                LbRules.Items.Refresh();
            }
        }

        private void LbRules_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LbRules.SelectedItem is not IContentRule selectedRule)
                return;

            int selectedIndex = _rules.IndexOf(selectedRule);
            if (selectedIndex < 0)
                return;

            var editRuleWindow = new AddZoneContentRuleWindow(_sidMapping, selectedRule, _rules)
            {
                Owner = this
            };

            if (editRuleWindow.ShowDialog() == true && editRuleWindow.CreatedRule != null)
            {
                /* Handle rule update logic, to ensure there is only one rule of each type */
                int existingTypeIndex = _rules.FindIndex(rule => rule.GetType() == editRuleWindow.CreatedRule.GetType());

                if (existingTypeIndex >= 0 && existingTypeIndex != selectedIndex)
                {
                    // Example scenario: Editing C into existing A/B: update A/B and remove original C.
                    _rules[existingTypeIndex] = editRuleWindow.CreatedRule;
                    _rules.RemoveAt(selectedIndex);

                    int finalIndex = selectedIndex < existingTypeIndex
                        ? existingTypeIndex - 1
                        : existingTypeIndex;
                    LbRules.SelectedIndex = finalIndex;
                }
                else
                {
                    _rules[selectedIndex] = editRuleWindow.CreatedRule;
                    LbRules.SelectedIndex = selectedIndex;
                }

                LbRules.Items.Refresh();
            }
        }

        private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (LbRules.SelectedItem is IContentRule selectedRule)
            {
                _rules.Remove(selectedRule);
                LbRules.Items.Refresh();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
