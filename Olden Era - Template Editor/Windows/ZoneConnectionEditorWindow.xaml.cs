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
        private Shape? _selectedVisibleLine;

        // ── Add-connection mode ──────────────────────────────────────────────
        private bool _addMode;
        private string? _pendingFromZone;
        private Ellipse? _pendingFromEllipse;
        private Line? _rubberBandLine;
        private bool _isDragging;

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

        // ── Guard-preset tables ──────────────────────────────────────────────
        private enum ZoneTier { Bronze, Silver, Gold, PlayerToPlayer }
        private static readonly string[] StrengthLabels = ["Weak", "Moderate", "Medium", "High", "Very High"];
        private static readonly int[,] GuardPresets =
        {
            //   Weak   Moderate  Medium   High  VeryHigh
            {  3_000,   6_000,   9_000,  12_000,  16_000 },  // Bronze
            { 18_000,  21_000,  24_000,  27_000,  30_000 },  // Silver
            { 36_000,  42_000,  48_000,  54_000,  60_000 },  // Gold
            { 10_000,  22_000,  34_000,  46_000,  58_000 },  // PlayerToPlayer
        };
        
        // Extra named values shown at the top of the guard-value dropdown
        private static readonly (string Label, int Value)[][] TierExtras =
        [
            [("Generator Default", 15_000)],  // Bronze
            [("Generator Default", 20_000)],  // Silver
            [("Generator Default", 25_000)],  // Gold
            [("Generator Default", 30_000)],  // PlayerToPlayer
        ];
        private static readonly string[] WeeklyIncrementLabels =
            ["Slow (5%)", "Normal (10%)", "Standard (15%)", "Fast (20%)", "Very Fast (25%)"];
        private static readonly double[] WeeklyIncrementValues =
            [0.05, 0.10, 0.15, 0.20, 0.25];

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

        // ── Constructor ──────────────────────────────────────────────────────

        static ZoneConnectionEditorWindow()
        {
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

            // Edit connection-type combobox (Proximity is not user-creatable)
            CmbConnectionType.Items.Add("Direct");
            CmbConnectionType.Items.Add("Portal");
            CmbConnectionType.SelectedIndex = 0;

            // Guard-zone combobox (property panel): populated per-connection in PopulatePropertyPanel

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
            // Group connections by unordered zone pair so parallel edges can be curved apart.
            const double BulgeGap = 18.0;   // perpendicular bulge step between parallel edges

            var pairGroups = new Dictionary<(string, string), List<Connection>>(
                EqualityComparer<(string, string)>.Default);

            foreach (var conn in _connections)
            {
                if (!_nodePositions.ContainsKey(conn.From) || !_nodePositions.ContainsKey(conn.To))
                    continue;
                var key = string.Compare(conn.From, conn.To, StringComparison.Ordinal) <= 0
                    ? (conn.From, conn.To) : (conn.To, conn.From);
                if (!pairGroups.TryGetValue(key, out var list))
                    pairGroups[key] = list = [];
                list.Add(conn);
            }

            // Build per-connection geometry (quadratic Bézier; control point at midpoint for N=1 → straight)
            var connGeometry = new Dictionary<Connection, (PathGeometry geo, Point labelPt)>(
                ReferenceEqualityComparer.Instance);

            foreach (var (key, group) in pairGroups)
            {
                int n = group.Count;

                // Canonical direction for the group (alphabetically sorted From→To)
                var (canonFrom, canonTo) = key;
                var canonFromPos = _nodePositions[canonFrom];
                var canonToPos   = _nodePositions[canonTo];
                double cdx  = canonToPos.X - canonFromPos.X;
                double cdy  = canonToPos.Y - canonFromPos.Y;
                double clen = Math.Sqrt(cdx * cdx + cdy * cdy);

                // Perpendicular unit normal in canonical direction (rotate 90° CCW)
                double cnx = clen > 0 ? -cdy / clen : 0;
                double cny = clen > 0 ?  cdx / clen : 0;

                // Base bulge that clears intermediate nodes that lie on the straight-line path
                double obstacleBase = ComputeObstacleAvoidanceBulge(
                    canonFromPos, canonToPos, canonFrom, canonTo);

                for (int i = 0; i < n; i++)
                {
                    var conn    = group[i];
                    var fromPos = _nodePositions[conn.From];
                    var toPos   = _nodePositions[conn.To];

                    // Individual bulge = obstacle offset + parallel-edge spread around it
                    double bulge = obstacleBase + (i - (n - 1) / 2.0) * BulgeGap;

                    var mid     = new Point((fromPos.X + toPos.X) / 2, (fromPos.Y + toPos.Y) / 2);
                    // Control point is 2× the desired midpoint displacement (quadratic Bézier property)
                    // Use canonical normal so all edges in the group bow in consistent directions
                    var ctrl    = new Point(mid.X + 2 * bulge * cnx, mid.Y + 2 * bulge * cny);
                    // Actual curve midpoint at t=0.5
                    var labelPt = new Point(mid.X + bulge * cnx, mid.Y + bulge * cny);

                    var figure = new PathFigure { StartPoint = fromPos, IsClosed = false };
                    figure.Segments.Add(new QuadraticBezierSegment(ctrl, toPos, true));
                    var geo = new PathGeometry([figure]);
                    geo.Freeze();

                    connGeometry[conn] = (geo, labelPt);
                }
            }

            // Draw each connection
            foreach (var conn in _connections)
            {
                if (!connGeometry.TryGetValue(conn, out var entry)) continue;
                var (geo, labelPt) = entry;

                bool isPortal   = string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal);
                bool isSelected = ReferenceEquals(conn, _selectedConnection);

                var normalBrush = isPortal ? BrushEdgePortal : BrushEdgeDirect;
                var strokeBrush = isSelected ? BrushEdgeSelected : normalBrush;

                var visiblePath = new Path
                {
                    Data = geo,
                    Stroke = strokeBrush,
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                    Fill = Brushes.Transparent
                };
                if (conn.IsUserAdded)
                    visiblePath.StrokeDashArray = [4.0, 3.0];

                if (isSelected)
                    _selectedVisibleLine = visiblePath;

                var hitPath = new Path
                {
                    Data = geo,
                    Stroke = Brushes.Transparent,
                    StrokeThickness = 12,
                    Fill = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };

                var capturedConn = conn;
                var capturedPath = visiblePath;
                hitPath.MouseLeftButtonDown += (_, e) =>
                {
                    if (_addMode) return;
                    e.Handled = true;
                    SelectEdge(capturedConn, capturedPath);
                };

                ZoneCanvas.Children.Add(hitPath);
                ZoneCanvas.Children.Add(visiblePath);

                // Guard-value label at the curve's midpoint
                if (conn.GuardValue.HasValue)
                {
                    var guardLabel = new TextBlock
                    {
                        Text = conn.GuardValue.Value.ToString(),
                        FontSize = 9,
                        Foreground = Brushes.LightYellow,
                        IsHitTestVisible = false
                    };
                    ZoneCanvas.Children.Add(guardLabel);
                    guardLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(guardLabel, labelPt.X - guardLabel.DesiredSize.Width  / 2.0);
                    Canvas.SetTop( guardLabel, labelPt.Y - guardLabel.DesiredSize.Height / 2.0);
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
                    Text = ZoneDisplayLabel(zone),
                    FontSize = 9,
                    Foreground = Brushes.White,
                    IsHitTestVisible = false,
                    TextWrapping = TextWrapping.NoWrap
                };
                ZoneCanvas.Children.Add(label);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, pos.X - label.DesiredSize.Width  / 2.0);
                Canvas.SetTop( label, pos.Y - label.DesiredSize.Height / 2.0);

                // Castle-count badge — bottom-right of the node circle
                int castleCount = ZoneCastleCount(zone);
                if (castleCount > 0)
                {
                    var badge = new System.Windows.Controls.Border
                    {
                        Background        = new SolidColorBrush(Color.FromRgb(28, 60, 35)),
                        BorderBrush       = new SolidColorBrush(Color.FromRgb(100, 200, 120)),
                        BorderThickness   = new Thickness(1),
                        CornerRadius      = new CornerRadius(4),
                        Padding           = new Thickness(3, 1, 3, 1),
                        IsHitTestVisible  = false,
                        Child = new TextBlock
                        {
                            Text       = $"🏰{castleCount}",
                            FontSize   = 9,
                            Foreground = new SolidColorBrush(Color.FromRgb(200, 245, 210)),
                        }
                    };
                    ZoneCanvas.Children.Add(badge);
                    badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    // Place at bottom-right edge of the circle
                    Canvas.SetLeft(badge, pos.X + NodeRadius * 0.55 - badge.DesiredSize.Width  / 2.0);
                    Canvas.SetTop( badge, pos.Y + NodeRadius * 0.55 - badge.DesiredSize.Height / 2.0);
                }
            }
        }

        private static int ZoneCastleCount(Zone zone)
        {
            int count = 0;
            foreach (var obj in zone.MainObjects ?? [])
                if (obj.Type is "City" or "Spawn" or "AbandonedOutpost")
                    count++;
            return count;
        }

        /// <summary>Returns a short display label for the zone node. Spawn zones show their player number (A=1, B=2…).</summary>
        private static string ZoneDisplayLabel(Zone zone)
        {
            if (zone.Name.StartsWith("Spawn-", StringComparison.Ordinal) && zone.Name.Length > 6)
            {
                char letter = char.ToUpperInvariant(zone.Name[6]);
                if (letter >= 'A' && letter <= 'Z')
                    return ((letter - 'A') + 1).ToString(CultureInfo.InvariantCulture);
            }
            return zone.Name;
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

        private ZoneTier GetZoneTier(string? zoneName)
        {
            if (zoneName is null) return ZoneTier.Bronze;
            var zone = _zones.FirstOrDefault(z => z.Name == zoneName);
            if (zone is null) return ZoneTier.Bronze;

            if (_playerZoneNames.Contains(zone.Name)) return ZoneTier.Bronze;
            if (zone.Name.Equals("Hub", StringComparison.Ordinal)
                || zone.Name.StartsWith("Hub-", StringComparison.Ordinal))
                return ZoneTier.Bronze;

            if (zone.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            {
                string pool = zone.GuardedContentPool?.FirstOrDefault() ?? "";
                if (pool.Contains("_t4_") || pool.Contains("_t5_")) return ZoneTier.Gold;
                if (pool.Contains("_t1_") || pool.Contains("_t2_")) return ZoneTier.Bronze;
                return ZoneTier.Silver;
            }

            return ZoneTier.Bronze;
        }

        private ZoneTier HigherTierOf(string? zoneA, string? zoneB)
        {
            bool aIsPlayer = zoneA is not null && _playerZoneNames.Contains(zoneA);
            bool bIsPlayer = zoneB is not null && _playerZoneNames.Contains(zoneB);
            if (aIsPlayer && bIsPlayer) return ZoneTier.PlayerToPlayer;
            return (ZoneTier)Math.Max((int)GetZoneTier(zoneA), (int)GetZoneTier(zoneB));
        }

        // ── Edge selection ────────────────────────────────────────────────────

        private void SelectEdge(Connection conn, Shape visibleLine)
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
                // Connection type
                int typeIdx = CmbConnectionType.Items.IndexOf(conn.ConnectionType ?? "Direct");
                CmbConnectionType.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;

                // Guard zone — only the two connected nodes
                CmbGuardZone.Items.Clear();
                CmbGuardZone.Items.Add(conn.From);
                CmbGuardZone.Items.Add(conn.To);
                int gzIdx = CmbGuardZone.Items.IndexOf(conn.GuardZone ?? conn.From);
                CmbGuardZone.SelectedIndex = gzIdx >= 0 ? gzIdx : 0;

                // Guard value — presets derived from tier, auto-expand Advanced if custom
                ZoneTier tier = HigherTierOf(conn.From, conn.To);
                PopulateGuardValueCombo(tier, conn.GuardValue);

                // Weekly increment — preset list, auto-expand Advanced if custom
                PopulateWeeklyIncrementCombo(conn.GuardWeeklyIncrement);

                // Advanced fields
                TxtGuardMatchGroup.Text   = conn.GuardMatchGroup ?? "";
                ChkGuardEscape.IsChecked  = conn.GuardEscape  ?? false;
                ChkSimTurnSquad.IsChecked = conn.SimTurnSquad ?? false;
            }
            finally
            {
                _suppressPropertyEvents = false;
            }
        }

        private void PopulateGuardValueCombo(ZoneTier tier, int? currentValue)
        {
            CmbGuardValue.Items.Clear();
            var extras     = TierExtras[(int)tier];
            int extraLen   = extras.Length;
            int presetOff  = extraLen > 0 ? extraLen + 1 : 0; // +1 accounts for separator

            // Generator Default entries at top
            foreach (var (label, value) in extras)
                CmbGuardValue.Items.Add($"{label}  ({value:N0})");
            if (extraLen > 0)
                CmbGuardValue.Items.Add(new Separator());

            // Standard presets below
            for (int i = 0; i < StrengthLabels.Length; i++)
                CmbGuardValue.Items.Add($"{StrengthLabels[i]}  ({GuardPresets[(int)tier, i]:N0})");
            if (ChkPropAdvanced.IsChecked == true)
                CmbGuardValue.Items.Add("Custom...");

            bool matched = false;
            if (currentValue.HasValue)
            {
                // Check extras first so Generator Default takes priority over a coincident preset
                for (int i = 0; i < extraLen; i++)
                {
                    if (extras[i].Value == currentValue.Value)
                    {
                        CmbGuardValue.SelectedIndex = i;
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    for (int i = 0; i < StrengthLabels.Length; i++)
                    {
                        if (GuardPresets[(int)tier, i] == currentValue.Value)
                        {
                            CmbGuardValue.SelectedIndex = presetOff + i;
                            matched = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                CmbGuardValue.SelectedIndex = presetOff + 2; // Medium
                matched = true;
            }
            if (!matched)
            {
                if (!CmbGuardValue.Items.Contains("Custom..."))
                    CmbGuardValue.Items.Add("Custom...");
                CmbGuardValue.SelectedIndex = CmbGuardValue.Items.Count - 1;
                TxtPropGuardValueCustom.Text = currentValue!.Value.ToString();
            }
            TxtPropGuardValueCustom.Visibility =
                CmbGuardValue.SelectedItem as string == "Custom..."
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PopulateWeeklyIncrementCombo(double? currentValue)
        {
            CmbGuardWeeklyIncrement.Items.Clear();
            foreach (string label in WeeklyIncrementLabels)
                CmbGuardWeeklyIncrement.Items.Add(label);
            if (ChkPropAdvanced.IsChecked == true)
                CmbGuardWeeklyIncrement.Items.Add("Custom...");

            bool matched = false;
            if (currentValue.HasValue)
            {
                for (int i = 0; i < WeeklyIncrementValues.Length; i++)
                {
                    if (Math.Abs(WeeklyIncrementValues[i] - currentValue.Value) < 0.001)
                    {
                        CmbGuardWeeklyIncrement.SelectedIndex = i;
                        matched = true;
                        break;
                    }
                }
            }
            else
            {
                CmbGuardWeeklyIncrement.SelectedIndex = 2; // Standard default
                matched = true;
            }
            if (!matched)
            {
                if (!CmbGuardWeeklyIncrement.Items.Contains("Custom..."))
                    CmbGuardWeeklyIncrement.Items.Add("Custom...");
                CmbGuardWeeklyIncrement.SelectedIndex = CmbGuardWeeklyIncrement.Items.Count - 1;
                TxtPropIncrementCustom.Text = currentValue!.Value.ToString("G", CultureInfo.InvariantCulture);
            }
            TxtPropIncrementCustom.Visibility =
                CmbGuardWeeklyIncrement.SelectedItem as string == "Custom..."
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Property panel event handlers ─────────────────────────────────────

        private void ChkPropAdvanced_Changed(object sender, RoutedEventArgs e)
        {
            bool advanced = ChkPropAdvanced.IsChecked == true;
            PnlPropsAdvanced.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;

            if (_selectedConnection is null) return;
            ZoneTier tier = HigherTierOf(_selectedConnection.From, _selectedConnection.To);

            // Add/remove "Custom..." from guard-value combo
            bool gvHasCustom = CmbGuardValue.Items.Contains("Custom...");
            if (advanced && !gvHasCustom)
            {
                CmbGuardValue.Items.Add("Custom...");
            }
            else if (!advanced && gvHasCustom)
            {
                if (CmbGuardValue.SelectedItem as string == "Custom...")
                {
                    CmbGuardValue.SelectedIndex = 2;
                    _selectedConnection.GuardValue = GuardPresets[(int)tier, 2];
                }
                CmbGuardValue.Items.Remove("Custom...");
                TxtPropGuardValueCustom.Text       = "";
                TxtPropGuardValueCustom.Visibility = Visibility.Collapsed;
            }

            // Add/remove "Custom..." from weekly-increment combo
            bool wiHasCustom = CmbGuardWeeklyIncrement.Items.Contains("Custom...");
            if (advanced && !wiHasCustom)
            {
                CmbGuardWeeklyIncrement.Items.Add("Custom...");
            }
            else if (!advanced && wiHasCustom)
            {
                if (CmbGuardWeeklyIncrement.SelectedItem as string == "Custom...")
                {
                    CmbGuardWeeklyIncrement.SelectedIndex = 2;
                    _selectedConnection.GuardWeeklyIncrement = WeeklyIncrementValues[2];
                }
                CmbGuardWeeklyIncrement.Items.Remove("Custom...");
                TxtPropIncrementCustom.Text       = "";
                TxtPropIncrementCustom.Visibility = Visibility.Collapsed;
            }
        }

        private void CmbConnectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.ConnectionType = CmbConnectionType.SelectedItem as string;
            ConnectionsWereModified = true;
        }

        private void CmbGuardValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            if (CmbGuardValue.SelectedItem is Separator) return;
            bool isCustom = CmbGuardValue.SelectedItem as string == "Custom...";
            TxtPropGuardValueCustom.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (isCustom) return;

            ZoneTier tier  = HigherTierOf(_selectedConnection.From, _selectedConnection.To);
            var extras     = TierExtras[(int)tier];
            int extraLen   = extras.Length;
            int presetOff  = extraLen > 0 ? extraLen + 1 : 0;
            int idx        = CmbGuardValue.SelectedIndex;

            if (idx < extraLen)
            {
                _selectedConnection.GuardValue = extras[idx].Value;
                ConnectionsWereModified = true;
                Refresh();
            }
            else if (idx >= presetOff)
            {
                int presetIdx = idx - presetOff;
                if (presetIdx < StrengthLabels.Length)
                {
                    _selectedConnection.GuardValue = GuardPresets[(int)tier, presetIdx];
                    ConnectionsWereModified = true;
                    Refresh();
                }
            }
        }

        private void TxtPropGuardValueCustom_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            if (int.TryParse(TxtPropGuardValueCustom.Text.Trim(), out int v))
            {
                _selectedConnection.GuardValue = v;
                ConnectionsWereModified = true;
                Refresh();
            }
        }

        private void CmbGuardWeeklyIncrement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            bool isCustom = CmbGuardWeeklyIncrement.SelectedItem as string == "Custom...";
            TxtPropIncrementCustom.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (!isCustom)
            {
                int idx = CmbGuardWeeklyIncrement.SelectedIndex;
                if (idx >= 0 && idx < WeeklyIncrementValues.Length)
                {
                    _selectedConnection.GuardWeeklyIncrement = WeeklyIncrementValues[idx];
                    ConnectionsWereModified = true;
                }
            }
        }

        private void TxtPropIncrementCustom_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            if (double.TryParse(TxtPropIncrementCustom.Text.Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                _selectedConnection.GuardWeeklyIncrement = v;
                ConnectionsWereModified = true;
            }
        }

        private void CmbGuardZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string? val = CmbGuardZone.SelectedItem as string;
            _selectedConnection.GuardZone = string.IsNullOrEmpty(val) ? null : val;
            ConnectionsWereModified = true;
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
            _addMode            = false;
            _pendingFromZone    = null;
            _pendingFromEllipse = null;
            _isDragging         = false;
            if (_rubberBandLine is not null)
            {
                ZoneCanvas.Children.Remove(_rubberBandLine);
                _rubberBandLine = null;
            }
            ZoneCanvas.ReleaseMouseCapture();
            ZoneCanvas.Cursor  = Cursors.Arrow;
            BtnAddMode.Content = "+ Add Connection";

            Refresh();
        }

        private void ZoneNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e, string zoneName)
        {
            if (!_addMode) return;
            e.Handled = true;

            _pendingFromZone = zoneName;
            _isDragging      = true;

            // Highlight source node and draw initial rubber-band
            Refresh();   // picks up _pendingFromZone to colour the node

            var fromPos = _nodePositions.GetValueOrDefault(zoneName);
            _rubberBandLine = new Line
            {
                X1 = fromPos.X, Y1 = fromPos.Y,
                X2 = fromPos.X, Y2 = fromPos.Y,
                Stroke = BrushEdgeSelected,
                StrokeThickness = 1.5,
                StrokeDashArray = [4.0, 3.0],
                IsHitTestVisible = false
            };
            ZoneCanvas.Children.Add(_rubberBandLine);
            ZoneCanvas.CaptureMouse();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _rubberBandLine is null) return;
            var pos = e.GetPosition(ZoneCanvas);
            _rubberBandLine.X2 = pos.X;
            _rubberBandLine.Y2 = pos.Y;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            var pos = e.GetPosition(ZoneCanvas);
            string? targetZone = HitTestZone(pos);

            if (targetZone is not null
                && !string.Equals(targetZone, _pendingFromZone, StringComparison.Ordinal))
            {
                AddConnectionWithDefaults(_pendingFromZone!, targetZone);
            }
            else
            {
                // Drag landed on the same zone or empty space — clean up and stay in add mode
                _isDragging = false;
                if (_rubberBandLine is not null)
                {
                    ZoneCanvas.Children.Remove(_rubberBandLine);
                    _rubberBandLine = null;
                }
                ZoneCanvas.ReleaseMouseCapture();
                _pendingFromZone = null;
                Refresh();
            }
        }

        private double ComputeObstacleAvoidanceBulge(
            Point p0, Point p1, string fromName, string toName)
        {
            const double Clearance = NodeRadius + 8.0;

            double dx    = p1.X - p0.X;
            double dy    = p1.Y - p0.Y;
            double lenSq = dx * dx + dy * dy;
            double len   = Math.Sqrt(lenSq);
            if (len < 1) return 0;

            double maxPos = 0;   // largest positive bulge required
            double maxNeg = 0;   // largest (most negative) negative bulge required

            foreach (var (name, pos) in _nodePositions)
            {
                if (string.Equals(name, fromName, StringComparison.Ordinal)) continue;
                if (string.Equals(name, toName,   StringComparison.Ordinal)) continue;

                double qx = pos.X - p0.X;
                double qy = pos.Y - p0.Y;

                // Projection parameter along the segment [0,1]
                double t = (qx * dx + qy * dy) / lenSq;
                if (t < 0.05 || t > 0.95) continue;   // outside the span between the two nodes

                // Signed perpendicular distance from the line (positive = left of p0→p1)
                double d = (dx * qy - dy * qx) / len;

                // Straight line already clears this node?
                if (Math.Abs(d) >= Clearance) continue;

                // Bézier mid-curve factor at this t: the curve displaces perpendicularly by factor*bulge
                double factor = 4.0 * t * (1.0 - t);
                if (factor < 1e-6) continue;

                // Bulge needed to pass on the positive (left) side
                double bPos = (d + Clearance) / factor;
                // Bulge needed to pass on the negative (right) side
                double bNeg = (d - Clearance) / factor;

                if (bPos > 0) maxPos = Math.Max(maxPos, bPos);
                if (bNeg < 0) maxNeg = Math.Min(maxNeg, bNeg);
            }

            if (maxPos == 0 && maxNeg == 0) return 0;
            if (maxPos == 0) return maxNeg;
            if (maxNeg == 0) return maxPos;
            // Obstacles on both sides — take the direction with the smaller required deflection
            return Math.Abs(maxPos) <= Math.Abs(maxNeg) ? maxPos : maxNeg;
        }

        private string? HitTestZone(Point pos)
        {
            foreach (var (name, center) in _nodePositions)
            {
                double dx = pos.X - center.X;
                double dy = pos.Y - center.Y;
                if (Math.Sqrt(dx * dx + dy * dy) <= NodeRadius)
                    return name;
            }
            return null;
        }

        private void AddConnectionWithDefaults(string from, string to)
        {
            ZoneTier tier = HigherTierOf(from, to);
            string baseName = $"Conn-{ZoneLetterFromName(from)}-{ZoneLetterFromName(to)}";
            string uniqueName = GetUniqueConnectionName(baseName);
            var newConn = new Connection
            {
                Name                 = uniqueName,
                From                 = from,
                To                   = to,
                ConnectionType       = "Direct",
                GuardValue           = TierExtras[(int)tier][0].Value,  // Generator Default
                GuardZone            = from,
                GuardMatchGroup      = $"rnd_guard_{ZoneLetterFromName(from)}_{ZoneLetterFromName(to)}",
                GuardWeeklyIncrement = WeeklyIncrementValues[2],       // Standard 15%
                IsUserAdded          = true
            };
            _connections.Add(newConn);
            ConnectionsWereModified = true;

            // Pre-select so Refresh() in ExitAddMode sets _selectedVisibleLine
            _selectedConnection = newConn;
            ExitAddMode();

            // Show property panel for the new connection
            PnlProperties.Visibility = Visibility.Visible;
            PopulatePropertyPanel(newConn);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Fires only when clicking the canvas background (no child handled it).
            if (!_addMode || _isDragging) return;
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

        private string GetUniqueConnectionName(string baseName)
        {
            var existing = _connections
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal);

            if (!existing.Contains(baseName))
                return baseName;

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{baseName}-{suffix}";
                suffix++;
            }
            while (existing.Contains(candidate));

            return candidate;
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
