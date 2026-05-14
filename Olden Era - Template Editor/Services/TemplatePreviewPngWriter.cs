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
            DrawConnections(dc, connections, positions, _zoneRadius);

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
            // Random and Balanced topologies use GeneratorPosition stamps; Balanced
            // uses the ring-snap pass while Random falls back to the Kamada-Kawai solver.
            if (topology != MapTopology.Random && topology != MapTopology.Balanced)
                return LayoutZonesRing(zones, connections);

            int n = zones.Count;
            if (n == 0)
            {
                _zoneRadius = ZoneRadiusMax;
                return [];
            }
            if (n == 1)
            {
                _zoneRadius = ZoneRadiusMax;
                return new Dictionary<string, Point>(StringComparer.Ordinal)
                    { [zones[0].Name] = new Point(Width / 2.0, Height / 2.0) };
            }

            const double margin = 18;
            const double minGap = 6;

            // ── Build adjacency (Direct connections only — no Proximity/Portal) ───
            var idx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++) idx[zones[i].Name] = i;

            // ── If the generator stamped positions onto zones, use them directly ──
            // The raw positions are in an arbitrary unit square; we re-scale them so
            // the average direct-connection length equals idealEdge (the same target
            // the FR spring embedder uses), giving consistent readable circle sizes.
            if (zones.All(z => z.GeneratorPosition.HasValue))
            {
                // ── Ring-snap pass (balanced concentric placement only) ─────────────
                // Snaps zones to clean evenly-spaced rings when the topology is
                // Balanced. Skipped for Random so that genuinely scattered positions
                // are never forced into a false circular arrangement.
                if (topology == MapTopology.Balanced)
                {
                    // Detect connected components to distinguish a standard single-cluster
                    // balanced layout from a two-cluster tournament balanced layout.
                    var bAdj = new HashSet<int>[n];
                    for (int i = 0; i < n; i++) bAdj[i] = [];
                    foreach (var conn in connections)
                    {
                        if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                        if (string.Equals(conn.ConnectionType, "Portal",    StringComparison.Ordinal)) continue;
                        if (!idx.TryGetValue(conn.From, out int ba)) continue;
                        if (!idx.TryGetValue(conn.To,   out int bb)) continue;
                        bAdj[ba].Add(bb); bAdj[bb].Add(ba);
                    }
                    var bCompId = new int[n];
                    Array.Fill(bCompId, -1);
                    var bComponents = new List<List<int>>();
                    for (int start = 0; start < n; start++)
                    {
                        if (bCompId[start] >= 0) continue;
                        var comp = new List<int>();
                        var bq = new Queue<int>();
                        bq.Enqueue(start); bCompId[start] = bComponents.Count;
                        while (bq.Count > 0)
                        {
                            int u = bq.Dequeue(); comp.Add(u);
                            foreach (int v in bAdj[u])
                                if (bCompId[v] < 0) { bCompId[v] = bComponents.Count; bq.Enqueue(v); }
                        }
                        bComponents.Add(comp);
                    }

                    const double ringGapThreshold = 0.03;

                    if (bComponents.Count == 2)
                    {
                        // ── Two-cluster tournament balanced layout ─────────────────────
                        // Skip ring-snap entirely: it constrains zoneRadius to fit ring
                        // spacing, which makes circles tiny for small clusters.
                        // Fall through to the bounding-box placement path below, which
                        // fills each half-canvas and derives the radius from actual node spacing.
                    }
                    else
                    {
                        // ── Standard single-cluster balanced layout (original logic) ───
                        double rawCx0 = zones.Average(z => z.GeneratorPosition!.Value.X);
                        double rawCy0 = zones.Average(z => z.GeneratorPosition!.Value.Y);
                        var rawDist = zones.Select(z =>
                        {
                            double dx = z.GeneratorPosition!.Value.X - rawCx0;
                            double dy = z.GeneratorPosition!.Value.Y - rawCy0;
                            return Math.Sqrt(dx * dx + dy * dy);
                        }).ToArray();

                        var sortedByDist = Enumerable.Range(0, n).OrderBy(i => rawDist[i]).ToArray();

                        var ringLabel = new int[n];
                        int ringCount = 0;
                        ringLabel[sortedByDist[0]] = 0;
                        for (int k = 1; k < n; k++)
                        {
                            if (rawDist[sortedByDist[k]] - rawDist[sortedByDist[k - 1]] > ringGapThreshold)
                                ringCount++;
                            ringLabel[sortedByDist[k]] = ringCount;
                        }
                        ringCount++;

                        if (ringCount >= 2)
                        {
                            var ringIndices = Enumerable.Range(0, ringCount)
                                .Select(r => Enumerable.Range(0, n).Where(i => ringLabel[i] == r).ToList())
                                .ToList();

                            double drawRadius = Math.Min(Width, Height) / 2.0 - margin - ZoneRadiusMax;

                            double[] AssignRingRadii(double zr)
                            {
                                double mc = 2.0 * zr + minGap;
                                var radii = new double[ringCount];
                                for (int r = 0; r < ringCount; r++)
                                {
                                    int cnt = ringIndices[r].Count;
                                    double natural    = drawRadius * (r + 1.0) / ringCount;
                                    double withinRing = cnt >= 2
                                        ? mc / (2.0 * Math.Sin(Math.PI / cnt))
                                        : (cnt == 1 && r > 0 ? mc : 0.0);
                                    double afterPrev = r > 0 ? radii[r - 1] + mc : 0.0;
                                    radii[r] = Math.Max(natural, Math.Max(withinRing, afterPrev));
                                }
                                return radii;
                            }

                            double lo = 8.0, hi = ZoneRadiusMax;
                            for (int iter = 0; iter < 32; iter++)
                            {
                                double mid = (lo + hi) / 2.0;
                                double[] r2 = AssignRingRadii(mid);
                                if (r2[ringCount - 1] <= drawRadius) lo = mid;
                                else hi = mid;
                            }
                            double ringZoneRadius = Math.Max(lo, 8.0);
                            _zoneRadius = ringZoneRadius;

                            double[] ringRadii = AssignRingRadii(ringZoneRadius);

                            double cx0 = Width / 2.0, cy0 = Height / 2.0;
                            var rPx = new double[n];
                            var rPy = new double[n];

                            for (int r = 0; r < ringCount; r++)
                            {
                                var group = ringIndices[r];
                                int cnt = group.Count;
                                double canvasRadius = ringRadii[r];

                                if (cnt == 1 && r == 0)
                                {
                                    rPx[group[0]] = cx0;
                                    rPy[group[0]] = cy0;
                                    continue;
                                }

                                var sortedByAngle = group
                                    .OrderBy(i => Math.Atan2(
                                        zones[i].GeneratorPosition!.Value.Y - rawCy0,
                                        zones[i].GeneratorPosition!.Value.X - rawCx0))
                                    .ToList();

                                double firstAngle = Math.Atan2(
                                    zones[sortedByAngle[0]].GeneratorPosition!.Value.Y - rawCy0,
                                    zones[sortedByAngle[0]].GeneratorPosition!.Value.X - rawCx0);

                                for (int j = 0; j < cnt; j++)
                                {
                                    double angle = firstAngle + 2.0 * Math.PI * j / cnt;
                                    rPx[sortedByAngle[j]] = cx0 + Math.Cos(angle) * canvasRadius;
                                    rPy[sortedByAngle[j]] = cy0 + Math.Sin(angle) * canvasRadius;
                                }
                            }

                            var rResult = new Dictionary<string, Point>(StringComparer.Ordinal);
                            for (int i = 0; i < n; i++)
                                rResult[zones[i].Name] = new Point(rPx[i], rPy[i]);
                            return rResult;
                        }
                    }
                }
                // ── End ring-snap pass ─────────────────────────────────────────────
                // ── Fall through for non-ringed layouts (Random topology) ──────────
                var gAdj = new HashSet<int>[n];
                for (int i = 0; i < n; i++) gAdj[i] = [];
                foreach (var conn in connections)
                {
                    if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                    if (string.Equals(conn.ConnectionType, "Portal",    StringComparison.Ordinal)) continue;
                    if (!idx.TryGetValue(conn.From, out int ga)) continue;
                    if (!idx.TryGetValue(conn.To,   out int gb)) continue;
                    gAdj[ga].Add(gb); gAdj[gb].Add(ga);
                }

                // Find connected components so we can size circles relative to the
                // largest single cluster (important for two-cluster tournament layouts).
                var gComp = new int[n];
                Array.Fill(gComp, -1);
                var gComponents = new List<List<int>>();
                for (int start = 0; start < n; start++)
                {
                    if (gComp[start] >= 0) continue;
                    var comp = new List<int>();
                    var q = new Queue<int>();
                    q.Enqueue(start); gComp[start] = gComponents.Count;
                    while (q.Count > 0)
                    {
                        int u = q.Dequeue(); comp.Add(u);
                        foreach (int v in gAdj[u]) if (gComp[v] < 0) { gComp[v] = gComponents.Count; q.Enqueue(v); }
                    }
                    gComponents.Add(comp);
                }
                int gMaxCompSize = gComponents.Max(c => c.Count);
                bool isTwoCluster = gComponents.Count == 2; // tournament: two isolated clusters

                // Compute the zone radius based on the largest cluster size.
                double gZoneRadius;
                {
                    double ringRadius0 = (isTwoCluster ? Width / 4.0 : Width / 2.0) - margin;
                    double chord0      = 2.0 * ringRadius0 * Math.Sin(Math.PI / Math.Max(gMaxCompSize, 2));
                    gZoneRadius = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
                    gZoneRadius = Math.Max(gZoneRadius, 8.0);
                }
                _zoneRadius = gZoneRadius;
                double idealEdge2 = gZoneRadius * 3.2;

                // Measure mean raw edge length among direct connections
                double rawEdgeSum = 0; int rawEdgeCount = 0;
                for (int i = 0; i < n; i++)
                    foreach (int j in gAdj[i])
                    {
                        if (j <= i) continue;
                        var pi2 = zones[i].GeneratorPosition!.Value;
                        var pj2 = zones[j].GeneratorPosition!.Value;
                        double dx2 = pi2.X - pj2.X, dy2 = pi2.Y - pj2.Y;
                        rawEdgeSum += Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                        rawEdgeCount++;
                    }

                // If no edges, fall back to spanning the whole draw area
                double gScale = rawEdgeCount > 0
                    ? idealEdge2 / (rawEdgeSum / rawEdgeCount)
                    : Math.Min((Width - 2 * margin) / 1.0, (Height - 2 * margin) / 1.0);

                double canvasCx = Width  / 2.0;
                double canvasCy = Height / 2.0;

                var gPx = new double[n];
                var gPy = new double[n];
                double gPad = gZoneRadius + margin;

                if (isTwoCluster)
                {
                    // Two-cluster tournament layout: map each cluster independently into
                    // its own canvas half (left / right) so there is always a visible gap.
                    // Use a generous initial padding, then compute the true zoneRadius from
                    // the actual minimum inter-node distance after placement.
                    const double initPad = margin + 4.0;
                    double halfDrawW = Width  / 2.0 - initPad;
                    double drawH     = Height - 2 * initPad;

                    foreach (var (compIdx, comp) in gComponents.Select((c, ci) => (ci, c)))
                    {
                        double cMinX = comp.Min(i => zones[i].GeneratorPosition!.Value.X);
                        double cMaxX = comp.Max(i => zones[i].GeneratorPosition!.Value.X);
                        double cMinY = comp.Min(i => zones[i].GeneratorPosition!.Value.Y);
                        double cMaxY = comp.Max(i => zones[i].GeneratorPosition!.Value.Y);
                        double cSpanX = Math.Max(cMaxX - cMinX, 0.001);
                        double cSpanY = Math.Max(cMaxY - cMinY, 0.001);

                        // Fill the half-canvas as much as possible — no edge-length cap.
                        double localScale = Math.Min(halfDrawW / cSpanX, drawH / cSpanY);

                        // Centre each cluster in its half: cluster 0 → left, cluster 1 → right
                        double halfCx = compIdx == 0
                            ? initPad + halfDrawW / 2.0
                            : Width - initPad - halfDrawW / 2.0;
                        double halfCy = Height / 2.0;

                        double rawCx = (cMinX + cMaxX) / 2.0;
                        double rawCy = (cMinY + cMaxY) / 2.0;

                        foreach (int i in comp)
                        {
                            var gp = zones[i].GeneratorPosition!.Value;
                            gPx[i] = halfCx + (gp.X - rawCx) * localScale;
                            gPy[i] = halfCy + (gp.Y - rawCy) * localScale;
                        }
                    }

                    // Derive zoneRadius from the actual minimum intra-cluster inter-node
                    // distance so circles fill the available space without overlapping.
                    // Only consider pairs within the same cluster — cross-cluster distances
                    // can be near zero (both clusters mapped to opposite canvas halves) and
                    // would otherwise collapse the radius to the minimum floor.
                    double minDist = double.MaxValue;
                    foreach (var comp in gComponents)
                    {
                        for (int ii = 0; ii < comp.Count; ii++)
                            for (int jj = ii + 1; jj < comp.Count; jj++)
                            {
                                double dx = gPx[comp[ii]] - gPx[comp[jj]];
                                double dy = gPy[comp[ii]] - gPy[comp[jj]];
                                double d = Math.Sqrt(dx * dx + dy * dy);
                                if (d < minDist) minDist = d;
                            }
                    }
                    if (minDist < double.MaxValue && minDist > 0.001)
                        gZoneRadius = Math.Min(ZoneRadiusMax, Math.Max((minDist - 26.0) / 2.0, 4.0));
                    _zoneRadius = gZoneRadius;

                    // Positions are already correctly placed — skip Pass A+B and final fit.
                    var tcResult = new Dictionary<string, Point>(StringComparer.Ordinal);
                    for (int i = 0; i < n; i++)
                        tcResult[zones[i].Name] = new Point(gPx[i], gPy[i]);
                    return tcResult;
                }
                else
                {
                    // Single cluster / regular random layout: centre on canvas.
                    double gCx = zones.Average(z => z.GeneratorPosition!.Value.X);
                    double gCy = zones.Average(z => z.GeneratorPosition!.Value.Y);

                    for (int i = 0; i < n; i++)
                    {
                        var gp = zones[i].GeneratorPosition!.Value;
                        gPx[i] = (gp.X - gCx) * gScale;
                        gPy[i] = (gp.Y - gCy) * gScale;
                    }

                    // Clamp to canvas with padding (only shrink, never stretch)
                    double rawMinX = gPx.Min(), rawMaxX = gPx.Max();
                    double rawMinY = gPy.Min(), rawMaxY = gPy.Max();
                    double drawW2 = Width  - 2 * gPad;
                    double drawH2 = Height - 2 * gPad;
                    double fitScale = 1.0;
                    if (rawMaxX - rawMinX > drawW2 && rawMaxX - rawMinX > 0.001) fitScale = Math.Min(fitScale, drawW2 / (rawMaxX - rawMinX));
                    if (rawMaxY - rawMinY > drawH2 && rawMaxY - rawMinY > 0.001) fitScale = Math.Min(fitScale, drawH2 / (rawMaxY - rawMinY));

                    for (int i = 0; i < n; i++) { gPx[i] = canvasCx + gPx[i] * fitScale; gPy[i] = canvasCy + gPy[i] * fitScale; }
                }

                // ── Pass A+B: same correction passes as the FR path ───────────────
                double gMinDist   = gZoneRadius * 3.8;
                double gEdgeClear = gZoneRadius * 1.2;

                for (int abPass = 0; abPass < 500; abPass++)
                {
                    bool anyAB = false;

                    // A: hard floor — minimum centre-to-centre distance
                    for (int i = 0; i < n; i++)
                        for (int j = i + 1; j < n; j++)
                        {
                            double dx = gPx[i] - gPx[j], dy = gPy[i] - gPy[j];
                            double d  = Math.Sqrt(dx * dx + dy * dy);
                            if (d >= gMinDist) continue;
                            if (d < 0.001) { dx = 1; dy = 0; d = 0.001; }
                            double push = (gMinDist - d) / 2.0;
                            gPx[i] += dx / d * push; gPy[i] += dy / d * push;
                            gPx[j] -= dx / d * push; gPy[j] -= dy / d * push;
                            anyAB = true;
                        }

                    // B: edge clearance — push nodes off connection lines
                    for (int a = 0; a < n; a++)
                        foreach (int b in gAdj[a])
                        {
                            if (b <= a) continue;
                            double ex = gPx[b] - gPx[a], ey = gPy[b] - gPy[a];
                            double elen2 = ex * ex + ey * ey;
                            if (elen2 < 0.001) continue;
                            double elenInv = 1.0 / Math.Sqrt(elen2);

                            for (int c2 = 0; c2 < n; c2++)
                            {
                                if (c2 == a || c2 == b) continue;
                                double tProj = ((gPx[c2] - gPx[a]) * ex + (gPy[c2] - gPy[a]) * ey) / elen2;
                                if (tProj < 0.0 || tProj > 1.0) continue;
                                double projX = gPx[a] + tProj * ex, projY = gPy[a] + tProj * ey;
                                double nx2 = gPx[c2] - projX, ny2 = gPy[c2] - projY;
                                double dist = Math.Sqrt(nx2 * nx2 + ny2 * ny2);
                                if (dist >= gEdgeClear) continue;

                                double perpX = (dist < 0.001) ? ey * elenInv : nx2 / dist;
                                double perpY = (dist < 0.001) ? -ex * elenInv : ny2 / dist;
                                double cx2A = projX + perpX * gEdgeClear, cy2A = projY + perpY * gEdgeClear;
                                double cx2B = projX - perpX * gEdgeClear, cy2B = projY - perpY * gEdgeClear;

                                double scoreA = 0, scoreB = 0;
                                foreach (int nb in gAdj[c2])
                                {
                                    double dax = cx2A - gPx[nb], day = cy2A - gPy[nb];
                                    double dbx = cx2B - gPx[nb], dby = cy2B - gPy[nb];
                                    scoreA += dax * dax + day * day;
                                    scoreB += dbx * dbx + dby * dby;
                                }
                                if (scoreB < scoreA) { gPx[c2] = cx2B; gPy[c2] = cy2B; }
                                else                 { gPx[c2] = cx2A; gPy[c2] = cy2A; }
                                anyAB = true;
                            }
                        }

                    if (!anyAB) break;
                }   // end Pass A+B

                // ── Final fit: re-centre and shrink-to-fit after correction passes ─
                // Passes A/B can push nodes outside the canvas — translate the whole
                // layout back to centre, then uniformly scale down if it still overflows.
                {
                    double finalMinX = gPx.Min(), finalMaxX = gPx.Max();
                    double finalMinY = gPy.Min(), finalMaxY = gPy.Max();
                    double finalCx = (finalMinX + finalMaxX) / 2.0;
                    double finalCy = (finalMinY + finalMaxY) / 2.0;
                    // Translate so the bounding box is centred on the canvas
                    for (int i = 0; i < n; i++) { gPx[i] += canvasCx - finalCx; gPy[i] += canvasCy - finalCy; }

                    // Recompute after translation
                    finalMinX = gPx.Min(); finalMaxX = gPx.Max();
                    finalMinY = gPy.Min(); finalMaxY = gPy.Max();
                    double spanX = finalMaxX - finalMinX, spanY = finalMaxY - finalMinY;
                    double allowW = Width  - 2 * gPad, allowH = Height - 2 * gPad;
                    double shrink = 1.0;
                    if (spanX > allowW && spanX > 0.001) shrink = Math.Min(shrink, allowW / spanX);
                    if (spanY > allowH && spanY > 0.001) shrink = Math.Min(shrink, allowH / spanY);
                    if (shrink < 1.0)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            gPx[i] = canvasCx + (gPx[i] - canvasCx) * shrink;
                            gPy[i] = canvasCy + (gPy[i] - canvasCy) * shrink;
                        }
                        // Also shrink the radius so circles don't overlap after scale-down
                        _zoneRadius = Math.Max(gZoneRadius * shrink, 8.0);
                    }
                }

                var gResult = new Dictionary<string, Point>(StringComparer.Ordinal);
                for (int i = 0; i < n; i++)
                    gResult[zones[i].Name] = new Point(gPx[i], gPy[i]);
                return gResult;
            }

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

            // ── Detect connected components ───────────────────────────────────────
            var component = new int[n];
            Array.Fill(component, -1);
            var components = new List<List<int>>();
            for (int start = 0; start < n; start++)
            {
                if (component[start] >= 0) continue;
                int cid = components.Count;
                var comp = new List<int>();
                components.Add(comp);
                var bfsQ = new Queue<int>();
                bfsQ.Enqueue(start);
                component[start] = cid;
                while (bfsQ.Count > 0)
                {
                    int u = bfsQ.Dequeue();
                    comp.Add(u);
                    foreach (int v in adj[u])
                    {
                        if (component[v] >= 0) continue;
                        component[v] = cid;
                        bfsQ.Enqueue(v);
                    }
                }
            }

            int numComps = components.Count;

            // ── Zone radius: based on the largest component so circles are readable ─
            int maxCompSize = components.Max(c => c.Count);
            double zoneRadius;
            if (maxCompSize <= 1)
            {
                // All components are singletons — sin(π/1) = 0 collapses the chord formula.
                // Use the maximum radius directly; the tiling logic will space the dots apart.
                zoneRadius = ZoneRadiusMax;
            }
            else
            {
                double ringRadius0 = Width / 2.0 - margin;
                double chord0      = 2.0 * ringRadius0 * Math.Sin(Math.PI / maxCompSize);
                zoneRadius = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
                zoneRadius = Math.Max(zoneRadius, 8.0);
            }
            _zoneRadius = zoneRadius;

            double idealEdge = zoneRadius * 3.2;

            // ── Run Kamada-Kawai independently per component ──────────────────────
            // Results stored in local arrays; we'll stitch bounding boxes together
            // after all components are solved.
            var compPx   = new double[numComps][];
            var compPy   = new double[numComps][];

            for (int c = 0; c < numComps; c++)
            {
                var comp = components[c];
                int cn   = comp.Count;
                var lpx  = new double[cn];
                var lpy  = new double[cn];

                // Map global index → local index within this component
                var localIdx = new Dictionary<int, int>();
                for (int i = 0; i < cn; i++) localIdx[comp[i]] = i;

                if (cn == 1) { compPx[c] = lpx; compPy[c] = lpy; continue; }

                // Build local adjacency for this component
                var ladj = new HashSet<int>[cn];
                for (int i = 0; i < cn; i++) ladj[i] = [];
                for (int i = 0; i < cn; i++)
                    foreach (int gv in adj[comp[i]])
                        if (localIdx.TryGetValue(gv, out int lv))
                        { ladj[i].Add(lv); ladj[lv].Add(i); }

                if (cn == 2)
                {
                    lpx[0] = -idealEdge / 2; lpy[0] = 0;
                    lpx[1] =  idealEdge / 2; lpy[1] = 0;
                    compPx[c] = lpx; compPy[c] = lpy; continue;
                }

                // ── Fruchterman-Reingold spring embedder ──────────────────────────
                // Seed deterministically on a circle so output is stable across runs.
                double seedR = idealEdge * cn / (2.0 * Math.PI);
                seedR = Math.Max(seedR, idealEdge);
                for (int i = 0; i < cn; i++)
                {
                    double ang = -Math.PI / 2.0 + i * 2.0 * Math.PI / cn;
                    lpx[i] = Math.Cos(ang) * seedR;
                    lpy[i] = Math.Sin(ang) * seedR;
                }

                // k = ideal edge length.  t starts large so nodes can cross the graph.
                double k  = idealEdge;
                double t  = k * 2.5;
                double tMin = k * 0.005;
                int    frIter = 400;
                double cool = Math.Pow(tMin / t, 1.0 / frIter);

                var fx = new double[cn];
                var fy = new double[cn];
                for (int iter = 0; iter < frIter; iter++)
                {
                    Array.Clear(fx, 0, cn);
                    Array.Clear(fy, 0, cn);

                    // Repulsion: every pair (O(n²) — n is small, ≤ ~15)
                    for (int i = 0; i < cn; i++)
                        for (int j = i + 1; j < cn; j++)
                        {
                            double dx = lpx[i] - lpx[j];
                            double dy = lpy[i] - lpy[j];
                            double d  = Math.Sqrt(dx * dx + dy * dy);
                            if (d < 0.001) { dx = 0.5 + i * 0.1; dy = 0.5 + j * 0.1; d = Math.Sqrt(dx * dx + dy * dy); }
                            double fr = k * k / d;
                            fx[i] += fr * dx / d;  fy[i] += fr * dy / d;
                            fx[j] -= fr * dx / d;  fy[j] -= fr * dy / d;
                        }

                    // Attraction: only along edges
                    for (int i = 0; i < cn; i++)
                        foreach (int j in ladj[i])
                        {
                            if (j <= i) continue;
                            double dx = lpx[i] - lpx[j];
                            double dy = lpy[i] - lpy[j];
                            double d  = Math.Sqrt(dx * dx + dy * dy);
                            if (d < 0.001) continue;
                            double fa = d * d / k;
                            fx[i] -= fa * dx / d;  fy[i] -= fa * dy / d;
                            fx[j] += fa * dx / d;  fy[j] += fa * dy / d;
                        }

                    // Move, capped to temperature
                    for (int i = 0; i < cn; i++)
                    {
                        double len = Math.Sqrt(fx[i] * fx[i] + fy[i] * fy[i]);
                        if (len > t && len > 0.001) { fx[i] = fx[i] / len * t; fy[i] = fy[i] / len * t; }
                        lpx[i] += fx[i];
                        lpy[i] += fy[i];
                    }
                    t = Math.Max(t * cool, tMin);
                }

                // Passes A and B run together until both are simultaneously satisfied.
                double minDist   = zoneRadius * 3.8;
                double edgeClear = zoneRadius * 1.2;

                for (int abPass = 0; abPass < 500; abPass++)
                {
                    bool anyAB = false;

                    // ── A: hard floor – minimum centre-to-centre distance ─────────
                    for (int i = 0; i < cn; i++)
                        for (int j = i + 1; j < cn; j++)
                        {
                            double dx = lpx[i] - lpx[j], dy = lpy[i] - lpy[j];
                            double d  = Math.Sqrt(dx * dx + dy * dy);
                            if (d >= minDist) continue;
                            if (d < 0.001) { dx = 1; dy = 0; d = 0.001; }
                            double push = (minDist - d) / 2.0;
                            lpx[i] += dx / d * push;  lpy[i] += dy / d * push;
                            lpx[j] -= dx / d * push;  lpy[j] -= dy / d * push;
                            anyAB = true;
                        }

                    // ── B: edge clearance – push nodes off connection lines ───────
                    // Pick the side (of the edge) that keeps c2 closer to its own
                    // neighbours.  Only flip if the other side is strictly better —
                    // this prevents oscillation while correcting wrong-side placements.
                    for (int a = 0; a < cn; a++)
                        foreach (int b in ladj[a])
                        {
                            if (b <= a) continue;
                            double ex = lpx[b] - lpx[a], ey = lpy[b] - lpy[a];
                            double elen2 = ex * ex + ey * ey;
                            if (elen2 < 0.001) continue;
                            double elenInv = 1.0 / Math.Sqrt(elen2);

                            for (int c2 = 0; c2 < cn; c2++)
                            {
                                if (c2 == a || c2 == b) continue;

                                double tProj = ((lpx[c2] - lpx[a]) * ex + (lpy[c2] - lpy[a]) * ey) / elen2;
                                if (tProj < 0.0 || tProj > 1.0) continue;

                                double projX = lpx[a] + tProj * ex;
                                double projY = lpy[a] + tProj * ey;
                                double nx2   = lpx[c2] - projX;
                                double ny2   = lpy[c2] - projY;
                                double dist  = Math.Sqrt(nx2 * nx2 + ny2 * ny2);
                                if (dist >= edgeClear) continue;

                                // Unit perp in current offset direction (or arbitrary if on line)
                                double perpX = (dist < 0.001) ? ey * elenInv : nx2 / dist;
                                double perpY = (dist < 0.001) ? -ex * elenInv : ny2 / dist;

                                // Both candidate positions at exactly edgeClear from projection
                                double cx2A = projX + perpX * edgeClear;
                                double cy2A = projY + perpY * edgeClear;
                                double cx2B = projX - perpX * edgeClear;
                                double cy2B = projY - perpY * edgeClear;

                                // Pick whichever side keeps c2 closer to its neighbours
                                double scoreA = 0, scoreB = 0;
                                foreach (int nb in ladj[c2])
                                {
                                    double dax = cx2A - lpx[nb], day = cy2A - lpy[nb];
                                    double dbx = cx2B - lpx[nb], dby = cy2B - lpy[nb];
                                    scoreA += dax * dax + day * day;
                                    scoreB += dbx * dbx + dby * dby;
                                }

                                if (scoreB < scoreA)
                                { lpx[c2] = cx2B; lpy[c2] = cy2B; }
                                else
                                { lpx[c2] = cx2A; lpy[c2] = cy2A; }

                                anyAB = true;
                            }
                        }

                    if (!anyAB) break;
                }

                // Final guarantee: hard-snap any node still violating an edge,
                // using score-based side selection, then re-enforce A.
                // Repeats until clean so A-corrections cannot re-introduce violations.
                for (int snapPass = 0; snapPass < 200; snapPass++)
                {
                    bool anySnap = false;

                    for (int a = 0; a < cn; a++)
                        foreach (int b in ladj[a])
                        {
                            if (b <= a) continue;
                            double ex = lpx[b] - lpx[a], ey = lpy[b] - lpy[a];
                            double elen2 = ex * ex + ey * ey;
                            if (elen2 < 0.001) continue;
                            double elenInv = 1.0 / Math.Sqrt(elen2);

                            for (int c2 = 0; c2 < cn; c2++)
                            {
                                if (c2 == a || c2 == b) continue;

                                double tProj = ((lpx[c2] - lpx[a]) * ex + (lpy[c2] - lpy[a]) * ey) / elen2;
                                if (tProj < 0.0 || tProj > 1.0) continue;

                                double projX = lpx[a] + tProj * ex;
                                double projY = lpy[a] + tProj * ey;
                                double nx2   = lpx[c2] - projX;
                                double ny2   = lpy[c2] - projY;
                                double dist  = Math.Sqrt(nx2 * nx2 + ny2 * ny2);
                                if (dist >= edgeClear) continue;

                                // Pick the side that keeps c2 closer to its own neighbours
                                double perpX = (dist < 0.001) ? ey * elenInv : nx2 / dist;
                                double perpY = (dist < 0.001) ? -ex * elenInv : ny2 / dist;

                                double cx2A = projX + perpX * edgeClear;
                                double cy2A = projY + perpY * edgeClear;
                                double cx2B = projX - perpX * edgeClear;
                                double cy2B = projY - perpY * edgeClear;

                                double scoreA = 0, scoreB = 0;
                                foreach (int nb in ladj[c2])
                                {
                                    double dax = cx2A - lpx[nb], day = cy2A - lpy[nb];
                                    double dbx = cx2B - lpx[nb], dby = cy2B - lpy[nb];
                                    scoreA += dax * dax + day * day;
                                    scoreB += dbx * dbx + dby * dby;
                                }

                                lpx[c2] = scoreB < scoreA ? cx2B : cx2A;
                                lpy[c2] = scoreB < scoreA ? cy2B : cy2A;
                                anySnap = true;
                            }
                        }

                    if (!anySnap) break;

                    // Re-enforce A after each snap round
                    for (int i = 0; i < cn; i++)
                        for (int j = i + 1; j < cn; j++)
                        {
                            double dx = lpx[i] - lpx[j], dy = lpy[i] - lpy[j];
                            double d  = Math.Sqrt(dx * dx + dy * dy);
                            if (d >= minDist) continue;
                            if (d < 0.001) { dx = 1; dy = 0; d = 0.001; }
                            double push = (minDist - d) / 2.0;
                            lpx[i] += dx / d * push;  lpy[i] += dy / d * push;
                            lpx[j] -= dx / d * push;  lpy[j] -= dy / d * push;
                        }
                }
                compPx[c] = lpx;   // ← this line is missing
                compPy[c] = lpy;
            }

            // ── Tile component bounding boxes across the canvas ───────────────────
            // Compute each component's bounding box, then lay them out left-to-right
            // (or top-to-bottom for 2 components to match portrait orientation) with
            // equal padding between them so the canvas is used efficiently.
            var compMinX = new double[numComps];
            var compMaxX = new double[numComps];
            var compMinY = new double[numComps];
            var compMaxY = new double[numComps];
            for (int c = 0; c < numComps; c++)
            {
                compMinX[c] = compPx[c].Min(); compMaxX[c] = compPx[c].Max();
                compMinY[c] = compPy[c].Min(); compMaxY[c] = compPy[c].Max();
            }

            double pad = zoneRadius + margin;

            var positions = new Dictionary<string, Point>(StringComparer.Ordinal);

            if (numComps == 1)
            {
                // Single component — centre on canvas, scale to fit.
                var comp = components[0];
                double spanX = Math.Max(compMaxX[0] - compMinX[0], 1);
                double spanY = Math.Max(compMaxY[0] - compMinY[0], 1);
                double drawW = Width  - 2 * pad;
                double drawH = Height - 2 * pad;
                double scale = Math.Min(drawW / spanX, drawH / spanY);
                double offX  = pad + (drawW - spanX * scale) / 2.0;
                double offY  = pad + (drawH - spanY * scale) / 2.0;
                for (int i = 0; i < comp.Count; i++)
                    positions[zones[comp[i]].Name] = new Point(
                        offX + (compPx[0][i] - compMinX[0]) * scale,
                        offY + (compPy[0][i] - compMinY[0]) * scale);
            }
            else
            {
                // Multiple components — tile side by side with equal inter-cluster gaps.
                // Choose tiling direction: portrait canvas → stack vertically for 2 comps.
                bool stackVertical = numComps == 2 && Height >= Width;

                // Reserve an explicit gap between clusters so they never touch.
                double interGap   = zoneRadius * 3.0;
                double totalDraw  = (stackVertical ? Height : Width) - 2 * pad - interGap * (numComps - 1);
                double slotSize   = totalDraw / numComps;
                double crossDraw  = (stackVertical ? Width : Height) - 2 * pad;

                // Find a uniform scale that fits all components in their allocated slot.
                // Zero-span (singleton) components do not constrain the scale — they will
                // be centred within their slot regardless of uniformScale.
                double uniformScale = double.MaxValue;
                for (int c = 0; c < numComps; c++)
                {
                    double spanMain  = stackVertical
                        ? compMaxY[c] - compMinY[c]
                        : compMaxX[c] - compMinX[c];
                    double spanCross = stackVertical
                        ? compMaxX[c] - compMinX[c]
                        : compMaxY[c] - compMinY[c];
                    if (spanMain  > 0.001) uniformScale = Math.Min(uniformScale, (slotSize - 2 * zoneRadius) / spanMain);
                    if (spanCross > 0.001) uniformScale = Math.Min(uniformScale, crossDraw / spanCross);
                }
                // All singletons → scale is unconstrained; set to 1 (extent will be 0 either way).
                if (uniformScale >= double.MaxValue / 2) uniformScale = 1.0;
                uniformScale = Math.Max(uniformScale, 0.1);

                double cursor = pad;
                for (int c = 0; c < numComps; c++)
                {
                    var comp = components[c];
                    double spanMain  = stackVertical
                        ? compMaxY[c] - compMinY[c]
                        : compMaxX[c] - compMinX[c];
                    double spanCross = stackVertical
                        ? compMaxX[c] - compMinX[c]
                        : compMaxY[c] - compMinY[c];

                    double mainExtent  = spanMain  * uniformScale;
                    double crossExtent = spanCross * uniformScale;

                    // Centre within the slot along main axis and within canvas along cross axis.
                    // When span is 0 (singleton), both extents are 0 and the zone lands exactly
                    // at the slot centre / canvas centre.
                    double mainStart  = cursor + (slotSize - mainExtent)  / 2.0;
                    double crossStart = pad    + (crossDraw - crossExtent) / 2.0;

                    for (int i = 0; i < comp.Count; i++)
                    {
                        double mainVal  = stackVertical ? compPy[c][i] : compPx[c][i];
                        double crossVal = stackVertical ? compPx[c][i] : compPy[c][i];
                        double mainOff  = stackVertical ? compMinY[c]  : compMinX[c];
                        double crossOff = stackVertical ? compMinX[c]  : compMinY[c];

                        double px2 = stackVertical
                            ? crossStart + (crossVal - crossOff) * uniformScale
                            :  mainStart + (mainVal  - mainOff)  * uniformScale;
                        double py2 = stackVertical
                            ?  mainStart + (mainVal  - mainOff)  * uniformScale
                            : crossStart + (crossVal - crossOff) * uniformScale;

                        positions[zones[comp[i]].Name] = new Point(px2, py2);
                    }

                    cursor += slotSize + interGap;
                }
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
            const double minGap = 6;
            double ringRadius0  = Width / 2.0 - margin;

            Zone? hub   = zones.FirstOrDefault(z => string.Equals(z.Name, "Hub", StringComparison.Ordinal));
            var outer   = hub is null ? zones : zones.Where(z => z != hub).ToList();
            int outerN  = Math.Max(1, outer.Count);

            // Solve for zoneRadius so that adjacent zone circles have a visible gap
            // for connection lines at the actual ring radius where they are placed.
            //
            // For non-hub layouts (chain / ring):
            //   actual ring radius: ringRadius = ringRadius0 - zoneRadius
            //   chord constraint:   2*(ringRadius0 - zoneRadius)*sin(π/outerN) = 2*zoneRadius + connectionGap
            //   → zoneRadius = (2*ringRadius0*sinA - connectionGap) / (2*(1 + sinA))
            //
            // connectionGap is deliberately generous so connection lines are clearly visible.
            //
            // For hub layouts the spoke-ring radius is pushed out by the hub clearance so
            // we keep the original chord estimate (conservative, always ≤ the no-hub size).
            const double connectionGap = 26; // minimum visible space between circle edges for connection lines
            double sinA = outerN > 1 ? Math.Sin(Math.PI / outerN) : 1.0;
            double zoneRadius;
            if (hub is null)
            {
                zoneRadius = (2.0 * ringRadius0 * sinA - connectionGap) / (2.0 * (1.0 + sinA));
            }
            else
            {
                double chord0 = 2.0 * ringRadius0 * Math.Sin(Math.PI / Math.Max(1, outerN));
                zoneRadius = (chord0 - connectionGap) / 2.0;
            }
            zoneRadius = Math.Min(ZoneRadiusMax, Math.Max(zoneRadius, 4.0));
            _zoneRadius = zoneRadius;

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

        private static void DrawConnections(DrawingContext dc, List<Connection> connections, Dictionary<string, Point> positions, double zoneRadius)
        {
            double r                   = 0;
            double curveThreshold      = zoneRadius * 1.3;  // zone within this of the line → add curve
            double curveOffset         = zoneRadius * 2.0;  // how far the control point is pushed away

            // ── Arc-clearance helper: push ctrl away from any zone circle the arc clips ──
            const int    ArcSamples   = 32;
            const int    MaxArcIter   = 20;
            const double ArcClearance = 6.0;

            Point RefineCtrl(Point p1, Point ctrl, Point p2, string fromName, string toName)
            {
                for (int arcIter = 0; arcIter < MaxArcIter; arcIter++)
                {
                    bool arcClean = true;
                    Point? worstZone = null;
                    double worstPenetration = 0;
                    double worstSample = 0.5;

                    for (int s = 1; s < ArcSamples; s++)
                    {
                        double st = (double)s / ArcSamples;
                        double mt = 1.0 - st;
                        double bx = mt * mt * p1.X + 2 * mt * st * ctrl.X + st * st * p2.X;
                        double by = mt * mt * p1.Y + 2 * mt * st * ctrl.Y + st * st * p2.Y;

                        foreach (var kvp in positions)
                        {
                            if (kvp.Key == fromName || kvp.Key == toName) continue;
                            double ex2 = bx - kvp.Value.X, ey2 = by - kvp.Value.Y;
                            double d2 = Math.Sqrt(ex2 * ex2 + ey2 * ey2);
                            double penetration = zoneRadius + ArcClearance - d2;
                            if (penetration > worstPenetration)
                            {
                                worstPenetration = penetration;
                                worstZone = kvp.Value;
                                worstSample = st;
                                arcClean = false;
                            }
                        }
                    }

                    if (arcClean) break;

                    double wmt = 1.0 - worstSample;
                    double wbx = wmt * wmt * p1.X + 2 * wmt * worstSample * ctrl.X + worstSample * worstSample * p2.X;
                    double wby = wmt * wmt * p1.Y + 2 * wmt * worstSample * ctrl.Y + worstSample * worstSample * p2.Y;

                    double ex3 = wbx - worstZone!.Value.X, ey3 = wby - worstZone!.Value.Y;
                    double el3 = Math.Sqrt(ex3 * ex3 + ey3 * ey3);
                    // fallback perpendicular to chord if on top of zone centre
                    if (el3 < 0.001)
                    {
                        double chx2 = p2.X - p1.X, chy2 = p2.Y - p1.Y;
                        double cl2 = Math.Sqrt(chx2 * chx2 + chy2 * chy2);
                        ex3 = cl2 > 0.001 ? -chy2 / cl2 : 1; ey3 = cl2 > 0.001 ? chx2 / cl2 : 0; el3 = 1;
                    }
                    double nx3 = ex3 / el3, ny3 = ey3 / el3;

                    double weight = 2 * wmt * worstSample;
                    if (weight < 0.01) weight = 0.01;
                    double nudge = (worstPenetration + 2.0) / weight;
                    ctrl = new Point(ctrl.X + nx3 * nudge, ctrl.Y + ny3 * nudge);
                }

                // --- NEW FAIL-SAFE CLAMPING LOGIC ---

                // 1. Deflection Cap: Prevent the curve from bowing out to ridiculous extremes
                double midX = (p1.X + p2.X) / 2.0;
                double midY = (p1.Y + p2.Y) / 2.0;
                double defX = ctrl.X - midX;
                double defY = ctrl.Y - midY;
                double defDist = Math.Sqrt(defX * defX + defY * defY);

                // Max allowed bow is ~40% of the canvas size
                double maxDeflection = Math.Min(Width, Height) * 0.4;

                if (defDist > maxDeflection)
                {
                    ctrl = new Point(
                        midX + (defX / defDist) * maxDeflection,
                        midY + (defY / defDist) * maxDeflection
                    );
                }

                // 2. Hard Canvas Clamp: Keep the control point safely inside the image bounds
                const double edgePad = 15.0;
                ctrl = new Point(
                    Math.Clamp(ctrl.X, edgePad, Width - edgePad),
                    Math.Clamp(ctrl.Y, edgePad, Height - edgePad)
                );

                // ------------------------------------
                return ctrl;
            }

            // ── Build path data for every connection ──────────────────────────────
            // Keep from/to names alongside each entry so RefineCtrl can exclude them.
            var drawnRaw = new List<(Point P1, Point Ctrl, Point P2, bool IsPortal, bool HasCurve, string From, string To)>();

            foreach (Connection conn in connections)
            {
                if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                if (!positions.TryGetValue(conn.From, out Point from)) continue;
                if (!positions.TryGetValue(conn.To,   out Point to))   continue;

                bool isPortal = string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal);

                double dx = to.X - from.X, dy = to.Y - from.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1) continue;
                double ux = dx / dist, uy = dy / dist;

                Point p1 = new Point(from.X + ux * r, from.Y + uy * r);
                Point p2 = new Point(to.X   - ux * r, to.Y   - uy * r);

                // Find zone closest to the straight chord — if within curveThreshold, add curve.
                double bestPerp  = curveThreshold;
                double bestT     = 0.5;
                Point? blockZone = null;

                foreach (var kvp in positions)
                {
                    if (kvp.Key == conn.From || kvp.Key == conn.To) continue;
                    var c = kvp.Value;
                    double t = ((c.X - from.X) * dx + (c.Y - from.Y) * dy) / (dist * dist);
                    if (t <= 0.05 || t >= 0.95) continue;
                    double projX = from.X + t * dx, projY = from.Y + t * dy;
                    double pd = Math.Sqrt((c.X - projX) * (c.X - projX) + (c.Y - projY) * (c.Y - projY));
                    if (pd < bestPerp) { bestPerp = pd; bestT = t; blockZone = c; }
                }

                bool  hasCurve = blockZone.HasValue;
                Point ctrl;
                if (hasCurve)
                {
                    Point mid  = new Point(from.X + bestT * dx, from.Y + bestT * dy);
                    double px  = mid.X - blockZone!.Value.X;
                    double py  = mid.Y - blockZone!.Value.Y;
                    double pl  = Math.Sqrt(px * px + py * py);
                    if (pl < 0.001) { px = -uy; py = ux; pl = 1; }
                    ctrl = new Point(mid.X + px / pl * curveOffset, mid.Y + py / pl * curveOffset);
                    ctrl = RefineCtrl(p1, ctrl, p2, conn.From, conn.To);
                }
                else
                {
                    ctrl = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
                }

                drawnRaw.Add((p1, ctrl, p2, isPortal, hasCurve, conn.From, conn.To));
            }

            // ── Shared-endpoint curve untangling ─────────────────────────────────
            // Two curved connections sharing a zone can have arcs that cross near
            // the shared endpoint. Detect and fix by mirroring the more-deflected
            // ctrl across its chord, then re-run arc-clearance on the flipped curve.
            {
                bool anyFlipped = true;
                for (int untanglePass = 0; untanglePass < 20 && anyFlipped; untanglePass++)
                {
                    anyFlipped = false;
                    for (int i = 0; i < drawnRaw.Count; i++)
                    {
                        if (!drawnRaw[i].HasCurve) continue;
                        for (int j = i + 1; j < drawnRaw.Count; j++)
                        {
                            if (!drawnRaw[j].HasCurve) continue;

                            var (p1i, ctrli, p2i, isPortalI, hasCurveI, fromI, toI) = drawnRaw[i];
                            var (p1j, ctrlj, p2j, isPortalJ, hasCurveJ, fromJ, toJ) = drawnRaw[j];

                            bool shareStart = (p1i.X == p1j.X && p1i.Y == p1j.Y) || (p1i.X == p2j.X && p1i.Y == p2j.Y);
                            bool shareEnd   = (p2i.X == p1j.X && p2i.Y == p1j.Y) || (p2i.X == p2j.X && p2i.Y == p2j.Y);
                            if (!shareStart && !shareEnd) continue;

                            const int UntangleSamples = 24;
                            var piPoly = SamplePath(p1i, ctrli, p2i, hasCurveI, UntangleSamples);
                            var pjPoly = SamplePath(p1j, ctrlj, p2j, hasCurveJ, UntangleSamples);
                            if (!PolylinesIntersect(piPoly, pjPoly, out _, out _, out _)) continue;

                            double DeflectionSq(Point a, Point ctrl0, Point b)
                            {
                                double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
                                double ox = ctrl0.X - mx, oy = ctrl0.Y - my;
                                return ox * ox + oy * oy;
                            }

                            Point FlipCtrl(Point a, Point ctrl0, Point b)
                            {
                                double chx = b.X - a.X, chy = b.Y - a.Y;
                                double len2 = chx * chx + chy * chy;
                                if (len2 < 0.001) return ctrl0;
                                double t2 = ((ctrl0.X - a.X) * chx + (ctrl0.Y - a.Y) * chy) / len2;
                                double footX = a.X + t2 * chx, footY = a.Y + t2 * chy;
                                return new Point(2 * footX - ctrl0.X, 2 * footY - ctrl0.Y);
                            }

                            if (DeflectionSq(p1i, ctrli, p2i) >= DeflectionSq(p1j, ctrlj, p2j))
                            {
                                var newCtrl = RefineCtrl(p1i, FlipCtrl(p1i, ctrli, p2i), p2i, fromI, toI);
                                drawnRaw[i] = (p1i, newCtrl, p2i, isPortalI, hasCurveI, fromI, toI);
                            }
                            else
                            {
                                var newCtrl = RefineCtrl(p1j, FlipCtrl(p1j, ctrlj, p2j), p2j, fromJ, toJ);
                                drawnRaw[j] = (p1j, newCtrl, p2j, isPortalJ, hasCurveJ, fromJ, toJ);
                            }
                            anyFlipped = true;
                        }
                    }
                }
            }

            var drawn = drawnRaw.Select(d => (d.P1, d.Ctrl, d.P2, d.IsPortal, d.HasCurve)).ToList();

            // ── Find pairwise crossings ───────────────────────────────────────────
            // Sample each path into a polyline so curves are tested accurately.
            const int CurveSamples = 32;
            var polylines = drawn.Select(d => SamplePath(d.P1, d.Ctrl, d.P2, d.HasCurve, CurveSamples)).ToList();

            var crossings = new List<(int I, int J, Point At, int SegI, int SegJ)>();
            for (int i = 0; i < drawn.Count; i++)
                for (int j = i + 1; j < drawn.Count; j++)
                    if (PolylinesIntersect(polylines[i], polylines[j], out Point pt, out int si, out int sj))
                        crossings.Add((i, j, pt, si, sj));

            // ── Determine over/under per crossing and build gap intervals ────────
            // "over" = shorter path (drawn on top). A path may be "over" in one crossing
            // and "under" in another, so we track draw-order separately from gaps.
            // overSet  = paths that are "over" in at least one crossing → drawn in second pass
            //            so they visually appear on top of any under-path they cross.
            // gapsByPath = gaps accumulated only for crossings where that path is "under".
            const double GapHalfPx = 7.0;
            var overSet    = new HashSet<int>();
            var gapsByPath = new Dictionary<int, List<(double Lo, double Hi)>>();

            foreach (var (i, j, at, si, sj) in crossings)
            {
                double lenI   = PolylineLength(polylines[i]);
                double lenJ   = PolylineLength(polylines[j]);
                bool   iIsOver = lenI <= lenJ;
                int    overIdx  = iIsOver ? i : j;
                int    underIdx = iIsOver ? j : i;
                int    segUnder = iIsOver ? sj : si;

                overSet.Add(overIdx); // remember for draw ordering

                if (!gapsByPath.TryGetValue(underIdx, out var gaps))
                    gapsByPath[underIdx] = gaps = [];

                var poly = polylines[underIdx];
                double arcTotal = PolylineLength(poly);
                if (arcTotal < 0.001) continue;

                // Arc length up to the crossing segment
                double arcBefore = 0;
                for (int s = 0; s < segUnder; s++)
                    arcBefore += PointDist(poly[s], poly[s + 1]);

                // Fraction along that segment via dot-product projection
                double segLen = PointDist(poly[segUnder], poly[segUnder + 1]);
                double fracOnSeg = 0;
                if (segLen > 0.001)
                {
                    double dx = poly[segUnder + 1].X - poly[segUnder].X;
                    double dy = poly[segUnder + 1].Y - poly[segUnder].Y;
                    double t  = ((at.X - poly[segUnder].X) * dx + (at.Y - poly[segUnder].Y) * dy) / (segLen * segLen);
                    fracOnSeg = Math.Clamp(t, 0, 1);
                }

                double arcAt = arcBefore + fracOnSeg * segLen;
                double tAt   = arcAt / arcTotal;
                double tGap  = GapHalfPx / arcTotal;
                gaps.Add((Math.Max(0, tAt - tGap), Math.Min(1, tAt + tGap)));
            }

            // ── Draw: paths never "over" first, then paths that are "over" in at least one
            //    crossing second (so they paint on top). Both passes respect each path's own
            //    gap list — a path that is "over" here but "under" elsewhere still gets its gap.
            for (int k = 0; k < drawn.Count; k++)
            {
                if (overSet.Contains(k)) continue; // handled in second pass
                var (p1, ctrl, p2, isPortal, hasCurve) = drawn[k];
                if (gapsByPath.TryGetValue(k, out var gaps2))
                    DrawConnectionPathWithGaps(dc, polylines[k], gaps2, isPortal);
                else
                    DrawConnectionPath(dc, p1, ctrl, p2, hasCurve, isPortal);
            }

            for (int k = 0; k < drawn.Count; k++)
            {
                if (!overSet.Contains(k)) continue;
                var (p1, ctrl, p2, isPortal, hasCurve) = drawn[k];
                if (gapsByPath.TryGetValue(k, out var gaps2))
                    DrawConnectionPathWithGaps(dc, polylines[k], gaps2, isPortal);
                else
                    DrawConnectionPath(dc, p1, ctrl, p2, hasCurve, isPortal);
            }
        }

        /// <summary>
        /// Draws a polyline path with a smooth fade-out/fade-in around each crossing gap.
        /// Outside gaps the line is drawn at full opacity; within the fade ramp the alpha
        /// is blended from full → 0 (fade out) then 0 → full (fade in).
        /// </summary>
        private static void DrawConnectionPathWithGaps(DrawingContext dc, Point[] poly, List<(double Lo, double Hi)> gaps, bool isPortal)
        {
            Color baseColor = isPortal ? PortalLineColor : DirectLineColor;
            double strokeW  = isPortal ? 2.0 : 3.0;

            double totalLen = PolylineLength(poly);
            if (totalLen < 0.001) return;

            const double FadeExtraPx  = 14.0;  // fade ramp length on each side of the gap
            const int    FadeSteps    = 50;     // micro-segments per fade ramp

            // Expand each gap into (fadeStart, gapLo, gapHi, fadeEnd) in arc-t space
            var zones = gaps.Select(g => (
                FadeStart : Math.Max(0, g.Lo - FadeExtraPx / totalLen),
                GapLo     : g.Lo,
                GapHi     : g.Hi,
                FadeEnd   : Math.Min(1, g.Hi + FadeExtraPx / totalLen)
            )).ToList();

            // Alpha at arc parameter t: 1 outside fades, 0 inside gap, smooth ramp in between
            double AlphaAt(double t)
            {
                double alpha = 1.0;
                foreach (var z in zones)
                {
                    if (t <= z.FadeStart || t >= z.FadeEnd) continue;
                    if (t >= z.GapLo && t <= z.GapHi) return 0.0;
                    // Fade-out ramp: FadeStart → GapLo
                    if (t < z.GapLo)
                    {
                        double ramp = (z.FadeStart < z.GapLo)
                            ? 1.0 - (t - z.FadeStart) / (z.GapLo - z.FadeStart)
                            : 0.0;
                        alpha = Math.Min(alpha, ramp);
                    }
                    // Fade-in ramp: GapHi → FadeEnd
                    else
                    {
                        double ramp = (z.GapHi < z.FadeEnd)
                            ? (t - z.GapHi) / (z.FadeEnd - z.GapHi)
                            : 1.0;
                        alpha = Math.Min(alpha, ramp);
                    }
                }
                return Math.Clamp(alpha, 0.0, 1.0);
            }

            Point EvalPoly(double t)
            {
                double target = t * totalLen;
                double arc = 0;
                for (int s = 0; s < poly.Length - 1; s++)
                {
                    double len = PointDist(poly[s], poly[s + 1]);
                    if (arc + len >= target || s == poly.Length - 2)
                    {
                        double frac = len > 0.001 ? (target - arc) / len : 0;
                        frac = Math.Clamp(frac, 0, 1);
                        return new Point(
                            poly[s].X + frac * (poly[s + 1].X - poly[s].X),
                            poly[s].Y + frac * (poly[s + 1].Y - poly[s].Y));
                    }
                    arc += len;
                }
                return poly[^1];
            }

            // Collect all t-events: path ends + fade zone boundaries + every polyline knot
            // The knot t-values ensure full-opacity sections are drawn segment-by-segment
            // along the actual sampled curve, not as a single flattened straight line.
            var tEvents = new SortedSet<double> { 0.0, 1.0 };
            foreach (var z in zones)
            {
                tEvents.Add(z.FadeStart); tEvents.Add(z.GapLo);
                tEvents.Add(z.GapHi);    tEvents.Add(z.FadeEnd);
            }
            // Add the arc-parameter of each polyline knot
            {
                double arc2 = 0;
                for (int s = 0; s < poly.Length - 1; s++)
                {
                    tEvents.Add(arc2 / totalLen);
                    arc2 += PointDist(poly[s], poly[s + 1]);
                }
                tEvents.Add(1.0);
            }

            var tList = tEvents.ToList();
            for (int seg = 0; seg < tList.Count - 1; seg++)
            {
                double tA = tList[seg], tB = tList[seg + 1];
                if (tB - tA < 1e-6) continue;

                double tMid = (tA + tB) / 2.0;
                double aMid = AlphaAt(tMid);
                if (aMid < 0.01) continue; // fully inside a gap — skip

                bool needsFade = zones.Any(z =>
                    tA < z.FadeEnd && tB > z.FadeStart); // segment overlaps any fade zone

                if (!needsFade)
                {
                    // Full-opacity segment — draw as one line
                    Color c = Color.FromArgb((byte)Math.Round(baseColor.A * aMid), baseColor.R, baseColor.G, baseColor.B);
                    dc.DrawLine(new Pen(new SolidColorBrush(c), strokeW), EvalPoly(tA), EvalPoly(tB));
                }
                else
                {
                    // Fade ramp — draw as micro-segments with per-step alpha
                    for (int step = 0; step < FadeSteps; step++)
                    {
                        double t0 = tA + (tB - tA) * step       / FadeSteps;
                        double t1 = tA + (tB - tA) * (step + 1) / FadeSteps;
                        double a  = AlphaAt((t0 + t1) / 2.0);
                        if (a < 0.01) continue;
                        Color c = Color.FromArgb((byte)Math.Round(baseColor.A * a), baseColor.R, baseColor.G, baseColor.B);
                        dc.DrawLine(new Pen(new SolidColorBrush(c), strokeW), EvalPoly(t0), EvalPoly(t1));
                    }
                }
            }
        }

        private static void DrawConnectionPath(DrawingContext dc, Point p1, Point ctrl, Point p2, bool hasCurve, bool isPortal)
        {
            Pen pen = isPortal
                ? new Pen(new SolidColorBrush(PortalLineColor), 2)
                : new Pen(new SolidColorBrush(DirectLineColor), 3);

            if (!hasCurve)
            {
                dc.DrawLine(pen, p1, p2);
                return;
            }

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(p1, false, false);
                ctx.QuadraticBezierTo(ctrl, p2, true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        /// <summary>Samples a straight or quadratic-Bézier path into a polyline.</summary>
        private static Point[] SamplePath(Point p1, Point ctrl, Point p2, bool hasCurve, int steps)
        {
            var pts = new Point[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                double t = (double)s / steps;
                if (hasCurve)
                {
                    // Quadratic Bézier: B(t) = (1-t)²·P1 + 2(1-t)t·Ctrl + t²·P2
                    double mt = 1.0 - t;
                    pts[s] = new Point(
                        mt * mt * p1.X + 2 * mt * t * ctrl.X + t * t * p2.X,
                        mt * mt * p1.Y + 2 * mt * t * ctrl.Y + t * t * p2.Y);
                }
                else
                {
                    pts[s] = new Point(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                }
            }
            return pts;
        }

        /// <summary>Returns true if two polylines have any intersecting segment pair, and sets the intersection point and the segment index on each polyline.</summary>
        private static bool PolylinesIntersect(Point[] a, Point[] b, out Point intersection, out int segA, out int segB)
        {
            intersection = default; segA = 0; segB = 0;
            for (int i = 0; i < a.Length - 1; i++)
                for (int j = 0; j < b.Length - 1; j++)
                    if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1], out intersection))
                    { segA = i; segB = j; return true; }
            return false;
        }

        private static double PolylineLength(Point[] pts)
        {
            double len = 0;
            for (int i = 0; i < pts.Length - 1; i++) len += PointDist(pts[i], pts[i + 1]);
            return len;
        }

        /// <summary>Returns true if segment p1→p2 intersects p3→p4, and sets <paramref name="intersection"/>.</summary>
        private static bool SegmentsIntersect(Point p1, Point p2, Point p3, Point p4, out Point intersection)
        {
            intersection = default;
            double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
            double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
            double cross = d1x * d2y - d1y * d2x;
            if (Math.Abs(cross) < 1e-8) return false; // parallel / collinear
            double t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / cross;
            double u = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / cross;
            // Exclude only exact endpoints (≤0 or ≥1) so crossings near segment junctions
            // are not silently dropped by an over-aggressive epsilon guard.
            if (t <= 0.0 || t >= 1.0 || u <= 0.0 || u >= 1.0) return false;
            intersection = new Point(p1.X + t * d1x, p1.Y + t * d1y);
            return true;
        }

        private static double PointDist(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
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
