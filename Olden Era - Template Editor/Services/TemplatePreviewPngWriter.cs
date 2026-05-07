using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Olden_Era___Template_Editor.Services
{
    public static class TemplatePreviewPngWriter
    {
        // Canvas size matches game's required preview resolution
        private const int Width  = 700;
        private const int Height = 700;

        // Neutral zone layout names → tier
        private const string SideLayoutName     = "zone_layout_sides";         // Bronze
        private const string TreasureLayoutName = "zone_layout_treasure_zone"; // Silver
        private const string CenterLayoutName   = "zone_layout_center";        // Gold

        private static readonly Color BackgroundColor = Color.FromRgb(28, 22, 16);

        // ── Neutral tier colours ─────────────────────────────────────────────────
        // Bronze
        private static readonly Color BronzeFill    = Color.FromRgb(101,  67,  33);
        private static readonly Color BronzeBorder  = Color.FromRgb(205, 127,  50);
        // Silver
        private static readonly Color SilverFill    = Color.FromRgb( 72,  76,  80);
        private static readonly Color SilverBorder  = Color.FromRgb(192, 192, 192);
        // Gold
        private static readonly Color GoldFill      = Color.FromRgb(120,  90,  20);
        private static readonly Color GoldBorder    = Color.FromRgb(255, 210,  50);

        // ── Spawn / player zone colours ──────────────────────────────────────────
        private static readonly Color SpawnFill    = Color.FromRgb( 42,  90,  50);
        private static readonly Color SpawnBorder  = Color.FromRgb(100, 200, 120);

        // ── Hub colour ───────────────────────────────────────────────────────────
        private static readonly Color HubFill   = Color.FromRgb(55, 80, 95);
        private static readonly Color HubBorder = Color.FromRgb(130, 180, 200);

        // ── Connection colours ───────────────────────────────────────────────────
        // Direct / Default / GladiatorArena → thick gold line
        private static readonly Color DirectLineColor = Color.FromRgb(180, 145, 60);
        // Portal → semi-transparent blue
        private static readonly Color PortalLineColor = Color.FromArgb(180, 90, 170, 210);

        // ── Radius ───────────────────────────────────────────────────────────────
        // Cap for spoke/neutral/spawn zones; actual radius is computed per-layout.
        private const double ZoneRadiusMax = 38;
        // Hub zones always render at least this large so the "Hub" label is never clipped.
        private const double HubRadiusMin  = 28;

        // ── Human icon (person silhouette drawn with geometry) ───────────────────
        // Drawn relative to the zone centre; scaled to fit the circle.

        public static string GetSidecarPath(string rmgJsonPath) =>
            rmgJsonPath.EndsWith(".rmg.json", StringComparison.OrdinalIgnoreCase)
                ? rmgJsonPath[..^".rmg.json".Length] + ".png"
                : Path.ChangeExtension(rmgJsonPath, ".png");

        public static void Save(RmgTemplate template, string previewPath, MapTopology topology = MapTopology.Default)
        {
            string? directory = Path.GetDirectoryName(previewPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var bitmap = Render(template, topology);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string tempPath = $"{previewPath}.{Guid.NewGuid():N}.tmp";
            using (var stream = File.Create(tempPath))
                encoder.Save(stream);

            File.Move(tempPath, previewPath, overwrite: true);
        }

        /// <summary>Renders the preview to a <see cref="BitmapSource"/> without writing any files.</summary>
        public static BitmapSource Render(RmgTemplate template, MapTopology topology = MapTopology.Default)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                DrawPreview(dc, template, topology);

            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Returns the canvas positions that would be used for each zone when rendering
        /// <paramref name="template"/>, keyed by zone name.
        /// </summary>
        public static Dictionary<string, Point> ComputeLayout(RmgTemplate template, MapTopology topology = MapTopology.Default)
        {
            Variant? variant = template.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? [];
            if (zones.Count == 0) return [];
            var orderedZones = OrderZones(zones, variant?.Orientation?.ZeroAngleZone);
            return LayoutZones(orderedZones, variant?.Connections ?? [], topology);
        }

        /// <summary>
        /// Returns the zone-circle radius used when rendering <paramref name="template"/>.
        /// Must be called from the same thread context as <see cref="ComputeLayout"/> for the same template.
        /// </summary>
        public static double GetLastZoneRadius() => _zoneRadius;

        // ── Main draw ────────────────────────────────────────────────────────────

        private static void DrawPreview(DrawingContext dc, RmgTemplate template, MapTopology topology)
        {
            dc.DrawRectangle(new SolidColorBrush(BackgroundColor), null, new Rect(0, 0, Width, Height));
            dc.DrawRoundedRectangle(null,
                new Pen(new SolidColorBrush(Color.FromRgb(143, 115, 63)), 3),
                new Rect(8, 8, Width - 16, Height - 16), 8, 8);

            Variant? variant = template.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? [];
            if (zones.Count == 0)
            {
                DrawText(dc, template.Name, new Point(Width / 2.0, Height / 2.0), 24, Brushes.White, centered: true);
                return;
            }

            var orderedZones = OrderZones(zones, variant?.Orientation?.ZeroAngleZone);
            var connections  = variant?.Connections ?? [];
            var positions    = LayoutZones(orderedZones, connections, topology);

            // Draw connections first (below zones)
            DrawConnections(dc, connections, positions);

            // Draw zone circles — non-player zones first, then spawn zones on top
            // so the castle-count badge is never obscured by an adjacent circle.
            foreach (Zone zone in orderedZones.Where(z => !z.Name.StartsWith("Spawn-", StringComparison.Ordinal)))
                DrawZone(dc, zone, positions[zone.Name]);
            foreach (Zone zone in orderedZones.Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal)))
                DrawZone(dc, zone, positions[zone.Name]);
        }

        // ── Zone ordering / layout ───────────────────────────────────────────────

        private static List<Zone> OrderZones(List<Zone> zones, string? zeroAngleZone)
        {
            var ordered = zones.ToList();
            int zeroIndex = !string.IsNullOrWhiteSpace(zeroAngleZone)
                ? ordered.FindIndex(z => string.Equals(z.Name, zeroAngleZone, StringComparison.Ordinal))
                : -1;
            if (zeroIndex <= 0) return ordered;
            return ordered.Skip(zeroIndex).Concat(ordered.Take(zeroIndex)).ToList();
        }

        private static Dictionary<string, Point> LayoutZones(List<Zone> zones, List<Connection> connections, MapTopology topology)
        {
            // For structured topologies use the simple ring layout — it already
            // matches the actual in-game arrangement perfectly.
            // Only Random topology uses the spring-embedder to infer placement.
            if (topology != MapTopology.Random)
                return LayoutZonesRing(zones, connections);

            int n = zones.Count;
            if (n == 0)
            {
                _zoneRadius = ZoneRadiusMax;
                return [];
            }

            // ── Determine zone radius ─────────────────────────────────────────────
            // Use the circle-ring formula as an upper bound so zones never overlap.
            const double margin = 18;
            double ringRadius0  = Width / 2.0 - margin;
            double chord0       = 2.0 * ringRadius0 * Math.Sin(Math.PI / Math.Max(1, n));
            const double minGap = 6;
            double zoneRadius   = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
            _zoneRadius = zoneRadius;

            // ── Seed positions on a ring ──────────────────────────────────────────
            double ringRadius = Math.Min(ringRadius0, Width / 2.0 - zoneRadius - margin);
            var center = new Point(Width / 2.0, Height / 2.0);
            var px = new double[n];
            var py = new double[n];
            for (int i = 0; i < n; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI * 2 / n;
                px[i] = center.X + Math.Cos(angle) * ringRadius;
                py[i] = center.Y + Math.Sin(angle) * ringRadius;
            }

            if (n == 1)
            {
                px[0] = center.X;
                py[0] = center.Y;
                var single = new Dictionary<string, Point>(StringComparer.Ordinal);
                single[zones[0].Name] = new Point(px[0], py[0]);
                return single;
            }

            // ── Build adjacency (Direct connections only — no Proximity/Portal) ───
            var idx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++) idx[zones[i].Name] = i;

            var adj = new HashSet<int>[n];
            for (int i = 0; i < n; i++) adj[i] = [];
            foreach (var conn in connections)
            {
                if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                if (string.Equals(conn.ConnectionType, "Portal",    StringComparison.Ordinal)) continue;
                if (!idx.TryGetValue(conn.From, out int a)) continue;
                if (!idx.TryGetValue(conn.To,   out int b)) continue;
                adj[a].Add(b);
                adj[b].Add(a);
            }

            // ── Spring-embedder layout ────────────────────────────────────────────
            // All connected pairs share one fixed ideal edge length so every
            // connection in the graph is the same visual distance.
            // Unconnected pairs are only repelled — no attraction between them.
            double idealEdge   = Math.Max(zoneRadius * 4.0, ringRadius * 0.7);
            double repStrength = idealEdge * idealEdge;

            double temperature = ringRadius * 0.5;
            const int iterations = 400;
            const double cooling = 0.975;
            var dx = new double[n];
            var dy = new double[n];

            for (int iter = 0; iter < iterations; iter++)
            {
                Array.Clear(dx, 0, n);
                Array.Clear(dy, 0, n);

                // Repulsion between every pair of zones
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double diffX = px[i] - px[j];
                        double diffY = py[i] - py[j];
                        double dist  = Math.Sqrt(diffX * diffX + diffY * diffY);
                        if (dist < 0.001) { dist = 0.001; diffX = 1; diffY = 0; }

                        double rep = repStrength / dist;
                        double ux  = diffX / dist * rep;
                        double uy  = diffY / dist * rep;
                        dx[i] += ux; dy[i] += uy;
                        dx[j] -= ux; dy[j] -= uy;
                    }
                }

                // Attraction along edges — fixed ideal length, same for every edge
                for (int i = 0; i < n; i++)
                {
                    foreach (int j in adj[i])
                    {
                        if (j <= i) continue;
                        double diffX = px[i] - px[j];
                        double diffY = py[i] - py[j];
                        double dist  = Math.Sqrt(diffX * diffX + diffY * diffY);
                        if (dist < 0.001) continue;

                        double att = (dist - idealEdge);   // Hooke's law: pull when > ideal, push when < ideal
                        double ux  = diffX / dist * att;
                        double uy  = diffY / dist * att;
                        dx[i] -= ux; dy[i] -= uy;
                        dx[j] += ux; dy[j] += uy;
                    }
                }

                // Weak gravity toward group centroid — prevents disconnected zones drifting away
                double cx = px.Average(), cy = py.Average();
                for (int i = 0; i < n; i++)
                {
                    dx[i] -= (px[i] - cx) * 0.04;
                    dy[i] -= (py[i] - cy) * 0.04;
                }

                // Apply displacements, clamped to temperature
                for (int i = 0; i < n; i++)
                {
                    double disp = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
                    if (disp < 0.001) continue;
                    double clamp = Math.Min(disp, temperature);
                    px[i] += dx[i] / disp * clamp;
                    py[i] += dy[i] / disp * clamp;
                }

                temperature *= cooling;
            }

            // ── Hard-floor separation pass ────────────────────────────────────────
            // Guarantee that no two zone centres are closer than 4× zoneRadius
            // (= gap between circle edges ≥ 2 diameters) after simulation settles.
            double minDist = zoneRadius * 4.0;
            for (int pass = 0; pass < 50; pass++)
            {
                bool anyMoved = false;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double diffX = px[i] - px[j];
                        double diffY = py[i] - py[j];
                        double dist  = Math.Sqrt(diffX * diffX + diffY * diffY);
                        if (dist >= minDist) continue;
                        if (dist < 0.001) { diffX = 1; diffY = 0; dist = 0.001; }
                        double push = (minDist - dist) / 2.0;
                        double ux = diffX / dist * push;
                        double uy = diffY / dist * push;
                        px[i] += ux; py[i] += uy;
                        px[j] -= ux; py[j] -= uy;
                        anyMoved = true;
                    }
                }
                if (!anyMoved) break;
            }

            // ── Normalise: fit result into the canvas with padding ────────────────
            double minX = px.Min(), maxX = px.Max();
            double minY = py.Min(), maxY = py.Max();
            double spanX = Math.Max(maxX - minX, 1);
            double spanY = Math.Max(maxY - minY, 1);

            double pad    = zoneRadius + margin;
            double drawW  = Width  - 2 * pad;
            double drawH  = Height - 2 * pad;

            double scaleX = drawW / spanX;
            double scaleY = drawH / spanY;
            double scale  = Math.Min(scaleX, scaleY);

            double offX = pad + (drawW - spanX * scale) / 2.0;
            double offY = pad + (drawH - spanY * scale) / 2.0;

            var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++)
            {
                positions[zones[i].Name] = new Point(
                    offX + (px[i] - minX) * scale,
                    offY + (py[i] - minY) * scale);
            }
            return positions;
        }

        /// <summary>
        /// Classic ring layout used for all structured topologies (Default, HubAndSpoke, Chain, SharedWeb).
        /// Zones are arranged in a circle; a Hub zone (if present) goes in the centre.
        /// When multiple Hub-* zones exist (tournament hub layout) each hub is placed at
        /// the centre of its own cluster and its spokes arranged around it.
        /// </summary>
        private static Dictionary<string, Point> LayoutZonesRing(List<Zone> zones, List<Connection> connections)
        {
            var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
            int n = zones.Count;
            if (n == 0)
            {
                _zoneRadius = ZoneRadiusMax;
                return positions;
            }

            // ── Multi-hub (tournament) cluster layout ─────────────────────────────
            var multiHubs = zones
                .Where(z => z.Name.StartsWith("Hub-", StringComparison.Ordinal))
                .ToList();

            if (multiHubs.Count >= 2)
                return LayoutZonesMultiHub(zones, connections, multiHubs);

            // ── Standard single-ring layout ───────────────────────────────────────
            const double margin = 18;
            double ringRadius0  = Width / 2.0 - margin;
            double chord0       = 2.0 * ringRadius0 * Math.Sin(Math.PI / Math.Max(1, n));
            const double minGap = 6;
            double zoneRadius   = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
            _zoneRadius = zoneRadius;

            Zone? hub   = zones.FirstOrDefault(z => string.Equals(z.Name, "Hub", StringComparison.Ordinal));
            var outer   = hub is null ? zones : zones.Where(z => z != hub).ToList();
            int outerN  = Math.Max(1, outer.Count);

            double ringRadius = Math.Max(
                HubRadiusMin + zoneRadius + minGap,
                Math.Min(ringRadius0, Width / 2.0 - zoneRadius - margin));
            var center = new Point(Width / 2.0, Height / 2.0);

            if (hub is not null)
                positions[hub.Name] = center;

            if (n == 1)
            {
                positions[zones[0].Name] = center;
                return positions;
            }

            for (int i = 0; i < outer.Count; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI * 2 / outerN;
                positions[outer[i].Name] = new Point(
                    center.X + Math.Cos(angle) * ringRadius,
                    center.Y + Math.Sin(angle) * ringRadius);
            }

            return positions;
        }

        /// <summary>
        /// Multi-hub cluster layout for tournament hub-and-spoke templates.
        /// Each Hub-* zone is placed at the centre of its cluster; its direct
        /// neighbours (spokes) are arranged in a ring around it.  The clusters
        /// themselves are evenly distributed around the canvas centre.
        /// </summary>
        private static Dictionary<string, Point> LayoutZonesMultiHub(
            List<Zone> zones,
            List<Connection> connections,
            List<Zone> multiHubs)
        {
            var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
            const double margin = 18;
            const double minGap = 6;

            // Build spoke lists for each hub (Direct connections only)
            var hubSpokes = new Dictionary<string, List<Zone>>(StringComparer.Ordinal);
            var zoneByName = zones.ToDictionary(z => z.Name, StringComparer.Ordinal);

            foreach (var hub in multiHubs)
            {
                var spokes = connections
                    .Where(c => !string.Equals(c.ConnectionType, "Proximity", StringComparison.Ordinal)
                             && !string.Equals(c.ConnectionType, "Portal",    StringComparison.Ordinal))
                    .Select(c => string.Equals(c.From, hub.Name, StringComparison.Ordinal) ? c.To : (
                                 string.Equals(c.To,   hub.Name, StringComparison.Ordinal) ? c.From : null))
                    .Where(name => name != null && zoneByName.ContainsKey(name!))
                    .Select(name => zoneByName[name!])
                    .Distinct()
                    .ToList();
                hubSpokes[hub.Name] = spokes;
            }

            int maxSpokes = hubSpokes.Values.Max(s => s.Count);
            int numHubs   = multiHubs.Count;

            // ── Closed-form sizing — top-down from canvas ─────────────────────────
            //
            // Variables:
            //   hubR   — distance from canvas centre to each hub centre
            //   spokeR — distance from hub centre to spoke centres
            //   zoneR  — circle radius
            //
            // Three simultaneous constraints, all solved for the optimum:
            //
            // (a) Canvas fit:   hubR + spokeR + zoneR + margin = Width/2
            //                   → spokeR + zoneR = canvasHalf - hubR       [radialLeft]
            //
            // (b) Cluster separation (no two cluster envelopes touch):
            //     inter-hub distance ≥ 2*(spokeR + zoneR + minGap/2)
            //     inter-hub = 2*hubR*sin(π/numHubs)  (chord of the hub ring)
            //     → hubR * sinB ≥ radialLeft + minGap/2  (sinB = sin(π/numHubs))
            //     Substituting radialLeft = canvasHalf - hubR:
            //     hubR*(1 + sinB) = canvasHalf + minGap/2
            //     → hubR = (canvasHalf + minGap/2) / (1 + sinB)            [minimum]
            //
            // (c) Spoke chord (spokes don't overlap each other):
            //     2*spokeR*sin(π/maxSpokes) ≥ 2*zoneR + minGap
            //     → zoneR ≤ (radialLeft*sinA - minGap/2) / (1 + sinA)     [maximum]
            //        where sinA = sin(π/maxSpokes)

            double canvasHalf = Width / 2.0 - margin;
            double sinB = numHubs > 1 ? Math.Sin(Math.PI / numHubs) : 0.0;
            double sinA = maxSpokes > 1 ? Math.Sin(Math.PI / maxSpokes) : 1.0;

            double hubRingRadius = numHubs > 1
                ? (canvasHalf + minGap / 2.0) / (1.0 + sinB)
                : 0.0;

            double radialLeft  = canvasHalf - hubRingRadius;
            // spokeRingRadius must be large enough that spoke circles don't overlap the hub circle
            double minSpokeR   = HubRadiusMin + minGap;
            double zoneRadius  = Math.Min(ZoneRadiusMax, (radialLeft * sinA - minGap / 2.0) / (1.0 + sinA));
            zoneRadius = Math.Max(1.0, zoneRadius);
            double spokeRingRadius = Math.Max(radialLeft - zoneRadius, minSpokeR + zoneRadius);

            _zoneRadius = zoneRadius;

            var canvasCenter = new Point(Width / 2.0, Height / 2.0);

            // Place each hub and its spokes.
            for (int h = 0; h < numHubs; h++)
            {
                var hub = multiHubs[h];

                // Hubs arranged evenly on a ring; first hub points upward.
                double hubAngle = -Math.PI / 2.0 + h * Math.PI * 2.0 / numHubs;
                var hubPos = numHubs == 1
                    ? canvasCenter
                    : new Point(
                        canvasCenter.X + Math.Cos(hubAngle) * hubRingRadius,
                        canvasCenter.Y + Math.Sin(hubAngle) * hubRingRadius);

                positions[hub.Name] = hubPos;

                var spokes = hubSpokes[hub.Name];
                int s = spokes.Count;
                if (s == 0) continue;

                // Spread spokes in a full ring around the hub.
                // Rotate so the first spoke points outward (away from canvas centre).
                double spokeBaseAngle = numHubs == 1 ? -Math.PI / 2.0 : hubAngle;
                for (int i = 0; i < s; i++)
                {
                    double spokeAngle = spokeBaseAngle + i * Math.PI * 2.0 / s;
                    positions[spokes[i].Name] = new Point(
                        hubPos.X + Math.Cos(spokeAngle) * spokeRingRadius,
                        hubPos.Y + Math.Sin(spokeAngle) * spokeRingRadius);
                }
            }

            // Any zones not yet placed (e.g. cross-cluster connections) go on the canvas centre.
            foreach (var zone in zones)
            {
                if (!positions.ContainsKey(zone.Name))
                    positions[zone.Name] = canvasCenter;
            }

            return positions;
        }

        // Per-layout computed zone radius (set by LayoutZones, used by DrawZone)
        [ThreadStatic] private static double _zoneRadius;

        // ── Connections ──────────────────────────────────────────────────────────

        private static void DrawConnections(DrawingContext dc, List<Connection> connections, Dictionary<string, Point> positions)
        {
            foreach (Connection conn in connections)
            {
                if (!positions.TryGetValue(conn.From, out Point from)) continue;
                if (!positions.TryGetValue(conn.To,   out Point to))   continue;

                bool isPortal = string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal);

                // Skip Proximity lines entirely — only Direct/Default/Portal are meaningful visually
                if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;

                Pen pen = isPortal
                    ? new Pen(new SolidColorBrush(PortalLineColor), 2)
                    : new Pen(new SolidColorBrush(DirectLineColor), 3);

                dc.DrawLine(pen, from, to);
            }
        }

        // ── Zone drawing ─────────────────────────────────────────────────────────

        private static void DrawZone(DrawingContext dc, Zone zone, Point pt)
        {
            bool isSpawn    = zone.Name.StartsWith("Spawn-",   StringComparison.Ordinal);
            bool isHub      = string.Equals(zone.Name, "Hub",  StringComparison.Ordinal)
                           || zone.Name.StartsWith("Hub-",     StringComparison.Ordinal);
            bool isNeutral  = zone.Name.StartsWith("Neutral-", StringComparison.Ordinal);
            bool isHoldCity = IsHoldCityZone(zone);
            int  castles    = CastleCount(zone);

            Brush fillBrush;
            Pen   outlinePen;

            if (isNeutral)
            {
                (fillBrush, outlinePen) = NeutralTierStyle(zone);
            }
            else if (isHub)
            {
                fillBrush  = new SolidColorBrush(HubFill);
                outlinePen = new Pen(new SolidColorBrush(HubBorder), 2);
            }
            else // player spawn
            {
                fillBrush  = new SolidColorBrush(SpawnFill);
                outlinePen = new Pen(new SolidColorBrush(SpawnBorder), 2.5);
            }

            // Hold-city zones get a bright golden outline on top of the normal one
            if (isHoldCity)
                outlinePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 215, 0)), 3.5);

            double drawRadius = isHub ? Math.Max(_zoneRadius, HubRadiusMin) : _zoneRadius;
            dc.DrawEllipse(fillBrush, outlinePen, pt, drawRadius, drawRadius);

            if (isHoldCity)
            {
                DrawHoldCityIcon(dc, pt, drawRadius);
            }
            else if (isSpawn)
            {
                DrawPlayerNumber(dc, zone, pt, drawRadius);
                if (castles > 1)
                    DrawCastleBadge(dc, pt, drawRadius, castles);
            }
            else if (isNeutral)
            {
                if (castles > 0)
                    DrawNeutralCastleContent(dc, pt, castles);
            }
            else if (isHub)
            {
                DrawText(dc, "Hub", pt, drawRadius * 1.1, Brushes.White, centered: true);
            }
        }

        // ── Hold-city detection ──────────────────────────────────────────────────

        private static bool IsHoldCityZone(Zone zone) =>
            zone.MainObjects?.Any(o => o.HoldCityWinCon == true) == true;

        // ── Hold-city icon (big golden house) ────────────────────────────────────
        // Drawn centred in the zone circle; a star/crown badge marks it as the target.

        private static void DrawHoldCityIcon(DrawingContext dc, Point centre, double r)
        {
            // Big golden house
            double iconSize = r * 1.35;
            var goldBrush   = new SolidColorBrush(Color.FromRgb(255, 215, 0));
            DrawHouseIcon(dc, centre, iconSize, goldBrush);

            // Small golden star badge at top-right of the circle
            double bx = centre.X + r * 0.62;
            double by = centre.Y - r * 0.62;
            double br = r * 0.30;
            dc.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(80, 60, 0)),
                new Pen(goldBrush, 1.2),
                new Point(bx, by), br, br);
            DrawText(dc, "★", new Point(bx, by), br * 1.55, goldBrush, centered: true, FontWeights.Bold);
        }

        // ── Castle badge (player zones) ──────────────────────────────────────────
        // Small filled circle at bottom-right edge of the zone circle, showing the count.

        private static void DrawCastleBadge(DrawingContext dc, Point zoneCentre, double r, int castles)
        {
            // Position: bottom-right quadrant, just on the border of the zone circle
            double bx = zoneCentre.X + r * 0.72;
            double by = zoneCentre.Y + r * 0.72;
            double br = r * 0.70;   // larger badge

            var badgeBg  = new SolidColorBrush(Color.FromRgb(28, 60, 35));
            var badgePen = new Pen(new SolidColorBrush(SpawnBorder), 1.5);
            dc.DrawEllipse(badgeBg, badgePen, new Point(bx, by), br, br);

            double iconSize = br * 0.60;
            double fontSize = br * 1.05;  // bigger font

            DrawHouseIcon(dc, new Point(bx - br * 0.32, by + 0.5), iconSize,
                new SolidColorBrush(Color.FromRgb(160, 230, 170)));

            DrawText(dc, castles.ToString(CultureInfo.InvariantCulture),
                new Point(bx + br * 0.40, by + 0.5), fontSize,
                new SolidColorBrush(Color.FromRgb(200, 245, 210)),
                centered: true, FontWeights.Bold);
        }

        // ── Neutral castle content (house icon + number centred in circle) ────────

        private static void DrawNeutralCastleContent(DrawingContext dc, Point pt, int castles)
        {
            string countStr = castles.ToString(CultureInfo.InvariantCulture);

            // Scale icon and font relative to current zone radius so they fit at any size
            double iconW   = _zoneRadius * 0.55;
            double fontSize = _zoneRadius * 0.62;
            double gap     = _zoneRadius * 0.12;

            double textW  = MeasureTextWidth(countStr, fontSize);
            double totalW = iconW + gap + textW;

            double startX = pt.X - totalW / 2;

            // House icon
            DrawHouseIcon(dc, new Point(startX + iconW / 2, pt.Y + 0.5), iconW,
                new SolidColorBrush(Color.FromRgb(220, 220, 200)));

            // Count number
            DrawText(dc, countStr,
                new Point(startX + iconW + gap + textW / 2, pt.Y + 0.5),
                fontSize, Brushes.White, centered: true, FontWeights.Bold);
        }

        // ── House icon ───────────────────────────────────────────────────────────
        // Simple roof (triangle) + body (rectangle) drawn with StreamGeometry.
        // `centre` is the horizontal+vertical midpoint of the icon bounding box.
        // `size` is the total height of the icon.

        private static void DrawHouseIcon(DrawingContext dc, Point centre, double size, Brush brush)
        {
            double w  = size * 0.9;   // width of house body
            double h  = size;         // total height
            double rh = h * 0.45;     // roof height
            double bh = h - rh;       // body height

            double left   = centre.X - w / 2;
            double right  = centre.X + w / 2;
            double top    = centre.Y - h / 2;
            double roofBt = top + rh;
            double bottom = top + h;

            // Roof triangle
            var roof = new StreamGeometry();
            using (var ctx = roof.Open())
            {
                ctx.BeginFigure(new Point(centre.X, top), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(right + w * 0.1, roofBt), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(left  - w * 0.1, roofBt), isStroked: false, isSmoothJoin: false);
            }
            roof.Freeze();
            dc.DrawGeometry(brush, null, roof);

            // Body rectangle
            dc.DrawRectangle(brush, null, new Rect(left, roofBt, w, bh));
        }

        // ── Neutral tier styles ──────────────────────────────────────────────────

        private static (Brush Fill, Pen Outline) NeutralTierStyle(Zone zone)
        {
            // Derive tier from the guarded content pool names, which encode the tier number
            // directly (e.g. "classic_template_pool_random_t4_item") and are never scaled.
            //   t4 or t5 → Gold  (High)
            //   t2       → Bronze (Low)
            //   anything else / t3 → Silver (Medium)
            var pool = zone.GuardedContentPool?.FirstOrDefault() ?? string.Empty;
            if (pool.Contains("_t4_") || pool.Contains("_t5_"))
                return (new SolidColorBrush(GoldFill),   new Pen(new SolidColorBrush(GoldBorder),   2.5));
            if (pool.Contains("_t2_") || pool.Contains("_t1_"))
                return (new SolidColorBrush(BronzeFill), new Pen(new SolidColorBrush(BronzeBorder), 2.5));
            return     (new SolidColorBrush(SilverFill), new Pen(new SolidColorBrush(SilverBorder), 2.5));
        }

        // ── Player number ────────────────────────────────────────────────────────
        // Shows the player number (1–8) read from the MainObject "spawn" field ("Player1" → "1").

        private static void DrawPlayerNumber(DrawingContext dc, Zone zone, Point centre, double r)
        {
            string label = "?";
            // Find the Spawn main object and parse its "spawn" value, e.g. "Player3" → "3"
            string? spawnValue = zone.MainObjects?
                .FirstOrDefault(o => o.Type == "Spawn")?.Spawn;
            if (spawnValue is not null && spawnValue.StartsWith("Player", StringComparison.Ordinal))
            {
                string number = spawnValue["Player".Length..];
                if (int.TryParse(number, out _))
                    label = number;
            }

            var brush = new SolidColorBrush(Color.FromRgb(160, 230, 170));
            DrawText(dc, label, centre, r * 1.05, brush, centered: true, FontWeights.Bold);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static int CastleCount(Zone zone)
        {
            int count = 0;
            foreach (MainObject obj in zone.MainObjects ?? [])
                if (obj.Type is "City" or "Spawn")
                    count++;
            return count;
        }

        private static void DrawText(DrawingContext dc, string text, Point point, double size,
            Brush brush, bool centered, FontWeight? weight = null)
        {
            var ft = MakeFormattedText(text, size, brush, weight);
            var origin = centered
                ? new Point(point.X - ft.Width / 2, point.Y - ft.Height / 2)
                : point;
            dc.DrawText(ft, origin);
        }

        private static FormattedText MakeFormattedText(string text, double size, Brush brush, FontWeight? weight = null)
            => new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"),
                    FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
                size, brush, 1.0);

        private static double MeasureTextWidth(string text, double size)
            => MakeFormattedText(text, size, Brushes.White).Width;
    }
}
