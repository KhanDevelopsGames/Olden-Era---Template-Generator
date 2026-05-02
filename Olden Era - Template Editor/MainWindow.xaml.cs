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

        private static readonly HttpClient Http = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Currently open settings file path (null = unsaved / untitled)
        private string? _currentSettingsPath = null;
        private bool _isDirty = false;
        private string _baseTitle = string.Empty;

        private static readonly (MapTopology Topology, string Label, string Description)[] TopologyOptions =
        [
            (MapTopology.Random,      "Random",        "Zones are placed at random positions. Each zone connects to all zones that border it — no fixed structure."),
            (MapTopology.Default,     "Ring",          "All zones are arranged in a circle. Each zone connects to the two zones next to it."),
            (MapTopology.HubAndSpoke, "Hub & Spoke [EXPERIMENTAL]",   "All zones connect to a shared central hub. Players never border each other directly."),
            (MapTopology.Chain,       "Chain",         "Zones are connected in a straight line from one end to the other, with no wrap-around."),
            (MapTopology.SharedWeb,   "Shared Web [EXPERIMENTAL]",    "Player zones connect to shared neutral zones. Neutrals form a ring between all players."),
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
            CmbMapSize.ItemsSource = KnownValues.MapSizes.Select(s => $"{s}x{s}").ToList();
            CmbMapSize.SelectedItem = "160x160";
            CmbVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            CmbVictory.SelectedIndex = 0; // Classic (win_condition_1)
            CmbTopology.ItemsSource = TopologyOptions.Select(t => t.Label).ToList();
            CmbTopology.SelectedIndex = 0; // Random is first

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

            TxtPlayers.Text = ((int)SldPlayers.Value).ToString();
            TxtHeroMin.Text = ((int)SldHeroMin.Value).ToString();
            TxtHeroMax.Text = ((int)SldHeroMax.Value).ToString();
            TxtHeroIncrement.Text = ((int)SldHeroIncrement.Value).ToString();
            TxtNeutral.Text = ((int)SldNeutral.Value).ToString();
            TxtPlayerCastles.Text = ((int)SldPlayerCastles.Value).ToString();
            TxtNeutralCastles.Text = ((int)SldNeutralCastles.Value).ToString();
            TxtContentDensity.Text = $"{(int)SldContentDensity.Value}%";

            MarkDirty();
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

            if (players + neutral > 4 && CmbMapSize.SelectedItem is string selStr && int.TryParse(selStr.Split('x')[0], out int selectedSize) && selectedSize < 128)
                warnings.Add("⚠️ Using a small map with many zones may freeze the game while loading the map. Consider using a larger map size.");

            TxtValidation.Text = string.Join("\n\n", warnings);

            BtnGenerate.IsEnabled = true;
            return true;
        }

        private void CmbTopology_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            int idx = CmbTopology.SelectedIndex;
            if (idx >= 0 && idx < TopologyOptions.Length)
                TxtTopologyDesc.Text = TopologyOptions[idx].Description;
            MarkDirty();
            Validate();
        }

        private void CmbMapSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            MarkDirty();
            Validate();
        }

        private void ChkOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            MarkDirty();
            Validate();
        }

        // ── Settings persistence ───────────────────────────────────────────────

        private SettingsFile GatherSettings() => new()
        {
            TemplateName          = TxtTemplateName.Text.Trim(),
            MapSize               = CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int ps) ? ps : 160,
            PlayerCount           = (int)SldPlayers.Value,
            NeutralZoneCount      = (int)SldNeutral.Value,
            PlayerZoneCastles     = (int)SldPlayerCastles.Value,
            NeutralZoneCastles    = (int)SldNeutralCastles.Value,
            HeroCountMin          = (int)SldHeroMin.Value,
            HeroCountMax          = (int)SldHeroMax.Value,
            HeroCountIncrement    = (int)SldHeroIncrement.Value,
            Topology              = TopologyOptions[CmbTopology.SelectedIndex].Topology,
            RandomPortals         = ChkRandomPortals.IsChecked == true,
            SpawnRemoteFootholds  = ChkSpawnFootholds.IsChecked == true,
            NoDirectPlayerConn    = ChkNoDirectPlayerConn.IsChecked == true,
            ContentDensityPercent = (int)SldContentDensity.Value,
        };

        private void ApplySettings(SettingsFile s)
        {
            TxtTemplateName.Text    = s.TemplateName;
            int sizeIdx = Array.IndexOf(KnownValues.MapSizes, s.MapSize);
            if (sizeIdx >= 0) CmbMapSize.SelectedIndex = sizeIdx;
            SldPlayers.Value        = s.PlayerCount;
            SldNeutral.Value        = s.NeutralZoneCount;
            SldPlayerCastles.Value  = s.PlayerZoneCastles;
            SldNeutralCastles.Value = s.NeutralZoneCastles;
            SldHeroMin.Value        = s.HeroCountMin;
            SldHeroMax.Value        = s.HeroCountMax;
            SldHeroIncrement.Value  = s.HeroCountIncrement;
            int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
            if (topoIdx >= 0) CmbTopology.SelectedIndex = topoIdx;
            ChkRandomPortals.IsChecked        = s.RandomPortals;
            ChkSpawnFootholds.IsChecked       = s.SpawnRemoteFootholds;
            ChkNoDirectPlayerConn.IsChecked   = s.NoDirectPlayerConn;
            SldContentDensity.Value           = s.ContentDensityPercent;
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
                MapSize = CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsedSize) ? parsedSize : 160,
                VictoryCondition = KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex],
                PlayerZoneCastles = (int)SldPlayerCastles.Value,
                NeutralZoneCastles = (int)SldNeutralCastles.Value,
                NoDirectPlayerConnections = ChkNoDirectPlayerConn.IsChecked == true,
                RandomPortals = ChkRandomPortals.IsChecked == true,
                SpawnRemoteFootholds = ChkSpawnFootholds.IsChecked == true,
                Topology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default,
                ContentDensityPercent = (int)SldContentDensity.Value
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

            string savedMsg = $"Template successfully saved to:\n\n{dlg.FileName}";
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
