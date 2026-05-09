using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        private const string GitHubPage = "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator";
        private const string GitHubApiLatestRelease = "https://api.github.com/repos/KhanDevelopsGames/Olden-Era---Template-Generator/releases/latest";
        private const string GitHubReleasesPage     = "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases";
        private const string DiscordServer = "https://discord.gg/UqT8KshsxW";
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
        private readonly ObservableCollection<ZoneContentItemUI> _zoneContentMines = new();
        private readonly ObservableCollection<ZoneContentItemUI> _treasureContentItems = new();
        private readonly ObservableCollection<ZoneContentItemUI> _randomHires = new();
        private readonly ObservableCollection<ZoneContentItemUI> _resourceBanks = new();

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
            CmbVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            CmbVictory.SelectedIndex = 0; // Classic (win_condition_1)
            CmbTopology.ItemsSource = TopologyOptions.Select(t => t.Label).ToList();
            CmbTopology.SelectedIndex = 0; // Random is first
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateBalancedZonePlacementDescVisibility();
            InitializeZoneContentPresets();
            InitializeDefaultPlayerZoneContents();
            DataContext = new
            {
                MineContentItems = _zoneContentMines,
                TreasureContentItems = _treasureContentItems,
                RandomHireContentItems = _randomHires,
                ResourceBankContentItems = _resourceBanks
            };
            // Fire-and-forget background update check — never blocks the UI.
            _ = CheckForUpdateAsync(version);

            TxtTemplateName.TextChanged += (_, _) => { MarkDirtyNameOnly(); Validate(); };
            UpdateTitle();
            TxtWindowTitle.Text = Title;
        }
        private void InitializeDefaultPlayerZoneContents()
        {
            // ── Basic mines — guarded, anchored near the player castle (every template). ──
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.MineWood, isGuarded: true, nearCastle: true, roadDistance: "Near"));
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.MineOre, isGuarded: true, nearCastle: true, roadDistance: "Near"));
            // ── Gold mine (Exodus/Staircase/Yin Yang pattern). ──
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.MineGold, isGuarded: true, roadDistance: "Near"));
            // ── Rare mines spread along roads (Exodus/Staircase/Yin Yang pattern). ──
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.MineCrystals, isGuarded: true, roadDistance: "Next To"));
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.MineMercury, isGuarded: true, roadDistance: "Next To"));
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.MineGemstones, isGuarded: true, roadDistance: "Next To"));
            _zoneContentMines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, isGuarded: true, roadDistance: "Next To"));
            // ── Loot — epic items + army pandora (Exodus/Blitz pattern). ──
            _treasureContentItems.Add(CreateZoneContentItem(ContentIds.PandoraBox, isGuarded: true));
            _treasureContentItems.Add(CreateZoneContentItem(ContentIds.RandomItemEpic, isGuarded: true));

            // ── Hiring — low-tier × 2 + high-tier × 1 + full pool × 1 (Kerberos + Universe blend). ──
            _randomHires.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier, isGuarded: true, count: 2, isGroup: true));
            _randomHires.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier, isGuarded: true, isGroup: true));
            _randomHires.Add(CreateZoneContentItem(IncludeListIds.RandomHiresAllTier, isGuarded: true, isGroup: true));

            // ── Guarded resource banks — tier 1 × 2 + tier 2 × 1 (Exodus pattern). ──
            _resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier1, isGuarded: true, count: 2, isGroup: true));
            _resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2, isGuarded: true, isGroup: true));
        }
        private void InitializeZoneContentPresets()
        {
            /* Populate the Mines dropdown menu */
            var mineNames = new List<string>();
            foreach (SidMapping sidMapping in ContentItemGroup.Mines)
            {
                mineNames.Add(sidMapping.Name);
            }
            CmbZoneContentPreset.ItemsSource = mineNames;
            CmbZoneContentPreset.SelectedIndex = 0;

            /* Populate the Treasures dropdown menu */
            var treasurePresetNames = new List<string>();
            foreach (SidMapping sidMapping in ContentItemGroup.Treasures)
            {
                treasurePresetNames.Add(sidMapping.Name);
            }
            CmbTreasureContentPreset.ItemsSource = treasurePresetNames;
            CmbTreasureContentPreset.SelectedIndex = 0;

            /* Populate the Random Hires dropdown menu */
            var randomHirePresetNames = new List<string>();
            foreach (SidMapping sidMapping in ContentItemGroup.HireBuildings)
            {
                randomHirePresetNames.Add(sidMapping.Name);
            }
            CmbRandomHireContentPreset.ItemsSource = randomHirePresetNames;
            CmbRandomHireContentPreset.SelectedIndex = 0;

             /* Populate the Resource Banks dropdown menu */
            var resourceBankPresetNames = new List<string>();
            foreach (SidMapping sidMapping in ContentItemGroup.ResourceBanks)
            {
                resourceBankPresetNames.Add(sidMapping.Name);
            }
            CmbResourceBankContentPreset.ItemsSource = resourceBankPresetNames;
            CmbResourceBankContentPreset.SelectedIndex = 0;
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
                _generatedTemplate.Name = TxtTemplateName.Text.Trim();
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
            TxtHubZoneSize.Text = $"{SldHubZoneSize.Value:F2}x";
            TxtHubCastles.Text = ((int)SldHubCastles.Value).ToString();
            TxtGuardRandomization.Text = $"{(int)SldGuardRandomization.Value}%";
            TxtLostStartCityDay.Text = ((int)SldLostStartCityDay.Value).ToString();
            TxtCityHoldDays.Text = ((int)SldCityHoldDays.Value).ToString();
            TxtGladiatorDelay.Text = ((int)SldGladiatorDelay.Value).ToString();
            TxtGladiatorCountDay.Text = ((int)SldGladiatorCountDay.Value).ToString();
            TxtTournamentPointsToWin.Text = ((int)SldTournamentPointsToWin.Value).ToString();
            TxtTournamentFirstTournamentDay.Text = ((int)SldTournamentFirstTournamentDay.Value).ToString();
            TxtTournamentInterval.Text = ((int)SldTournamentInterval.Value).ToString();
        }

        private void SetValidationText(string text)
        {
            TxtValidation.Text = text;
            TxtValidation.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool Validate()
        {
            int heroMin = (int)SldHeroMin.Value;
            int heroMax = (int)SldHeroMax.Value;
            int players = (int)SldPlayers.Value;
            int neutral = TotalNeutralZonesFromUi();

            if (heroMin > heroMax)
            {
                SetValidationText("Min Heroes cannot be greater than Max Heroes.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            int maxZones = _advancedZoneSettings ? AdvancedModeMaxZones : SimpleModeMaxZones;
            if (players + neutral > maxZones)
            {
                SetValidationText($"Total zones (players + neutral) cannot exceed {maxZones}.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtTemplateName.Text))
            {
                SetValidationText("Template name cannot be empty.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            var warnings = new System.Collections.Generic.List<string>();

            if (TxtTemplateName.Text.Trim().Equals("Custom Template", StringComparison.OrdinalIgnoreCase))
                warnings.Add("⚠️ The template is still using the default name \"Custom Template\". Consider renaming it before saving.");

            int selectedMapSize = SelectedMapSize();
            int totalZones = players + neutral;
            var selectedTopology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default;
            // Hub layout has an extra central zone that also occupies map area.
            int totalZonesIncludingHub = selectedTopology == MapTopology.HubAndSpoke ? totalZones + 1 : totalZones;
            if (totalZonesIncludingHub > 0 && (selectedMapSize * selectedMapSize) / totalZonesIncludingHub < 1024)
                warnings.Add($"⚠️ Estimated zone size is too small. The game may freeze when loading the map. Increase the map size or reduce the number of zones.");

            if (selectedMapSize > KnownValues.MaxOfficialMapSize)
                warnings.Add("Experimental map sizes above 240x240 are not confirmed by official templates; generated maps may fail, freeze, or behave unpredictably in game.");

            if (totalZones > 10)
            {
                int playerCastles = (int)SldPlayerCastles.Value;
                int neutralCastles = _advancedZoneSettings ? 0 : (int)SldNeutralCastles.Value;
                if (playerCastles > 1 || neutralCastles > 1)
                    warnings.Add("⚠️ Using more than 1 castle per zone with more than 10 total zones may cause the game to freeze when generating the map. Consider reducing the number of castles.");
            }

            int minNeutralBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value;
            if (_advancedZoneSettings && minNeutralBetweenPlayers > 0)
            {
                var separationSettings = new GeneratorSettings
                {
                    PlayerCount = players,
                    Topology = selectedTopology,
                    RandomPortals = ChkRandomPortals.IsChecked == true,
                    MinNeutralZonesBetweenPlayers = minNeutralBetweenPlayers
                };

                if (!TemplateGenerator.CanHonorNeutralSeparation(separationSettings, neutral))
                    warnings.Add("Minimum neutral separation cannot be guaranteed with the current layout, neutral zone total, or portal setting; generation will ignore that option.");
            }
            if(selectedTopology == MapTopology.Random && ChkBalancedZonePlacement.IsChecked == true && totalZones >= 24)
                warnings.Add("Balanced zone placement may produce unexpected results with the random map layout and more than 24 total zones.");

            bool cityHoldActive = ChkCityHold.IsChecked == true;
            if (cityHoldActive)
            {
                if (selectedTopology != MapTopology.HubAndSpoke && neutral == 0)
                {
                    SetValidationText("City Hold requires at least one neutral zone to place the hold city. Add a neutral zone or switch to the Hub layout.");
                    BtnPreview.IsEnabled = false;
                    return false;
                }
            }

            if (ChkNoDirectPlayerConn.IsChecked == true && neutral == 0)
            {
                SetValidationText("\"Connect via neutral zones only\" requires at least one neutral zone. Add a neutral zone or disable this option.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            string selectedVictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1";
            if (selectedVictoryCondition == "win_condition_6" && players != 2)
            {
                SetValidationText("Tournament mode only supports exactly 2 players.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            SetValidationText(string.Join("\n\n", warnings));

            BtnPreview.IsEnabled = true;
            return true;
        }

        private int TotalNeutralZonesFromUi()
        {
            if (!_advancedZoneSettings)
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
            if (CmbMapSize == null) return;

            int selectedSize = requestedSize ?? SelectedMapSize();
            bool includeExperimental = ChkExperimentalMapSizes?.IsChecked == true;
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
            UpdateAdvancedZoneSettingsVisibility();
            PnlHubZoneSize.Visibility = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;
            PnlHubCastles.Visibility  = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;

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
            UpdateBalancedZonePlacementDescVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkRandomPortals_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            PnlMaxPortals.Visibility = ChkRandomPortals.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            MarkDirty();
            Validate();
        }

        private void SldMaxPortals_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            LblMaxPortals.Text = ((int)SldMaxPortals.Value).ToString();
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
            _advancedZoneSettings = ChkAdvancedZoneSettings.IsChecked == true;
            if (_advancedZoneSettings && TotalAdvancedNeutralZonesFromSliders() == 0 && (int)SldNeutral.Value > 0)
            {
                if ((int)SldNeutralCastles.Value > 0)
                    SldNeutralMediumCastle.Value = SldNeutral.Value;
                else
                    SldNeutralMediumNoCastle.Value = SldNeutral.Value;
            }

            UpdateAdvancedZoneSettingsVisibility();
            UpdateValueLabels();
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

        private void UpdateAdvancedZoneSettingsVisibility()
        {
            if (PnlAdvancedNeutralZones == null) return;
            bool advanced = _advancedZoneSettings;
            PnlAdvancedNeutralZones.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlAdvancedZoneSizes.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlSimpleNeutralCountLabel.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
            SldNeutral.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
            if (ChkAdvancedZoneSettings != null)
                ChkAdvancedZoneSettings.IsChecked = advanced;
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

            SldTournamentPointsToWin.Value = 2;
            SldTournamentInterval.Value = 7;
            SldTournamentFirstTournamentDay.Value = 14;
            ChkTournamentSaveArmy.IsChecked = true;

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

            bool isTournament = selectedVictoryCondition == "win_condition_6";

            if (isTournament)
            {
                // Tournament is exclusive — force it on and disable all other conditions.
                ChkTournament.IsChecked = true;
                ChkLostStartCity.IsChecked = false;
                ChkLostStartHero.IsChecked = false;
                ChkCityHold.IsChecked = false;
                ChkGladiatorArena.IsChecked = false;
            }
            else
            {
                // Tournament is unavailable outside of the Tournament win condition.
                ChkTournament.IsChecked = false;
                if (selectedVictoryCondition == "win_condition_3")
                    ChkLostStartCity.IsChecked = true;
                if (selectedVictoryCondition == "win_condition_4")
                {
                    ChkLostStartHero.IsChecked = true;
                    ChkGladiatorArena.IsChecked = true;
                }
                if (selectedVictoryCondition == "win_condition_5")
                    ChkCityHold.IsChecked = true;
            }

            ChkLostStartCity.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_3";
            ChkLostStartHero.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_4";
            ChkCityHold.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_5";
            ChkGladiatorArena.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_4";
            ChkTournament.IsChecked = isTournament;
            ChkTournament.IsEnabled = isTournament;

            PnlLostStartCityDetails.Visibility = ChkLostStartCity.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlCityHoldDetails.Visibility = ChkCityHold.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGladiatorDetails.Visibility = ChkGladiatorArena.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlTournamentDetails.Visibility = isTournament ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExperimentalMapSizeWarningVisibility()
        {
            if (TxtExperimentalMapSizeWarning == null) return;
            bool includeExperimental = ChkExperimentalMapSizes?.IsChecked == true;
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

        private void UpdateBalancedZonePlacementDescVisibility()
        {
            if (TxtBalancedZonePlacementDesc == null || ChkBalancedZonePlacement == null) return;
            TxtBalancedZonePlacementDesc.Visibility = ChkBalancedZonePlacement.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnAddMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = CmbZoneContentPreset.SelectedItem as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return;

            SidMapping? preset = GlobalContent.GetByName(name);
            if (preset == null)
                return;
            _zoneContentMines.Add(CreateZoneContentItem(preset));
            MarkDirty();
        }

        private void BtnAddTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = CmbTreasureContentPreset.SelectedItem as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return;

            SidMapping? preset = GlobalContent.GetByName(name);
            if (preset == null)
                return;
            _treasureContentItems.Add(CreateZoneContentItem(preset));
            MarkDirty();
        }

        private void BtnAddRandomHireContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbRandomHireContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return;

            SidMapping? preset = GlobalContent.GetByName(name);
            if (preset == null)
                return;

            if(preset.Sid.ToLower().Contains("content")) 
            {
                _randomHires.Add(CreateZoneContentItem(preset, isGroup: true));
            }
            else
            {
                _randomHires.Add(CreateZoneContentItem(preset, isGroup: false));
            }
            MarkDirty();
        }

        private void BtnAddResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return;

            SidMapping? preset = GlobalContent.GetByName(name);
            if (preset == null)
                return;

            _resourceBanks.Add(CreateZoneContentItem(preset, isGroup: true));
            MarkDirty();
        }

        private void BtnRemoveZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            if (sender is not Button button || button.DataContext is not ZoneContentItemUI item)
                return;

            if (_zoneContentMines.Remove(item)
                || _treasureContentItems.Remove(item)
                || _randomHires.Remove(item)
                || _resourceBanks.Remove(item))
                MarkDirty();
        }

        private void BtnResetPlayerZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            _zoneContentMines.Clear();
            _treasureContentItems.Clear();
            _randomHires.Clear();
            _resourceBanks.Clear();

            InitializeDefaultPlayerZoneContents();
            MarkDirty();
        }

        private static ZoneContentItemUI CreateZoneContentItem(SidMapping preset, int count = 1, bool isGuarded = false, bool nearCastle = false, string roadDistance = "Any", bool isGroup = false)
        {
            return new ZoneContentItemUI
            {
                SidMapping = preset,
                Count = count,
                IsGuarded = isGuarded,
                NearCastle = nearCastle,
                RoadDistance = roadDistance,
                IsGroup = isGroup
            };
        }

        // -- Settings persistence -----------------------------------------------

        private SettingsFile GatherSettings() => new()
        {
            TemplateName          = TxtTemplateName.Text.Trim(),
            MapSize               = SelectedMapSize(),
            PlayerCount           = (int)SldPlayers.Value,
            NeutralZoneCount      = (int)SldNeutral.Value,
            PlayerZoneCastles     = (int)SldPlayerCastles.Value,
            NeutralZoneCastles    = (int)SldNeutralCastles.Value,
            AdvancedMode          = _advancedZoneSettings,
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
            PlayerZoneSize        = _advancedZoneSettings ? SldPlayerZoneSize.Value : 1.0,
            NeutralZoneSize       = _advancedZoneSettings ? SldNeutralZoneSize.Value : 1.0,
            HubZoneSize           = SldHubZoneSize.Value,
            HubZoneCastles        = (int)SldHubCastles.Value,
            GuardRandomization    = SldGuardRandomization.Value / 100.0,
            HeroCountMin          = (int)SldHeroMin.Value,
            HeroCountMax          = (int)SldHeroMax.Value,
            HeroCountIncrement    = (int)SldHeroIncrement.Value,
            Topology              = TopologyOptions[CmbTopology.SelectedIndex].Topology,
            RandomPortals         = ChkRandomPortals.IsChecked == true,
            MaxPortalConnections  = (int)SldMaxPortals.Value,
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
            AstrologyExpPercent   = (int)SldAstrologyExp.Value,
            LostStartCity         = ChkLostStartCity.IsChecked == true,
            LostStartCityDay = (int)SldLostStartCityDay.Value,
            LostStartHero         = ChkLostStartHero.IsChecked == true,
            CityHold              = ChkCityHold.IsChecked == true,
            CityHoldDays = (int)SldCityHoldDays.Value,
            GladiatorArena               = ChkGladiatorArena.IsChecked == true,
            GladiatorArenaDaysDelayStart = (int)SldGladiatorDelay.Value,
            GladiatorArenaCountDay       = (int)SldGladiatorCountDay.Value,
            Tournament                   = ChkTournament.IsChecked == true,
            TournamentFirstTournamentDay = (int)SldTournamentFirstTournamentDay.Value,
            TournamentInterval = (int)SldTournamentInterval.Value,
            TournamentPointsToWin = (int)SldTournamentPointsToWin.Value,
            TournamentSaveArmy = ChkTournamentSaveArmy.IsChecked == true,
            PlayerZoneMandatoryContent = BuildPlayerZoneMandatoryContentFromUi(),
        };

        private void ApplySettings(SettingsFile s)
        {
            TxtTemplateName.Text    = s.TemplateName;
            bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001 || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
            bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
            _advancedZoneSettings = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;
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
            SldHubZoneSize.Value = Math.Clamp(s.HubZoneSize, 0.25, 3.0);
            SldHubCastles.Value = Math.Clamp(s.HubZoneCastles, 0, 4);
            SldGuardRandomization.Value = GuardRandomizationPercent(s.GuardRandomization);
            SldHeroMin.Value        = s.HeroCountMin;
            SldHeroMax.Value        = s.HeroCountMax;
            SldHeroIncrement.Value  = s.HeroCountIncrement;
            int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
            if (topoIdx >= 0) CmbTopology.SelectedIndex = topoIdx;
            ChkRandomPortals.IsChecked        = s.RandomPortals;
            SldMaxPortals.Value               = Math.Clamp(s.MaxPortalConnections, 1, 32);
            PnlMaxPortals.Visibility          = s.RandomPortals ? Visibility.Visible : Visibility.Collapsed;
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
            SldTournamentFirstTournamentDay.Value = Math.Clamp(s.TournamentFirstTournamentDay, 1, 60);
            SldTournamentInterval.Value = Math.Clamp(s.TournamentInterval, 1, 30);
            SldTournamentPointsToWin.Value = Math.Clamp(s.TournamentPointsToWin, 1, 10);
            ChkTournamentSaveArmy.IsChecked = s.TournamentSaveArmy;
            ApplyPlayerZoneMandatoryContentFromSettings(s.PlayerZoneMandatoryContent);
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();
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

        // -- Generate ----------------------------------------------------------

        // The most recently generated template — used by BtnSaveGenerated_Click
        private RmgTemplate? _generatedTemplate;
        private MapTopology  _generatedTopology;
        private bool _templateOutdated = false;

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate()) return;
            var settings = BuildSettings();
            _generatedTemplate = TemplateGenerator.Generate(settings);
            _generatedTopology = settings.Topology;
            _templateOutdated = false;
            ImgPreview.Source = TemplatePreviewPngWriter.Render(_generatedTemplate, _generatedTopology);
            lblNoPreview.Content = "?";
            BtnSaveGenerated.Visibility = Visibility.Visible;
            UpdateOutdatedWarning();
            Validate(); // refresh warnings now that template is up to date
        }

        private void BtnSaveGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedTemplate is null) return;

            string? gameTemplatesPath = FindOldenEraTemplatesPath();

            string currentTemplateName = TxtTemplateName.Text.Trim();

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

            string previewPath = TemplatePreviewPngWriter.GetSidecarPath(dlg.FileName);
            string? previewError = null;
            if (ChkSavePreviewImage.IsChecked == true)
            {
                try
                {
                    TemplatePreviewPngWriter.Save(_generatedTemplate, previewPath, _generatedTopology);
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
            TemplateName = TxtTemplateName.Text.Trim(),
            GameMode = CmbGameMode.SelectedItem as string ?? "Classic",
            PlayerCount = (int)SldPlayers.Value,
            HeroSettings = new HeroSettings
            {
                HeroCountMin = (int)SldHeroMin.Value,
                HeroCountMax = (int)SldHeroMax.Value,
                HeroCountIncrement = (int)SldHeroIncrement.Value
            },
            MapSize = SelectedMapSize(),
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                    ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                    : "win_condition_1",
                LostStartCity = ChkLostStartCity.IsChecked == true,
                LostStartCityDay = (int)SldLostStartCityDay.Value,
                LostStartHero = ChkLostStartHero.IsChecked == true,
                CityHold = ChkCityHold.IsChecked == true,
                CityHoldDays = (int)SldCityHoldDays.Value,
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = (int)SldNeutral.Value,
                PlayerZoneCastles = (int)SldPlayerCastles.Value,
                NeutralZoneCastles = (int)SldNeutralCastles.Value,
                ResourceDensityPercent = (int)SldResourceDensity.Value,
                StructureDensityPercent = (int)SldStructureDensity.Value,
                NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
                BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
                HubZoneSize = SldHubZoneSize.Value,
                HubZoneCastles = (int)SldHubCastles.Value,
                Advanced = new AdvancedSettings
                {
                    Enabled = _advancedZoneSettings,
                    NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
                    NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
                    NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
                    NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
                    NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
                    NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
                    PlayerZoneSize = _advancedZoneSettings ? SldPlayerZoneSize.Value : 1.0,
                    NeutralZoneSize = _advancedZoneSettings ? SldNeutralZoneSize.Value : 1.0,
                    GuardRandomization = _advancedZoneSettings ? SldGuardRandomization.Value / 100.0 : 0.05,
                }
            },
            PlayerZoneMandatoryContent = BuildPlayerZoneMandatoryContentFromUi(),
            // Neutral zones between players can be influenced by advanced zone settings, but is functionally independent.
            MinNeutralZonesBetweenPlayers = _advancedZoneSettings ? (int)SldMinNeutralBetweenPlayers.Value : 0,
            MatchPlayerCastleFactions = ChkMatchPlayerCastleFactions.IsChecked == true,
            ExperimentalBalancedZonePlacement = ChkBalancedZonePlacement.IsChecked == true,
            NoDirectPlayerConnections = ChkNoDirectPlayerConn.IsChecked == true,
            RandomPortals = ChkRandomPortals.IsChecked == true,
            MaxPortalConnections = (int)SldMaxPortals.Value,
            SpawnRemoteFootholds = ChkSpawnFootholds.IsChecked == true,
            GenerateRoads = ChkGenerateRoads.IsChecked == true,
            Topology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default,
            FactionLawsExpPercent = (int)SldFactionLawsExp.Value,
            AstrologyExpPercent = (int)SldAstrologyExp.Value,
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = ChkGladiatorArena.IsChecked == true,
                DaysDelayStart = (int)SldGladiatorDelay.Value,
                CountDay = (int)SldGladiatorCountDay.Value
            },
            TournamentRules = new TournamentRules
            {
                Enabled = ChkTournament.IsChecked == true,
                FirstTournamentDay = (int)SldTournamentFirstTournamentDay.Value,
                Interval = (int)SldTournamentInterval.Value,
                PointsToWin = (int)SldTournamentPointsToWin.Value,
                SaveArmy = ChkTournamentSaveArmy.IsChecked == true
            }
        };
        
        /* Creates list of ContentItems for the player zone mandatory content, according to the UI settings. */
        private List<ContentItem> BuildPlayerZoneMandatoryContentFromUi()
        {
            var result = new List<ContentItem>();

            foreach (var item in _zoneContentMines
                         .Concat(_treasureContentItems)
                         .Concat(_randomHires)
                         .Concat(_resourceBanks))
            {
                /* Some initial sanity checks*/
                if (item.Count <= 0) continue;
                if(item.SidMapping == null) continue;

                /* Parse the road distance from the UI setting. "Any" is handled separately. */
                var distance = item.RoadDistance switch
                {
                    "Next To" => DistancePresets.NextTo,
                    "Near" => DistancePresets.Near,
                    "Far" => DistancePresets.Far,
                    "Very Far" => DistancePresets.VeryFar,
                    _ => DistancePresets.Medium
                };

                for (int i = 0; i < item.Count; i++)
                {
                    if (item.IsGroup)
                    {
                        var groupItem = new ContentItem
                        {
                            IncludeLists = new List<string> { item.SidMapping.Sid },
                            IsGuarded = item.IsGuarded
                        };

                        if (item.RoadDistance != "Any")
                        {
                            groupItem.Rules = new List<ContentPlacementRule>
                            {
                                RulePresets.RoadDistance(distance)
                            };
                        }

                        result.Add(groupItem);
                        continue;
                    }

                    var builder = ContentItemBuilder
                        .Create(item.SidMapping.Sid)
                        .Guarded(item.IsGuarded);
                    
                    if(_zoneContentMines.Contains(item))
                        builder.Mine();
                    
                    if (item.NearCastle)
                        builder.AddRule(RulePresets.NearCastle());

                    /* Do not include road placement for "Any" distance */
                    if(item.RoadDistance != "Any")
                        builder.RoadDistance(distance);
                    
                    result.Add(builder.Build());
                }
            }

            return result;
        }

        private void ApplyPlayerZoneMandatoryContentFromSettings(List<ContentItem>? contentItems)
        {
            _zoneContentMines.Clear();
            _treasureContentItems.Clear();
            _randomHires.Clear();
            _resourceBanks.Clear();

            if (contentItems is null || contentItems.Count == 0)
            {
                InitializeDefaultPlayerZoneContents();
                return;
            }

            var groupedItems = new Dictionary<PlayerZoneContentKey, int>();

            foreach (var contentItem in contentItems)
            {
                bool isGroup = contentItem.IncludeLists is { Count: > 0 };
                string? sid = isGroup
                    ? contentItem.IncludeLists![0]
                    : contentItem.Sid;

                if (string.IsNullOrWhiteSpace(sid))
                    continue;

                SidMapping? sidMapping = GlobalContent.GetBySid(sid);
                if (sidMapping is null)
                    continue;

                bool isMine = contentItem.IsMine == true;
                bool isGuarded = contentItem.IsGuarded == true;
                bool nearCastle = HasNearCastleRule(contentItem.Rules);
                string roadDistance = GetRoadDistanceLabel(contentItem.Rules);

                var key = new PlayerZoneContentKey(
                    sidMapping.Sid,
                    isGroup,
                    isMine,
                    isGuarded,
                    nearCastle,
                    roadDistance);

                if (groupedItems.TryGetValue(key, out int currentCount))
                {
                    groupedItems[key] = currentCount + 1;
                }
                else
                {
                    groupedItems[key] = 1;
                }
            }

            foreach (var kvp in groupedItems)
            {
                SidMapping? sidMapping = GlobalContent.GetBySid(kvp.Key.Sid);
                if (sidMapping is null)
                    continue;

                var uiItem = CreateZoneContentItem(
                    sidMapping,
                    count: kvp.Value,
                    isGuarded: kvp.Key.IsGuarded,
                    nearCastle: kvp.Key.NearCastle,
                    roadDistance: kvp.Key.RoadDistance,
                    isGroup: kvp.Key.IsGroup);

                if (kvp.Key.IsMine)
                {
                    _zoneContentMines.Add(uiItem);
                }
                else if (IsRandomHireSid(kvp.Key.Sid))
                {
                    _randomHires.Add(uiItem);
                }
                else if (IsResourceBankSid(kvp.Key.Sid))
                {
                    _resourceBanks.Add(uiItem);
                }
                else
                {
                    _treasureContentItems.Add(uiItem);
                }
            }
        }

        private static bool HasNearCastleRule(List<ContentPlacementRule>? rules)
            => rules?.Any(rule =>
                string.Equals(rule.Type, "MainObject", StringComparison.OrdinalIgnoreCase) &&
                rule.Args?.Any(arg => arg == "0") == true) == true;

        private static string GetRoadDistanceLabel(List<ContentPlacementRule>? rules)
        {
            ContentPlacementRule? roadRule = rules?.FirstOrDefault(rule =>
                string.Equals(rule.Type, "Road", StringComparison.OrdinalIgnoreCase));

            if (roadRule is null || roadRule.TargetMin is null || roadRule.TargetMax is null)
                return "Any";

            double min = roadRule.TargetMin.Value;
            double max = roadRule.TargetMax.Value;

            if (IsDistance(min, max, DistancePresets.NextTo)) return "Next To";
            if (IsDistance(min, max, DistancePresets.Near)) return "Near";
            if (IsDistance(min, max, DistancePresets.Far)) return "Far";
            if (IsDistance(min, max, DistancePresets.VeryFar)) return "Very Far";
            if (IsDistance(min, max, DistancePresets.Medium)) return "Medium";

            return "Medium";
        }

        private static bool IsDistance(double min, double max, DistanceVariation preset)
            => Math.Abs(min - preset.Min) < 0.0001 && Math.Abs(max - preset.Max) < 0.0001;

        private static bool IsRandomHireSid(string sid)
            => ContentItemGroup.HireBuildings.Any(item => string.Equals(item.Sid, sid, StringComparison.OrdinalIgnoreCase))
                || string.Equals(IncludeListIds.RandomHiresLowTier.Sid, sid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(IncludeListIds.RandomHiresHighTier.Sid, sid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(IncludeListIds.RandomHiresAllTier.Sid, sid, StringComparison.OrdinalIgnoreCase);

        private static bool IsResourceBankSid(string sid)
            => ContentItemGroup.ResourceBanks.Any(item => string.Equals(item.Sid, sid, StringComparison.OrdinalIgnoreCase))
                || string.Equals(IncludeListIds.ResourceBanksTier1.Sid, sid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(IncludeListIds.ResourceBanksTier2.Sid, sid, StringComparison.OrdinalIgnoreCase);

        private readonly record struct PlayerZoneContentKey(
            string Sid,
            bool IsGroup,
            bool IsMine,
            bool IsGuarded,
            bool NearCastle,
            string RoadDistance);

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
            const string expectedTail = @"HeroesOldenEra_Data\StreamingAssets\map_templates";
            return chosenDir.EndsWith(expectedTail, StringComparison.OrdinalIgnoreCase)
                || chosenDir.Contains(expectedTail + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
        private void ChkSavePreviewImage_Click(object sender, RoutedEventArgs e)
        {
            if (ChkSavePreviewImage.IsChecked == true)
            {
                ImgPreview.Visibility = Visibility.Visible;
                lblNoPreview.Visibility = Visibility.Collapsed;

            }
            else
            {
                ImgPreview.Visibility = Visibility.Collapsed;
                lblNoPreview.Visibility = Visibility.Visible;
            }
        }

        private void BtnDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DiscordServer,
                UseShellExecute = true
            });
        }

        private void BtnGithub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubPage,
                UseShellExecute = true
            });
        }
    }
}
