using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Olden_Era___Template_Editor
{
    public partial class ZoneConnectionEditorWindow : Window
    {
        // ── Data ─────────────────────────────────────────────────────────────
        private readonly List<Zone> _zones;
        private readonly List<Connection> _connections;
        private readonly List<Connection> _originalConnections;
        private readonly MapTopology _topology;
        private readonly HashSet<string> _playerZoneNames;
        private Dictionary<string, Point> _nodePositions = new(StringComparer.Ordinal);

        // ── Selection state ──────────────────────────────────────────────────
        private Connection? _selectedConnection;
        private Line? _selectedVisibleLine;

        // ── Add-connection mode ──────────────────────────────────────────────
        private bool _addMode;
        private string? _pendingFromZone;
        private string? _pendingToZone;
        private Ellipse? _pendingFromEllipse;

        // ── Event-suppression flag ───────────────────────────────────────────
        private bool _suppressPropertyEvents;

        // ── Public result state ──────────────────────────────────────────────
        /// <summary>True if the user made any changes to the connection list or properties.</summary>
        public bool ConnectionsWereModified { get; private set; }

        /// <summary>
        /// True when at least one connection references a zone name that does not exist in
        /// the zone list.  Recomputed after every canvas refresh.  When true, export is blocked.
        /// </summary>
        public bool HasUnresolvedErrors { get; private set; }

        // ── Rendering constants ──────────────────────────────────────────────
        private const double NodeRadius = 18.0;

        // Zone layout names (matching TemplatePreviewPngWriter)
        private const string LayoutSpawns  = "zone_layout_spawns";
        private const string LayoutSides   = "zone_layout_sides";
        private const string LayoutTreasure = "zone_layout_treasure_zone";
        private const string LayoutCenter  = "zone_layout_center";

        // ── Colours (mirroring TemplatePreviewPngWriter constants) ──────────
        private static readonly SolidColorBrush BrushPlayerFill    = new(Color.FromRgb( 42,  90,  50));
        private static readonly SolidColorBrush BrushPlayerBorder  = new(Color.FromRgb(100, 200, 120));
        private static readonly SolidColorBrush BrushBronzeFill    = new(Color.FromRgb(101,  67,  33));
        private static readonly SolidColorBrush BrushBronzeBorder  = new(Color.FromRgb(205, 127,  50));
        private static readonly SolidColorBrush BrushSilverFill    = new(Color.FromRgb( 72,  76,  80));
        private static readonly SolidColorBrush BrushSilverBorder  = new(Color.FromRgb(192, 192, 192));
        private static readonly SolidColorBrush BrushGoldFill      = new(Color.FromRgb(120,  90,  20));
        private static readonly SolidColorBrush BrushGoldBorder    = new(Color.FromRgb(255, 210,  50));
        private static readonly SolidColorBrush BrushHubFill       = new(Color.FromRgb( 55,  80,  95));
        private static readonly SolidColorBrush BrushHubBorder     = new(Color.FromRgb(130, 180, 200));
        private static readonly SolidColorBrush BrushEdgeDirect    = new(Color.FromRgb(180, 145,  60));
        private static readonly SolidColorBrush BrushEdgePortal    = new(Color.FromArgb(210,  90, 170, 210));
        private static readonly SolidColorBrush BrushEdgeSelected  = new(Color.FromRgb(255, 140,   0));
        private static readonly SolidColorBrush BrushNormalBorder;

        // ── Constructor ──────────────────────────────────────────────────────

        static ZoneConnectionEditorWindow()
        {
            BrushNormalBorder = new SolidColorBrush(Color.FromRgb(90, 74, 40));
        }

        public ZoneConnectionEditorWindow(
            List<Zone> zones,
            List<Connection> connections,
            List<Connection> originalConnections,
            MapTopology topology,
            HashSet<string> playerZoneNames)
        {
            _zones               = zones;
            _connections         = connections;
            _originalConnections = originalConnections;
            _topology            = topology;
            _playerZoneNames     = playerZoneNames;

            InitializeComponent();

            // Populate add-connection type combobox
            CmbAddType.Items.Add("Direct");
            CmbAddType.Items.Add("Portal");
            CmbAddType.SelectedIndex = 0;

            // Populate edit connection-type combobox
            CmbConnectionType.Items.Add("Direct");
            CmbConnectionType.Items.Add("Portal");
            CmbConnectionType.Items.Add("Proximity");
            CmbConnectionType.SelectedIndex = 0;

            Loaded           += (_, _) => RenderAll();
            ZoneCanvas.SizeChanged += (_, _) => RenderAll();
        }

        // ── Window chrome ────────────────────────────────────────────────────

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Layout / position computation ────────────────────────────────────

        private void ComputeNodePositions()
        {
            double cw = ZoneCanvas.ActualWidth;
            double ch = ZoneCanvas.ActualHeight;
            if (cw <= 0 || ch <= 0) return;

            // Build a minimal RmgTemplate so we can reuse TemplatePreviewPngWriter's
            // already-correct layout algorithm for every topology type.
            var miniTemplate = new RmgTemplate
            {
                Variants = [new Variant { Zones = _zones, Connections = _connections }]
            };
            var rawPositions = TemplatePreviewPngWriter.ComputeLayout(miniTemplate, _topology);

            // The PNG writer computes positions in a 700×700 coordinate space.
            // Scale them to the current canvas size.
            double sx = cw / 700.0;
            double sy = ch / 700.0;

            _nodePositions = new Dictionary<string, Point>(StringComparer.Ordinal);
            foreach (var (name, pos) in rawPositions)
                _nodePositions[name] = new Point(pos.X * sx, pos.Y * sy);
        }

        // ── Main render entry points ─────────────────────────────────────────

        /// <summary>Recompute node positions from the current canvas size, then redraw everything.</summary>
        private void RenderAll()
        {
            ComputeNodePositions();
            Refresh();
        }

        /// <summary>Redraw the canvas without recomputing node positions.</summary>
        private void Refresh()
        {
            ZoneCanvas.Children.Clear();
            RenderEdges();   // edges added first → lower z-order than nodes
            RenderNodes();   // nodes drawn on top of edges
        }

        // ── Edge rendering ────────────────────────────────────────────────────

        private void RenderEdges()
        {
            foreach (var conn in _connections)
            {
                if (!_nodePositions.TryGetValue(conn.From, out var fromPos)) continue;
                if (!_nodePositions.TryGetValue(conn.To,   out var toPos))   continue;

                bool isPortal   = string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal);
                bool isSelected = ReferenceEquals(conn, _selectedConnection);

                var normalBrush  = isPortal ? BrushEdgePortal : BrushEdgeDirect;
                var strokeBrush  = isSelected ? BrushEdgeSelected : normalBrush;

                // Visible line (2 px, not hit-testable)
                var visibleLine = new Line
                {
                    X1 = fromPos.X, Y1 = fromPos.Y,
                    X2 = toPos.X,   Y2 = toPos.Y,
                    Stroke = strokeBrush,
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                if (conn.IsUserAdded)
                    visibleLine.StrokeDashArray = [4.0, 3.0];

                // If this is the newly rendered line for the selected connection, update the reference.
                if (isSelected)
                    _selectedVisibleLine = visibleLine;

                // Transparent 12 px hit-area line for click detection
                var hitLine = new Line
                {
                    X1 = fromPos.X, Y1 = fromPos.Y,
                    X2 = toPos.X,   Y2 = toPos.Y,
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 12,
                    Cursor = Cursors.Hand
                };

                var capturedConn    = conn;
                var capturedVisible = visibleLine;
                hitLine.MouseLeftButtonDown += (_, e) =>
                {
                    if (_addMode) return;   // edges are not selectable during add mode
                    e.Handled = true;
                    SelectEdge(capturedConn, capturedVisible);
                };

                ZoneCanvas.Children.Add(hitLine);
                ZoneCanvas.Children.Add(visibleLine);

                // Guard-value label at edge midpoint
                string guardText = conn.GuardValue.HasValue ? conn.GuardValue.Value.ToString() : "";
                if (guardText.Length > 0)
                {
                    var guardLabel = new TextBlock
                    {
                        Text = guardText,
                        FontSize = 9,
                        Foreground = Brushes.LightYellow,
                        IsHitTestVisible = false
                    };
                    ZoneCanvas.Children.Add(guardLabel);
                    guardLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double mx = (fromPos.X + toPos.X) / 2.0;
                    double my = (fromPos.Y + toPos.Y) / 2.0;
                    Canvas.SetLeft(guardLabel, mx - guardLabel.DesiredSize.Width  / 2.0);
                    Canvas.SetTop( guardLabel, my - guardLabel.DesiredSize.Height / 2.0);
                }
            }

            // Recompute HasUnresolvedErrors
            var zoneNameSet = new HashSet<string>(_zones.Select(z => z.Name), StringComparer.Ordinal);
            HasUnresolvedErrors = _connections.Any(c =>
                !zoneNameSet.Contains(c.From) || !zoneNameSet.Contains(c.To));

            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            int count = _connections.Count;
            string status = $"{count} connection(s)";

            var zoneNameSet = new HashSet<string>(_zones.Select(z => z.Name), StringComparer.Ordinal);
            var isolated = _zones
                .Where(z => !_connections.Any(c =>
                    string.Equals(c.From, z.Name, StringComparison.Ordinal) ||
                    string.Equals(c.To,   z.Name, StringComparison.Ordinal)))
                .Select(z => z.Name)
                .ToList();

            if (isolated.Count > 0)
                status += $"  ⚠ Isolated zones: {string.Join(", ", isolated)}";

            if (HasUnresolvedErrors)
                status += "  ⛔ Invalid zone references — export blocked";

            TxtStatus.Text = status;
        }

        // ── Node rendering ────────────────────────────────────────────────────

        private void RenderNodes()
        {
            foreach (var zone in _zones)
            {
                if (!_nodePositions.TryGetValue(zone.Name, out var pos)) continue;

                var (fillBrush, borderBrush) = GetZoneColors(zone);

                var ellipse = new Ellipse
                {
                    Width  = NodeRadius * 2,
                    Height = NodeRadius * 2,
                    Fill   = fillBrush,
                    Stroke = borderBrush,
                    StrokeThickness = 2,
                    Cursor = _addMode ? Cursors.Hand : Cursors.Arrow
                };

                // Highlight the pending "from" zone in add mode
                if (_addMode && string.Equals(zone.Name, _pendingFromZone, StringComparison.Ordinal))
                {
                    ellipse.Stroke      = BrushEdgeSelected;
                    _pendingFromEllipse = ellipse;
                }

                string zoneName = zone.Name;
                ellipse.MouseLeftButtonDown += (s, e) => ZoneNode_MouseLeftButtonDown(s, e, zoneName);

                Canvas.SetLeft(ellipse, pos.X - NodeRadius);
                Canvas.SetTop( ellipse, pos.Y - NodeRadius);
                ZoneCanvas.Children.Add(ellipse);

                // Zone name label (centred over the node)
                var label = new TextBlock
                {
                    Text = zone.Name,
                    FontSize = 9,
                    Foreground = Brushes.White,
                    IsHitTestVisible = false,
                    TextWrapping = TextWrapping.NoWrap
                };
                ZoneCanvas.Children.Add(label);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, pos.X - label.DesiredSize.Width  / 2.0);
                Canvas.SetTop( label, pos.Y - label.DesiredSize.Height / 2.0);
            }
        }

        private (SolidColorBrush fill, SolidColorBrush border) GetZoneColors(Zone zone)
        {
            if (_playerZoneNames.Contains(zone.Name))
                return (BrushPlayerFill, BrushPlayerBorder);

            if (zone.Name.Equals("Hub", StringComparison.Ordinal)
                || zone.Name.StartsWith("Hub-", StringComparison.Ordinal))
                return (BrushHubFill, BrushHubBorder);

            if (zone.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            {
                string pool = zone.GuardedContentPool?.FirstOrDefault() ?? "";
                if (pool.Contains("_t4_") || pool.Contains("_t5_"))
                    return (BrushGoldFill,   BrushGoldBorder);
                if (pool.Contains("_t2_") || pool.Contains("_t1_"))
                    return (BrushBronzeFill, BrushBronzeBorder);
                return (BrushSilverFill, BrushSilverBorder);
            }

            return (BrushBronzeFill, BrushBronzeBorder);
        }

        // ── Edge selection ────────────────────────────────────────────────────

        private void SelectEdge(Connection conn, Line visibleLine)
        {
            // Restore the previously selected edge to its normal colour
            if (_selectedConnection is not null && _selectedVisibleLine is not null)
            {
                bool wasPortal = string.Equals(_selectedConnection.ConnectionType, "Portal", StringComparison.Ordinal);
                _selectedVisibleLine.Stroke = wasPortal ? BrushEdgePortal : BrushEdgeDirect;
            }

            _selectedConnection  = conn;
            _selectedVisibleLine = visibleLine;
            visibleLine.Stroke   = BrushEdgeSelected;

            PnlProperties.Visibility = Visibility.Visible;
            PopulatePropertyPanel(conn);
        }

        private void PopulatePropertyPanel(Connection conn)
        {
            _suppressPropertyEvents = true;
            try
            {
                TxtFromZone.Text = conn.From;
                TxtToZone.Text   = conn.To;
                TxtConnectionName.Text = conn.Name ?? "";

                int typeIdx = CmbConnectionType.Items.IndexOf(conn.ConnectionType ?? "Direct");
                CmbConnectionType.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;

                TxtGuardValue.Text = conn.GuardValue.HasValue
                    ? conn.GuardValue.Value.ToString()
                    : "";

                TxtGuardWeeklyIncrement.Text = conn.GuardWeeklyIncrement.HasValue
                    ? conn.GuardWeeklyIncrement.Value.ToString("G", CultureInfo.InvariantCulture)
                    : "";

                TxtGuardZone.Text       = conn.GuardZone      ?? "";
                TxtGuardMatchGroup.Text = conn.GuardMatchGroup ?? "";
                ChkGuardEscape.IsChecked  = conn.GuardEscape  ?? false;
                ChkSimTurnSquad.IsChecked = conn.SimTurnSquad ?? false;

                // Reset validation borders
                TxtConnectionName.BorderBrush       = BrushNormalBorder;
                TxtGuardValue.BorderBrush            = BrushNormalBorder;
                TxtGuardWeeklyIncrement.BorderBrush  = BrushNormalBorder;
                TxtGuardZone.BorderBrush             = BrushNormalBorder;

                ValidatePropertyPanel();
            }
            finally
            {
                _suppressPropertyEvents = false;
            }
        }

        // ── Property panel event handlers ─────────────────────────────────────

        private void TxtConnectionName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string val = TxtConnectionName.Text.Trim();
            _selectedConnection.Name = val.Length > 0 ? val : null;
            ConnectionsWereModified = true;

            // Duplicate-name warning (yellow border)
            bool isDuplicate = val.Length > 0 && _connections
                .Where(c => !ReferenceEquals(c, _selectedConnection))
                .Any(c => string.Equals(c.Name, val, StringComparison.OrdinalIgnoreCase));
            TxtConnectionName.BorderBrush = isDuplicate
                ? new SolidColorBrush(Colors.Yellow)
                : BrushNormalBorder;
        }

        private void CmbConnectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.ConnectionType = CmbConnectionType.SelectedItem as string;
            ConnectionsWereModified = true;
        }

        private void TxtGuardValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string text = TxtGuardValue.Text.Trim();
            if (text.Length == 0)
            {
                _selectedConnection.GuardValue = null;
                TxtGuardValue.BorderBrush = BrushNormalBorder;
            }
            else if (int.TryParse(text, out int v))
            {
                _selectedConnection.GuardValue = v;
                TxtGuardValue.BorderBrush = BrushNormalBorder;
            }
            else
            {
                TxtGuardValue.BorderBrush = new SolidColorBrush(Colors.Red);
            }
            ConnectionsWereModified = true;
            // Refresh guard label on edge
            Refresh();
        }

        private void TxtGuardWeeklyIncrement_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string text = TxtGuardWeeklyIncrement.Text.Trim();
            if (text.Length == 0)
            {
                _selectedConnection.GuardWeeklyIncrement = null;
                TxtGuardWeeklyIncrement.BorderBrush = BrushNormalBorder;
            }
            else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                _selectedConnection.GuardWeeklyIncrement = v;
                TxtGuardWeeklyIncrement.BorderBrush = BrushNormalBorder;
            }
            else
            {
                TxtGuardWeeklyIncrement.BorderBrush = new SolidColorBrush(Colors.Red);
            }
            ConnectionsWereModified = true;
        }

        private void TxtGuardZone_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string val = TxtGuardZone.Text.Trim();
            _selectedConnection.GuardZone = val.Length > 0 ? val : null;
            ConnectionsWereModified = true;
            ValidateGuardZone();
        }

        private void TxtGuardMatchGroup_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string val = TxtGuardMatchGroup.Text.Trim();
            _selectedConnection.GuardMatchGroup = val.Length > 0 ? val : null;
            ConnectionsWereModified = true;
        }

        private void ChkGuardEscape_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.GuardEscape = ChkGuardEscape.IsChecked;
            ConnectionsWereModified = true;
        }

        private void ChkSimTurnSquad_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.SimTurnSquad = ChkSimTurnSquad.IsChecked;
            ConnectionsWereModified = true;
        }

        private void ValidatePropertyPanel()
        {
            ValidateGuardZone();
            ValidateGuardValue();
            ValidateGuardWeeklyIncrement();
        }

        private void ValidateGuardZone()
        {
            string val = TxtGuardZone.Text.Trim();
            var zoneNames = new HashSet<string>(_zones.Select(z => z.Name), StringComparer.Ordinal);
            bool isInvalid = val.Length > 0 && !zoneNames.Contains(val);
            TxtGuardZone.BorderBrush = isInvalid
                ? new SolidColorBrush(Colors.Red)
                : BrushNormalBorder;
        }

        private void ValidateGuardValue()
        {
            string text = TxtGuardValue.Text.Trim();
            bool isInvalid = text.Length > 0 && !int.TryParse(text, out _);
            TxtGuardValue.BorderBrush = isInvalid
                ? new SolidColorBrush(Colors.Red)
                : BrushNormalBorder;
        }

        private void ValidateGuardWeeklyIncrement()
        {
            string text = TxtGuardWeeklyIncrement.Text.Trim();
            bool isInvalid = text.Length > 0 && !double.TryParse(text,
                NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            TxtGuardWeeklyIncrement.BorderBrush = isInvalid
                ? new SolidColorBrush(Colors.Red)
                : BrushNormalBorder;
        }

        // ── Delete connection ─────────────────────────────────────────────────

        private void BtnDeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConnection is null) return;
            DeleteSelectedConnection();
        }

        private void DeleteSelectedConnection()
        {
            if (_selectedConnection is null) return;
            _connections.Remove(_selectedConnection);
            _selectedConnection  = null;
            _selectedVisibleLine = null;
            PnlProperties.Visibility = Visibility.Collapsed;
            ConnectionsWereModified = true;
            Refresh();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Delete && _selectedConnection is not null)
            {
                DeleteSelectedConnection();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_addMode)
                    ExitAddMode();
                e.Handled = true;
            }
        }

        // ── Add-connection mode ───────────────────────────────────────────────

        private void BtnAddMode_Click(object sender, RoutedEventArgs e)
        {
            if (_addMode)
                ExitAddMode();
            else
                EnterAddMode();
        }

        private void EnterAddMode()
        {
            _addMode         = true;
            _pendingFromZone = null;
            _pendingToZone   = null;
            _pendingFromEllipse = null;
            ZoneCanvas.Cursor = Cursors.Cross;
            BtnAddMode.Content = "✕  Cancel Add Mode";

            // Deselect any currently selected edge
            if (_selectedVisibleLine is not null && _selectedConnection is not null)
            {
                bool wasPortal = string.Equals(_selectedConnection.ConnectionType, "Portal", StringComparison.Ordinal);
                _selectedVisibleLine.Stroke = wasPortal ? BrushEdgePortal : BrushEdgeDirect;
            }
            _selectedConnection  = null;
            _selectedVisibleLine = null;
            PnlProperties.Visibility = Visibility.Collapsed;

            Refresh();
        }

        private void ExitAddMode()
        {
            _addMode         = false;
            _pendingFromZone = null;
            _pendingToZone   = null;
            _pendingFromEllipse = null;
            ZoneCanvas.Cursor  = Cursors.Arrow;
            BtnAddMode.Content = "+ Add Connection";
            PnlAddConfirm.Visibility = Visibility.Collapsed;

            Refresh();
        }

        private void ZoneNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e, string zoneName)
        {
            if (!_addMode) return;
            e.Handled = true;

            if (_pendingFromZone is null)
            {
                // First click: set the "from" zone and highlight it
                _pendingFromZone    = zoneName;
                _pendingFromEllipse = sender as Ellipse;
                if (_pendingFromEllipse is not null)
                    _pendingFromEllipse.Stroke = BrushEdgeSelected;
            }
            else if (!string.Equals(_pendingFromZone, zoneName, StringComparison.Ordinal))
            {
                // Second click on a different zone: show the confirmation strip
                _pendingToZone = zoneName;
                ShowAddConfirmation();
            }
            // Clicking the same zone twice is ignored
        }

        private void ShowAddConfirmation()
        {
            TxtAddFromTo.Text = $"New connection:  {_pendingFromZone}  →  {_pendingToZone}";
            TxtAddName.Text                    = "";
            CmbAddType.SelectedIndex           = 0;
            TxtAddGuardValue.Text              = "";
            TxtAddGuardZone.Text               = "";
            TxtAddGuardZone.Tag                = (string)(_pendingFromZone ?? "");   // dynamic placeholder
            TxtAddGuardMatchGroup.Text         = "";
            TxtAddGuardMatchGroup.Tag          = (string)$"rnd_guard_{ZoneLetterFromName(_pendingFromZone ?? "")}_{ZoneLetterFromName(_pendingToZone ?? "")}"; // dynamic placeholder
            TxtAddGuardWeeklyIncrement.Text    = "";
            ChkAddSimTurnSquad.IsChecked       = true;               // default on
            PnlAddConfirm.Visibility = Visibility.Visible;
            TxtAddName.Focus();
        }

        private void BtnAddConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingFromZone is null || _pendingToZone is null) return;

            // GuardValue: default to 15000 when left blank
            string gvs = TxtAddGuardValue.Text.Trim();
            if (gvs.Length == 0) gvs = "15000";
            int? guardValue = null;
            if (int.TryParse(gvs, out int gv))
                guardValue = gv;

            string? addName = TxtAddName.Text.Trim();
            if (addName.Length == 0) addName = null;

            // GuardZone: default to the From zone when left blank
            string? addGuardZone = TxtAddGuardZone.Text.Trim();
            if (addGuardZone.Length == 0) addGuardZone = _pendingFromZone;

            // GuardMatchGroup: default to rnd_guard_{fromLetter}_{toLetter} when left blank
            string? addGuardMatchGroup = TxtAddGuardMatchGroup.Text.Trim();
            if (addGuardMatchGroup.Length == 0)
                addGuardMatchGroup = $"rnd_guard_{ZoneLetterFromName(_pendingFromZone ?? "")}_{ZoneLetterFromName(_pendingToZone ?? "")}";

            // WeeklyIncrement: default to 0.15 when left blank
            string wis = TxtAddGuardWeeklyIncrement.Text.Trim();
            if (wis.Length == 0) wis = "0.15";
            double? addWeeklyIncrement = null;
            if (double.TryParse(wis,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double wi))
                addWeeklyIncrement = wi;

            // SimTurnSquad: checkbox defaults to checked; null when false to avoid serialising false
            bool addSimTurnSquad = ChkAddSimTurnSquad.IsChecked == true;

            var newConn = new Connection
            {
                From                 = _pendingFromZone!,
                To                   = _pendingToZone!,
                ConnectionType       = CmbAddType.SelectedItem as string ?? "Direct",
                GuardValue           = guardValue,
                Name                 = addName,
                GuardZone            = addGuardZone,
                GuardMatchGroup      = addGuardMatchGroup,
                GuardWeeklyIncrement = addWeeklyIncrement,
                SimTurnSquad         = addSimTurnSquad ? true : null,
                IsUserAdded          = true
            };

            _connections.Add(newConn);
            ConnectionsWereModified = true;
            ExitAddMode();   // also calls Refresh()
        }

        private void BtnAddCancel_Click(object sender, RoutedEventArgs e) => ExitAddMode();

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Fires only when no child element handled the event (i.e. empty canvas background).
            if (!_addMode) return;
            if (PnlAddConfirm.Visibility == Visibility.Visible) return;
            ExitAddMode();
        }

        // ── Reset to generated ────────────────────────────────────────────────

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all connections to the auto-generated set? This will discard your edits.",
                "Reset Connections",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _connections.Clear();
            foreach (var orig in _originalConnections)
                _connections.Add(CloneConnection(orig, isUserAdded: false));

            _selectedConnection  = null;
            _selectedVisibleLine = null;
            PnlProperties.Visibility = Visibility.Collapsed;
            ConnectionsWereModified = true;

            Refresh();
        }

        // ── Deep-clone helper ─────────────────────────────────────────────────

        /// <summary>Extracts the letter/identifier from a zone name, e.g. "Spawn-A" → "A", "Neutral-C" → "C".</summary>
        private static string ZoneLetterFromName(string zoneName)
        {
            int dash = zoneName.IndexOf('-');
            return dash >= 0 ? zoneName[(dash + 1)..] : zoneName;
        }

        public static Connection CloneConnection(Connection c, bool isUserAdded = false) => new()
        {
            Name                     = c.Name,
            From                     = c.From,
            To                       = c.To,
            ConnectionType           = c.ConnectionType,
            GuardZone                = c.GuardZone,
            GuardEscape              = c.GuardEscape,
            SimTurnSquad             = c.SimTurnSquad,
            GuardValue               = c.GuardValue,
            GuardWeeklyIncrement     = c.GuardWeeklyIncrement,
            GuardMatchGroup          = c.GuardMatchGroup,
            PortalPlacementRulesFrom = c.PortalPlacementRulesFrom,
            PortalPlacementRulesTo   = c.PortalPlacementRulesTo,
            Road                     = c.Road,
            GatePlacement            = c.GatePlacement,
            Length                   = c.Length,
            IsUserAdded              = isUserAdded
        };
    }
}
