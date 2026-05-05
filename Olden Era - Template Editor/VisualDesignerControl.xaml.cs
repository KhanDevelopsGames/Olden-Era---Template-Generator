using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Line = System.Windows.Shapes.Line;

namespace Olden_Era___Template_Editor
{
    public partial class VisualDesignerControl : UserControl
    {
        private static readonly JsonSerializerOptions VisualJsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions RmgJsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private VisualTemplateDocument _document = VisualTemplateOperations.CreateDefaultDocument();
        private string? _currentVisualPath;
        private string? _selectedZoneId;
        private string? _selectedConnectionId;
        private string? _draggingZoneId;
        private Point _dragStart;
        private double _dragStartX;
        private double _dragStartY;
        private VisualConnectionType? _connectMode;
        private string? _connectFromZoneId;
        private VisualTemplateClipboardPayload? _lastClipboardPayload;
        private bool _isRefreshingGlobals;
        private bool _isRefreshingInspector;
        private int _selectedCastleIndex;

        public VisualDesignerControl()
        {
            _isRefreshingGlobals = true;
            _isRefreshingInspector = true;
            InitializeComponent();
            _isRefreshingGlobals = false;
            _isRefreshingInspector = false;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CmbVisualMapSize.ItemsSource = KnownValues.AllMapSizes.Select(FormatMapSize).ToList();
            CmbVisualGameMode.ItemsSource = KnownValues.GameModes;
            CmbVisualVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            CmbZoneType.ItemsSource = Enum.GetValues<VisualZoneType>();
            CmbZoneCastles.ItemsSource = Enumerable.Range(0, 5).ToList();
            CmbFactionMode.ItemsSource = Enum.GetValues<VisualCastleFactionMode>();
            CmbConnectionType.ItemsSource = Enum.GetValues<VisualConnectionType>();
            LoadDocumentIntoUi(_document);
        }

        private void LoadDocumentIntoUi(VisualTemplateDocument document)
        {
            _document = document;
            _selectedZoneId = null;
            _selectedConnectionId = null;
            _connectMode = null;
            _connectFromZoneId = null;
            RefreshGlobalControls();
            RefreshCanvas();
            RefreshInspector();
            ValidateAndShow();
        }

        private void RefreshGlobalControls()
        {
            _isRefreshingGlobals = true;
            try
            {
                TxtVisualTemplateName.Text = _document.TemplateName;
                TxtVisualDescription.Text = _document.Description ?? string.Empty;
                CmbVisualMapSize.SelectedItem = FormatMapSize(_document.MapSize);
                if (CmbVisualMapSize.SelectedIndex < 0)
                    CmbVisualMapSize.SelectedItem = FormatMapSize(160);
                CmbVisualGameMode.SelectedItem = _document.GameMode;
                if (CmbVisualGameMode.SelectedIndex < 0)
                    CmbVisualGameMode.SelectedIndex = 0;
                int victoryIndex = Array.IndexOf(KnownValues.VictoryConditionIds, _document.VictoryCondition);
                CmbVisualVictory.SelectedIndex = victoryIndex >= 0 ? victoryIndex : 0;
                ChkVisualRoads.IsChecked = _document.GenerateRoads;
                ChkVisualFootholds.IsChecked = _document.SpawnRemoteFootholds;
                TxtVisualHeroMin.Text = _document.HeroCountMin.ToString();
                TxtVisualHeroMax.Text = _document.HeroCountMax.ToString();
                TxtVisualHeroIncrement.Text = _document.HeroCountIncrement.ToString();
                TxtVisualFactionXp.Text = _document.FactionLawsExpPercent.ToString();
                TxtVisualAstrologyXp.Text = _document.AstrologyExpPercent.ToString();
                TxtVisualBorderGuard.Text = _document.BorderGuardStrengthPercent.ToString();
                ChkVisualLostCity.IsChecked = _document.LostStartCity;
                TxtVisualLostCityDay.Text = _document.LostStartCityDay.ToString();
                ChkVisualLostHero.IsChecked = _document.LostStartHero;
                ChkVisualCityHold.IsChecked = _document.CityHold;
                TxtVisualCityHoldDays.Text = _document.CityHoldDays.ToString();
                ChkVisualTournament.IsChecked = _document.Tournament;
                TxtVisualTournamentPoints.Text = _document.TournamentPointsToWin.ToString();
            }
            finally
            {
                _isRefreshingGlobals = false;
            }
        }

