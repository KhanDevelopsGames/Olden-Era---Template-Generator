using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using MapTopology = Olden_Era___Template_Editor.Models.MapTopology;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Olden_Era___Template_Editor
{
    public partial class ZoneConnectionEditorControl : UserControl
    {
        private List<Zone> _zones = [];
        private List<Connection> _connections = [];
        private List<Connection> _originalConnections = [];
        private MapTopology _topology;
        private HashSet<string> _playerZoneNames = new(StringComparer.Ordinal);
        private Dictionary<string, Point> _nodePositions = new(StringComparer.Ordinal);

        private Connection? _selectedConnection;
        private Shape? _selectedVisibleLine;
        private string? _selectedZoneName;

        private string? _pendingFromZone;
        private Point _dragStartPoint;
        private Line? _rubberBandLine;
        private bool _isPotentialDrag;
        private bool _isDragging;

        private bool _suppressPropertyEvents;

        private List<(Connection conn, PathGeometry geo, Path visiblePath)> _connectionGeometries = [];
        private Dictionary<string, ZoneSnapshot> _originalZonesByName = new(StringComparer.Ordinal);
        private Dictionary<string, ZoneOverrideSettings> _zoneOverridesByName = new(StringComparer.Ordinal);

        public bool ConnectionsWereModified { get; private set; }
        public bool HasUnresolvedErrors { get; private set; }

        public event EventHandler? ConnectionsModified;
        public event EventHandler? ZoneOverridesModified;
        public event EventHandler? ErrorsChanged;

        private const double NodeRadius = 18.0;
        private const double DragStartThreshold = 6.0;

        private sealed record ZoneSnapshot(
            double? Size,
            string? Layout,
            int? GuardCutoffValue,
            double? GuardRandomization,
            double? GuardMultiplier,
            double? GuardWeeklyIncrement,
            int? GuardedContentValue,
            int? GuardedContentValuePerArea,
            int? UnguardedContentValue,
            int? UnguardedContentValuePerArea,
            int? ResourcesValue,
            int? ResourcesValuePerArea);

        private enum ZoneTier { Bronze, Silver, Gold, PlayerToPlayer }
        private static readonly string[] StrengthLabels = ["Weak", "Moderate", "Medium", "High", "Very High"];
        private static readonly int[,] GuardPresets =
        {
            {  3_000,   6_000,   9_000,  12_000,  16_000 },
            { 18_000,  21_000,  24_000,  27_000,  30_000 },
            { 36_000,  42_000,  48_000,  54_000,  60_000 },
            { 10_000,  22_000,  34_000,  46_000,  58_000 },
        };

        private static readonly (string Label, int Value)[][] TierExtras =
        [
            [("Generator Default", 15_000)],
            [("Generator Default", 20_000)],
            [("Generator Default", 25_000)],
            [("Generator Default", 30_000)],
        ];
        private static readonly string[] WeeklyIncrementLabels =
            ["Slow (5%)", "Normal (10%)", "Standard (15%)", "Fast (20%)", "Very Fast (25%)"];
        private static readonly double[] WeeklyIncrementValues =
            [0.05, 0.10, 0.15, 0.20, 0.25];

        private static readonly SolidColorBrush BrushPlayerFill = new(Color.FromRgb(42, 90, 50));
        private static readonly SolidColorBrush BrushPlayerBorder = new(Color.FromRgb(100, 200, 120));
        private static readonly SolidColorBrush BrushBronzeFill = new(Color.FromRgb(101, 67, 33));
        private static readonly SolidColorBrush BrushBronzeBorder = new(Color.FromRgb(205, 127, 50));
        private static readonly SolidColorBrush BrushSilverFill = new(Color.FromRgb(72, 76, 80));
        private static readonly SolidColorBrush BrushSilverBorder = new(Color.FromRgb(192, 192, 192));
        private static readonly SolidColorBrush BrushGoldFill = new(Color.FromRgb(120, 90, 20));
        private static readonly SolidColorBrush BrushGoldBorder = new(Color.FromRgb(255, 210, 50));
        private static readonly SolidColorBrush BrushHubFill = new(Color.FromRgb(55, 80, 95));
        private static readonly SolidColorBrush BrushHubBorder = new(Color.FromRgb(130, 180, 200));
        private static readonly SolidColorBrush BrushEdgeDirect = new(Color.FromRgb(180, 145, 60));
        private static readonly SolidColorBrush BrushEdgePortal = new(Color.FromArgb(210, 90, 170, 210));
        private static readonly SolidColorBrush BrushEdgeSelected = new(Color.FromRgb(255, 140, 0));

        public ZoneConnectionEditorControl()
        {
            InitializeComponent();

            CmbConnectionType.Items.Add("Direct");
            CmbConnectionType.Items.Add("Portal");
            CmbConnectionType.SelectedIndex = 0;

            Loaded += (_, _) => RenderAll();
            ZoneCanvas.SizeChanged += (_, _) => RenderAll();
            PreviewKeyDown += ZoneConnectionEditorControl_PreviewKeyDown;
        }

        public void InitializeEditor(
            List<Zone> zones,
            List<Connection> connections,
            List<Connection> originalConnections,
            MapTopology topology,
            HashSet<string> playerZoneNames)
        {
            _zones = zones;
            _connections = connections;
            _originalConnections = originalConnections;
            _topology = topology;
            _playerZoneNames = playerZoneNames;
            _originalZonesByName = _zones
                .Where(zone => !string.IsNullOrWhiteSpace(zone.Name))
                .ToDictionary(zone => zone.Name, CreateSnapshot, StringComparer.Ordinal);
            _zoneOverridesByName = new Dictionary<string, ZoneOverrideSettings>(StringComparer.Ordinal);

            ConnectionsWereModified = false;
            _selectedConnection = null;
            _selectedVisibleLine = null;
            _selectedZoneName = null;
            PnlProperties.Visibility = Visibility.Collapsed;
            CancelDragAdd();
            RenderAll();
        }

        public IReadOnlyList<ZoneOverrideSettings> GetZoneOverridesSnapshot()
            => _zoneOverridesByName.Values
                .OrderBy(overrideSettings => overrideSettings.ZoneName, StringComparer.Ordinal)
                .Select(CloneZoneOverride)
                .ToList();

        private void MarkConnectionsModified()
        {
            ConnectionsWereModified = true;
            ConnectionsModified?.Invoke(this, EventArgs.Empty);
        }

        private void MarkZoneOverridesModified()
            => ZoneOverridesModified?.Invoke(this, EventArgs.Empty);

        private void ZoneConnectionEditorControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && _selectedConnection is not null)
            {
                DeleteSelectedConnection();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelDragAdd();
                e.Handled = true;
            }
        }

        private void ComputeNodePositions()
        {
            double cw = ZoneCanvas.ActualWidth;
            double ch = ZoneCanvas.ActualHeight;
            if (cw <= 0 || ch <= 0) return;

            var miniTemplate = new RmgTemplate
            {
                Variants = [new Variant { Zones = _zones, Connections = _connections }]
            };
            var rawPositions = TemplatePreviewPngWriter.ComputeLayout(miniTemplate, _topology);

            double sx = cw / 700.0;
            double sy = ch / 700.0;

            _nodePositions = new Dictionary<string, Point>(StringComparer.Ordinal);
            foreach (var (name, pos) in rawPositions)
                _nodePositions[name] = new Point(pos.X * sx, pos.Y * sy);
        }

        private void RenderAll()
        {
            ComputeNodePositions();
            Refresh();
        }

        private void Refresh()
        {
            ZoneCanvas.Children.Clear();
            RenderEdges();
            RenderNodes();
        }

        private void RenderEdges()
        {
            const double BulgeGap = 18.0;

            _connectionGeometries.Clear();

            var pairGroups = new Dictionary<(string, string), List<Connection>>(EqualityComparer<(string, string)>.Default);

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

            var connGeometry = new Dictionary<Connection, (PathGeometry geo, Point labelPt)>(ReferenceEqualityComparer.Instance);

            foreach (var (key, group) in pairGroups)
            {
                int n = group.Count;

                var (canonFrom, canonTo) = key;
                var canonFromPos = _nodePositions[canonFrom];
                var canonToPos = _nodePositions[canonTo];
                double cdx = canonToPos.X - canonFromPos.X;
                double cdy = canonToPos.Y - canonFromPos.Y;
                double clen = Math.Sqrt(cdx * cdx + cdy * cdy);

                double cnx = clen > 0 ? -cdy / clen : 0;
                double cny = clen > 0 ? cdx / clen : 0;

                double obstacleBase = ComputeObstacleAvoidanceBulge(canonFromPos, canonToPos, canonFrom, canonTo);

                for (int i = 0; i < n; i++)
                {
                    var conn = group[i];
                    var fromPos = _nodePositions[conn.From];
                    var toPos = _nodePositions[conn.To];

                    double bulge = obstacleBase + (i - (n - 1) / 2.0) * BulgeGap;

                    var mid = new Point((fromPos.X + toPos.X) / 2, (fromPos.Y + toPos.Y) / 2);
                    var ctrl = new Point(mid.X + 2 * bulge * cnx, mid.Y + 2 * bulge * cny);
                    var labelPt = new Point(mid.X + bulge * cnx, mid.Y + bulge * cny);

                    var figure = new PathFigure { StartPoint = fromPos, IsClosed = false };
                    figure.Segments.Add(new QuadraticBezierSegment(ctrl, toPos, true));
                    var geo = new PathGeometry([figure]);
                    geo.Freeze();

                    connGeometry[conn] = (geo, labelPt);
                }
            }

            foreach (var conn in _connections)
            {
                if (!connGeometry.TryGetValue(conn, out var entry)) continue;
                var (geo, labelPt) = entry;

                bool isPortal = string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal);
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

                _connectionGeometries.Add((conn, geo, visiblePath));

                ZoneCanvas.Children.Add(hitPath);
                ZoneCanvas.Children.Add(visiblePath);

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
                    Canvas.SetLeft(guardLabel, labelPt.X - guardLabel.DesiredSize.Width / 2.0);
                    Canvas.SetTop(guardLabel, labelPt.Y - guardLabel.DesiredSize.Height / 2.0);
                }
            }

            var zoneNameSet = new HashSet<string>(_zones.Select(z => z.Name), StringComparer.Ordinal);
            bool oldHasErrors = HasUnresolvedErrors;
            HasUnresolvedErrors = _connections.Any(c => !zoneNameSet.Contains(c.From) || !zoneNameSet.Contains(c.To));
            if (oldHasErrors != HasUnresolvedErrors)
                ErrorsChanged?.Invoke(this, EventArgs.Empty);

            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            int count = _connections.Count;
            string status = $"{count} connection(s)";

            var isolated = _zones
                .Where(z => !_connections.Any(c =>
                    string.Equals(c.From, z.Name, StringComparison.Ordinal) ||
                    string.Equals(c.To, z.Name, StringComparison.Ordinal)))
                .Select(z => z.Name)
                .ToList();

            if (isolated.Count > 0)
                status += $"  Isolated zones: {string.Join(", ", isolated)}";

            if (HasUnresolvedErrors)
                status += "  Invalid zone references; export blocked";

            TxtStatus.Text = status;
        }

        private void RenderNodes()
        {
            foreach (var zone in _zones)
            {
                if (!_nodePositions.TryGetValue(zone.Name, out var pos)) continue;

                var (fillBrush, borderBrush) = GetZoneColors(zone);

                var ellipse = new Ellipse
                {
                    Width = NodeRadius * 2,
                    Height = NodeRadius * 2,
                    Fill = fillBrush,
                    Stroke = string.Equals(_selectedZoneName, zone.Name, StringComparison.Ordinal)
                        ? BrushEdgeSelected
                        : borderBrush,
                    StrokeThickness = string.Equals(_selectedZoneName, zone.Name, StringComparison.Ordinal) ? 3 : 2,
                    Cursor = Cursors.Hand
                };

                string zoneName = zone.Name;
                ellipse.MouseLeftButtonDown += (s, e) => ZoneNode_MouseLeftButtonDown(s, e, zoneName);

                Canvas.SetLeft(ellipse, pos.X - NodeRadius);
                Canvas.SetTop(ellipse, pos.Y - NodeRadius);
                ZoneCanvas.Children.Add(ellipse);

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
                Canvas.SetLeft(label, pos.X - label.DesiredSize.Width / 2.0);
                Canvas.SetTop(label, pos.Y - label.DesiredSize.Height / 2.0);
            }
        }

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
                    return (BrushGoldFill, BrushGoldBorder);
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

        private void SelectEdge(Connection conn, Shape visibleLine)
        {
            if (_selectedConnection is not null && _selectedVisibleLine is not null)
            {
                bool wasPortal = string.Equals(_selectedConnection.ConnectionType, "Portal", StringComparison.Ordinal);
                _selectedVisibleLine.Stroke = wasPortal ? BrushEdgePortal : BrushEdgeDirect;
            }

            _selectedZoneName = null;
            _selectedConnection = conn;
            _selectedVisibleLine = visibleLine;
            visibleLine.Stroke = BrushEdgeSelected;

            ShowConnectionPropertyPanel();
            PopulatePropertyPanel(conn);
        }

        private void SelectZone(string zoneName)
        {
            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, zoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            if (_selectedVisibleLine is not null && _selectedConnection is not null)
            {
                bool wasPortal = string.Equals(_selectedConnection.ConnectionType, "Portal", StringComparison.Ordinal);
                _selectedVisibleLine.Stroke = wasPortal ? BrushEdgePortal : BrushEdgeDirect;
            }

            _selectedConnection = null;
            _selectedVisibleLine = null;
            _selectedZoneName = zone.Name;

            ShowZonePropertyPanel();
            PopulateZonePropertyPanel(zone);
            Refresh();
        }

        private void ShowConnectionPropertyPanel()
        {
            PnlProperties.Visibility = Visibility.Visible;
            TxtPropertiesHeader.Text = "CONNECTION PROPERTIES";
            PnlConnectionProperties.Visibility = Visibility.Visible;
            PnlZoneProperties.Visibility = Visibility.Collapsed;
            ChkPropAdvanced.Visibility = Visibility.Visible;
        }

        private void ShowZonePropertyPanel()
        {
            PnlProperties.Visibility = Visibility.Visible;
            TxtPropertiesHeader.Text = "ZONE PROPERTIES";
            PnlConnectionProperties.Visibility = Visibility.Collapsed;
            PnlZoneProperties.Visibility = Visibility.Visible;
            ChkPropAdvanced.Visibility = Visibility.Visible;
            bool advanced = ChkPropAdvanced.IsChecked == true;
            PnlZoneAdvanced.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlZoneAdvancedContentValues.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            PnlZoneContentMultipliers.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PopulateZonePropertyPanel(Zone zone)
        {
            _suppressPropertyEvents = true;
            try
            {
                TxtZoneNameDisplay.Text = zone.Name;
                SldZoneSize.Value = Math.Round((zone.Size ?? 1.0) * 100.0, 0, MidpointRounding.AwayFromZero);
                TxtZoneGuardCutoff.Text = zone.GuardCutoffValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                TxtZoneGuardedContentValue.Text = zone.GuardedContentValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                TxtZoneGuardedContentValuePerArea.Text = zone.GuardedContentValuePerArea?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                TxtZoneUnguardedContentValue.Text = zone.UnguardedContentValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                TxtZoneUnguardedContentValuePerArea.Text = zone.UnguardedContentValuePerArea?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                TxtZoneResourcesValue.Text = zone.ResourcesValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                TxtZoneResourcesValuePerArea.Text = zone.ResourcesValuePerArea?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

                SldZoneGuardRandomization.Value = Math.Round((zone.GuardRandomization ?? 0.05) * 100.0, 0, MidpointRounding.AwayFromZero);
                SldZoneGuardMultiplier.Value = Math.Round((zone.GuardMultiplier ?? 1.0) * 100.0, 0, MidpointRounding.AwayFromZero);
                SldZoneGuardWeeklyIncrement.Value = SnapWeeklyPercent(Math.Round((zone.GuardWeeklyIncrement ?? 0.15) * 100.0, 0, MidpointRounding.AwayFromZero));

                ZoneSnapshot original = _originalZonesByName.TryGetValue(zone.Name, out ZoneSnapshot? snapshot)
                    ? snapshot
                    : CreateSnapshot(zone);

                SldZoneGuardedContentValueMultiplier.Value = ComputeContentMultiplierPercent(original.GuardedContentValue, zone.GuardedContentValue);
                SldZoneUnguardedContentValueMultiplier.Value = ComputeContentMultiplierPercent(original.UnguardedContentValue, zone.UnguardedContentValue);
                SldZoneResourcesValueMultiplier.Value = ComputeContentMultiplierPercent(original.ResourcesValue, zone.ResourcesValue);
                UpdateZoneSliderLabels();
            }
            finally
            {
                _suppressPropertyEvents = false;
            }
        }

        private void PopulatePropertyPanel(Connection conn)
        {
            _suppressPropertyEvents = true;
            try
            {
                int typeIdx = CmbConnectionType.Items.IndexOf(conn.ConnectionType ?? "Direct");
                CmbConnectionType.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;

                CmbGuardZone.Items.Clear();
                CmbGuardZone.Items.Add(conn.From);
                CmbGuardZone.Items.Add(conn.To);
                int gzIdx = CmbGuardZone.Items.IndexOf(conn.GuardZone ?? conn.From);
                CmbGuardZone.SelectedIndex = gzIdx >= 0 ? gzIdx : 0;

                ZoneTier tier = HigherTierOf(conn.From, conn.To);
                PopulateGuardValueCombo(tier, conn.GuardValue);
                PopulateWeeklyIncrementCombo(conn.GuardWeeklyIncrement);

                TxtGuardMatchGroup.Text = conn.GuardMatchGroup ?? "";
                ChkGuardEscape.IsChecked = conn.GuardEscape ?? false;
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
            var extras = TierExtras[(int)tier];
            int extraLen = extras.Length;
            int presetOff = extraLen > 0 ? extraLen + 1 : 0;

            foreach (var (label, value) in extras)
                CmbGuardValue.Items.Add($"{label}  ({value:N0})");
            if (extraLen > 0)
                CmbGuardValue.Items.Add(new Separator());

            for (int i = 0; i < StrengthLabels.Length; i++)
                CmbGuardValue.Items.Add($"{StrengthLabels[i]}  ({GuardPresets[(int)tier, i]:N0})");
            if (ChkPropAdvanced.IsChecked == true)
                CmbGuardValue.Items.Add("Custom...");

            bool matched = false;
            if (currentValue.HasValue)
            {
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
                CmbGuardValue.SelectedIndex = presetOff + 2;
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
                CmbGuardWeeklyIncrement.SelectedIndex = 2;
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

        private void ChkPropAdvanced_Changed(object sender, RoutedEventArgs e)
        {
            bool advanced = ChkPropAdvanced.IsChecked == true;
            PnlPropsAdvanced.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;

            if (_selectedZoneName is not null)
            {
                PnlZoneAdvanced.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
                PnlZoneAdvancedContentValues.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
                PnlZoneContentMultipliers.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
                return;
            }

            if (_selectedConnection is null) return;
            ZoneTier tier = HigherTierOf(_selectedConnection.From, _selectedConnection.To);

            bool gvHasCustom = CmbGuardValue.Items.Contains("Custom...");
            if (advanced && !gvHasCustom)
            {
                CmbGuardValue.Items.Add("Custom...");
            }
            else if (!advanced && gvHasCustom)
            {
                if (CmbGuardValue.SelectedItem as string == "Custom...")
                {
                    CmbGuardValue.SelectedIndex = 0;
                    _selectedConnection.GuardValue = GuardPresets[(int)tier, 0];
                }
                CmbGuardValue.Items.Remove("Custom...");
                TxtPropGuardValueCustom.Text = "";
                TxtPropGuardValueCustom.Visibility = Visibility.Collapsed;
            }

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
                TxtPropIncrementCustom.Text = "";
                TxtPropIncrementCustom.Visibility = Visibility.Collapsed;
            }
        }

        private void CmbConnectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.ConnectionType = CmbConnectionType.SelectedItem as string;
            MarkConnectionsModified();
        }

        private void CmbGuardValue_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            if (CmbGuardValue.SelectedItem is Separator) return;
            bool isCustom = CmbGuardValue.SelectedItem as string == "Custom...";
            TxtPropGuardValueCustom.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (isCustom) return;

            ZoneTier tier = HigherTierOf(_selectedConnection.From, _selectedConnection.To);
            var extras = TierExtras[(int)tier];
            int extraLen = extras.Length;
            int presetOff = extraLen > 0 ? extraLen + 1 : 0;
            int idx = CmbGuardValue.SelectedIndex;

            if (idx < extraLen)
            {
                _selectedConnection.GuardValue = extras[idx].Value;
                MarkConnectionsModified();
                Refresh();
            }
            else if (idx >= presetOff)
            {
                int presetIdx = idx - presetOff;
                if (presetIdx < StrengthLabels.Length)
                {
                    _selectedConnection.GuardValue = GuardPresets[(int)tier, presetIdx];
                    MarkConnectionsModified();
                    Refresh();
                }
            }
        }

        private void TxtPropGuardValueCustom_Commit(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            if (int.TryParse(TxtPropGuardValueCustom.Text.Trim(), out int v) && v != _selectedConnection.GuardValue)
            {
                _selectedConnection.GuardValue = v;
                MarkConnectionsModified();
                Refresh();
            }
        }

        private void TxtPropGuardValueCustom_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtPropGuardValueCustom_Commit(sender, e);
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
                    MarkConnectionsModified();
                }
            }
        }

        private void TxtPropIncrementCustom_Commit(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            if (double.TryParse(TxtPropIncrementCustom.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                && Math.Abs(v - (_selectedConnection.GuardWeeklyIncrement ?? 0)) > 1e-9)
            {
                _selectedConnection.GuardWeeklyIncrement = v;
                MarkConnectionsModified();
            }
        }

        private void TxtPropIncrementCustom_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtPropIncrementCustom_Commit(sender, e);
        }

        private void CmbGuardZone_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string? val = CmbGuardZone.SelectedItem as string;
            _selectedConnection.GuardZone = string.IsNullOrEmpty(val) ? null : val;
            MarkConnectionsModified();
        }

        private void TxtGuardMatchGroup_Commit(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            string val = TxtGuardMatchGroup.Text.Trim();
            string? newValue = val.Length > 0 ? val : null;
            if (!string.Equals(_selectedConnection.GuardMatchGroup, newValue, StringComparison.Ordinal))
            {
                _selectedConnection.GuardMatchGroup = newValue;
                MarkConnectionsModified();
            }
        }

        private void TxtGuardMatchGroup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtGuardMatchGroup_Commit(sender, e);
        }

        private void ChkGuardEscape_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.GuardEscape = ChkGuardEscape.IsChecked;
            MarkConnectionsModified();
        }

        private void ChkSimTurnSquad_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPropertyEvents || _selectedConnection is null) return;
            _selectedConnection.SimTurnSquad = ChkSimTurnSquad.IsChecked;
            MarkConnectionsModified();
        }

        private void TxtZoneGuardCutoff_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneInt(TxtZoneGuardCutoff, value => Math.Max(0, value), (zone, value) => zone.GuardCutoffValue = value, z => z.GuardCutoffValue);

        private void TxtZoneGuardCutoff_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneGuardCutoff_Commit(sender, e);
        }

        private void TxtZoneGuardedContentValue_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneNullableInt(TxtZoneGuardedContentValue, value => Math.Max(0, value), (zone, value) => zone.GuardedContentValue = value, z => z.GuardedContentValue);

        private void TxtZoneGuardedContentValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneGuardedContentValue_Commit(sender, e);
        }

        private void TxtZoneUnguardedContentValue_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneNullableInt(TxtZoneUnguardedContentValue, value => Math.Max(0, value), (zone, value) => zone.UnguardedContentValue = value, z => z.UnguardedContentValue);

        private void TxtZoneUnguardedContentValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneUnguardedContentValue_Commit(sender, e);
        }

        private void TxtZoneResourcesValue_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneNullableInt(TxtZoneResourcesValue, value => Math.Max(0, value), (zone, value) => zone.ResourcesValue = value, z => z.ResourcesValue);

        private void TxtZoneResourcesValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneResourcesValue_Commit(sender, e);
        }

        private void TxtZoneGuardedContentValuePerArea_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneNullableInt(TxtZoneGuardedContentValuePerArea, value => Math.Max(0, value), (zone, value) => zone.GuardedContentValuePerArea = value, z => z.GuardedContentValuePerArea);

        private void TxtZoneGuardedContentValuePerArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneGuardedContentValuePerArea_Commit(sender, e);
        }

        private void TxtZoneUnguardedContentValuePerArea_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneNullableInt(TxtZoneUnguardedContentValuePerArea, value => Math.Max(0, value), (zone, value) => zone.UnguardedContentValuePerArea = value, z => z.UnguardedContentValuePerArea);

        private void TxtZoneUnguardedContentValuePerArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneUnguardedContentValuePerArea_Commit(sender, e);
        }

        private void TxtZoneResourcesValuePerArea_Commit(object sender, RoutedEventArgs e)
            => CommitSelectedZoneNullableInt(TxtZoneResourcesValuePerArea, value => Math.Max(0, value), (zone, value) => zone.ResourcesValuePerArea = value, z => z.ResourcesValuePerArea);

        private void TxtZoneResourcesValuePerArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TxtZoneResourcesValuePerArea_Commit(sender, e);
        }

        private void SldZoneSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateZoneSliderLabels();
            if (_suppressPropertyEvents) return;
            if (_selectedZoneName is null) return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null) return;

            double value = Math.Round(Math.Clamp(SldZoneSize.Value / 100.0, 0.25, 2.0), 2, MidpointRounding.AwayFromZero);
            if (AreClose(zone.Size, value)) return;
            zone.Size = value;
            UpdateZoneOverride(zone);
        }

        private void SldZoneGuardRandomization_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateZoneSliderLabels();
            if (_suppressPropertyEvents) return;
            if (_selectedZoneName is null) return;
            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null) return;

            double value = Math.Round(Math.Clamp(SldZoneGuardRandomization.Value / 100.0, 0.0, 0.5), 3, MidpointRounding.AwayFromZero);
            if (AreClose(zone.GuardRandomization, value)) return;
            zone.GuardRandomization = value;
            UpdateZoneOverride(zone);
        }

        private void SldZoneGuardMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateZoneSliderLabels();
            if (_suppressPropertyEvents) return;
            if (_selectedZoneName is null) return;
            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null) return;

            double value = Math.Round(Math.Clamp(SldZoneGuardMultiplier.Value / 100.0, 0.25, 3.0), 3, MidpointRounding.AwayFromZero);
            if (AreClose(zone.GuardMultiplier, value)) return;
            zone.GuardMultiplier = value;
            UpdateZoneOverride(zone);
        }

        private void SldZoneGuardWeeklyIncrement_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressPropertyEvents)
            {
                UpdateZoneSliderLabels();
                return;
            }

            double snapped = SnapWeeklyPercent(SldZoneGuardWeeklyIncrement.Value);
            if (Math.Abs(SldZoneGuardWeeklyIncrement.Value - snapped) > 0.001)
            {
                _suppressPropertyEvents = true;
                SldZoneGuardWeeklyIncrement.Value = snapped;
                _suppressPropertyEvents = false;
            }

            UpdateZoneSliderLabels();

            if (_selectedZoneName is null) return;
            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null) return;

            double value = Math.Round(snapped / 100.0, 3, MidpointRounding.AwayFromZero);
            if (AreClose(zone.GuardWeeklyIncrement, value)) return;
            zone.GuardWeeklyIncrement = value;
            UpdateZoneOverride(zone);
        }

        private void SldZoneGuardedContentValueMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateZoneSliderLabels();
            if (_suppressPropertyEvents || _selectedZoneName is null)
                return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            ZoneSnapshot original = _originalZonesByName.TryGetValue(zone.Name, out ZoneSnapshot? snapshot)
                ? snapshot
                : CreateSnapshot(zone);

            int value = ScaleContentValueFromMultiplier(original.GuardedContentValue, SldZoneGuardedContentValueMultiplier.Value);
            if (zone.GuardedContentValue == value)
                return;

            zone.GuardedContentValue = value;
            TxtZoneGuardedContentValue.Text = value.ToString(CultureInfo.InvariantCulture);
            UpdateZoneOverride(zone);
        }

        private void SldZoneUnguardedContentValueMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateZoneSliderLabels();
            if (_suppressPropertyEvents || _selectedZoneName is null)
                return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            ZoneSnapshot original = _originalZonesByName.TryGetValue(zone.Name, out ZoneSnapshot? snapshot)
                ? snapshot
                : CreateSnapshot(zone);

            int value = ScaleContentValueFromMultiplier(original.UnguardedContentValue, SldZoneUnguardedContentValueMultiplier.Value);
            if (zone.UnguardedContentValue == value)
                return;

            zone.UnguardedContentValue = value;
            TxtZoneUnguardedContentValue.Text = value.ToString(CultureInfo.InvariantCulture);
            UpdateZoneOverride(zone);
        }

        private void SldZoneResourcesValueMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateZoneSliderLabels();
            if (_suppressPropertyEvents || _selectedZoneName is null)
                return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            ZoneSnapshot original = _originalZonesByName.TryGetValue(zone.Name, out ZoneSnapshot? snapshot)
                ? snapshot
                : CreateSnapshot(zone);

            int value = ScaleContentValueFromMultiplier(original.ResourcesValue, SldZoneResourcesValueMultiplier.Value);
            if (zone.ResourcesValue == value)
                return;

            zone.ResourcesValue = value;
            TxtZoneResourcesValue.Text = value.ToString(CultureInfo.InvariantCulture);
            UpdateZoneOverride(zone);
        }

        private void UpdateZoneSliderLabels()
        {
            if (TxtZoneSizeValue is null
                || TxtZoneGuardRandomizationValue is null
                || TxtZoneGuardMultiplierValue is null
                || TxtZoneGuardWeeklyIncrementValue is null
                || TxtZoneGuardedContentValueMultiplierValue is null
                || TxtZoneUnguardedContentValueMultiplierValue is null
                || TxtZoneResourcesValueMultiplierValue is null
                || TxtZoneGuardedContentBaseValue is null
                || TxtZoneUnguardedContentBaseValue is null
                || TxtZoneResourcesBaseValue is null)
            {
                return;
            }

            TxtZoneSizeValue.Text = $"{Math.Round(SldZoneSize.Value, 0, MidpointRounding.AwayFromZero)}%";
            TxtZoneGuardRandomizationValue.Text = $"{Math.Round(SldZoneGuardRandomization.Value, 0, MidpointRounding.AwayFromZero)}%";
            TxtZoneGuardMultiplierValue.Text = $"{Math.Round(SldZoneGuardMultiplier.Value, 0, MidpointRounding.AwayFromZero)}%";
            TxtZoneGuardWeeklyIncrementValue.Text = $"{Math.Round(SldZoneGuardWeeklyIncrement.Value, 0, MidpointRounding.AwayFromZero)}%";
            TxtZoneGuardedContentValueMultiplierValue.Text = $"{Math.Round(SldZoneGuardedContentValueMultiplier.Value, 0, MidpointRounding.AwayFromZero)}%";
            TxtZoneUnguardedContentValueMultiplierValue.Text = $"{Math.Round(SldZoneUnguardedContentValueMultiplier.Value, 0, MidpointRounding.AwayFromZero)}%";
            TxtZoneResourcesValueMultiplierValue.Text = $"{Math.Round(SldZoneResourcesValueMultiplier.Value, 0, MidpointRounding.AwayFromZero)}%";

            Zone? zone = _selectedZoneName is null
                ? null
                : _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));

            TxtZoneGuardedContentBaseValue.Text = $"Current value: {FormatZoneInt(zone?.GuardedContentValue)}";
            TxtZoneUnguardedContentBaseValue.Text = $"Current value: {FormatZoneInt(zone?.UnguardedContentValue)}";
            TxtZoneResourcesBaseValue.Text = $"Current value: {FormatZoneInt(zone?.ResourcesValue)}";
        }

        private static double SnapWeeklyPercent(double value)
        {
            double[] allowed = [5.0, 10.0, 15.0, 20.0, 25.0, 30.0];
            return allowed.OrderBy(candidate => Math.Abs(candidate - value)).First();
        }

        private static string FormatZoneInt(int? value)
            => value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "(none)";

        private static double ComputeContentMultiplierPercent(int? originalValue, int? currentValue)
        {
            int baseline = Math.Max(0, originalValue ?? 0);
            if (baseline == 0)
                return 100.0;

            int current = Math.Max(0, currentValue ?? 0);
            double raw = current * 100.0 / baseline;
            return SnapStepFivePercent(Math.Clamp(raw, 25.0, 300.0));
        }

        private static int ScaleContentValueFromMultiplier(int? originalValue, double multiplierPercent)
        {
            int baseline = Math.Max(0, originalValue ?? 0);
            if (baseline == 0)
                return 0;

            double scaled = baseline * (multiplierPercent / 100.0);
            return (int)Math.Clamp(Math.Round(scaled, 0, MidpointRounding.AwayFromZero), 0, int.MaxValue);
        }

        private static double SnapStepFivePercent(double value)
            => Math.Round(value / 5.0, MidpointRounding.AwayFromZero) * 5.0;

        private void CommitSelectedZoneInt(TextBox source, Func<int, int> normalize, Action<Zone, int> applyValue, Func<Zone, int?> readValue)
        {
            if (_suppressPropertyEvents || _selectedZoneName is null)
                return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            if (!int.TryParse(source.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                PopulateZonePropertyPanel(zone);
                return;
            }

            int normalized = normalize(parsed);
            if (readValue(zone) == normalized)
            {
                source.Text = normalized.ToString(CultureInfo.InvariantCulture);
                return;
            }

            applyValue(zone, normalized);
            source.Text = normalized.ToString(CultureInfo.InvariantCulture);
            UpdateZoneOverride(zone);
        }

        private void CommitSelectedZoneNullableInt(TextBox source, Func<int, int> normalize, Action<Zone, int?> applyValue, Func<Zone, int?> readValue)
        {
            if (_suppressPropertyEvents || _selectedZoneName is null)
                return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            string text = source.Text.Trim();
            if (text.Length == 0)
            {
                if (readValue(zone).HasValue)
                {
                    applyValue(zone, null);
                    PopulateZonePropertyPanel(zone);
                    UpdateZoneOverride(zone);
                }
                return;
            }

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                PopulateZonePropertyPanel(zone);
                return;
            }

            int normalized = normalize(parsed);
            if (readValue(zone) == normalized)
            {
                source.Text = normalized.ToString(CultureInfo.InvariantCulture);
                return;
            }

            applyValue(zone, normalized);
            source.Text = normalized.ToString(CultureInfo.InvariantCulture);
            PopulateZonePropertyPanel(zone);
            UpdateZoneOverride(zone);
        }

        private void CommitSelectedZoneDouble(TextBox source, Func<double, double> normalize, Action<Zone, double> applyValue, Func<Zone, double?> readValue)
        {
            if (_suppressPropertyEvents || _selectedZoneName is null)
                return;

            Zone? zone = _zones.FirstOrDefault(z => string.Equals(z.Name, _selectedZoneName, StringComparison.Ordinal));
            if (zone is null)
                return;

            if (!double.TryParse(source.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed))
            {
                PopulateZonePropertyPanel(zone);
                return;
            }

            double normalized = normalize(parsed);
            if (AreClose(readValue(zone), normalized))
            {
                source.Text = normalized.ToString("0.###", CultureInfo.InvariantCulture);
                return;
            }

            applyValue(zone, normalized);
            source.Text = normalized.ToString("0.###", CultureInfo.InvariantCulture);
            UpdateZoneOverride(zone);
        }

        private void BtnDeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedConnection is null) return;
            DeleteSelectedConnection();
        }

        private void DeleteSelectedConnection()
        {
            if (_selectedConnection is null) return;
            _connections.Remove(_selectedConnection);
            _selectedConnection = null;
            _selectedVisibleLine = null;
            PnlProperties.Visibility = Visibility.Collapsed;
            MarkConnectionsModified();
            Refresh();
        }

        private void CancelDragAdd()
        {
            _pendingFromZone = null;
            _isPotentialDrag = false;
            _isDragging = false;
            if (_rubberBandLine is not null)
            {
                ZoneCanvas.Children.Remove(_rubberBandLine);
                _rubberBandLine = null;
            }
            ZoneCanvas.ReleaseMouseCapture();
            ZoneCanvas.Cursor = Cursors.Arrow;
        }

        private void ZoneNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e, string zoneName)
        {
            e.Handled = true;

            _pendingFromZone = zoneName;
            _dragStartPoint = e.GetPosition(ZoneCanvas);
            _isPotentialDrag = true;
            _isDragging = false;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(ZoneCanvas);

            if (_pendingFromZone is null)
                return;

            if (_isPotentialDrag && !_isDragging)
            {
                if (e.LeftButton != MouseButtonState.Pressed)
                {
                    CancelDragAdd();
                    return;
                }

                double dx = pos.X - _dragStartPoint.X;
                double dy = pos.Y - _dragStartPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) >= DragStartThreshold)
                {
                    _isDragging = true;
                    _isPotentialDrag = false;
                    ZoneCanvas.Cursor = Cursors.Cross;

                    var fromPos = _nodePositions.GetValueOrDefault(_pendingFromZone);
                    _rubberBandLine = new Line
                    {
                        X1 = fromPos.X,
                        Y1 = fromPos.Y,
                        X2 = fromPos.X,
                        Y2 = fromPos.Y,
                        Stroke = BrushEdgeSelected,
                        StrokeThickness = 1.5,
                        StrokeDashArray = [4.0, 3.0],
                        IsHitTestVisible = false
                    };
                    ZoneCanvas.Children.Add(_rubberBandLine);
                    ZoneCanvas.CaptureMouse();
                }
            }

            if (_isDragging && _rubberBandLine is not null)
            {
                _rubberBandLine.X2 = pos.X;
                _rubberBandLine.Y2 = pos.Y;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_pendingFromZone is null)
                return;

            if (_isPotentialDrag && !_isDragging)
            {
                string zoneName = _pendingFromZone;
                CancelDragAdd();
                SelectZone(zoneName);
                return;
            }

            if (!_isDragging)
                return;

            var pos = e.GetPosition(ZoneCanvas);
            string? targetZone = HitTestZone(pos);

            if (targetZone is not null && !string.Equals(targetZone, _pendingFromZone, StringComparison.Ordinal))
            {
                AddConnectionWithDefaults(_pendingFromZone!, targetZone);
            }
            else
            {
                CancelDragAdd();
                Refresh();
            }
        }

        private double ComputeObstacleAvoidanceBulge(Point p0, Point p1, string fromName, string toName)
        {
            const double Clearance = NodeRadius + 8.0;

            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            double lenSq = dx * dx + dy * dy;
            double len = Math.Sqrt(lenSq);
            if (len < 1) return 0;

            double maxPos = 0;
            double maxNeg = 0;

            foreach (var (name, pos) in _nodePositions)
            {
                if (string.Equals(name, fromName, StringComparison.Ordinal)) continue;
                if (string.Equals(name, toName, StringComparison.Ordinal)) continue;

                double qx = pos.X - p0.X;
                double qy = pos.Y - p0.Y;

                double t = (qx * dx + qy * dy) / lenSq;
                if (t < 0.05 || t > 0.95) continue;

                double d = (dx * qy - dy * qx) / len;

                if (Math.Abs(d) >= Clearance) continue;

                double factor = 4.0 * t * (1.0 - t);
                if (factor < 1e-6) continue;

                double bPos = (d + Clearance) / factor;
                double bNeg = (d - Clearance) / factor;

                if (bPos > 0) maxPos = Math.Max(maxPos, bPos);
                if (bNeg < 0) maxNeg = Math.Min(maxNeg, bNeg);
            }

            if (maxPos == 0 && maxNeg == 0) return 0;
            if (maxPos == 0) return maxNeg;
            if (maxNeg == 0) return maxPos;
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
                Name = uniqueName,
                From = from,
                To = to,
                ConnectionType = "Direct",
                GuardValue = TierExtras[(int)tier][0].Value,
                GuardZone = from,
                GuardMatchGroup = $"rnd_guard_{ZoneLetterFromName(from)}_{ZoneLetterFromName(to)}",
                GuardWeeklyIncrement = WeeklyIncrementValues[2],
                IsUserAdded = true
            };
            _connections.Add(newConn);
            MarkConnectionsModified();

            _selectedConnection = newConn;
            _selectedZoneName = null;
            CancelDragAdd();

            ShowConnectionPropertyPanel();
            PopulatePropertyPanel(newConn);
            Refresh();
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

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging) return;

            var pos = e.GetPosition(ZoneCanvas);
            var hit = FindNearestConnection(pos, threshold: 14.0);
            if (hit is not null)
            {
                e.Handled = true;
                CancelDragAdd();
                SelectEdge(hit.Value.conn, hit.Value.visiblePath);
                Refresh();
            }
            else
            {
                CancelDragAdd();
                if (_selectedConnection is not null || _selectedZoneName is not null)
                {
                    _selectedConnection = null;
                    _selectedVisibleLine = null;
                    _selectedZoneName = null;
                    PnlProperties.Visibility = Visibility.Collapsed;
                    Refresh();
                }
            }
        }

        private (Connection conn, Path visiblePath)? FindNearestConnection(Point pos, double threshold)
        {
            double minDist = threshold;
            Connection? nearest = null;
            Path? nearestPath = null;

            foreach (var (conn, geo, visiblePath) in _connectionGeometries)
            {
                double dist = DistanceToQuadraticBezier(pos, geo);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = conn;
                    nearestPath = visiblePath;
                }
            }

            return nearest is not null ? (nearest, nearestPath!) : null;
        }

        private static double DistanceToQuadraticBezier(Point p, PathGeometry geo)
        {
            const int Samples = 48;
            double minDist = double.MaxValue;

            foreach (var figure in geo.Figures)
            {
                var start = figure.StartPoint;
                foreach (var segment in figure.Segments)
                {
                    if (segment is not QuadraticBezierSegment qbs) continue;

                    var p0 = start;
                    var p1 = qbs.Point1;
                    var p2 = qbs.Point2;

                    for (int i = 0; i <= Samples; i++)
                    {
                        double t  = i / (double)Samples;
                        double mt = 1.0 - t;
                        double x  = mt * mt * p0.X + 2 * mt * t * p1.X + t * t * p2.X;
                        double y  = mt * mt * p0.Y + 2 * mt * t * p1.Y + t * t * p2.Y;
                        double dx = p.X - x;
                        double dy = p.Y - y;
                        double d  = Math.Sqrt(dx * dx + dy * dy);
                        if (d < minDist) minDist = d;
                    }

                    start = p2;
                }
            }

            return minDist;
        }

        private static ZoneSnapshot CreateSnapshot(Zone zone)
            => new(
                zone.Size,
                zone.Layout,
                zone.GuardCutoffValue,
                zone.GuardRandomization,
                zone.GuardMultiplier,
                zone.GuardWeeklyIncrement,
                zone.GuardedContentValue,
                zone.GuardedContentValuePerArea,
                zone.UnguardedContentValue,
                zone.UnguardedContentValuePerArea,
                zone.ResourcesValue,
                zone.ResourcesValuePerArea);

        private void UpdateZoneOverride(Zone zone)
        {
            if (!_originalZonesByName.TryGetValue(zone.Name, out ZoneSnapshot? original))
                return;

            ZoneOverrideSettings? computed = BuildZoneOverride(zone, original);
            if (computed is null)
                _zoneOverridesByName.Remove(zone.Name);
            else
                _zoneOverridesByName[zone.Name] = computed;

            MarkZoneOverridesModified();
            Refresh();
        }

        private static ZoneOverrideSettings? BuildZoneOverride(Zone zone, ZoneSnapshot original)
        {
            var overrideSettings = new ZoneOverrideSettings
            {
                ZoneName = zone.Name
            };

            if (!AreClose(zone.Size, original.Size))
                overrideSettings.Size = zone.Size;

            if (!string.Equals(zone.Layout, original.Layout, StringComparison.Ordinal))
                overrideSettings.Layout = zone.Layout;

            if (zone.GuardCutoffValue != original.GuardCutoffValue)
                overrideSettings.GuardCutoffValue = zone.GuardCutoffValue;

            if (!AreClose(zone.GuardRandomization, original.GuardRandomization))
                overrideSettings.GuardRandomization = zone.GuardRandomization;

            if (!AreClose(zone.GuardMultiplier, original.GuardMultiplier))
                overrideSettings.GuardMultiplier = zone.GuardMultiplier;

            if (!AreClose(zone.GuardWeeklyIncrement, original.GuardWeeklyIncrement))
                overrideSettings.GuardWeeklyIncrement = zone.GuardWeeklyIncrement;

            if (zone.GuardedContentValue != original.GuardedContentValue)
                overrideSettings.GuardedContentValue = zone.GuardedContentValue;

            if (zone.GuardedContentValuePerArea != original.GuardedContentValuePerArea)
                overrideSettings.GuardedContentValuePerArea = zone.GuardedContentValuePerArea;

            if (zone.UnguardedContentValue != original.UnguardedContentValue)
                overrideSettings.UnguardedContentValue = zone.UnguardedContentValue;

            if (zone.UnguardedContentValuePerArea != original.UnguardedContentValuePerArea)
                overrideSettings.UnguardedContentValuePerArea = zone.UnguardedContentValuePerArea;

            if (zone.ResourcesValue != original.ResourcesValue)
                overrideSettings.ResourcesValue = zone.ResourcesValue;

            if (zone.ResourcesValuePerArea != original.ResourcesValuePerArea)
                overrideSettings.ResourcesValuePerArea = zone.ResourcesValuePerArea;

            return HasChangedFields(overrideSettings) ? overrideSettings : null;
        }

        private static bool HasChangedFields(ZoneOverrideSettings value)
            => value.Size.HasValue
               || value.Layout != null
               || value.GuardCutoffValue.HasValue
               || value.GuardRandomization.HasValue
               || value.GuardMultiplier.HasValue
               || value.GuardWeeklyIncrement.HasValue
               || value.GuardReactionDistribution is { Count: > 0 }
               || value.DiplomacyModifier.HasValue
               || value.GuardedContentPool is { Count: > 0 }
               || value.UnguardedContentPool is { Count: > 0 }
               || value.ResourcesContentPool is { Count: > 0 }
               || value.MandatoryContent is { Count: > 0 }
               || value.ContentCountLimits is { Count: > 0 }
               || value.GuardedContentValue.HasValue
               || value.GuardedContentValuePerArea.HasValue
               || value.UnguardedContentValue.HasValue
               || value.UnguardedContentValuePerArea.HasValue
               || value.ResourcesValue.HasValue
               || value.ResourcesValuePerArea.HasValue
               || value.MainObjects is { Count: > 0 };

        private static ZoneOverrideSettings CloneZoneOverride(ZoneOverrideSettings value)
            => new()
            {
                ZoneName = value.ZoneName,
                Size = value.Size,
                Layout = value.Layout,
                GuardCutoffValue = value.GuardCutoffValue,
                GuardRandomization = value.GuardRandomization,
                GuardMultiplier = value.GuardMultiplier,
                GuardWeeklyIncrement = value.GuardWeeklyIncrement,
                GuardReactionDistribution = value.GuardReactionDistribution == null ? null : [.. value.GuardReactionDistribution],
                DiplomacyModifier = value.DiplomacyModifier,
                GuardedContentPool = value.GuardedContentPool == null ? null : [.. value.GuardedContentPool],
                UnguardedContentPool = value.UnguardedContentPool == null ? null : [.. value.UnguardedContentPool],
                ResourcesContentPool = value.ResourcesContentPool == null ? null : [.. value.ResourcesContentPool],
                MandatoryContent = value.MandatoryContent == null ? null : [.. value.MandatoryContent],
                ContentCountLimits = value.ContentCountLimits == null ? null : [.. value.ContentCountLimits],
                GuardedContentValue = value.GuardedContentValue,
                GuardedContentValuePerArea = value.GuardedContentValuePerArea,
                UnguardedContentValue = value.UnguardedContentValue,
                UnguardedContentValuePerArea = value.UnguardedContentValuePerArea,
                ResourcesValue = value.ResourcesValue,
                ResourcesValuePerArea = value.ResourcesValuePerArea,
                MainObjects = value.MainObjects == null ? null : value.MainObjects.Select(CloneMainObjectOverride).ToList(),
            };

        private static ZoneMainObjectOverride CloneMainObjectOverride(ZoneMainObjectOverride value)
            => new()
            {
                Type = value.Type,
                Spawn = value.Spawn,
                Owner = value.Owner,
                GuardChance = value.GuardChance,
                GuardValue = value.GuardValue,
                GuardWeeklyIncrement = value.GuardWeeklyIncrement,
                RemoveGuardIfHasOwner = value.RemoveGuardIfHasOwner,
                BuildingsConstructionSid = value.BuildingsConstructionSid,
                Faction = value.Faction,
                Placement = value.Placement,
                PlacementArgs = value.PlacementArgs == null ? null : [.. value.PlacementArgs],
                HoldCityWinCon = value.HoldCityWinCon,
            };

        private static bool AreClose(double? left, double? right)
        {
            if (!left.HasValue && !right.HasValue)
                return true;
            if (!left.HasValue || !right.HasValue)
                return false;

            return Math.Abs(left.Value - right.Value) < 1e-9;
        }

        private static string ZoneLetterFromName(string zoneName)
        {
            int dash = zoneName.IndexOf('-');
            return dash >= 0 ? zoneName[(dash + 1)..] : zoneName;
        }

        public static Connection CloneConnection(Connection c, bool isUserAdded = false) => new()
        {
            Name = c.Name,
            From = c.From,
            To = c.To,
            ConnectionType = c.ConnectionType,
            GuardZone = c.GuardZone,
            GuardEscape = c.GuardEscape,
            SimTurnSquad = c.SimTurnSquad,
            GuardValue = c.GuardValue,
            GuardWeeklyIncrement = c.GuardWeeklyIncrement,
            GuardMatchGroup = c.GuardMatchGroup,
            PortalPlacementRulesFrom = c.PortalPlacementRulesFrom,
            PortalPlacementRulesTo = c.PortalPlacementRulesTo,
            Road = c.Road,
            GatePlacement = c.GatePlacement,
            Length = c.Length,
            IsUserAdded = isUserAdded
        };
    }
}
