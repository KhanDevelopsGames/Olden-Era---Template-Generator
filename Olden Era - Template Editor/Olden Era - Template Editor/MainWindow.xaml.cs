using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class MainWindow : Window
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public MainWindow()
        {
            InitializeComponent();
            CmbGameMode.ItemsSource = KnownValues.GameModes;
            CmbGameMode.SelectedIndex = 0;
            CmbMapSize.ItemsSource = KnownValues.MapSizes.Select(s => $"{s}x{s}").ToList();
            CmbMapSize.SelectedItem = "160x160";
            CmbVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            CmbVictory.SelectedIndex = 2; // Hold City (win_condition_5)
        }

        // Keep value labels in sync with slider positions.
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;

            TxtPlayers.Text = ((int)SldPlayers.Value).ToString();
            TxtHeroMin.Text = ((int)SldHeroMin.Value).ToString();
            TxtHeroMax.Text = ((int)SldHeroMax.Value).ToString();
            TxtHeroIncrement.Text = ((int)SldHeroIncrement.Value).ToString();
            TxtNeutral.Text = ((int)SldNeutral.Value).ToString();
            TxtPlayerCastles.Text = ((int)SldPlayerCastles.Value).ToString();
            TxtNeutralCastles.Text = ((int)SldNeutralCastles.Value).ToString();

            Validate();
        }

        private bool Validate()
        {
            int heroMin = (int)SldHeroMin.Value;
            int heroMax = (int)SldHeroMax.Value;
            int players = (int)SldPlayers.Value;
            int neutral = (int)SldNeutral.Value;

            if (heroMin > heroMax)
            {
                TxtValidation.Text = "Min Heroes cannot be greater than Max Heroes.";
                BtnGenerate.IsEnabled = false;
                return false;
            }

            if (players + neutral > 16)
            {
                TxtValidation.Text = "Total zones (players + neutral) cannot exceed 16.";
                BtnGenerate.IsEnabled = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtTemplateName.Text))
            {
                TxtValidation.Text = "Template name cannot be empty.";
                BtnGenerate.IsEnabled = false;
                return false;
            }

            var warnings = new System.Collections.Generic.List<string>();

            if (players + neutral > 4 && CmbMapSize.SelectedItem is string selStr && int.TryParse(selStr.Split('x')[0], out int selectedSize) && selectedSize < 160)
                warnings.Add("⚠️ Using a small map with many zones may freeze the game while loading the map. Consider using a larger map size.");

            if (ChkNoDirectPlayerConn.IsChecked == true && neutral < players && players > 1)
                warnings.Add($"⚠️ \"No player zones connected\" requires at least {players} neutral zones to separate each player. The generator will automatically create them.");

            TxtValidation.Text = string.Join("\n\n", warnings);

            BtnGenerate.IsEnabled = true;
            return true;
        }

        private void CmbMapSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            Validate();
        }

        private void ChkOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            Validate();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate()) return;

            var settings = new GeneratorSettings
            {
                TemplateName = TxtTemplateName.Text.Trim(),
                GameMode = CmbGameMode.SelectedItem as string ?? "Classic",
                PlayerCount = (int)SldPlayers.Value,
                HeroCountMin = (int)SldHeroMin.Value,
                HeroCountMax = (int)SldHeroMax.Value,
                HeroCountIncrement = (int)SldHeroIncrement.Value,
                NeutralZoneCount = (int)SldNeutral.Value,
                MapSize = CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsedSize) ? parsedSize : 160,
                VictoryCondition = KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex],
                PlayerZoneCastles = (int)SldPlayerCastles.Value,
                NeutralZoneCastles = (int)SldNeutralCastles.Value,
                NoDirectPlayerConnections = ChkNoDirectPlayerConn.IsChecked == true,
                RandomPortals = ChkRandomPortals.IsChecked == true,
                SpawnRemoteFootholds = ChkSpawnFootholds.IsChecked == true
            };

            var template = TemplateGenerator.Generate(settings);

            var dlg = new SaveFileDialog
            {
                Title = "Save Template",
                Filter = "RMG Template (*.rmg.json)|*.rmg.json",
                FileName = $"{settings.TemplateName}.rmg.json"
            };

            if (dlg.ShowDialog() != true) return;

            string json = JsonSerializer.Serialize(template, JsonOptions);
            File.WriteAllText(dlg.FileName, json);

            MessageBox.Show($"Template saved to:\n{dlg.FileName}",
                "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