        private void SyncGlobalControlsToDocument()
        {
            if (_isRefreshingGlobals)
                return;

            _document.TemplateName = TxtVisualTemplateName.Text.Trim();
            _document.Description = string.IsNullOrWhiteSpace(TxtVisualDescription.Text) ? null : TxtVisualDescription.Text.Trim();
            _document.MapSize = SelectedMapSize();
            _document.GameMode = CmbVisualGameMode.SelectedItem as string ?? "Classic";
            _document.VictoryCondition = CmbVisualVictory.SelectedIndex >= 0 && CmbVisualVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVisualVictory.SelectedIndex]
                : "win_condition_1";
            _document.GenerateRoads = ChkVisualRoads.IsChecked == true;
            _document.SpawnRemoteFootholds = ChkVisualFootholds.IsChecked == true;
            _document.HeroCountMin = ReadInt(TxtVisualHeroMin, 4, 1, 30);
            _document.HeroCountMax = ReadInt(TxtVisualHeroMax, 8, 1, 30);
            _document.HeroCountIncrement = ReadInt(TxtVisualHeroIncrement, 1, 0, 10);
            _document.FactionLawsExpPercent = ReadInt(TxtVisualFactionXp, 100, 25, 200);
            _document.AstrologyExpPercent = ReadInt(TxtVisualAstrologyXp, 100, 25, 200);
            _document.BorderGuardStrengthPercent = ReadInt(TxtVisualBorderGuard, 100, 25, 300);
            _document.LostStartCity = ChkVisualLostCity.IsChecked == true;
            _document.LostStartCityDay = ReadInt(TxtVisualLostCityDay, 3, 1, 30);
            _document.LostStartHero = ChkVisualLostHero.IsChecked == true;
            _document.CityHold = ChkVisualCityHold.IsChecked == true;
            _document.CityHoldDays = ReadInt(TxtVisualCityHoldDays, 6, 1, 30);
            _document.Tournament = ChkVisualTournament.IsChecked == true;
            _document.TournamentPointsToWin = ReadInt(TxtVisualTournamentPoints, 2, 1, 5);
        }

