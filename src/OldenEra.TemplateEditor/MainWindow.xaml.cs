using Microsoft.Win32;
using OldenEra.Generator.Constants;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.TemplateEditor.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace OldenEra.TemplateEditor
{
    public partial class MainWindow : Window
    {
        private const string GitHubApiLatestRelease = "https://api.github.com/repos/KhanDevelopsGames/Olden-Era---Template-Generator/releases/latest";
        private const string GitHubReleasesPage     = "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases";
        private const int SimpleModeMaxZones = 32;
        private const int AdvancedModeMaxZones = 32;

        private static readonly HttpClient Http = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Currently open settings file path (null = unsaved / untitled)
        private string? _currentSettingsPath = null;
        private bool _isDirty = false;
        private bool _isRefreshingMapSizes = false;
        private string _baseTitle = string.Empty;

        private static readonly (MapTopology Topology, string Label, string Description)[] TopologyOptions =
        [
            (MapTopology.Random,      "Random",        "Zones are placed at random positions. Each zone connects to all zones that border it — no fixed structure."),
            (MapTopology.Default,     "Ring",          "All zones are arranged in a circle. Each zone connects to the two zones next to it."),
            (MapTopology.HubAndSpoke, "Hub",   "All zones connect to a shared central hub. Players never border each other directly."),
            (MapTopology.Chain,       "Chain",         "Zones are connected in a straight line from one end to the other, with no wrap-around.")
            ];

        public MainWindow()
        {
            InitializeComponent();

            // Re-wire events for controls now hosted in MapPanel UserControl.
            PnlMap.CmbMapSize.SelectionChanged += CmbMapSize_SelectionChanged;
            PnlMap.ChkExperimentalMapSizes.Checked += ChkExperimentalMapSizes_Changed;
            PnlMap.ChkExperimentalMapSizes.Unchecked += ChkExperimentalMapSizes_Changed;
            PnlMap.SldPlayers.ValueChanged += Slider_ValueChanged;

            // Re-wire events for controls now hosted in HeroesPanel UserControl.
            PnlHeroes.SldHeroMin.ValueChanged += Slider_ValueChanged;
            PnlHeroes.SldHeroMax.ValueChanged += Slider_ValueChanged;
            PnlHeroes.SldHeroIncrement.ValueChanged += Slider_ValueChanged;

            // Re-wire events for controls now hosted in TopologyPanel UserControl.
            PnlTopology.CmbTopology.SelectionChanged += CmbTopology_SelectionChanged;
            PnlTopology.SldHubZoneSize.ValueChanged += Slider_ValueChanged;
            PnlTopology.SldHubCastles.ValueChanged += Slider_ValueChanged;

            // Zones panel — slider events.
            PnlZones.SldNeutral.ValueChanged += Slider_ValueChanged;
            PnlZones.SldPlayerCastles.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralCastles.ValueChanged += Slider_ValueChanged;
            PnlZones.SldResourceDensity.ValueChanged += Slider_ValueChanged;
            PnlZones.SldStructureDensity.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralStackStrength.ValueChanged += Slider_ValueChanged;
            PnlZones.SldBorderGuardStrength.ValueChanged += Slider_ValueChanged;
            PnlZones.SldPlayerZoneSize.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralZoneSize.ValueChanged += Slider_ValueChanged;
            PnlZones.SldGuardRandomization.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralLowNoCastle.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralLowCastle.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralMediumNoCastle.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralMediumCastle.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralHighNoCastle.ValueChanged += Slider_ValueChanged;
            PnlZones.SldNeutralHighCastle.ValueChanged += Slider_ValueChanged;
            PnlZones.SldMinNeutralBetweenPlayers.ValueChanged += Slider_ValueChanged;
            PnlZones.SldMaxPortals.ValueChanged += SldMaxPortals_ValueChanged;

            // Zones panel — checkbox events.
            PnlZones.ChkAdvancedZoneSettings.Checked   += BtnAdvancedZoneSettings_Click;
            PnlZones.ChkAdvancedZoneSettings.Unchecked += BtnAdvancedZoneSettings_Click;
            PnlZones.ChkMatchPlayerCastleFactions.Checked   += ChkOption_Changed;
            PnlZones.ChkMatchPlayerCastleFactions.Unchecked += ChkOption_Changed;
            PnlZones.ChkGenerateRoads.Checked   += ChkOption_Changed;
            PnlZones.ChkGenerateRoads.Unchecked += ChkOption_Changed;
            PnlZones.ChkSpawnFootholds.Checked   += ChkOption_Changed;
            PnlZones.ChkSpawnFootholds.Unchecked += ChkOption_Changed;
            PnlZones.ChkBalancedZonePlacement.Checked   += ChkOption_Changed;
            PnlZones.ChkBalancedZonePlacement.Unchecked += ChkOption_Changed;
            PnlZones.ChkNoDirectPlayerConn.Checked   += ChkOption_Changed;
            PnlZones.ChkNoDirectPlayerConn.Unchecked += ChkOption_Changed;
            PnlZones.ChkRandomPortals.Checked   += ChkRandomPortals_Changed;
            PnlZones.ChkRandomPortals.Unchecked += ChkRandomPortals_Changed;

            // Game Rules panel — combo + slider events.
            PnlGameRules.CmbVictory.SelectionChanged += CmbVictory_SelectionChanged;
            PnlGameRules.SldFactionLawsExp.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldAstrologyExp.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldLostStartCityDay.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldCityHoldDays.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldGladiatorDelay.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldGladiatorCountDay.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldTournamentPointsToWin.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldTournamentFirstTournamentDay.ValueChanged += Slider_ValueChanged;
            PnlGameRules.SldTournamentInterval.ValueChanged += Slider_ValueChanged;

            // Game Rules panel — checkbox events.
            PnlGameRules.ChkLostStartCity.Checked   += WinConditionOption_Changed;
            PnlGameRules.ChkLostStartCity.Unchecked += WinConditionOption_Changed;
            PnlGameRules.ChkLostStartHero.Checked   += WinConditionOption_Changed;
            PnlGameRules.ChkLostStartHero.Unchecked += WinConditionOption_Changed;
            PnlGameRules.ChkCityHold.Checked   += WinConditionOption_Changed;
            PnlGameRules.ChkCityHold.Unchecked += WinConditionOption_Changed;
            PnlGameRules.ChkGladiatorArena.Checked   += WinConditionOption_Changed;
            PnlGameRules.ChkGladiatorArena.Unchecked += WinConditionOption_Changed;
            PnlGameRules.ChkTournament.Checked   += WinConditionOption_Changed;
            PnlGameRules.ChkTournament.Unchecked += WinConditionOption_Changed;
            PnlGameRules.ChkTournamentSaveArmy.Checked   += ChkOption_Changed;
            PnlGameRules.ChkTournamentSaveArmy.Unchecked += ChkOption_Changed;

            // Clamp startup size to the available work area so the window never
            // overflows the screen at high-DPI scaling (e.g. 125 %, 150 %, 200 %).
            var area = SystemParameters.WorkArea;
            if (Height > area.Height) { Height = area.Height; MinHeight = area.Height; }
            if (Width  > area.Width)  { Width  = area.Width;  MinWidth  = area.Width;  }

            // Stamp version from assembly metadata into all visible locations.
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string versionLabel = version != null ? FormatVersion(version) : "v?";
            _baseTitle = $"Olden Era - Simple Template Generator {versionLabel}";
            TxtAppTitle.Text = $"Olden Era - Simple Template Generator  {versionLabel}";
            TxtWipWarning.Text = $"⚠️ Work in progress — Some generated templates may contain game-breaking bugs or issues.";

            CmbGameMode.ItemsSource = KnownValues.GameModes;
            CmbGameMode.SelectedIndex = 0;
            RefreshMapSizeOptions(160);
            PnlGameRules.CmbVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            PnlGameRules.CmbVictory.SelectedIndex = 0; // Classic (win_condition_1)
            PnlTopology.CmbTopology.ItemsSource = TopologyOptions.Select(t => t.Label).ToList();
            PnlTopology.CmbTopology.SelectedIndex = 0; // Random is first
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateRoadsHintVisibility();
            UpdateBalancedZonePlacementDescVisibility();

            // Fire-and-forget background update check — never blocks the UI.
            _ = CheckForUpdateAsync(version);

            PnlMap.TxtTemplateName.TextChanged += (_, _) => { MarkDirtyNameOnly(); Validate(); };
            UpdateTitle();
            TxtWindowTitle.Text = Title;
        }

        private async Task CheckForUpdateAsync(Version? currentVersion)
        {
            try
            {
                Http.DefaultRequestHeaders.UserAgent.Clear();
                Http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("OldenEraTemplateGenerator", currentVersion?.ToString() ?? "0"));

                using var response = await Http.GetAsync(GitHubApiLatestRelease);
                if (!response.IsSuccessStatusCode) return;

                using var stream = await response.Content.ReadAsStreamAsync();
                var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);
                if (release?.TagName == null) return;

                // Tag expected format: "v1.2", "1.2", or "v1.2.3" — parse major.minor[.build].
                string tag = release.TagName.TrimStart('v');
                if (!Version.TryParse(tag, out Version? latestVersion)) return;
                if (currentVersion == null || latestVersion <= currentVersion) return;

                // A newer version exists — prompt on the UI thread.
                Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        $"A new version is available: {FormatVersion(latestVersion)}\n" +
                        $"You are running: {FormatVersion(currentVersion)}\n\n" +
                        "Open the releases page to download the update?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GitHubReleasesPage) { UseShellExecute = true });
                });
            }
            catch { /* Network unavailable or API error — silently ignore. */ }
        }

        // Formats a Version as "vMajor.Minor" or "vMajor.Minor.Build" when build > 0.
        private static string FormatVersion(Version v)
            => v.Build > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}" : $"v{v.Major}.{v.Minor}";

        private void LinkWebVersion_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // Minimal model for GitHub releases API response.
        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }
        }

        private void MarkDirty()
        {
            if (!IsInitialized) return;
            _isDirty = true;
            if (_generatedTemplate is not null)
                _templateOutdated = true;
            UpdateOutdatedWarning();
            UpdateTitle();
        }

        private void MarkDirtyNameOnly()
        {
            if (!IsInitialized) return;
            _isDirty = true;
            if (_generatedTemplate is not null)
                _generatedTemplate.Name = PnlMap.TxtTemplateName.Text.Trim();
            UpdateTitle();
        }

        private void UpdateOutdatedWarning()
        {
            if (TxtOutdatedWarning == null) return;
            bool outdated = _templateOutdated && _generatedTemplate is not null;
            TxtOutdatedWarning.Visibility = outdated ? Visibility.Visible : Visibility.Hidden;
            if (BtnSaveGenerated != null)
                BtnSaveGenerated.IsEnabled = _generatedTemplate is not null && !outdated;
        }

        private void UpdateTitle()
        {
            string file = _currentSettingsPath is not null
                ? System.IO.Path.GetFileName(_currentSettingsPath)
                : "Untitled";
            string full = _isDirty
                ? $"{_baseTitle}  —  {file}*"
                : $"{_baseTitle}  —  {file}";
            Title = full;
            if (IsInitialized) TxtWindowTitle.Text = full;
        }

        private void LstNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PnlMap is null || PnlHeroes is null || PnlTopology is null
                || PnlZones is null || PnlGameRules is null || PnlExperimental is null) return;
            if (LstNav.SelectedItem is not ListBoxItem item) return;

            var tag = item.Tag as string;
            PnlMap.Visibility          = tag == "Map"           ? Visibility.Visible : Visibility.Collapsed;
            PnlHeroes.Visibility       = tag == "Heroes"        ? Visibility.Visible : Visibility.Collapsed;
            PnlTopology.Visibility     = tag == "Topology"      ? Visibility.Visible : Visibility.Collapsed;
            PnlZones.Visibility        = tag == "Zones"         ? Visibility.Visible : Visibility.Collapsed;
            PnlGameRules.Visibility    = tag == "WinConditions" ? Visibility.Visible : Visibility.Collapsed;
            PnlExperimental.Visibility = tag == "Experimental"  ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ChkExperimentalEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (LstNavExperimental is null) return;
            bool on = ChkExperimentalEnabled.IsChecked == true;
            LstNavExperimental.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            // If the user just turned it off while sitting on the Experimental
            // tab, bounce them back to Map.
            if (!on && LstNav.SelectedItem is ListBoxItem item && (item.Tag as string) == "Experimental")
                LstNav.SelectedIndex = 0;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else if (e.ClickCount == 1)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            ToggleMaximize();

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (BtnMaximize == null) return;
            if (WindowState == WindowState.Maximized)
            {
                BtnMaximize.Content = "🗗";
                BtnMaximize.ToolTip = "Restore";
            }
            else
            {
                BtnMaximize.Content = "🗖";
                BtnMaximize.ToolTip = "Maximize";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) =>
            Close();

        // Keep value labels in sync with slider positions.
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;

            UpdateValueLabels();
            UpdatePlayerCastleFactionVisibility();
            UpdateAdvancedZoneSettingsVisibility();
            UpdateRoadsHintVisibility();
            MarkDirty();
            Validate();
        }

        private void UpdateValueLabels()
        {
            PnlMap.TxtPlayers.Text = ((int)PnlMap.SldPlayers.Value).ToString();
            PnlHeroes.TxtHeroMin.Text = ((int)PnlHeroes.SldHeroMin.Value).ToString();
            PnlHeroes.TxtHeroMax.Text = ((int)PnlHeroes.SldHeroMax.Value).ToString();
            PnlHeroes.TxtHeroIncrement.Text = ((int)PnlHeroes.SldHeroIncrement.Value).ToString();
            PnlZones.TxtNeutral.Text = ((int)PnlZones.SldNeutral.Value).ToString();
            PnlZones.TxtPlayerCastles.Text = ((int)PnlZones.SldPlayerCastles.Value).ToString();
            PnlZones.TxtNeutralCastles.Text = ((int)PnlZones.SldNeutralCastles.Value).ToString();
            PnlZones.TxtResourceDensity.Text = $"{(int)PnlZones.SldResourceDensity.Value}%";
            PnlZones.TxtStructureDensity.Text = $"{(int)PnlZones.SldStructureDensity.Value}%";
            PnlZones.TxtNeutralStackStrength.Text = $"{(int)PnlZones.SldNeutralStackStrength.Value}%";
            PnlZones.TxtBorderGuardStrength.Text = $"{(int)PnlZones.SldBorderGuardStrength.Value}%";
            PnlGameRules.TxtFactionLawsExp.Text = $"{(int)PnlGameRules.SldFactionLawsExp.Value}%";
            PnlGameRules.TxtAstrologyExp.Text = $"{(int)PnlGameRules.SldAstrologyExp.Value}%";
            PnlZones.TxtNeutralLowNoCastle.Text = ((int)PnlZones.SldNeutralLowNoCastle.Value).ToString();
            PnlZones.TxtNeutralLowCastle.Text = ((int)PnlZones.SldNeutralLowCastle.Value).ToString();
            PnlZones.TxtNeutralMediumNoCastle.Text = ((int)PnlZones.SldNeutralMediumNoCastle.Value).ToString();
            PnlZones.TxtNeutralMediumCastle.Text = ((int)PnlZones.SldNeutralMediumCastle.Value).ToString();
            PnlZones.TxtNeutralHighNoCastle.Text = ((int)PnlZones.SldNeutralHighNoCastle.Value).ToString();
            PnlZones.TxtNeutralHighCastle.Text = ((int)PnlZones.SldNeutralHighCastle.Value).ToString();
            PnlZones.TxtMinNeutralBetweenPlayers.Text = ((int)PnlZones.SldMinNeutralBetweenPlayers.Value).ToString();
            PnlZones.TxtPlayerZoneSize.Text = $"{PnlZones.SldPlayerZoneSize.Value:F2}x";
            PnlZones.TxtNeutralZoneSize.Text = $"{PnlZones.SldNeutralZoneSize.Value:F2}x";
            PnlTopology.TxtHubZoneSize.Text = $"{PnlTopology.SldHubZoneSize.Value:F2}x";
            PnlTopology.TxtHubCastles.Text = ((int)PnlTopology.SldHubCastles.Value).ToString();
            PnlZones.TxtGuardRandomization.Text = $"{(int)PnlZones.SldGuardRandomization.Value}%";
            PnlGameRules.TxtLostStartCityDay.Text = ((int)PnlGameRules.SldLostStartCityDay.Value).ToString();
            PnlGameRules.TxtCityHoldDays.Text = ((int)PnlGameRules.SldCityHoldDays.Value).ToString();
            PnlGameRules.TxtGladiatorDelay.Text = ((int)PnlGameRules.SldGladiatorDelay.Value).ToString();
            PnlGameRules.TxtGladiatorCountDay.Text = ((int)PnlGameRules.SldGladiatorCountDay.Value).ToString();
            PnlGameRules.TxtTournamentPointsToWin.Text = ((int)PnlGameRules.SldTournamentPointsToWin.Value).ToString();
            PnlGameRules.TxtTournamentFirstTournamentDay.Text = ((int)PnlGameRules.SldTournamentFirstTournamentDay.Value).ToString();
            PnlGameRules.TxtTournamentInterval.Text = ((int)PnlGameRules.SldTournamentInterval.Value).ToString();
        }

        private void SetValidationText(string text)
        {
            TxtValidation.Text = text;
            TxtValidation.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool Validate()
        {
            var settings = BuildSettings();
            int maxZones = _advancedZoneSettings ? AdvancedModeMaxZones : SimpleModeMaxZones;
            var result = SettingsValidator.Validate(settings, maxZones);

            if (!result.IsValid)
            {
                SetValidationText(result.Blockers[0]);
                BtnPreview.IsEnabled = false;
                return false;
            }

            SetValidationText(string.Join("\n\n", result.Warnings));
            BtnPreview.IsEnabled = true;
            return true;
        }

        private int SelectedMapSize() =>
            PnlMap.CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsedSize)
                ? parsedSize
                : 160;

        private static string FormatMapSize(int size) =>
            KnownValues.IsExperimentalMapSize(size)
                ? $"{size}x{size} ({KnownValues.MapSizeLabel(size)}) (Experimental)"
                : $"{size}x{size} ({KnownValues.MapSizeLabel(size)})";

        private static double GuardRandomizationPercent(double guardRandomization)
        {
            if (double.IsNaN(guardRandomization) || double.IsInfinity(guardRandomization))
                return 5.0;

            return Math.Clamp(guardRandomization * 100.0, 0.0, 50.0);
        }

        private void RefreshMapSizeOptions(int? requestedSize = null)
        {
            if (PnlMap.CmbMapSize == null) return;

            int selectedSize = requestedSize ?? SelectedMapSize();
            bool includeExperimental = PnlMap.ChkExperimentalMapSizes?.IsChecked == true;
            int[] sizes = includeExperimental ? KnownValues.AllMapSizes : KnownValues.MapSizes;

            if (!includeExperimental && KnownValues.IsExperimentalMapSize(selectedSize))
                selectedSize = KnownValues.MaxOfficialMapSize;
            else if (!sizes.Contains(selectedSize))
                selectedSize = KnownValues.MapSizes.Contains(selectedSize) ? selectedSize : 160;

            _isRefreshingMapSizes = true;
            try
            {
                PnlMap.CmbMapSize.ItemsSource = sizes.Select(FormatMapSize).ToList();
                PnlMap.CmbMapSize.SelectedItem = FormatMapSize(selectedSize);
                if (PnlMap.CmbMapSize.SelectedIndex < 0)
                    PnlMap.CmbMapSize.SelectedItem = FormatMapSize(160);
            }
            finally
            {
                _isRefreshingMapSizes = false;
            }

            UpdateExperimentalMapSizeWarningVisibility();
        }

        private void CmbTopology_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            int idx = PnlTopology.CmbTopology.SelectedIndex;
            if (idx >= 0 && idx < TopologyOptions.Length)
                PnlTopology.TxtTopologyDesc.Text = TopologyOptions[idx].Description;

            // Isolate option is only meaningful for Random and Chain topologies.
            var topo = idx >= 0 && idx < TopologyOptions.Length ? TopologyOptions[idx].Topology : MapTopology.Default;
            bool isolateApplicable = topo is MapTopology.Random;
            PnlZones.ChkNoDirectPlayerConn.Visibility = isolateApplicable ? Visibility.Visible : Visibility.Collapsed;
            if (!isolateApplicable) PnlZones.ChkNoDirectPlayerConn.IsChecked = false;
            UpdateIsolateDescVisibility();
            UpdateAdvancedZoneSettingsVisibility();
            PnlTopology.PnlHubZoneSize.Visibility = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;
            PnlTopology.PnlHubCastles.Visibility  = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;

            MarkDirty();
            Validate();
        }

        private void CmbMapSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _isRefreshingMapSizes) return;
            UpdateExperimentalMapSizeWarningVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateIsolateDescVisibility();
            UpdateRoadsHintVisibility();
            UpdateBalancedZonePlacementDescVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkRandomPortals_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            PnlZones.PnlMaxPortals.Visibility = PnlZones.ChkRandomPortals.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            MarkDirty();
            Validate();
        }

        private void SldMaxPortals_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            PnlZones.LblMaxPortals.Text = ((int)PnlZones.SldMaxPortals.Value).ToString();
            MarkDirty();
        }

        private void WinConditionOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private bool _advancedZoneSettings = false;

        private void BtnAdvancedZoneSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _advancedZoneSettings = PnlZones.ChkAdvancedZoneSettings.IsChecked == true;
            if (_advancedZoneSettings && TotalAdvancedNeutralZonesFromSliders() == 0 && (int)PnlZones.SldNeutral.Value > 0)
            {
                if ((int)PnlZones.SldNeutralCastles.Value > 0)
                    PnlZones.SldNeutralMediumCastle.Value = PnlZones.SldNeutral.Value;
                else
                    PnlZones.SldNeutralMediumNoCastle.Value = PnlZones.SldNeutral.Value;
            }

            UpdateAdvancedZoneSettingsVisibility();
            UpdateValueLabels();
            UpdateRoadsHintVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkExperimentalMapSizes_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            RefreshMapSizeOptions();
            MarkDirty();
            Validate();
        }

        private int TotalAdvancedNeutralZonesFromSliders() =>
            (int)PnlZones.SldNeutralLowNoCastle.Value
            + (int)PnlZones.SldNeutralLowCastle.Value
            + (int)PnlZones.SldNeutralMediumNoCastle.Value
            + (int)PnlZones.SldNeutralMediumCastle.Value
            + (int)PnlZones.SldNeutralHighNoCastle.Value
            + (int)PnlZones.SldNeutralHighCastle.Value;

        private void UpdateAdvancedZoneSettingsVisibility()
        {
            if (PnlZones.PnlAdvancedNeutralZones == null) return;
            bool advanced = _advancedZoneSettings;
            PnlZones.PnlAdvancedNeutralZones.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlZones.PnlAdvancedZoneSizes.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            // Mirror web UX: keep the simple neutral-zones slider visible but
            // disabled in advanced mode rather than hiding it. Avoids the
            // "controls disappeared" feeling when toggling the Advanced switch.
            PnlZones.SldNeutral.IsEnabled = !advanced;
            PnlZones.PnlSimpleNeutralCountLabel.IsEnabled = !advanced;
            if (PnlZones.ChkAdvancedZoneSettings != null)
                PnlZones.ChkAdvancedZoneSettings.IsChecked = advanced;
        }

        private void CmbVictory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;

            int idx = PnlGameRules.CmbVictory.SelectedIndex;
            if (idx >= 0 && idx < KnownValues.VictoryConditionIds.Length)
                ApplyVictoryPreset(KnownValues.VictoryConditionIds[idx]);

            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ApplyVictoryPreset(string victoryCondition)
        {
            PnlGameRules.ChkLostStartCity.IsChecked = false;
            PnlGameRules.ChkLostStartHero.IsChecked = false;
            PnlGameRules.ChkCityHold.IsChecked = false;
            PnlGameRules.ChkGladiatorArena.IsChecked = false;
            PnlGameRules.ChkTournament.IsChecked = false;

            PnlGameRules.SldLostStartCityDay.Value = 3;
            PnlGameRules.SldCityHoldDays.Value = 6;
            PnlGameRules.SldGladiatorDelay.Value = 30;
            PnlGameRules.SldGladiatorCountDay.Value = 3;

            PnlGameRules.SldTournamentPointsToWin.Value = 2;
            PnlGameRules.SldTournamentInterval.Value = 7;
            PnlGameRules.SldTournamentFirstTournamentDay.Value = 14;
            PnlGameRules.ChkTournamentSaveArmy.IsChecked = true;

            switch (victoryCondition)
            {
                case "win_condition_3":
                    PnlGameRules.ChkLostStartCity.IsChecked = true;
                    break;
                case "win_condition_4":
                    PnlGameRules.ChkLostStartHero.IsChecked = true;
                    PnlGameRules.ChkGladiatorArena.IsChecked = true;
                    break;
                case "win_condition_5":
                    PnlGameRules.ChkCityHold.IsChecked = true;
                    break;
                case "win_condition_6":
                    PnlGameRules.ChkLostStartHero.IsChecked = true;
                    PnlGameRules.ChkTournament.IsChecked = true;
                    PnlGameRules.SldGladiatorDelay.Value = 21;
                    PnlGameRules.SldGladiatorCountDay.Value = 8;
                    break;
            }
        }

        private void UpdateWinConditionDetailVisibility()
        {
            if (PnlGameRules.PnlLostStartCityDetails == null) return;

            string selectedVictoryCondition = PnlGameRules.CmbVictory.SelectedIndex >= 0 && PnlGameRules.CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[PnlGameRules.CmbVictory.SelectedIndex]
                : "win_condition_1";

            bool isTournament = selectedVictoryCondition == "win_condition_6";

            if (isTournament)
            {
                // Tournament is exclusive — force it on and disable all other conditions.
                PnlGameRules.ChkTournament.IsChecked = true;
                PnlGameRules.ChkLostStartCity.IsChecked = false;
                PnlGameRules.ChkLostStartHero.IsChecked = false;
                PnlGameRules.ChkCityHold.IsChecked = false;
                PnlGameRules.ChkGladiatorArena.IsChecked = false;
            }
            else
            {
                // Tournament is unavailable outside of the Tournament win condition.
                PnlGameRules.ChkTournament.IsChecked = false;
                if (selectedVictoryCondition == "win_condition_3")
                    PnlGameRules.ChkLostStartCity.IsChecked = true;
                if (selectedVictoryCondition == "win_condition_4")
                {
                    PnlGameRules.ChkLostStartHero.IsChecked = true;
                    PnlGameRules.ChkGladiatorArena.IsChecked = true;
                }
                if (selectedVictoryCondition == "win_condition_5")
                    PnlGameRules.ChkCityHold.IsChecked = true;
            }

            PnlGameRules.ChkLostStartCity.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_3";
            PnlGameRules.ChkLostStartHero.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_4";
            PnlGameRules.ChkCityHold.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_5";
            PnlGameRules.ChkGladiatorArena.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_4";
            PnlGameRules.ChkTournament.IsChecked = isTournament;
            PnlGameRules.ChkTournament.IsEnabled = isTournament;

            PnlGameRules.PnlLostStartCityDetails.Visibility = PnlGameRules.ChkLostStartCity.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGameRules.PnlCityHoldDetails.Visibility = PnlGameRules.ChkCityHold.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGameRules.PnlGladiatorDetails.Visibility = PnlGameRules.ChkGladiatorArena.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGameRules.PnlTournamentDetails.Visibility = isTournament ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExperimentalMapSizeWarningVisibility()
        {
            if (PnlMap.TxtExperimentalMapSizeWarning == null) return;
            bool includeExperimental = PnlMap.ChkExperimentalMapSizes?.IsChecked == true;
            PnlMap.TxtExperimentalMapSizeWarning.Visibility = includeExperimental ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePlayerCastleFactionVisibility()
        {
            if (PnlZones.PnlPlayerCastleFactionOption == null || PnlZones.SldPlayerCastles == null) return;
            bool hasExtraCastles = (int)PnlZones.SldPlayerCastles.Value > 1;
            PnlZones.PnlPlayerCastleFactionOption.Visibility = hasExtraCastles ? Visibility.Visible : Visibility.Collapsed;
            if (!hasExtraCastles)
                PnlZones.ChkMatchPlayerCastleFactions.IsChecked = false;
        }

        private void UpdateIsolateDescVisibility()
        {
            if (PnlZones.TxtIsolateDesc == null || PnlZones.ChkNoDirectPlayerConn == null) return;
            PnlZones.TxtIsolateDesc.Visibility = PnlZones.ChkNoDirectPlayerConn.IsChecked == true && PnlZones.ChkNoDirectPlayerConn.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateRoadsHintVisibility()
        {
            if (PnlZones.TxtRoadsHint == null || PnlZones.ChkGenerateRoads == null || PnlZones.SldNeutralCastles == null) return;
            PnlZones.TxtRoadsHint.Visibility = PnlZones.ChkGenerateRoads.IsChecked == true && (int)PnlZones.SldNeutralCastles.Value == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateBalancedZonePlacementDescVisibility()
        {
            if (PnlZones.TxtBalancedZonePlacementDesc == null || PnlZones.ChkBalancedZonePlacement == null) return;
            PnlZones.TxtBalancedZonePlacementDesc.Visibility = PnlZones.ChkBalancedZonePlacement.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // -- Settings persistence -----------------------------------------------

        private SettingsFile GatherSettings() => new()
        {
            TemplateName          = PnlMap.TxtTemplateName.Text.Trim(),
            MapSize               = SelectedMapSize(),
            PlayerCount           = (int)PnlMap.SldPlayers.Value,
            NeutralZoneCount      = (int)PnlZones.SldNeutral.Value,
            PlayerZoneCastles     = (int)PnlZones.SldPlayerCastles.Value,
            NeutralZoneCastles    = (int)PnlZones.SldNeutralCastles.Value,
            AdvancedMode          = _advancedZoneSettings,
            NeutralLowNoCastleCount = (int)PnlZones.SldNeutralLowNoCastle.Value,
            NeutralLowCastleCount = (int)PnlZones.SldNeutralLowCastle.Value,
            NeutralMediumNoCastleCount = (int)PnlZones.SldNeutralMediumNoCastle.Value,
            NeutralMediumCastleCount = (int)PnlZones.SldNeutralMediumCastle.Value,
            NeutralHighNoCastleCount = (int)PnlZones.SldNeutralHighNoCastle.Value,
            NeutralHighCastleCount = (int)PnlZones.SldNeutralHighCastle.Value,
            MatchPlayerCastleFactions = PnlZones.ChkMatchPlayerCastleFactions.IsChecked == true,
            MinNeutralZonesBetweenPlayers = (int)PnlZones.SldMinNeutralBetweenPlayers.Value,
            ExperimentalBalancedZonePlacement = PnlZones.ChkBalancedZonePlacement.IsChecked == true,
            ExperimentalMapSizes  = PnlMap.ChkExperimentalMapSizes.IsChecked == true,
            PlayerZoneSize        = _advancedZoneSettings ? PnlZones.SldPlayerZoneSize.Value : 1.0,
            NeutralZoneSize       = _advancedZoneSettings ? PnlZones.SldNeutralZoneSize.Value : 1.0,
            HubZoneSize           = PnlTopology.SldHubZoneSize.Value,
            HubZoneCastles        = (int)PnlTopology.SldHubCastles.Value,
            GuardRandomization    = PnlZones.SldGuardRandomization.Value / 100.0,
            HeroCountMin          = (int)PnlHeroes.SldHeroMin.Value,
            HeroCountMax          = (int)PnlHeroes.SldHeroMax.Value,
            HeroCountIncrement    = (int)PnlHeroes.SldHeroIncrement.Value,
            HeroBans              = PnlHeroes.GetHeroBans(),
            FixedStartingHeroByFaction = PnlHeroes.GetFixedStartingHeroByFaction(),
            Topology              = TopologyOptions[PnlTopology.CmbTopology.SelectedIndex].Topology,
            RandomPortals         = PnlZones.ChkRandomPortals.IsChecked == true,
            MaxPortalConnections  = (int)PnlZones.SldMaxPortals.Value,
            SpawnRemoteFootholds  = PnlZones.ChkSpawnFootholds.IsChecked == true,
            GenerateRoads         = PnlZones.ChkGenerateRoads.IsChecked == true,
            NoDirectPlayerConn    = PnlZones.ChkNoDirectPlayerConn.IsChecked == true,
            ResourceDensityPercent = (int)PnlZones.SldResourceDensity.Value,
            StructureDensityPercent = (int)PnlZones.SldStructureDensity.Value,
            NeutralStackStrengthPercent = (int)PnlZones.SldNeutralStackStrength.Value,
            BorderGuardStrengthPercent = (int)PnlZones.SldBorderGuardStrength.Value,
            VictoryCondition = PnlGameRules.CmbVictory.SelectedIndex >= 0 && PnlGameRules.CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[PnlGameRules.CmbVictory.SelectedIndex]
                : "win_condition_1",
            FactionLawsExpPercent = (int)PnlGameRules.SldFactionLawsExp.Value,
            AstrologyExpPercent   = (int)PnlGameRules.SldAstrologyExp.Value,
            LostStartCity         = PnlGameRules.ChkLostStartCity.IsChecked == true,
            LostStartCityDay = (int)PnlGameRules.SldLostStartCityDay.Value,
            LostStartHero         = PnlGameRules.ChkLostStartHero.IsChecked == true,
            CityHold              = PnlGameRules.ChkCityHold.IsChecked == true,
            CityHoldDays = (int)PnlGameRules.SldCityHoldDays.Value,
            GladiatorArena               = PnlGameRules.ChkGladiatorArena.IsChecked == true,
            GladiatorArenaDaysDelayStart = (int)PnlGameRules.SldGladiatorDelay.Value,
            GladiatorArenaCountDay       = (int)PnlGameRules.SldGladiatorCountDay.Value,
            Tournament                   = PnlGameRules.ChkTournament.IsChecked == true,
            TournamentFirstTournamentDay = (int)PnlGameRules.SldTournamentFirstTournamentDay.Value,
            TournamentInterval = (int)PnlGameRules.SldTournamentInterval.Value,
            TournamentPointsToWin = (int)PnlGameRules.SldTournamentPointsToWin.Value,

            // ── Experimental ─────────────────────────────────────────────────
            ExperimentalEnabled = ChkExperimentalEnabled.IsChecked == true,
            GameMode            = PnlExperimental.ChkSingleHero.IsChecked == true ? "SingleHero" : "Classic",
            HeroHireBan         = PnlExperimental.ChkHeroHireBan.IsChecked == true,
            DesertionDay        = ParseInt(PnlExperimental.TxtDesertionDay.Text),
            DesertionValue      = ParseInt(PnlExperimental.TxtDesertionValue.Text),
            TerrainObstaclesFill = PnlExperimental.SldTerrainObstacles.Value / 100.0,
            TerrainLakesFill     = PnlExperimental.SldTerrainLakes.Value / 100.0,
            BuildingPresetPlayer = PresetFromCombo(PnlExperimental.CmbPlayerPreset),
            BuildingPresetNeutral = PresetFromCombo(PnlExperimental.CmbNeutralPreset),
            ZoneGuardWeeklyIncrement = PnlExperimental.SldZoneGuardWeekly.Value / 100.0,
            ConnectionGuardWeeklyIncrement = PnlExperimental.SldConnectionGuardWeekly.Value / 100.0,
            NeutralCityGuardChance = PnlExperimental.SldNeutralGuardChance.Value / 100.0,
            NeutralCityGuardValuePercent = (int)PnlExperimental.SldNeutralGuardValue.Value,
            GlobalBans = ParseBansCsv(PnlExperimental.TxtGlobalBans.Text),
            ContentCountLimits = ParseLimits(PnlExperimental.TxtCountLimits.Text),
            BonusResources = BuildBonusResourcesDict(),
            BonusHeroAttack     = (int)PnlExperimental.SldBonusAttack.Value,
            BonusHeroDefense    = (int)PnlExperimental.SldBonusDefense.Value,
            BonusHeroSpellpower = (int)PnlExperimental.SldBonusSpellpower.Value,
            BonusHeroKnowledge  = (int)PnlExperimental.SldBonusKnowledge.Value,
            BonusHeroStatStartHeroOnly = PnlExperimental.ChkBonusHeroStatStartHeroOnly.IsChecked == true,
            BonusItemSid               = PnlExperimental.TxtBonusItemSid.Text.Trim(),
            BonusItemStartHeroOnly     = PnlExperimental.ChkBonusItemStartHeroOnly.IsChecked == true,
            BonusSpellSid              = PnlExperimental.TxtBonusSpellSid.Text.Trim(),
            BonusSpellStartHeroOnly    = PnlExperimental.ChkBonusSpellStartHeroOnly.IsChecked == true,
            BonusUnitMultiplier        = PnlExperimental.SldBonusUnitMultiplier.Value / 100.0,
            BonusUnitMultiplierStartHeroOnly = PnlExperimental.ChkBonusUnitMultiplierStartHeroOnly.IsChecked == true,
            TierLow    = new TierOverrideFile { BuildingPreset = PresetFromCombo(PnlExperimental.CmbLowTierPreset),    GuardWeeklyIncrement = PnlExperimental.SldLowTierGuardWeekly.Value / 100.0 },
            TierMedium = new TierOverrideFile { BuildingPreset = PresetFromCombo(PnlExperimental.CmbMediumTierPreset), GuardWeeklyIncrement = PnlExperimental.SldMediumTierGuardWeekly.Value / 100.0 },
            TierHigh   = new TierOverrideFile { BuildingPreset = PresetFromCombo(PnlExperimental.CmbHighTierPreset),   GuardWeeklyIncrement = PnlExperimental.SldHighTierGuardWeekly.Value / 100.0 },
        };

        private static int ParseInt(string s) => int.TryParse(s, out int v) && v >= 0 ? v : 0;
        private static string PresetFromCombo(System.Windows.Controls.ComboBox c)
        {
            if (c.SelectedIndex <= 0) return "";
            return c.SelectedItem as string ?? "";
        }
        private static List<string> ParseBansCsv(string raw) =>
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Where(s => s.Length > 0).ToList();
        private static List<ContentLimitFile> ParseLimits(string raw)
        {
            var list = new List<ContentLimitFile>();
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string sid = line[..eq].Trim();
                if (!int.TryParse(line[(eq + 1)..].Trim(), out int max) || max < 0) continue;
                list.Add(new ContentLimitFile { Sid = sid, MaxPerPlayer = max });
            }
            return list;
        }
        private Dictionary<string, int> BuildBonusResourcesDict()
        {
            var d = new Dictionary<string, int>();
            void Add(string sid, double val)
            {
                int v = (int)val;
                if (v > 0) d[sid] = v;
            }
            Add("gold",      PnlExperimental.SldBonusGold.Value);
            Add("wood",      PnlExperimental.SldBonusWood.Value);
            Add("ore",       PnlExperimental.SldBonusOre.Value);
            Add("mercury",   PnlExperimental.SldBonusMercury.Value);
            Add("crystals",  PnlExperimental.SldBonusCrystals.Value);
            Add("gemstones", PnlExperimental.SldBonusGemstones.Value);
            return d;
        }

        private void ApplySettings(SettingsFile s)
        {
            PnlMap.TxtTemplateName.Text    = s.TemplateName;
            bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001 || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
            bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
            _advancedZoneSettings = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;
            PnlMap.ChkExperimentalMapSizes.IsChecked = needsExperimentalMapSizes;
            RefreshMapSizeOptions(s.MapSize);
            PnlMap.SldPlayers.Value        = s.PlayerCount;
            PnlZones.SldNeutral.Value        = s.NeutralZoneCount;
            PnlZones.SldPlayerCastles.Value  = s.PlayerZoneCastles;
            PnlZones.SldNeutralCastles.Value = s.NeutralZoneCastles;
            PnlZones.SldNeutralLowNoCastle.Value = s.NeutralLowNoCastleCount;
            PnlZones.SldNeutralLowCastle.Value = s.NeutralLowCastleCount;
            PnlZones.SldNeutralMediumNoCastle.Value = s.NeutralMediumNoCastleCount;
            PnlZones.SldNeutralMediumCastle.Value = s.NeutralMediumCastleCount;
            PnlZones.SldNeutralHighNoCastle.Value = s.NeutralHighNoCastleCount;
            PnlZones.SldNeutralHighCastle.Value = s.NeutralHighCastleCount;
            PnlZones.ChkMatchPlayerCastleFactions.IsChecked = s.MatchPlayerCastleFactions;
            PnlZones.SldMinNeutralBetweenPlayers.Value = s.MinNeutralZonesBetweenPlayers;
            PnlZones.ChkBalancedZonePlacement.IsChecked = s.ExperimentalBalancedZonePlacement;
            PnlZones.SldPlayerZoneSize.Value = Math.Clamp(s.PlayerZoneSize, 0.1, 2.0);
            PnlZones.SldNeutralZoneSize.Value = Math.Clamp(s.NeutralZoneSize, 0.1, 2.0);
            PnlTopology.SldHubZoneSize.Value = Math.Clamp(s.HubZoneSize, 0.25, 3.0);
            PnlTopology.SldHubCastles.Value = Math.Clamp(s.HubZoneCastles, 0, 4);
            PnlZones.SldGuardRandomization.Value = GuardRandomizationPercent(s.GuardRandomization);
            PnlHeroes.SldHeroMin.Value        = s.HeroCountMin;
            PnlHeroes.SldHeroMax.Value        = s.HeroCountMax;
            PnlHeroes.SldHeroIncrement.Value  = s.HeroCountIncrement;
            PnlHeroes.ApplyHeroSelection(s.HeroBans, s.FixedStartingHeroByFaction);
            int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
            if (topoIdx >= 0) PnlTopology.CmbTopology.SelectedIndex = topoIdx;
            PnlZones.ChkRandomPortals.IsChecked        = s.RandomPortals;
            PnlZones.SldMaxPortals.Value               = Math.Clamp(s.MaxPortalConnections, 1, 32);
            PnlZones.PnlMaxPortals.Visibility          = s.RandomPortals ? Visibility.Visible : Visibility.Collapsed;
            PnlZones.ChkSpawnFootholds.IsChecked       = s.SpawnRemoteFootholds;
            PnlZones.ChkGenerateRoads.IsChecked        = s.GenerateRoads;
            PnlZones.ChkNoDirectPlayerConn.IsChecked   = s.NoDirectPlayerConn;
            PnlZones.SldResourceDensity.Value          = s.EffectiveResourceDensityPercent;
            PnlZones.SldStructureDensity.Value         = s.EffectiveStructureDensityPercent;
            PnlZones.SldNeutralStackStrength.Value     = s.NeutralStackStrengthPercent;
            PnlZones.SldBorderGuardStrength.Value      = s.BorderGuardStrengthPercent;
            int victoryIdx = Array.IndexOf(KnownValues.VictoryConditionIds, s.VictoryCondition);
            PnlGameRules.CmbVictory.SelectedIndex = victoryIdx >= 0 ? victoryIdx : 0;
            PnlGameRules.SldFactionLawsExp.Value = Math.Clamp(s.FactionLawsExpPercent, 25, 200);
            PnlGameRules.SldAstrologyExp.Value = Math.Clamp(s.AstrologyExpPercent, 25, 200);
            PnlGameRules.ChkLostStartCity.IsChecked = s.LostStartCity;
            PnlGameRules.SldLostStartCityDay.Value = Math.Clamp(s.LostStartCityDay, 1, 30);
            PnlGameRules.ChkLostStartHero.IsChecked = s.LostStartHero;
            PnlGameRules.ChkCityHold.IsChecked = s.CityHold;
            PnlGameRules.SldCityHoldDays.Value = Math.Clamp(s.CityHoldDays, 1, 30);
            PnlGameRules.ChkGladiatorArena.IsChecked = s.GladiatorArena;
            PnlGameRules.SldGladiatorDelay.Value = Math.Clamp(s.GladiatorArenaDaysDelayStart, 1, 60);
            PnlGameRules.SldGladiatorCountDay.Value = Math.Clamp(s.GladiatorArenaCountDay, 1, 30);
            PnlGameRules.ChkTournament.IsChecked = s.Tournament;
            PnlGameRules.SldTournamentFirstTournamentDay.Value = Math.Clamp(s.TournamentFirstTournamentDay, 1, 60);
            PnlGameRules.SldTournamentInterval.Value = Math.Clamp(s.TournamentInterval, 1, 30);
            PnlGameRules.SldTournamentPointsToWin.Value = Math.Clamp(s.TournamentPointsToWin, 1, 10);
            PnlGameRules.ChkTournamentSaveArmy.IsChecked = s.TournamentSaveArmy;
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateRoadsHintVisibility();
            UpdateBalancedZonePlacementDescVisibility();
            UpdateWinConditionDetailVisibility();

            // ── Experimental ─────────────────────────────────────────────────
            ChkExperimentalEnabled.IsChecked = s.ExperimentalEnabled;
            LstNavExperimental.Visibility = s.ExperimentalEnabled ? Visibility.Visible : Visibility.Collapsed;
            PnlExperimental.ChkSingleHero.IsChecked = s.GameMode == "SingleHero";
            PnlExperimental.ChkHeroHireBan.IsChecked = s.HeroHireBan;
            PnlExperimental.ChkHeroHireBan.IsEnabled = s.GameMode != "SingleHero";
            PnlExperimental.TxtDesertionDay.Text = s.DesertionDay.ToString();
            PnlExperimental.TxtDesertionValue.Text = s.DesertionValue.ToString();
            PnlExperimental.SldTerrainObstacles.Value = Math.Clamp(s.TerrainObstaclesFill * 100.0, 0, 80);
            PnlExperimental.SldTerrainLakes.Value = Math.Clamp(s.TerrainLakesFill * 100.0, 0, 80);
            SetPresetCombo(PnlExperimental.CmbPlayerPreset, s.BuildingPresetPlayer);
            SetPresetCombo(PnlExperimental.CmbNeutralPreset, s.BuildingPresetNeutral);
            PnlExperimental.SldZoneGuardWeekly.Value = Math.Clamp(s.ZoneGuardWeeklyIncrement * 100.0, 0, 50);
            PnlExperimental.SldConnectionGuardWeekly.Value = Math.Clamp(s.ConnectionGuardWeeklyIncrement * 100.0, 0, 50);
            PnlExperimental.SldNeutralGuardChance.Value = Math.Clamp(s.NeutralCityGuardChance * 100.0, 0, 100);
            PnlExperimental.SldNeutralGuardValue.Value = Math.Clamp(s.NeutralCityGuardValuePercent <= 0 ? 100 : s.NeutralCityGuardValuePercent, 25, 300);
            PnlExperimental.TxtGlobalBans.Text = string.Join(", ", s.GlobalBans ?? new List<string>());
            PnlExperimental.TxtCountLimits.Text = string.Join("\n",
                (s.ContentCountLimits ?? new List<ContentLimitFile>())
                .Select(l => $"{l.Sid}={l.MaxPerPlayer}"));
            PnlExperimental.SldBonusGold.Value      = ResourceValue(s, "gold");
            PnlExperimental.SldBonusWood.Value      = ResourceValue(s, "wood");
            PnlExperimental.SldBonusOre.Value       = ResourceValue(s, "ore");
            PnlExperimental.SldBonusMercury.Value   = ResourceValue(s, "mercury");
            PnlExperimental.SldBonusCrystals.Value  = ResourceValue(s, "crystals");
            PnlExperimental.SldBonusGemstones.Value = ResourceValue(s, "gemstones");
            PnlExperimental.SldBonusAttack.Value     = Math.Clamp(s.BonusHeroAttack, 0, 20);
            PnlExperimental.SldBonusDefense.Value    = Math.Clamp(s.BonusHeroDefense, 0, 20);
            PnlExperimental.SldBonusSpellpower.Value = Math.Clamp(s.BonusHeroSpellpower, 0, 20);
            PnlExperimental.SldBonusKnowledge.Value  = Math.Clamp(s.BonusHeroKnowledge, 0, 20);
            PnlExperimental.ChkBonusHeroStatStartHeroOnly.IsChecked = s.BonusHeroStatStartHeroOnly;
            PnlExperimental.TxtBonusItemSid.Text = s.BonusItemSid ?? "";
            PnlExperimental.ChkBonusItemStartHeroOnly.IsChecked = s.BonusItemStartHeroOnly;
            PnlExperimental.TxtBonusSpellSid.Text = s.BonusSpellSid ?? "";
            PnlExperimental.ChkBonusSpellStartHeroOnly.IsChecked = s.BonusSpellStartHeroOnly;
            PnlExperimental.SldBonusUnitMultiplier.Value = Math.Clamp(s.BonusUnitMultiplier * 100.0, 0, 500);
            PnlExperimental.ChkBonusUnitMultiplierStartHeroOnly.IsChecked = s.BonusUnitMultiplierStartHeroOnly;
            SetPresetCombo(PnlExperimental.CmbLowTierPreset,    s.TierLow?.BuildingPreset ?? "");
            SetPresetCombo(PnlExperimental.CmbMediumTierPreset, s.TierMedium?.BuildingPreset ?? "");
            SetPresetCombo(PnlExperimental.CmbHighTierPreset,   s.TierHigh?.BuildingPreset ?? "");
            PnlExperimental.SldLowTierGuardWeekly.Value    = Math.Clamp((s.TierLow?.GuardWeeklyIncrement    ?? 0) * 100.0, 0, 50);
            PnlExperimental.SldMediumTierGuardWeekly.Value = Math.Clamp((s.TierMedium?.GuardWeeklyIncrement ?? 0) * 100.0, 0, 50);
            PnlExperimental.SldHighTierGuardWeekly.Value   = Math.Clamp((s.TierHigh?.GuardWeeklyIncrement   ?? 0) * 100.0, 0, 50);
        }

        private static int ResourceValue(SettingsFile s, string sid) =>
            s.BonusResources is { } d && d.TryGetValue(sid, out int v) ? Math.Clamp(v, 0, 100) : 0;

        private static void SetPresetCombo(System.Windows.Controls.ComboBox c, string sid)
        {
            if (string.IsNullOrEmpty(sid)) { c.SelectedIndex = 0; return; }
            int idx = -1;
            for (int i = 0; i < c.Items.Count; i++)
            {
                if ((c.Items[i] as string) == sid) { idx = i; break; }
            }
            c.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private bool SaveToPath(string path)
        {
            try
            {
                var json = JsonSerializer.Serialize(GatherSettings(), JsonOptions);
                File.WriteAllText(path, json);
                _currentSettingsPath = path;
                _isDirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Save Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all settings to defaults?", "New Settings",
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            ApplySettings(new SettingsFile());
            _currentSettingsPath = null;
            _isDirty = false;
            UpdateTitle();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Open Settings File",
                Filter = "Template Settings (*.oetgs)|*.oetgs|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var s = JsonSerializer.Deserialize<SettingsFile>(json, JsonOptions);
                if (s is null) throw new InvalidDataException("File is empty or invalid.");
                ApplySettings(s);
                _currentSettingsPath = dlg.FileName;
                _isDirty = false;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open settings:\n{ex.Message}", "Open Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSettingsPath is not null)
                SaveToPath(_currentSettingsPath);
            else
                BtnSaveAs_Click(sender, e);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Save Settings As",
                Filter     = "Template Settings (*.oetgs)|*.oetgs|All files (*.*)|*.*",
                FileName   = PnlMap.TxtTemplateName.Text.Trim().Length > 0 ? PnlMap.TxtTemplateName.Text.Trim() : "My Settings",
                DefaultExt = ".oetgs",
            };
            if (dlg.ShowDialog() == true)
                SaveToPath(dlg.FileName);
        }

        // -- Generate ----------------------------------------------------------

        // The most recently generated template — used by BtnSaveGenerated_Click
        private RmgTemplate? _generatedTemplate;
        private MapTopology  _generatedTopology;
        private bool _templateOutdated = false;

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate()) return;
            var settings = BuildSettings();
            ApplyExperimentalTierOverrides(settings);
            _generatedTemplate = TemplateGenerator.Generate(settings);
            _generatedTopology = settings.Topology;
            _templateOutdated = false;
            byte[] previewPng = TemplatePreviewRenderer.RenderPng(_generatedTemplate, _generatedTopology);
            ImgPreview.Source = WpfPreviewAdapter.ToBitmapImage(previewPng);
            BtnSaveGenerated.Visibility = Visibility.Visible;
            UpdateOutdatedWarning();
            Validate(); // refresh warnings now that template is up to date
        }

        private void BtnSaveGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedTemplate is null) return;

            string? gameTemplatesPath = FindOldenEraTemplatesPath();

            string currentTemplateName = PnlMap.TxtTemplateName.Text.Trim();

            var dlg = new SaveFileDialog
            {
                Title = "Save Template",
                Filter = "RMG Template (*.rmg.json)|*.rmg.json",
                FileName = $"{(currentTemplateName.Length > 0 ? currentTemplateName : "Custom Template")}.rmg.json",
                DefaultExt = ".rmg.json"
            };

            if (gameTemplatesPath != null)
                dlg.InitialDirectory = gameTemplatesPath;

            if (dlg.ShowDialog() != true) return;

            if (!IsInsideGameTemplatesFolder(dlg.FileName, gameTemplatesPath))
            {
                string expectedDesc = gameTemplatesPath != null
                    ? $"Expected:\n{gameTemplatesPath}\n\n"
                    : $"Expected folder structure:\n...\\HeroesOldenEra_Data\\StreamingAssets\\map_templates\n\n";
                var wrongFolderResult = MessageBox.Show(
                    $"The file is being saved outside the expected templates folder.\n\n{expectedDesc}Chosen:\n{Path.GetDirectoryName(dlg.FileName)}\n\nTemplates saved elsewhere will not appear in-game. Save here anyway?",
                    "Wrong Folder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (wrongFolderResult != MessageBoxResult.Yes) return;
            }

            string chosenBaseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dlg.FileName));
            if (!chosenBaseName.Equals(currentTemplateName, StringComparison.Ordinal))
            {
                var mismatchResult = MessageBox.Show(
                    $"The file will be saved as \"{Path.GetFileName(dlg.FileName)}\", but the template will appear in-game as \"{currentTemplateName}\".\n\nClick Yes to save anyway, or No to go back and rename the template first.",
                    "Template Name Mismatch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (mismatchResult != MessageBoxResult.Yes) return;
            }

            string json = JsonSerializer.Serialize(_generatedTemplate, JsonOptions);
            File.WriteAllText(dlg.FileName, json);

            string previewPath = PreviewSidecar.GetSidecarPath(dlg.FileName);
            string? previewError = null;
            if (ChkSavePreviewImage.IsChecked == true)
            {
                try
                {
                    byte[] sidecarPng = TemplatePreviewRenderer.RenderPng(_generatedTemplate, _generatedTopology);
                    string? sidecarDir = Path.GetDirectoryName(previewPath);
                    if (!string.IsNullOrEmpty(sidecarDir))
                        Directory.CreateDirectory(sidecarDir);
                    string tempPath = $"{previewPath}.{Guid.NewGuid():N}.tmp";
                    File.WriteAllBytes(tempPath, sidecarPng);
                    File.Move(tempPath, previewPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    previewError = ex.Message;
                }
            }

            string savedMsg = $"Template successfully saved to:\n\n{dlg.FileName}";
            if (ChkSavePreviewImage.IsChecked == true)
            {
                if (previewError == null)
                    savedMsg += $"\n\nPreview PNG saved to:\n\n{previewPath}";
                else
                    savedMsg += $"\n\nTemplate saved, but the preview PNG could not be written:\n{previewError}";
            }
            if (gameTemplatesPath == null)
                savedMsg += "\n\n\n💡 Tip: Templates must be placed in:\n<Olden Era install folder>\\HeroesOldenEra_Data\\StreamingAssets\\map_templates";

            MessageBox.Show(savedMsg, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private GeneratorSettings BuildSettings() => new()
        {
            TemplateName = PnlMap.TxtTemplateName.Text.Trim(),
            GameMode = CmbGameMode.SelectedItem as string ?? "Classic",
            PlayerCount = (int)PnlMap.SldPlayers.Value,
            HeroSettings = new HeroSettings
            {
                HeroCountMin = (int)PnlHeroes.SldHeroMin.Value,
                HeroCountMax = (int)PnlHeroes.SldHeroMax.Value,
                HeroCountIncrement = (int)PnlHeroes.SldHeroIncrement.Value,
                HeroBans = PnlHeroes.GetHeroBans(),
                FixedStartingHeroByFaction = PnlHeroes.GetFixedStartingHeroByFaction(),
            },
            MapSize = SelectedMapSize(),
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = PnlGameRules.CmbVictory.SelectedIndex >= 0 && PnlGameRules.CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                    ? KnownValues.VictoryConditionIds[PnlGameRules.CmbVictory.SelectedIndex]
                    : "win_condition_1",
                LostStartCity = PnlGameRules.ChkLostStartCity.IsChecked == true,
                LostStartCityDay = (int)PnlGameRules.SldLostStartCityDay.Value,
                LostStartHero = PnlGameRules.ChkLostStartHero.IsChecked == true,
                CityHold = PnlGameRules.ChkCityHold.IsChecked == true,
                CityHoldDays = (int)PnlGameRules.SldCityHoldDays.Value,
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = (int)PnlZones.SldNeutral.Value,
                PlayerZoneCastles = (int)PnlZones.SldPlayerCastles.Value,
                NeutralZoneCastles = (int)PnlZones.SldNeutralCastles.Value,
                ResourceDensityPercent = (int)PnlZones.SldResourceDensity.Value,
                StructureDensityPercent = (int)PnlZones.SldStructureDensity.Value,
                NeutralStackStrengthPercent = (int)PnlZones.SldNeutralStackStrength.Value,
                BorderGuardStrengthPercent = (int)PnlZones.SldBorderGuardStrength.Value,
                HubZoneSize = PnlTopology.SldHubZoneSize.Value,
                HubZoneCastles = (int)PnlTopology.SldHubCastles.Value,
                Advanced = new AdvancedSettings
                {
                    Enabled = _advancedZoneSettings,
                    NeutralLowNoCastleCount = (int)PnlZones.SldNeutralLowNoCastle.Value,
                    NeutralLowCastleCount = (int)PnlZones.SldNeutralLowCastle.Value,
                    NeutralMediumNoCastleCount = (int)PnlZones.SldNeutralMediumNoCastle.Value,
                    NeutralMediumCastleCount = (int)PnlZones.SldNeutralMediumCastle.Value,
                    NeutralHighNoCastleCount = (int)PnlZones.SldNeutralHighNoCastle.Value,
                    NeutralHighCastleCount = (int)PnlZones.SldNeutralHighCastle.Value,
                    PlayerZoneSize = _advancedZoneSettings ? PnlZones.SldPlayerZoneSize.Value : 1.0,
                    NeutralZoneSize = _advancedZoneSettings ? PnlZones.SldNeutralZoneSize.Value : 1.0,
                    GuardRandomization = _advancedZoneSettings ? PnlZones.SldGuardRandomization.Value / 100.0 : 0.05,
                }
            },
            // Neutral zones between players can be influenced by advanced zone settings, but is functionally independent.
            MinNeutralZonesBetweenPlayers = _advancedZoneSettings ? (int)PnlZones.SldMinNeutralBetweenPlayers.Value : 0,
            MatchPlayerCastleFactions = PnlZones.ChkMatchPlayerCastleFactions.IsChecked == true,
            ExperimentalBalancedZonePlacement = PnlZones.ChkBalancedZonePlacement.IsChecked == true,
            NoDirectPlayerConnections = PnlZones.ChkNoDirectPlayerConn.IsChecked == true,
            RandomPortals = PnlZones.ChkRandomPortals.IsChecked == true,
            MaxPortalConnections = (int)PnlZones.SldMaxPortals.Value,
            SpawnRemoteFootholds = PnlZones.ChkSpawnFootholds.IsChecked == true,
            GenerateRoads = PnlZones.ChkGenerateRoads.IsChecked == true,
            Topology = PnlTopology.CmbTopology.SelectedIndex >= 0 ? TopologyOptions[PnlTopology.CmbTopology.SelectedIndex].Topology : MapTopology.Default,
            FactionLawsExpPercent = (int)PnlGameRules.SldFactionLawsExp.Value,
            AstrologyExpPercent = (int)PnlGameRules.SldAstrologyExp.Value,
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = PnlGameRules.ChkGladiatorArena.IsChecked == true,
                DaysDelayStart = (int)PnlGameRules.SldGladiatorDelay.Value,
                CountDay = (int)PnlGameRules.SldGladiatorCountDay.Value
            },
            TournamentRules = new TournamentRules
            {
                Enabled = PnlGameRules.ChkTournament.IsChecked == true,
                FirstTournamentDay = (int)PnlGameRules.SldTournamentFirstTournamentDay.Value,
                Interval = (int)PnlGameRules.SldTournamentInterval.Value,
                PointsToWin = (int)PnlGameRules.SldTournamentPointsToWin.Value,
                SaveArmy = PnlGameRules.ChkTournamentSaveArmy.IsChecked == true
            },

            // ── Experimental ─────────────────────────────────────────────────
            HeroHireBan    = PnlExperimental.ChkHeroHireBan.IsChecked == true,
            DesertionDay   = ParseInt(PnlExperimental.TxtDesertionDay.Text),
            DesertionValue = ParseInt(PnlExperimental.TxtDesertionValue.Text),
            Terrain = new TerrainSettings
            {
                ObstaclesFill = PnlExperimental.SldTerrainObstacles.Value / 100.0,
                LakesFill = PnlExperimental.SldTerrainLakes.Value / 100.0,
            },
            BuildingPresets = new BuildingPresetSettings
            {
                PlayerZonePreset = PresetFromCombo(PnlExperimental.CmbPlayerPreset),
                NeutralZonePreset = PresetFromCombo(PnlExperimental.CmbNeutralPreset),
            },
            GuardProgression = new GuardProgressionSettings
            {
                ZoneGuardWeeklyIncrement = PnlExperimental.SldZoneGuardWeekly.Value / 100.0,
                ConnectionGuardWeeklyIncrement = PnlExperimental.SldConnectionGuardWeekly.Value / 100.0,
            },
            NeutralCities = new NeutralCitySettings
            {
                GuardChance = PnlExperimental.SldNeutralGuardChance.Value / 100.0,
                GuardValuePercent = (int)PnlExperimental.SldNeutralGuardValue.Value,
            },
            Content = new ContentControlSettings
            {
                GlobalBans = ParseBansCsv(PnlExperimental.TxtGlobalBans.Text),
                ContentCountLimits = ParseLimits(PnlExperimental.TxtCountLimits.Text)
                    .ConvertAll(l => new ContentLimit { Sid = l.Sid, MaxPerPlayer = l.MaxPerPlayer }),
            },
            Bonuses = new StartingBonusSettings
            {
                Resources = BuildBonusResourcesDict(),
                HeroAttack = (int)PnlExperimental.SldBonusAttack.Value,
                HeroDefense = (int)PnlExperimental.SldBonusDefense.Value,
                HeroSpellpower = (int)PnlExperimental.SldBonusSpellpower.Value,
                HeroKnowledge = (int)PnlExperimental.SldBonusKnowledge.Value,
                HeroStatStartHeroOnly = PnlExperimental.ChkBonusHeroStatStartHeroOnly.IsChecked == true,
                ItemSid = PnlExperimental.TxtBonusItemSid.Text.Trim(),
                ItemStartHeroOnly = PnlExperimental.ChkBonusItemStartHeroOnly.IsChecked == true,
                SpellSid = PnlExperimental.TxtBonusSpellSid.Text.Trim(),
                SpellStartHeroOnly = PnlExperimental.ChkBonusSpellStartHeroOnly.IsChecked == true,
                UnitMultiplier = PnlExperimental.SldBonusUnitMultiplier.Value / 100.0,
                UnitMultiplierStartHeroOnly = PnlExperimental.ChkBonusUnitMultiplierStartHeroOnly.IsChecked == true,
            },
        };

        // Apply per-tier overrides after object init since AdvancedSettings.LowTier/MediumTier/HighTier are not in the initializer above.
        private void ApplyExperimentalTierOverrides(GeneratorSettings settings)
        {
            settings.ZoneCfg.Advanced.LowTier = new TierOverrides
            {
                BuildingPreset = PresetFromCombo(PnlExperimental.CmbLowTierPreset),
                GuardWeeklyIncrement = PnlExperimental.SldLowTierGuardWeekly.Value / 100.0,
            };
            settings.ZoneCfg.Advanced.MediumTier = new TierOverrides
            {
                BuildingPreset = PresetFromCombo(PnlExperimental.CmbMediumTierPreset),
                GuardWeeklyIncrement = PnlExperimental.SldMediumTierGuardWeekly.Value / 100.0,
            };
            settings.ZoneCfg.Advanced.HighTier = new TierOverrides
            {
                BuildingPreset = PresetFromCombo(PnlExperimental.CmbHighTierPreset),
                GuardWeeklyIncrement = PnlExperimental.SldHighTierGuardWeekly.Value / 100.0,
            };
        }

        /// <summary>
        /// Returns true when <paramref name="filePath"/> is inside the expected game templates folder
        /// (including any sub-folders, since the game supports those).
        /// If <paramref name="gameTemplatesPath"/> was resolved, a prefix match is used.
        /// Otherwise the chosen directory is checked against the known folder-structure tail
        /// <c>HeroesOldenEra_Data\StreamingAssets\map_templates</c>.
        /// </summary>
        private static bool IsInsideGameTemplatesFolder(string filePath, string? gameTemplatesPath)
        {
            string chosenDir = Path.GetDirectoryName(filePath) ?? string.Empty;

            if (gameTemplatesPath != null)
            {
                // Normalise both paths to ensure consistent separator and casing comparison.
                string normalised = Path.GetFullPath(chosenDir);
                string expected   = Path.GetFullPath(gameTemplatesPath);
                // Accept the folder itself or any sub-folder inside it.
                return normalised.Equals(expected, StringComparison.OrdinalIgnoreCase)
                    || normalised.StartsWith(expected + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            // Game not found via registry/fallback paths — match on the known folder-structure tail.
            const string expectedTail = OldenEraSteamInfo.TemplatesSubpath;
            return chosenDir.EndsWith(expectedTail, StringComparison.OrdinalIgnoreCase)
                || chosenDir.Contains(expectedTail + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tries to locate the Olden Era map_templates folder via the Steam registry.
        /// Returns null if the game installation cannot be found.
        /// </summary>
        private static string? FindOldenEraTemplatesPath()
        {
            // Steam stores per-app install paths under this key.
            string[] registryRoots =
            [
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {OldenEraSteamInfo.AppId}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {OldenEraSteamInfo.AppId}"
            ];

            foreach (var keyPath in registryRoots)
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key?.GetValue("InstallLocation") is string installDir && Directory.Exists(installDir))
                    {
                        string templatesDir = Path.Combine(installDir, OldenEraSteamInfo.TemplatesSubpath);
                        if (Directory.Exists(templatesDir))
                            return templatesDir;
                    }
                }
                catch { /* registry access denied — skip */ }
            }

            // Fallback: check common Steam library locations manually.
            string[] steamLibraryRoots =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", OldenEraSteamInfo.SteamFolderName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Steam", "steamapps", "common", OldenEraSteamInfo.SteamFolderName),
            ];

            foreach (var candidate in steamLibraryRoots)
            {
                string templatesDir = Path.Combine(candidate, OldenEraSteamInfo.TemplatesSubpath);
                if (Directory.Exists(templatesDir))
                    return templatesDir;
            }
            return null;
        }
        
    }
}
