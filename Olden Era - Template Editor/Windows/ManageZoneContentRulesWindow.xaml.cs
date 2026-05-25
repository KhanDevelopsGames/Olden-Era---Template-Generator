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
            var addRuleWindow = new AddZoneContentRuleWindow(_sidMapping)
            {
                Owner = this
            };

            if (addRuleWindow.ShowDialog() == true && addRuleWindow.CreatedRule != null)
            {
                _rules.Add(addRuleWindow.CreatedRule);
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