        private void GlobalControl_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshingGlobals)
                return;

            SyncGlobalControlsToDocument();
            ValidateAndShow();
        }

        private void RefreshCanvas()
        {
            DesignerCanvas.Children.Clear();
            DrawGrid();
            var zonesById = _document.Zones.ToDictionary(zone => zone.Id, StringComparer.Ordinal);

            foreach (VisualConnection connection in _document.Connections)
            {
                if (!zonesById.TryGetValue(connection.FromZoneId, out VisualZone? from)) continue;
                if (!zonesById.TryGetValue(connection.ToZoneId, out VisualZone? to)) continue;

                var line = new Line
                {
                    X1 = from.CanvasX,
                    Y1 = from.CanvasY,
                    X2 = to.CanvasX,
                    Y2 = to.CanvasY,
                    Stroke = connection.ConnectionType == VisualConnectionType.Portal
                        ? new SolidColorBrush(Color.FromRgb(90, 170, 210))
                        : new SolidColorBrush(Color.FromRgb(180, 145, 60)),
                    StrokeThickness = connection.Id == _selectedConnectionId ? 5 : 3,
                    Tag = connection.Id,
                    Cursor = Cursors.Hand
                };
                if (connection.ConnectionType == VisualConnectionType.Portal)
                    line.StrokeDashArray = [5, 4];
                line.MouseLeftButtonDown += Connection_MouseLeftButtonDown;
                DesignerCanvas.Children.Add(line);
            }

            foreach (VisualZone zone in _document.Zones)
                DrawZone(zone);
        }

        private void DrawGrid()
        {
            var brush = new SolidColorBrush(Color.FromArgb(55, 143, 115, 63));
            for (int pos = 50; pos < VisualTemplatePreviewPngWriter.CanvasWidth; pos += 50)
            {
                DesignerCanvas.Children.Add(new Line
                {
                    X1 = pos,
                    Y1 = 0,
                    X2 = pos,
                    Y2 = VisualTemplatePreviewPngWriter.CanvasHeight,
                    Stroke = brush,
                    StrokeThickness = 1
                });
                DesignerCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = pos,
                    X2 = VisualTemplatePreviewPngWriter.CanvasWidth,
                    Y2 = pos,
                    Stroke = brush,
                    StrokeThickness = 1
                });
            }
        }

        private void DrawZone(VisualZone zone)
        {
            const double size = 68;
            System.Windows.Controls.Border border = new()
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = ZoneFill(zone),
                BorderBrush = zone.Id == _selectedZoneId ? Brushes.White : ZoneOutline(zone),
                BorderThickness = new Thickness(zone.Id == _selectedZoneId ? 3 : 2),
                Tag = zone.Id,
                Cursor = Cursors.Hand,
                ToolTip = $"{VisualTemplateGenerator.ZoneName(zone)} ({zone.ZoneType})"
            };

            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(new TextBlock
            {
                Text = ZoneLabel(zone),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 17,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = zone.CastleCount > 0 ? $"{zone.CastleCount} castle" : "no castle",
                Foreground = new SolidColorBrush(Color.FromRgb(235, 228, 204)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            border.Child = panel;
            border.MouseLeftButtonDown += Zone_MouseLeftButtonDown;

            Canvas.SetLeft(border, zone.CanvasX - size / 2);
            Canvas.SetTop(border, zone.CanvasY - size / 2);
            DesignerCanvas.Children.Add(border);
        }

        private void RefreshInspector()
        {
            _isRefreshingInspector = true;
            try
            {
                VisualZone? zone = SelectedZone();
                VisualConnection? connection = SelectedConnection();
                TxtNoVisualSelection.Visibility = zone is null && connection is null ? Visibility.Visible : Visibility.Collapsed;
                PnlZoneInspector.Visibility = zone is not null ? Visibility.Visible : Visibility.Collapsed;
                PnlConnectionInspector.Visibility = connection is not null ? Visibility.Visible : Visibility.Collapsed;

                if (zone is not null)
                    RefreshZoneInspector(zone);
                if (connection is not null)
                    RefreshConnectionInspector(connection);
            }
            finally
            {
                _isRefreshingInspector = false;
            }
        }

        private void RefreshZoneInspector(VisualZone zone)
        {
            CmbZoneType.SelectedItem = zone.ZoneType;
            TxtZoneLetter.Text = zone.ExportLetter;
            TxtZonePlayer.Text = zone.PlayerSlot?.ToString() ?? string.Empty;
            CmbZoneCastles.SelectedItem = zone.CastleCount;
            TxtZoneSize.Text = zone.ZoneSize.ToString("0.##");
            TxtZoneGuardRandom.Text = ((int)Math.Round(zone.GuardRandomization * 100)).ToString();
            TxtZoneNeutralStrength.Text = zone.NeutralStackStrengthPercent.ToString();
            TxtZoneResources.Text = zone.ResourceDensityPercent.ToString();
            TxtZoneStructures.Text = zone.StructureDensityPercent.ToString();
            ChkZoneRoads.IsChecked = zone.GenerateRoads;
            ChkZoneFoothold.IsChecked = zone.SpawnRemoteFoothold;

            CmbCastleIndex.ItemsSource = Enumerable.Range(1, Math.Max(0, zone.CastleCount))
                .Select(index => $"Castle {index}")
                .ToList();
            if (zone.CastleCount == 0)
            {
                CmbCastleIndex.SelectedIndex = -1;
                SetCastleControlsEnabled(false);
            }
            else
            {
                _selectedCastleIndex = Math.Clamp(_selectedCastleIndex, 0, zone.CastleCount - 1);
                CmbCastleIndex.SelectedIndex = _selectedCastleIndex;
                SetCastleControlsEnabled(true);
                RefreshCastleInspector(zone);
            }
        }

        private void RefreshCastleInspector(VisualZone zone)
        {
            if (_selectedCastleIndex < 0 || _selectedCastleIndex >= zone.Castles.Count)
                return;

            VisualCastle castle = zone.Castles[_selectedCastleIndex];
            CmbFactionMode.SelectedItem = castle.FactionMode;
            TxtFactionMatchPlayer.Text = castle.MatchPlayerSlot?.ToString() ?? string.Empty;
            SetFactionChecks(castle.AllowedFactions);
        }

        private void RefreshConnectionInspector(VisualConnection connection)
        {
            CmbConnectionType.SelectedItem = connection.ConnectionType;
            TxtConnectionGuard.Text = connection.GuardStrengthPercent.ToString();
        }

        private void ZoneInspector_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshingInspector)
                return;

            VisualZone? zone = SelectedZone();
            if (zone is null)
                return;

            zone.ZoneType = CmbZoneType.SelectedItem is VisualZoneType type ? type : zone.ZoneType;
            zone.ExportLetter = TxtZoneLetter.Text.Trim().ToUpperInvariant();
            zone.PlayerSlot = zone.ZoneType == VisualZoneType.PlayerSpawn ? ReadNullableInt(TxtZonePlayer, 1, 8) ?? NextPlayerSlot() : null;
            int selectedCastleCount = CmbZoneCastles.SelectedItem is int count ? count : zone.CastleCount;
            zone.CastleCount = Math.Clamp(selectedCastleCount, zone.ZoneType == VisualZoneType.PlayerSpawn ? 1 : 0, 4);
            zone.ZoneSize = ReadDouble(TxtZoneSize, 1.0, 0.1, 2.0);
            zone.GuardRandomization = ReadDouble(TxtZoneGuardRandom, 5, 0, 50) / 100.0;
            zone.NeutralStackStrengthPercent = ReadInt(TxtZoneNeutralStrength, 100, 25, 300);
            zone.ResourceDensityPercent = ReadInt(TxtZoneResources, 100, 20, 400);
            zone.StructureDensityPercent = ReadInt(TxtZoneStructures, 100, 20, 400);
            zone.GenerateRoads = ChkZoneRoads.IsChecked == true;
            zone.SpawnRemoteFoothold = ChkZoneFoothold.IsChecked == true;
            VisualTemplateOperations.NormalizeCastles(zone);

            RefreshCanvas();
            RefreshInspector();
            ValidateAndShow();
        }

        private void CastleInspector_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshingInspector)
                return;

            VisualZone? zone = SelectedZone();
            if (zone is null || zone.CastleCount == 0)
                return;

            int selectedIndex = CmbCastleIndex.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex != _selectedCastleIndex)
            {
                _selectedCastleIndex = selectedIndex;
                _isRefreshingInspector = true;
                try
                {
                    RefreshCastleInspector(zone);
                }
                finally
                {
                    _isRefreshingInspector = false;
                }
                return;
            }

            if (_selectedCastleIndex < 0 || _selectedCastleIndex >= zone.Castles.Count)
                return;

            VisualCastle castle = zone.Castles[_selectedCastleIndex];
            castle.FactionMode = CmbFactionMode.SelectedItem is VisualCastleFactionMode mode ? mode : VisualCastleFactionMode.Unrestricted;
            castle.MatchPlayerSlot = ReadNullableInt(TxtFactionMatchPlayer, 1, 8);
            castle.AllowedFactions = CheckedFactionLabels();
            ValidateAndShow();
        }

        private void ConnectionInspector_Changed(object sender, RoutedEventArgs e)
        {
            if (_isRefreshingInspector)
                return;

            VisualConnection? connection = SelectedConnection();
            if (connection is null)
                return;

            connection.ConnectionType = CmbConnectionType.SelectedItem is VisualConnectionType type ? type : VisualConnectionType.Direct;
            connection.GuardStrengthPercent = ReadInt(TxtConnectionGuard, 100, 25, 300);
            RefreshCanvas();
            ValidateAndShow();
        }

        private void BtnAddSpawn_Click(object sender, RoutedEventArgs e) => AddZone(VisualZoneType.PlayerSpawn);
        private void BtnAddLow_Click(object sender, RoutedEventArgs e) => AddZone(VisualZoneType.NeutralLow);
        private void BtnAddMedium_Click(object sender, RoutedEventArgs e) => AddZone(VisualZoneType.NeutralMedium);
        private void BtnAddHigh_Click(object sender, RoutedEventArgs e) => AddZone(VisualZoneType.NeutralHigh);

        private void AddZone(VisualZoneType type)
        {
            int count = _document.Zones.Count;
            var zone = new VisualZone
            {
                Id = Guid.NewGuid().ToString("N"),
                ExportLetter = VisualTemplateOperations.NextAvailableLetter(_document),
                ZoneType = type,
                PlayerSlot = type == VisualZoneType.PlayerSpawn ? NextPlayerSlot() : null,
                CanvasX = 160 + (count * 70 % 380),
                CanvasY = 160 + (count * 50 % 380),
                CastleCount = type == VisualZoneType.PlayerSpawn ? 1 : 0,
                Castles = type == VisualZoneType.PlayerSpawn ? [new VisualCastle()] : []
            };

            _document.Zones.Add(zone);
            SelectZone(zone.Id);
        }

        private void BtnConnectDirect_Click(object sender, RoutedEventArgs e) => StartConnectionMode(VisualConnectionType.Direct);
        private void BtnConnectPortal_Click(object sender, RoutedEventArgs e) => StartConnectionMode(VisualConnectionType.Portal);

        private void StartConnectionMode(VisualConnectionType type)
        {
            _connectMode = type;
            _connectFromZoneId = null;
            TxtVisualValidation.Text = $"Select the first zone for a {type} connection.";
        }

        private void Zone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string id)
                return;

            if (_connectMode.HasValue)
            {
                HandleConnectionClick(id);
                e.Handled = true;
                return;
            }

            SelectZone(id);
            VisualZone? zone = SelectedZone();
            if (zone is not null)
            {
                _draggingZoneId = id;
                _dragStart = e.GetPosition(DesignerCanvas);
                _dragStartX = zone.CanvasX;
                _dragStartY = zone.CanvasY;
                DesignerCanvas.CaptureMouse();
            }

            e.Handled = true;
        }

        private void HandleConnectionClick(string zoneId)
        {
            if (_connectFromZoneId is null)
            {
                _connectFromZoneId = zoneId;
                SelectZone(zoneId);
                TxtVisualValidation.Text = $"Select the second zone for a {_connectMode} connection.";
                return;
            }

            if (_connectFromZoneId != zoneId)
            {
                var connection = new VisualConnection
                {
                    FromZoneId = _connectFromZoneId,
                    ToZoneId = zoneId,
                    ConnectionType = _connectMode ?? VisualConnectionType.Direct,
                    GuardStrengthPercent = 100
                };
                _document.Connections.Add(connection);
                SelectConnection(connection.Id);
            }

            _connectMode = null;
            _connectFromZoneId = null;
            RefreshCanvas();
            ValidateAndShow();
        }

        private void Connection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string id)
            {
                SelectConnection(id);
                e.Handled = true;
            }
        }

        private void DesignerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == DesignerCanvas)
            {
                _connectMode = null;
                _connectFromZoneId = null;
                _selectedZoneId = null;
                _selectedConnectionId = null;
                RefreshCanvas();
                RefreshInspector();
                ValidateAndShow();
            }
        }

        private void DesignerCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingZoneId is null || e.LeftButton != MouseButtonState.Pressed)
                return;

            VisualZone? zone = _document.Zones.FirstOrDefault(z => z.Id == _draggingZoneId);
            if (zone is null)
                return;

            Point current = e.GetPosition(DesignerCanvas);
            zone.CanvasX = Math.Clamp(_dragStartX + current.X - _dragStart.X, 34, VisualTemplatePreviewPngWriter.CanvasWidth - 34);
            zone.CanvasY = Math.Clamp(_dragStartY + current.Y - _dragStart.Y, 34, VisualTemplatePreviewPngWriter.CanvasHeight - 34);
            RefreshCanvas();
        }

        private void DesignerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingZoneId is not null)
            {
                _draggingZoneId = null;
                DesignerCanvas.ReleaseMouseCapture();
                RefreshInspector();
                ValidateAndShow();
            }
        }

        private void BtnVisualCopy_Click(object sender, RoutedEventArgs e) => CopySelectedZone();
        private void BtnVisualPaste_Click(object sender, RoutedEventArgs e) => PasteZones();
        private void BtnVisualDelete_Click(object sender, RoutedEventArgs e) => DeleteSelection();

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                CopySelectedZone();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                PasteZones();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteSelection();
                e.Handled = true;
            }
        }

        private void CopySelectedZone()
        {
            if (_selectedZoneId is null)
                return;

            _lastClipboardPayload = VisualTemplateOperations.CopyZones(_document, [_selectedZoneId]);
            try
            {
                Clipboard.SetText(JsonSerializer.Serialize(_lastClipboardPayload, VisualJsonOptions));
            }
            catch
            {
                // Clipboard can be unavailable if another process owns it; keep in-memory paste working.
            }
        }

        private void PasteZones()
        {
            VisualTemplateClipboardPayload? payload = _lastClipboardPayload;
            try
            {
                if (Clipboard.ContainsText())
                    payload = JsonSerializer.Deserialize<VisualTemplateClipboardPayload>(Clipboard.GetText(), VisualJsonOptions) ?? payload;
            }
            catch
            {
                // Fall back to the in-memory payload.
            }

            if (payload is null || payload.Zones.Count == 0)
                return;

            List<VisualZone> pasted = VisualTemplateOperations.PasteZones(_document, payload);
            SelectZone(pasted.Last().Id);
        }

        private void DeleteSelection()
        {
            if (_selectedZoneId is not null)
            {
                _document.Zones.RemoveAll(zone => zone.Id == _selectedZoneId);
                _document.Connections.RemoveAll(connection => connection.FromZoneId == _selectedZoneId || connection.ToZoneId == _selectedZoneId);
                _selectedZoneId = null;
            }
            else if (_selectedConnectionId is not null)
            {
                _document.Connections.RemoveAll(connection => connection.Id == _selectedConnectionId);
                _selectedConnectionId = null;
            }

            RefreshCanvas();
            RefreshInspector();
            ValidateAndShow();
        }

        private void BtnVisualNew_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset the visual template designer?", "New Visual Template",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            _currentVisualPath = null;
            LoadDocumentIntoUi(VisualTemplateOperations.CreateDefaultDocument());
        }

        private void BtnVisualOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open Visual Template Settings",
                Filter = "Visual Template Settings (*.oetgsv)|*.oetgsv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                string json = File.ReadAllText(dlg.FileName);
                VisualTemplateDocument document = JsonSerializer.Deserialize<VisualTemplateDocument>(json, VisualJsonOptions)
                    ?? throw new InvalidDataException("File is empty or invalid.");
                _currentVisualPath = dlg.FileName;
                LoadDocumentIntoUi(document);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open visual settings:\n{ex.Message}", "Open Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVisualSave_Click(object sender, RoutedEventArgs e)
        {
            SyncGlobalControlsToDocument();
            VisualValidationResult validation = ValidateAndShow();
            if (!validation.IsValid)
            {
                MessageBox.Show(string.Join("\n", validation.Errors), "Visual Template Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentVisualPath is null)
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Save Visual Template Settings",
                    Filter = "Visual Template Settings (*.oetgsv)|*.oetgsv|All files (*.*)|*.*",
                    FileName = SafeFileName(_document.TemplateName, "Visual Template"),
                    DefaultExt = ".oetgsv"
                };
                if (dlg.ShowDialog() != true)
                    return;
                _currentVisualPath = dlg.FileName;
            }

            SaveVisualProject(_currentVisualPath);
        }

        private void SaveVisualProject(string path)
        {
            try
            {
                string json = JsonSerializer.Serialize(_document, VisualJsonOptions);
                File.WriteAllText(path, json);
                MessageBox.Show($"Visual settings saved to:\n\n{path}", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save visual settings:\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVisualExport_Click(object sender, RoutedEventArgs e)
        {
            SyncGlobalControlsToDocument();
            VisualValidationResult validation = ValidateAndShow();
            if (!validation.IsValid)
            {
                MessageBox.Show(string.Join("\n", validation.Errors), "Visual Template Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RmgTemplate template;
            try
            {
                template = VisualTemplateGenerator.Generate(_document);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate visual template:\n{ex.Message}", "Generation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string? gameTemplatesPath = FindOldenEraTemplatesPath();
            var dlg = new SaveFileDialog
            {
                Title = "Export RMG Template",
                Filter = "RMG Template (*.rmg.json)|*.rmg.json",
                FileName = $"{SafeFileName(template.Name, "Visual Template")}.rmg.json",
                DefaultExt = ".rmg.json"
            };
            if (gameTemplatesPath != null)
                dlg.InitialDirectory = gameTemplatesPath;
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(template, RmgJsonOptions));
                string message = $"Template exported to:\n\n{dlg.FileName}";
                if (ChkVisualSavePreview.IsChecked == true)
                {
                    string previewPath = VisualTemplatePreviewPngWriter.GetSidecarPath(dlg.FileName);
                    VisualTemplatePreviewPngWriter.Save(_document, previewPath);
                    message += $"\n\nPreview PNG saved to:\n\n{previewPath}";
                }

                MessageBox.Show(message, "Exported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export visual template:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private VisualValidationResult ValidateAndShow()
        {
            VisualValidationResult result = VisualTemplateValidator.Validate(_document);
            var lines = new List<string>();
            lines.AddRange(result.Errors);
            lines.AddRange(result.Warnings);
            if (_connectMode.HasValue)
                lines.Insert(0, _connectFromZoneId is null
                    ? $"Connection mode: select the first {_connectMode} zone."
                    : $"Connection mode: select the second {_connectMode} zone.");

            TxtVisualValidation.Text = string.Join("\n", lines);
            TxtVisualValidation.Foreground = result.Errors.Count > 0
                ? (Brush)FindResource("BrushError")
                : (Brush)FindResource("BrushTextDim");
            return result;
        }

        private void SelectZone(string id)
        {
            _selectedZoneId = id;
            _selectedConnectionId = null;
            _selectedCastleIndex = 0;
            RefreshCanvas();
            RefreshInspector();
            ValidateAndShow();
        }

        private void SelectConnection(string id)
        {
            _selectedConnectionId = id;
            _selectedZoneId = null;
            RefreshCanvas();
            RefreshInspector();
            ValidateAndShow();
        }

        private VisualZone? SelectedZone() =>
            _selectedZoneId is null ? null : _document.Zones.FirstOrDefault(zone => zone.Id == _selectedZoneId);

        private VisualConnection? SelectedConnection() =>
            _selectedConnectionId is null ? null : _document.Connections.FirstOrDefault(connection => connection.Id == _selectedConnectionId);

        private int NextPlayerSlot()
        {
            var used = _document.Zones
                .Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn && zone.PlayerSlot.HasValue)
                .Select(zone => zone.PlayerSlot!.Value)
                .ToHashSet();

            for (int player = 1; player <= 8; player++)
            {
                if (!used.Contains(player))
                    return player;
            }

            return 8;
        }

        private int SelectedMapSize() =>
            CmbVisualMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsed)
                ? parsed
                : 160;

        private static string FormatMapSize(int size) =>
            KnownValues.IsExperimentalMapSize(size) ? $"{size}x{size} (Experimental)" : $"{size}x{size}";

        private static int ReadInt(TextBox textBox, int fallback, int min, int max) =>
            int.TryParse(textBox.Text, out int value) ? Math.Clamp(value, min, max) : fallback;

        private static int? ReadNullableInt(TextBox textBox, int min, int max) =>
            int.TryParse(textBox.Text, out int value) ? Math.Clamp(value, min, max) : null;

        private static double ReadDouble(TextBox textBox, double fallback, double min, double max) =>
            double.TryParse(textBox.Text, out double value) ? Math.Clamp(value, min, max) : fallback;

        private static string SafeFileName(string? value, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }

        private static Brush ZoneFill(VisualZone zone) => zone.ZoneType switch
        {
            VisualZoneType.PlayerSpawn => new SolidColorBrush(Color.FromRgb(42, 90, 50)),
            VisualZoneType.NeutralLow => new SolidColorBrush(Color.FromRgb(101, 67, 33)),
            VisualZoneType.NeutralHigh => new SolidColorBrush(Color.FromRgb(120, 90, 20)),
            _ => new SolidColorBrush(Color.FromRgb(72, 76, 80))
        };

        private static Brush ZoneOutline(VisualZone zone) => zone.ZoneType switch
        {
            VisualZoneType.PlayerSpawn => new SolidColorBrush(Color.FromRgb(100, 200, 120)),
            VisualZoneType.NeutralLow => new SolidColorBrush(Color.FromRgb(205, 127, 50)),
            VisualZoneType.NeutralHigh => new SolidColorBrush(Color.FromRgb(255, 210, 50)),
            _ => new SolidColorBrush(Color.FromRgb(192, 192, 192))
        };

        private static string ZoneLabel(VisualZone zone) => zone.ZoneType switch
        {
            VisualZoneType.PlayerSpawn => $"P{zone.PlayerSlot ?? 0}",
            VisualZoneType.NeutralLow => "Low",
            VisualZoneType.NeutralHigh => "High",
            _ => "Med"
        };

        private void SetCastleControlsEnabled(bool enabled)
        {
            CmbFactionMode.IsEnabled = enabled;
            TxtFactionMatchPlayer.IsEnabled = enabled;
            foreach (CheckBox checkBox in FactionCheckBoxes())
                checkBox.IsEnabled = enabled;
        }

        private void SetFactionChecks(List<string> labels)
        {
            var set = labels.ToHashSet(StringComparer.Ordinal);
            ChkFactionTemple.IsChecked = set.Contains("Temple");
            ChkFactionHive.IsChecked = set.Contains("Hive");
            ChkFactionDungeon.IsChecked = set.Contains("Dungeon");
            ChkFactionGrove.IsChecked = set.Contains("Grove");
            ChkFactionNecropolis.IsChecked = set.Contains("Necropolis");
            ChkFactionSchism.IsChecked = set.Contains("Schism");
        }

        private List<string> CheckedFactionLabels()
        {
            var labels = new List<string>();
            foreach (CheckBox checkBox in FactionCheckBoxes())
            {
                if (checkBox.IsChecked == true && checkBox.Content is string label)
                    labels.Add(label);
            }

            return labels;
        }

        private IEnumerable<CheckBox> FactionCheckBoxes()
        {
            yield return ChkFactionTemple;
            yield return ChkFactionHive;
            yield return ChkFactionDungeon;
            yield return ChkFactionGrove;
            yield return ChkFactionNecropolis;
            yield return ChkFactionSchism;
        }

        private static string? FindOldenEraTemplatesPath()
        {
            const string appId = "3105440";
            string[] registryRoots =
            [
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}"
            ];

            foreach (string keyPath in registryRoots)
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
                catch
                {
                    // Registry access can be denied.
                }
            }

            string[] steamLibraryRoots =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
            ];

            foreach (string candidate in steamLibraryRoots)
            {
                string templatesDir = Path.Combine(candidate, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                if (Directory.Exists(templatesDir))
                    return templatesDir;
            }

            return null;
        }
    }
}
