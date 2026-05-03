using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class MainWindow : Window
    {
        private const string GitHubApiLatestRelease = "https://api.github.com/repos/KhanDevelopsGames/Olden-Era---Template-Generator/releases/latest";
        private const string GitHubReleasesPage     = "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases";
        private const int SimpleModeMaxZones = 16;
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
            (MapTopology.Chain,       "Chain",         "Zones are connected in a straight line from one end to the other, with no wrap-around."),
            (MapTopology.SharedWeb,   "Shared Web [NOT FUNCTIONAL]",    "Player zones connect to shared neutral zones. Neutrals form a ring between all players."),
        ];

        public MainWindow()
        {
            InitializeComponent();

            // Stamp version from assembly metadata into all visible locations.
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string versionLabel = version != null ? $"v{version.Major}.{version.Minor}" : "v?";
            _baseTitle = $"Olden Era - Simple Template Generator {versionLabel}";
            TxtAppTitle.Text = $"Olden Era - Simple Template Generator  {versionLabel}";
            TxtWipWarning.Text = $"⚠️ Work in progress — Some generated templates may contain game-breaking bugs or issues.";

            CmbGameMode.ItemsSource = KnownValues.GameModes;
            CmbGameMode.SelectedIndex = 0;
            RefreshMapSizeOptions(160);
            CmbVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            CmbVictory.SelectedIndex = 0; // Classic (win_condition_1)
            CmbTopology.ItemsSource = TopologyOptions.Select(t => t.Label).ToList();
            CmbTopology.SelectedIndex = 0; // Random is first
            UpdateValueLabels();
            UpdateAdvancedModeVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateRoadsHintVisibility();
            UpdateBalancedZonePlacementDescVisibility();

            // Fire-and-forget background update check — never blocks the UI.
            _ = CheckForUpdateAsync(version);

            TxtTemplateName.TextChanged += (_, _) => MarkDirty();
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

                // Tag expected format: "v1.2" or "1.2" — parse major.minor.
                string tag = release.TagName.TrimStart('v');
                if (!Version.TryParse(tag, out Version? latestVersion)) return;
                if (currentVersion == null || latestVersion <= currentVersion) return;

                // A newer version exists — prompt on the UI thread.
                Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        $"A new version is available: v{latestVersion.Major}.{latestVersion.Minor}\n" +
                        $"You are running: v{currentVersion.Major}.{currentVersion.Minor}\n\n" +
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
            UpdateTitle();
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

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) =>
            Close();

        // Keep value labels in sync with slider positions.
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;

            UpdateValueLabels();
            UpdatePlayerCastleFactionVisibility();
            UpdateAdvancedModeVisibility();
            UpdateRoadsHintVisibility();
            MarkDirty();
            Validate();
        }

        private void UpdateValueLabels()
        {
            TxtPlayers.Text = ((int)SldPlayers.Value).ToString();
            TxtHeroMin.Text = ((int)SldHeroMin.Value).ToString();
            TxtHeroMax.Text = ((int)SldHeroMax.Value).ToString();
            TxtHeroIncrement.Text = ((int)SldHeroIncrement.Value).ToString();
            TxtNeutral.Text = ((int)SldNeutral.Value).ToString();
            TxtPlayerCastles.Text = ((int)SldPlayerCastles.Value).ToString();
            TxtNeutralCastles.Text = ((int)SldNeutralCastles.Value).ToString();
            TxtResourceDensity.Text = $"{(int)SldResourceDensity.Value}%";
            TxtStructureDensity.Text = $"{(int)SldStructureDensity.Value}%";
            TxtNeutralStackStrength.Text = $"{(int)SldNeutralStackStrength.Value}%";
            TxtBorderGuardStrength.Text = $"{(int)SldBorderGuardStrength.Value}%";
            TxtFactionLawsExp.Text = $"{(int)SldFactionLawsExp.Value}%";
            TxtAstrologyExp.Text = $"{(int)SldAstrologyExp.Value}%";
            TxtNeutralLowNoCastle.Text = ((int)SldNeutralLowNoCastle.Value).ToString();
            TxtNeutralLowCastle.Text = ((int)SldNeutralLowCastle.Value).ToString();
            TxtNeutralMediumNoCastle.Text = ((int)SldNeutralMediumNoCastle.Value).ToString();
            TxtNeutralMediumCastle.Text = ((int)SldNeutralMediumCastle.Value).ToString();
            TxtNeutralHighNoCastle.Text = ((int)SldNeutralHighNoCastle.Value).ToString();
            TxtNeutralHighCastle.Text = ((int)SldNeutralHighCastle.Value).ToString();
            TxtMinNeutralBetweenPlayers.Text = ((int)SldMinNeutralBetweenPlayers.Value).ToString();
            TxtPlayerZoneSize.Text = $"{SldPlayerZoneSize.Value:F2}x";
            TxtNeutralZoneSize.Text = $"{SldNeutralZoneSize.Value:F2}x";
            TxtGuardRandomization.Text = $"{(int)SldGuardRandomization.Value}%";
            TxtLostStartCityDay.Text = ((int)SldLostStartCityDay.Value).ToString();
            TxtCityHoldDays.Text = ((int)SldCityHoldDays.Value).ToString();
            TxtGladiatorDelay.Text = ((int)SldGladiatorDelay.Value).ToString();
            TxtGladiatorCountDay.Text = ((int)SldGladiatorCountDay.Value).ToString();
            TxtTournamentRoundCount.Text = ((int)SldTournamentRoundCount.Value).ToString();
            TxtTournamentRoundDuration.Text = ((int)SldTournamentRoundDuration.Value).ToString();
            TxtTournamentFirstAnnounceDay.Text = ((int)SldTournamentFirstAnnounceDay.Value).ToString();
            TxtTournamentRoundInterval.Text = ((int)SldTournamentRoundInterval.Value).ToString();
            TxtTournamentPointsToWin.Text = ((int)SldTournamentPointsToWin.Value).ToString();
        }

        private bool Validate()
        {
            int heroMin = (int)SldHeroMin.Value;
            int heroMax = (int)SldHeroMax.Value;
            int players = (int)SldPlayers.Value;
            int neutral = TotalNeutralZonesFromUi();

            if (heroMin > heroMax)
            {
                TxtValidation.Text = "Min Heroes cannot be greater than Max Heroes.";
                BtnGenerate.IsEnabled = false;
                return false;
            }

            int maxZones = ChkAdvancedMode.IsChecked == true ? AdvancedModeMaxZones : SimpleModeMaxZones;
            if (players + neutral > maxZones)
            {
                TxtValidation.Text = $"Total zones (players + neutral) cannot exceed {maxZones}.";
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

            int selectedMapSize = SelectedMapSize();
            if (players + neutral > 4 && selectedMapSize < 128)
                warnings.Add("⚠️ Using a small map with many zones may freeze the game while loading the map. Consider using a larger map size.");

            if (selectedMapSize > KnownValues.MaxOfficialMapSize)
                warnings.Add("Experimental map sizes above 240x240 are not confirmed by official templates; generated maps may fail, freeze, or behave unpredictably in game.");

            int minNeutralBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value;
            if (ChkAdvancedMode.IsChecked == true && minNeutralBetweenPlayers > 0)
            {
                var topology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default;
                var separationSettings = new GeneratorSettings
                {
                    PlayerCount = players,
                    Topology = topology,
                    RandomPortals = ChkRandomPortals.IsChecked == true,
                    MinNeutralZonesBetweenPlayers = minNeutralBetweenPlayers
                };

                if (!TemplateGenerator.CanHonorNeutralSeparation(separationSettings, neutral))
                    warnings.Add("Minimum neutral separation cannot be guaranteed with the current layout, neutral zone total, or portal setting; generation will ignore that option.");
            }

            TxtValidation.Text = string.Join("\n\n", warnings);

            BtnGenerate.IsEnabled = true;
            return true;
        }

        private int TotalNeutralZonesFromUi()
        {
            if (ChkAdvancedMode.IsChecked != true)
                return (int)SldNeutral.Value;

            return (int)SldNeutralLowNoCastle.Value
                + (int)SldNeutralLowCastle.Value
                + (int)SldNeutralMediumNoCastle.Value
                + (int)SldNeutralMediumCastle.Value
                + (int)SldNeutralHighNoCastle.Value
                + (int)SldNeutralHighCastle.Value;
        }

        private int SelectedMapSize() =>
            CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsedSize)
                ? parsedSize
                : 160;

        private static string FormatMapSize(int size) =>
            KnownValues.IsExperimentalMapSize(size) ? $"{size}x{size} (Experimental)" : $"{size}x{size}";

        private static double GuardRandomizationPercent(double guardRandomization)
        {
            if (double.IsNaN(guardRandomization) || double.IsInfinity(guardRandomization))
                return 5.0;

            return Math.Clamp(guardRandomization * 100.0, 0.0, 50.0);
        }

        private void RefreshMapSizeOptions(int? requestedSize = null)
        {
            if (CmbMapSize == null) return;

            int selectedSize = requestedSize ?? SelectedMapSize();
            bool includeExperimental = ChkAdvancedMode?.IsChecked == true && ChkExperimentalMapSizes?.IsChecked == true;
            int[] sizes = includeExperimental ? KnownValues.AllMapSizes : KnownValues.MapSizes;

            if (!includeExperimental && KnownValues.IsExperimentalMapSize(selectedSize))
                selectedSize = KnownValues.MaxOfficialMapSize;
            else if (!sizes.Contains(selectedSize))
                selectedSize = KnownValues.MapSizes.Contains(selectedSize) ? selectedSize : 160;

            _isRefreshingMapSizes = true;
            try
            {
                CmbMapSize.ItemsSource = sizes.Select(FormatMapSize).ToList();
                CmbMapSize.SelectedItem = FormatMapSize(selectedSize);
                if (CmbMapSize.SelectedIndex < 0)
                    CmbMapSize.SelectedItem = FormatMapSize(160);
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
            int idx = CmbTopology.SelectedIndex;
            if (idx >= 0 && idx < TopologyOptions.Length)
                TxtTopologyDesc.Text = TopologyOptions[idx].Description;

            // Isolate option is only meaningful for Random and Chain topologies.
            var topo = idx >= 0 && idx < TopologyOptions.Length ? TopologyOptions[idx].Topology : MapTopology.Default;
            bool isolateApplicable = topo is MapTopology.Random;
            ChkNoDirectPlayerConn.Visibility = isolateApplicable ? Visibility.Visible : Visibility.Collapsed;
            if (!isolateApplicable) ChkNoDirectPlayerConn.IsChecked = false;
            UpdateIsolateDescVisibility();
            UpdateAdvancedModeVisibility();

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

        private void WinConditionOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkAdvancedMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            if (ChkAdvancedMode.IsChecked == true && TotalAdvancedNeutralZonesFromSliders() == 0 && (int)SldNeutral.Value > 0)
            {
                if ((int)SldNeutralCastles.Value > 0)
                    SldNeutralMediumCastle.Value = SldNeutral.Value;
                else
                    SldNeutralMediumNoCastle.Value = SldNeutral.Value;
            }

            UpdateAdvancedModeVisibility();
            UpdateValueLabels();
            UpdateRoadsHintVisibility();
            UpdateWinConditionDetailVisibility();
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
            (int)SldNeutralLowNoCastle.Value
            + (int)SldNeutralLowCastle.Value
            + (int)SldNeutralMediumNoCastle.Value
            + (int)SldNeutralMediumCastle.Value
            + (int)SldNeutralHighNoCastle.Value
            + (int)SldNeutralHighCastle.Value;

        private void UpdateAdvancedModeVisibility()
        {
            if (PnlAdvancedNeutralZones == null) return;
            bool advanced = ChkAdvancedMode.IsChecked == true;
            PnlAdvancedNeutralZones.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlAdvancedZoneSizes.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlAdvancedSeparation.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlAdvancedGameRules.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlSimpleNeutralCountLabel.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
            SldNeutral.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
            ChkExperimentalMapSizes.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            if (!advanced)
                ChkExperimentalMapSizes.IsChecked = false;
            RefreshMapSizeOptions();
            UpdateWinConditionDetailVisibility();
        }

        private void CmbVictory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;

            int idx = CmbVictory.SelectedIndex;
            if (idx >= 0 && idx < KnownValues.VictoryConditionIds.Length)
                ApplyVictoryPreset(KnownValues.VictoryConditionIds[idx]);

            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ApplyVictoryPreset(string victoryCondition)
        {
            ChkLostStartCity.IsChecked = false;
            ChkLostStartHero.IsChecked = false;
            ChkCityHold.IsChecked = false;
            ChkGladiatorArena.IsChecked = false;
            ChkTournament.IsChecked = false;

            SldLostStartCityDay.Value = 3;
            SldCityHoldDays.Value = 6;
            SldGladiatorDelay.Value = 30;
            SldGladiatorCountDay.Value = 3;
            SldTournamentRoundCount.Value = 3;
            SldTournamentRoundDuration.Value = 3;
            SldTournamentFirstAnnounceDay.Value = 7;
            SldTournamentRoundInterval.Value = 7;
            SldTournamentPointsToWin.Value = 2;

            switch (victoryCondition)
            {
                case "win_condition_3":
                    ChkLostStartCity.IsChecked = true;
                    break;
                case "win_condition_4":
                    ChkLostStartHero.IsChecked = true;
                    ChkGladiatorArena.IsChecked = true;
                    break;
                case "win_condition_5":
                    ChkCityHold.IsChecked = true;
                    break;
                case "win_condition_6":
                    ChkLostStartHero.IsChecked = true;
                    ChkTournament.IsChecked = true;
                    SldGladiatorDelay.Value = 21;
                    SldGladiatorCountDay.Value = 8;
                    break;
            }
        }

        private void UpdateWinConditionDetailVisibility()
        {
            if (PnlLostStartCityDetails == null) return;

            string selectedVictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1";

            if (selectedVictoryCondition == "win_condition_3")
                ChkLostStartCity.IsChecked = true;
            if (selectedVictoryCondition == "win_condition_4")
            {
                ChkLostStartHero.IsChecked = true;
                ChkGladiatorArena.IsChecked = true;
            }
            if (selectedVictoryCondition == "win_condition_5")
                ChkCityHold.IsChecked = true;
            if (selectedVictoryCondition == "win_condition_6")
            {
                ChkLostStartHero.IsChecked = true;
                ChkTournament.IsChecked = true;
            }

            ChkLostStartCity.IsEnabled = selectedVictoryCondition != "win_condition_3";
            ChkLostStartHero.IsEnabled = selectedVictoryCondition is not "win_condition_4" and not "win_condition_6";
            ChkCityHold.IsEnabled = selectedVictoryCondition != "win_condition_5";
            ChkGladiatorArena.IsEnabled = selectedVictoryCondition != "win_condition_4";
            ChkTournament.IsEnabled = selectedVictoryCondition != "win_condition_6";

            PnlLostStartCityDetails.Visibility = ChkLostStartCity.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlCityHoldDetails.Visibility = ChkCityHold.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGladiatorDetails.Visibility = ChkGladiatorArena.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlTournamentDetails.Visibility = ChkTournament.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExperimentalMapSizeWarningVisibility()
        {
            if (TxtExperimentalMapSizeWarning == null) return;
            bool includeExperimental = ChkAdvancedMode?.IsChecked == true && ChkExperimentalMapSizes?.IsChecked == true;
            TxtExperimentalMapSizeWarning.Visibility = includeExperimental ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePlayerCastleFactionVisibility()
        {
            if (PnlPlayerCastleFactionOption == null || SldPlayerCastles == null) return;
            bool hasExtraCastles = (int)SldPlayerCastles.Value > 1;
            PnlPlayerCastleFactionOption.Visibility = hasExtraCastles ? Visibility.Visible : Visibility.Collapsed;
            if (!hasExtraCastles)
                ChkMatchPlayerCastleFactions.IsChecked = false;
        }

        private void UpdateIsolateDescVisibility()
        {
            if (TxtIsolateDesc == null || ChkNoDirectPlayerConn == null) return;
            TxtIsolateDesc.Visibility = ChkNoDirectPlayerConn.IsChecked == true && ChkNoDirectPlayerConn.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateRoadsHintVisibility()
        {
            if (TxtRoadsHint == null || ChkGenerateRoads == null || SldNeutralCastles == null) return;
            TxtRoadsHint.Visibility = ChkGenerateRoads.IsChecked == true && (int)SldNeutralCastles.Value == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateBalancedZonePlacementDescVisibility()
        {
            if (TxtBalancedZonePlacementDesc == null || ChkBalancedZonePlacement == null) return;
            TxtBalancedZonePlacementDesc.Visibility = ChkBalancedZonePlacement.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Settings persistence ───────────────────────────────────────────────

        private SettingsFile GatherSettings() => new()
        {
            TemplateName          = TxtTemplateName.Text.Trim(),
            MapSize               = SelectedMapSize(),
            PlayerCount           = (int)SldPlayers.Value,
            NeutralZoneCount      = (int)SldNeutral.Value,
            PlayerZoneCastles     = (int)SldPlayerCastles.Value,
            NeutralZoneCastles    = (int)SldNeutralCastles.Value,
            AdvancedMode          = ChkAdvancedMode.IsChecked == true,
            NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
            NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
            NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
            NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
            NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
            NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
            MatchPlayerCastleFactions = ChkMatchPlayerCastleFactions.IsChecked == true,
            MinNeutralZonesBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value,
            ExperimentalBalancedZonePlacement = ChkBalancedZonePlacement.IsChecked == true,
            ExperimentalMapSizes  = ChkExperimentalMapSizes.IsChecked == true,
            PlayerZoneSize        = ChkAdvancedMode.IsChecked == true ? SldPlayerZoneSize.Value : 1.0,
            NeutralZoneSize       = ChkAdvancedMode.IsChecked == true ? SldNeutralZoneSize.Value : 1.0,
            GuardRandomization    = SldGuardRandomization.Value / 100.0,
            HeroCountMin          = (int)SldHeroMin.Value,
            HeroCountMax          = (int)SldHeroMax.Value,
            HeroCountIncrement    = (int)SldHeroIncrement.Value,
            Topology              = TopologyOptions[CmbTopology.SelectedIndex].Topology,
            RandomPortals         = ChkRandomPortals.IsChecked == true,
            SpawnRemoteFootholds  = ChkSpawnFootholds.IsChecked == true,
            GenerateRoads         = ChkGenerateRoads.IsChecked == true,
            NoDirectPlayerConn    = ChkNoDirectPlayerConn.IsChecked == true,
            ResourceDensityPercent = (int)SldResourceDensity.Value,
            StructureDensityPercent = (int)SldStructureDensity.Value,
            NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
            BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
            VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1",
            FactionLawsExpPercent = (int)SldFactionLawsExp.Value,
            AstrologyExpPercent = (int)SldAstrologyExp.Value,
            LostStartCity = ChkLostStartCity.IsChecked == true,
            LostStartCityDay = (int)SldLostStartCityDay.Value,
            LostStartHero = ChkLostStartHero.IsChecked == true,
            CityHold = ChkCityHold.IsChecked == true,
            CityHoldDays = (int)SldCityHoldDays.Value,
            GladiatorArena = ChkGladiatorArena.IsChecked == true,
            GladiatorArenaDaysDelayStart = (int)SldGladiatorDelay.Value,
            GladiatorArenaCountDay = (int)SldGladiatorCountDay.Value,
            Tournament = ChkTournament.IsChecked == true,
            TournamentRoundCount = (int)SldTournamentRoundCount.Value,
            TournamentRoundDuration = (int)SldTournamentRoundDuration.Value,
            TournamentFirstAnnounceDay = (int)SldTournamentFirstAnnounceDay.Value,
            TournamentRoundInterval = (int)SldTournamentRoundInterval.Value,
            TournamentPointsToWin = (int)SldTournamentPointsToWin.Value,
        };

        private void ApplySettings(SettingsFile s)
        {
            TxtTemplateName.Text    = s.TemplateName;
            bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001 || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
            bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
            ChkAdvancedMode.IsChecked = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;
            ChkExperimentalMapSizes.IsChecked = needsExperimentalMapSizes;
            RefreshMapSizeOptions(s.MapSize);
            SldPlayers.Value        = s.PlayerCount;
            SldNeutral.Value        = s.NeutralZoneCount;
            SldPlayerCastles.Value  = s.PlayerZoneCastles;
            SldNeutralCastles.Value = s.NeutralZoneCastles;
            SldNeutralLowNoCastle.Value = s.NeutralLowNoCastleCount;
            SldNeutralLowCastle.Value = s.NeutralLowCastleCount;
            SldNeutralMediumNoCastle.Value = s.NeutralMediumNoCastleCount;
            SldNeutralMediumCastle.Value = s.NeutralMediumCastleCount;
            SldNeutralHighNoCastle.Value = s.NeutralHighNoCastleCount;
            SldNeutralHighCastle.Value = s.NeutralHighCastleCount;
            ChkMatchPlayerCastleFactions.IsChecked = s.MatchPlayerCastleFactions;
            SldMinNeutralBetweenPlayers.Value = s.MinNeutralZonesBetweenPlayers;
            ChkBalancedZonePlacement.IsChecked = s.ExperimentalBalancedZonePlacement;
            SldPlayerZoneSize.Value = Math.Clamp(s.PlayerZoneSize, 0.1, 2.0);
            SldNeutralZoneSize.Value = Math.Clamp(s.NeutralZoneSize, 0.1, 2.0);
            SldGuardRandomization.Value = GuardRandomizationPercent(s.GuardRandomization);
            SldHeroMin.Value        = s.HeroCountMin;
            SldHeroMax.Value        = s.HeroCountMax;
            SldHeroIncrement.Value  = s.HeroCountIncrement;
            int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
            if (topoIdx >= 0) CmbTopology.SelectedIndex = topoIdx;
            ChkRandomPortals.IsChecked        = s.RandomPortals;
            ChkSpawnFootholds.IsChecked       = s.SpawnRemoteFootholds;
            ChkGenerateRoads.IsChecked        = s.GenerateRoads;
            ChkNoDirectPlayerConn.IsChecked   = s.NoDirectPlayerConn;
            SldResourceDensity.Value          = s.EffectiveResourceDensityPercent;
            SldStructureDensity.Value         = s.EffectiveStructureDensityPercent;
            SldNeutralStackStrength.Value     = s.NeutralStackStrengthPercent;
            SldBorderGuardStrength.Value      = s.BorderGuardStrengthPercent;
            int victoryIdx = Array.IndexOf(KnownValues.VictoryConditionIds, s.VictoryCondition);
            CmbVictory.SelectedIndex = victoryIdx >= 0 ? victoryIdx : 0;
            SldFactionLawsExp.Value = Math.Clamp(s.FactionLawsExpPercent, 25, 200);
            SldAstrologyExp.Value = Math.Clamp(s.AstrologyExpPercent, 25, 200);
            ChkLostStartCity.IsChecked = s.LostStartCity;
            SldLostStartCityDay.Value = Math.Clamp(s.LostStartCityDay, 1, 30);
            ChkLostStartHero.IsChecked = s.LostStartHero;
            ChkCityHold.IsChecked = s.CityHold;
            SldCityHoldDays.Value = Math.Clamp(s.CityHoldDays, 1, 30);
            ChkGladiatorArena.IsChecked = s.GladiatorArena;
            SldGladiatorDelay.Value = Math.Clamp(s.GladiatorArenaDaysDelayStart, 1, 60);
            SldGladiatorCountDay.Value = Math.Clamp(s.GladiatorArenaCountDay, 1, 30);
            ChkTournament.IsChecked = s.Tournament;
            SldTournamentRoundCount.Value = Math.Clamp(s.TournamentRoundCount, 1, 5);
            SldTournamentRoundDuration.Value = Math.Clamp(s.TournamentRoundDuration, 1, 14);
            SldTournamentFirstAnnounceDay.Value = Math.Clamp(s.TournamentFirstAnnounceDay, 1, 60);
            SldTournamentRoundInterval.Value = Math.Clamp(s.TournamentRoundInterval, 1, 30);
            SldTournamentPointsToWin.Value = Math.Clamp(s.TournamentPointsToWin, 1, 5);
            UpdateValueLabels();
            UpdateAdvancedModeVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateRoadsHintVisibility();
            UpdateBalancedZonePlacementDescVisibility();
            UpdateWinConditionDetailVisibility();
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
                FileName   = TxtTemplateName.Text.Trim().Length > 0 ? TxtTemplateName.Text.Trim() : "My Settings",
                DefaultExt = ".oetgs",
            };
            if (dlg.ShowDialog() == true)
                SaveToPath(dlg.FileName);
        }

        // ── Generate ──────────────────────────────────────────────────────────

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
                MapSize = SelectedMapSize(),
                VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                    ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                    : "win_condition_1",
                PlayerZoneCastles = (int)SldPlayerCastles.Value,
                NeutralZoneCastles = (int)SldNeutralCastles.Value,
                AdvancedMode = ChkAdvancedMode.IsChecked == true,
                NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
                NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
                NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
                NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
                NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
                NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
                MatchPlayerCastleFactions = ChkMatchPlayerCastleFactions.IsChecked == true,
                MinNeutralZonesBetweenPlayers = ChkAdvancedMode.IsChecked == true ? (int)SldMinNeutralBetweenPlayers.Value : 0,
                ExperimentalBalancedZonePlacement = ChkBalancedZonePlacement.IsChecked == true,
                PlayerZoneSize = ChkAdvancedMode.IsChecked == true ? SldPlayerZoneSize.Value : 1.0,
                NeutralZoneSize = ChkAdvancedMode.IsChecked == true ? SldNeutralZoneSize.Value : 1.0,
                GuardRandomization = ChkAdvancedMode.IsChecked == true ? SldGuardRandomization.Value / 100.0 : 0.05,
                NoDirectPlayerConnections = ChkNoDirectPlayerConn.IsChecked == true,
                RandomPortals = ChkRandomPortals.IsChecked == true,
                SpawnRemoteFootholds = ChkSpawnFootholds.IsChecked == true,
                GenerateRoads = ChkGenerateRoads.IsChecked == true,
                Topology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default,
                ResourceDensityPercent = (int)SldResourceDensity.Value,
                StructureDensityPercent = (int)SldStructureDensity.Value,
                NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
                BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
                FactionLawsExpPercent = ChkAdvancedMode.IsChecked == true ? (int)SldFactionLawsExp.Value : 100,
                AstrologyExpPercent = ChkAdvancedMode.IsChecked == true ? (int)SldAstrologyExp.Value : 100,
                LostStartCity = ChkAdvancedMode.IsChecked == true && ChkLostStartCity.IsChecked == true,
                LostStartCityDay = (int)SldLostStartCityDay.Value,
                LostStartHero = ChkAdvancedMode.IsChecked == true && ChkLostStartHero.IsChecked == true,
                CityHold = ChkAdvancedMode.IsChecked == true && ChkCityHold.IsChecked == true,
                CityHoldDays = (int)SldCityHoldDays.Value,
                GladiatorArena = ChkAdvancedMode.IsChecked == true && ChkGladiatorArena.IsChecked == true,
                GladiatorArenaDaysDelayStart = (int)SldGladiatorDelay.Value,
                GladiatorArenaCountDay = (int)SldGladiatorCountDay.Value,
                Tournament = ChkAdvancedMode.IsChecked == true && ChkTournament.IsChecked == true,
                TournamentRoundCount = (int)SldTournamentRoundCount.Value,
                TournamentRoundDuration = (int)SldTournamentRoundDuration.Value,
                TournamentFirstAnnounceDay = (int)SldTournamentFirstAnnounceDay.Value,
                TournamentRoundInterval = (int)SldTournamentRoundInterval.Value,
                TournamentPointsToWin = (int)SldTournamentPointsToWin.Value
            };

            var template = TemplateGenerator.Generate(settings);

            string? gameTemplatesPath = FindOldenEraTemplatesPath();

            var dlg = new SaveFileDialog
            {
                Title = "Save Template",
                Filter = "RMG Template (*.rmg.json)|*.rmg.json",
                FileName = $"{settings.TemplateName}.rmg.json"
            };

            if (gameTemplatesPath != null)
                dlg.InitialDirectory = gameTemplatesPath;

            if (dlg.ShowDialog() != true) return;

            string json = JsonSerializer.Serialize(template, JsonOptions);
            File.WriteAllText(dlg.FileName, json);

            string previewPath = TemplatePreviewPngWriter.GetSidecarPath(dlg.FileName);
            string? previewError = null;
            try
            {
                TemplatePreviewPngWriter.Save(template, previewPath);
            }
            catch (Exception ex)
            {
                previewError = ex.Message;
            }

            string savedMsg = $"Template successfully saved to:\n\n{dlg.FileName}";
            if (previewError == null)
                savedMsg += $"\n\nPreview PNG saved to:\n\n{previewPath}";
            else
                savedMsg += $"\n\nTemplate saved, but the preview PNG could not be created:\n{previewError}";
            if (gameTemplatesPath == null)
                savedMsg += "\n\n\n💡 Tip: Templates must be placed in:\n<Olden Era install folder>\\HeroesOldenEra_Data\\StreamingAssets\\map_templates";

            MessageBox.Show(savedMsg, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Tries to locate the Olden Era map_templates folder via the Steam registry.
        /// Returns null if the game installation cannot be found.
        /// </summary>
        private static string? FindOldenEraTemplatesPath()
        {
            // Olden Era Steam App ID
            const string appId = "3105440";

            // Steam stores per-app install paths under this key.
            string[] registryRoots =
            [
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}"
            ];

            foreach (var keyPath in registryRoots)
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key?.GetValue("InstallLocation") is string installDir && Directory.Exists(installDir))
                    {
                        string templatesDir = Path.Combine(installDir, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                        if (Directory.Exists(templatesDir))
                            return templatesDir;
                    }
                }
                catch { /* registry access denied — skip */ }
            }

            // Fallback: check common Steam library locations manually.
            string[] steamLibraryRoots =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
            ];

            foreach (var candidate in steamLibraryRoots)
            {
                string templatesDir = Path.Combine(candidate, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                if (Directory.Exists(templatesDir))
                    return templatesDir;
            }
            return null;
        }
    }
}
