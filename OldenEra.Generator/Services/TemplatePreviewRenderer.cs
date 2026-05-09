using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Cross-platform PNG preview renderer for <see cref="RmgTemplate"/>.
    /// Currently <see cref="RenderPng"/> is a stub; <see cref="ComputeLayout"/> is the
    /// fully ported pure-math layout engine from the WPF preview writer.
    /// </summary>
    public static class TemplatePreviewRenderer
    {
        public const int Width = 700;
        public const int Height = 700;

        // Cap for spoke/neutral/spawn zones; actual radius is computed per-layout.
        private const double ZoneRadiusMax = 38;
        // Hub zones always render at least this large so the "Hub" label is never clipped.
        private const double HubRadiusMin = 28;

        // Per-layout computed zone radius (set by LayoutZones)
        [ThreadStatic] private static double _zoneRadius;

        /// <summary>
        /// Renders a 700x700 PNG preview of the template. Returns the encoded PNG bytes.
        /// (Stub: returns a 1x1 transparent PNG until the full Skia renderer lands.)
        /// </summary>
        public static byte[] RenderPng(RmgTemplate template, MapTopology topology = MapTopology.Default)
        {
            using var bitmap = new SKBitmap(1, 1);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <summary>
        /// Returns the canvas position for each zone, keyed by zone name.
        /// </summary>
        public static IReadOnlyDictionary<string, (double X, double Y)> ComputeLayout(
            RmgTemplate template, MapTopology topology = MapTopology.Default)
        {
            Variant? variant = template.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? new List<Zone>();
            var result = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            if (zones.Count == 0) return result;

            var orderedZones = OrderZones(zones, variant?.Orientation?.ZeroAngleZone);
            var positions = LayoutZones(orderedZones, variant?.Connections ?? new List<Connection>(), topology);
            foreach (var kvp in positions)
                result[kvp.Key] = (kvp.Value.X, kvp.Value.Y);
            return result;
        }

        /// <summary>
        /// Returns the zone-circle radius used in the most recent <see cref="ComputeLayout"/> call
        /// on the current thread.
        /// </summary>
        public static double GetLastZoneRadius() => _zoneRadius;

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

        private static Dictionary<string, SKPoint> LayoutZones(List<Zone> zones, List<Connection> connections, MapTopology topology)
        {
            // Structured topologies use the simple ring layout; only Random uses the spring embedder.
            if (topology != MapTopology.Random)
                return LayoutZonesRing(zones, connections);

            int n = zones.Count;
            if (n == 0)
            {
                _zoneRadius = ZoneRadiusMax;
                return new Dictionary<string, SKPoint>(StringComparer.Ordinal);
            }
            if (n == 1)
            {
                _zoneRadius = ZoneRadiusMax;
                return new Dictionary<string, SKPoint>(StringComparer.Ordinal)
                    { [zones[0].Name] = new SKPoint(Width / 2.0f, Height / 2.0f) };
            }

            const double margin = 18;
            const double minGap = 6;

            var idx = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++) idx[zones[i].Name] = i;

            // ── If zones already carry generator-stamped positions, use them ──
            if (zones.All(z => z.GeneratorPosition.HasValue))
            {
                var gAdj = new HashSet<int>[n];
                for (int i = 0; i < n; i++) gAdj[i] = new HashSet<int>();
                foreach (var conn in connections)
                {
                    if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                    if (string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal)) continue;
                    if (!idx.TryGetValue(conn.From, out int ga)) continue;
                    if (!idx.TryGetValue(conn.To, out int gb)) continue;
                    gAdj[ga].Add(gb); gAdj[gb].Add(ga);
                }

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
                bool isTwoCluster = gComponents.Count == 2;

                double gZoneRadius;
                {
                    double ringRadius0 = (isTwoCluster ? Width / 4.0 : Width / 2.0) - margin;
                    double chord0 = 2.0 * ringRadius0 * Math.Sin(Math.PI / Math.Max(gMaxCompSize, 2));
                    gZoneRadius = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
                    gZoneRadius = Math.Max(gZoneRadius, 8.0);
                }
                _zoneRadius = gZoneRadius;
                double idealEdge2 = gZoneRadius * 3.2;

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

                double gScale = rawEdgeCount > 0
                    ? idealEdge2 / (rawEdgeSum / rawEdgeCount)
                    : Math.Min((Width - 2 * margin) / 1.0, (Height - 2 * margin) / 1.0);

                double canvasCx = Width / 2.0;
                double canvasCy = Height / 2.0;

                var gPx = new double[n];
                var gPy = new double[n];
                double gPad = gZoneRadius + margin;

                if (isTwoCluster)
                {
                    double halfDrawW = Width / 2.0 - gPad;
                    double drawH = Height - 2 * gPad;

                    foreach (var (compIdx, comp) in gComponents.Select((c, ci) => (ci, c)))
                    {
                        double cMinX = comp.Min(i => zones[i].GeneratorPosition!.Value.X);
                        double cMaxX = comp.Max(i => zones[i].GeneratorPosition!.Value.X);
                        double cMinY = comp.Min(i => zones[i].GeneratorPosition!.Value.Y);
                        double cMaxY = comp.Max(i => zones[i].GeneratorPosition!.Value.Y);
                        double cSpanX = Math.Max(cMaxX - cMinX, 0.001);
                        double cSpanY = Math.Max(cMaxY - cMinY, 0.001);

                        double scaleX = halfDrawW / cSpanX;
                        double scaleY = drawH / cSpanY;
                        double localScale = Math.Min(Math.Min(scaleX, scaleY), gScale);

                        double halfCx = compIdx == 0
                            ? gPad + halfDrawW / 2.0
                            : Width - gPad - halfDrawW / 2.0;
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
                }
                else
                {
                    double gCx = zones.Average(z => z.GeneratorPosition!.Value.X);
                    double gCy = zones.Average(z => z.GeneratorPosition!.Value.Y);

                    for (int i = 0; i < n; i++)
                    {
                        var gp = zones[i].GeneratorPosition!.Value;
                        gPx[i] = (gp.X - gCx) * gScale;
                        gPy[i] = (gp.Y - gCy) * gScale;
                    }

                    double rawMinX = gPx.Min(), rawMaxX = gPx.Max();
                    double rawMinY = gPy.Min(), rawMaxY = gPy.Max();
                    double drawW2 = Width - 2 * gPad;
                    double drawH2 = Height - 2 * gPad;
                    double fitScale = 1.0;
                    if (rawMaxX - rawMinX > drawW2 && rawMaxX - rawMinX > 0.001) fitScale = Math.Min(fitScale, drawW2 / (rawMaxX - rawMinX));
                    if (rawMaxY - rawMinY > drawH2 && rawMaxY - rawMinY > 0.001) fitScale = Math.Min(fitScale, drawH2 / (rawMaxY - rawMinY));

                    for (int i = 0; i < n; i++) { gPx[i] = canvasCx + gPx[i] * fitScale; gPy[i] = canvasCy + gPy[i] * fitScale; }
                }

                double gMinDist = gZoneRadius * 3.8;
                double gEdgeClear = gZoneRadius * 1.2;

                for (int abPass = 0; abPass < 500; abPass++)
                {
                    bool anyAB = false;

                    for (int i = 0; i < n; i++)
                        for (int j = i + 1; j < n; j++)
                        {
                            double dx = gPx[i] - gPx[j], dy = gPy[i] - gPy[j];
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d >= gMinDist) continue;
                            if (d < 0.001) { dx = 1; dy = 0; d = 0.001; }
                            double push = (gMinDist - d) / 2.0;
                            gPx[i] += dx / d * push; gPy[i] += dy / d * push;
                            gPx[j] -= dx / d * push; gPy[j] -= dy / d * push;
                            anyAB = true;
                        }

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
                                else { gPx[c2] = cx2A; gPy[c2] = cy2A; }
                                anyAB = true;
                            }
                        }

                    if (!anyAB) break;
                }

                {
                    double finalMinX = gPx.Min(), finalMaxX = gPx.Max();
                    double finalMinY = gPy.Min(), finalMaxY = gPy.Max();
                    double finalCx = (finalMinX + finalMaxX) / 2.0;
                    double finalCy = (finalMinY + finalMaxY) / 2.0;
                    for (int i = 0; i < n; i++) { gPx[i] += canvasCx - finalCx; gPy[i] += canvasCy - finalCy; }

                    finalMinX = gPx.Min(); finalMaxX = gPx.Max();
                    finalMinY = gPy.Min(); finalMaxY = gPy.Max();
                    double spanX = finalMaxX - finalMinX, spanY = finalMaxY - finalMinY;
                    double allowW = Width - 2 * gPad, allowH = Height - 2 * gPad;
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
                        _zoneRadius = Math.Max(gZoneRadius * shrink, 8.0);
                    }
                }

                var gResult = new Dictionary<string, SKPoint>(StringComparer.Ordinal);
                for (int i = 0; i < n; i++)
                    gResult[zones[i].Name] = new SKPoint((float)gPx[i], (float)gPy[i]);
                return gResult;
            }

            // ── Otherwise: Fruchterman-Reingold spring embedder per component ──
            var adj = new HashSet<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new HashSet<int>();
            foreach (var conn in connections)
            {
                if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                if (string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal)) continue;
                if (!idx.TryGetValue(conn.From, out int a)) continue;
                if (!idx.TryGetValue(conn.To, out int b)) continue;
                adj[a].Add(b);
                adj[b].Add(a);
            }

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

            int maxCompSize = components.Max(c => c.Count);
            double zoneRadius;
            if (maxCompSize <= 1)
            {
                zoneRadius = ZoneRadiusMax;
            }
            else
            {
                double ringRadius0 = Width / 2.0 - margin;
                double chord0 = 2.0 * ringRadius0 * Math.Sin(Math.PI / maxCompSize);
                zoneRadius = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
                zoneRadius = Math.Max(zoneRadius, 8.0);
            }
            _zoneRadius = zoneRadius;

            double idealEdge = zoneRadius * 3.2;

            var compPx = new double[numComps][];
            var compPy = new double[numComps][];

            for (int c = 0; c < numComps; c++)
            {
                var comp = components[c];
                int cn = comp.Count;
                var lpx = new double[cn];
                var lpy = new double[cn];

                var localIdx = new Dictionary<int, int>();
                for (int i = 0; i < cn; i++) localIdx[comp[i]] = i;

                if (cn == 1) { compPx[c] = lpx; compPy[c] = lpy; continue; }

                var ladj = new HashSet<int>[cn];
                for (int i = 0; i < cn; i++) ladj[i] = new HashSet<int>();
                for (int i = 0; i < cn; i++)
                    foreach (int gv in adj[comp[i]])
                        if (localIdx.TryGetValue(gv, out int lv))
                        { ladj[i].Add(lv); ladj[lv].Add(i); }

                if (cn == 2)
                {
                    lpx[0] = -idealEdge / 2; lpy[0] = 0;
                    lpx[1] = idealEdge / 2; lpy[1] = 0;
                    compPx[c] = lpx; compPy[c] = lpy; continue;
                }

                double seedR = idealEdge * cn / (2.0 * Math.PI);
                seedR = Math.Max(seedR, idealEdge);
                for (int i = 0; i < cn; i++)
                {
                    double ang = -Math.PI / 2.0 + i * 2.0 * Math.PI / cn;
                    lpx[i] = Math.Cos(ang) * seedR;
                    lpy[i] = Math.Sin(ang) * seedR;
                }

                double k = idealEdge;
                double t = k * 2.5;
                double tMin = k * 0.005;
                int frIter = 400;
                double cool = Math.Pow(tMin / t, 1.0 / frIter);

                var fx = new double[cn];
                var fy = new double[cn];
                for (int iter = 0; iter < frIter; iter++)
                {
                    Array.Clear(fx, 0, cn);
                    Array.Clear(fy, 0, cn);

                    for (int i = 0; i < cn; i++)
                        for (int j = i + 1; j < cn; j++)
                        {
                            double dx = lpx[i] - lpx[j];
                            double dy = lpy[i] - lpy[j];
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d < 0.001) { dx = 0.5 + i * 0.1; dy = 0.5 + j * 0.1; d = Math.Sqrt(dx * dx + dy * dy); }
                            double fr = k * k / d;
                            fx[i] += fr * dx / d; fy[i] += fr * dy / d;
                            fx[j] -= fr * dx / d; fy[j] -= fr * dy / d;
                        }

                    for (int i = 0; i < cn; i++)
                        foreach (int j in ladj[i])
                        {
                            if (j <= i) continue;
                            double dx = lpx[i] - lpx[j];
                            double dy = lpy[i] - lpy[j];
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d < 0.001) continue;
                            double fa = d * d / k;
                            fx[i] -= fa * dx / d; fy[i] -= fa * dy / d;
                            fx[j] += fa * dx / d; fy[j] += fa * dy / d;
                        }

                    for (int i = 0; i < cn; i++)
                    {
                        double len = Math.Sqrt(fx[i] * fx[i] + fy[i] * fy[i]);
                        if (len > t && len > 0.001) { fx[i] = fx[i] / len * t; fy[i] = fy[i] / len * t; }
                        lpx[i] += fx[i];
                        lpy[i] += fy[i];
                    }
                    t = Math.Max(t * cool, tMin);
                }

                double minDist = zoneRadius * 3.8;
                double edgeClear = zoneRadius * 1.2;

                for (int abPass = 0; abPass < 500; abPass++)
                {
                    bool anyAB = false;

                    for (int i = 0; i < cn; i++)
                        for (int j = i + 1; j < cn; j++)
                        {
                            double dx = lpx[i] - lpx[j], dy = lpy[i] - lpy[j];
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d >= minDist) continue;
                            if (d < 0.001) { dx = 1; dy = 0; d = 0.001; }
                            double push = (minDist - d) / 2.0;
                            lpx[i] += dx / d * push; lpy[i] += dy / d * push;
                            lpx[j] -= dx / d * push; lpy[j] -= dy / d * push;
                            anyAB = true;
                        }

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
                                double nx2 = lpx[c2] - projX;
                                double ny2 = lpy[c2] - projY;
                                double dist = Math.Sqrt(nx2 * nx2 + ny2 * ny2);
                                if (dist >= edgeClear) continue;

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

                                if (scoreB < scoreA)
                                { lpx[c2] = cx2B; lpy[c2] = cy2B; }
                                else
                                { lpx[c2] = cx2A; lpy[c2] = cy2A; }

                                anyAB = true;
                            }
                        }

                    if (!anyAB) break;
                }

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
                                double nx2 = lpx[c2] - projX;
                                double ny2 = lpy[c2] - projY;
                                double dist = Math.Sqrt(nx2 * nx2 + ny2 * ny2);
                                if (dist >= edgeClear) continue;

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

                    for (int i = 0; i < cn; i++)
                        for (int j = i + 1; j < cn; j++)
                        {
                            double dx = lpx[i] - lpx[j], dy = lpy[i] - lpy[j];
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d >= minDist) continue;
                            if (d < 0.001) { dx = 1; dy = 0; d = 0.001; }
                            double push = (minDist - d) / 2.0;
                            lpx[i] += dx / d * push; lpy[i] += dy / d * push;
                            lpx[j] -= dx / d * push; lpy[j] -= dy / d * push;
                        }
                }
                compPx[c] = lpx;
                compPy[c] = lpy;
            }

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

            var positions = new Dictionary<string, SKPoint>(StringComparer.Ordinal);

            if (numComps == 1)
            {
                var comp = components[0];
                double spanX = Math.Max(compMaxX[0] - compMinX[0], 1);
                double spanY = Math.Max(compMaxY[0] - compMinY[0], 1);
                double drawW = Width - 2 * pad;
                double drawH = Height - 2 * pad;
                double scale = Math.Min(drawW / spanX, drawH / spanY);
                double offX = pad + (drawW - spanX * scale) / 2.0;
                double offY = pad + (drawH - spanY * scale) / 2.0;
                for (int i = 0; i < comp.Count; i++)
                    positions[zones[comp[i]].Name] = new SKPoint(
                        (float)(offX + (compPx[0][i] - compMinX[0]) * scale),
                        (float)(offY + (compPy[0][i] - compMinY[0]) * scale));
            }
            else
            {
                bool stackVertical = numComps == 2 && Height >= Width;

                double interGap = zoneRadius * 3.0;
                double totalDraw = (stackVertical ? Height : Width) - 2 * pad - interGap * (numComps - 1);
                double slotSize = totalDraw / numComps;
                double crossDraw = (stackVertical ? Width : Height) - 2 * pad;

                double uniformScale = double.MaxValue;
                for (int c = 0; c < numComps; c++)
                {
                    double spanMain = stackVertical
                        ? compMaxY[c] - compMinY[c]
                        : compMaxX[c] - compMinX[c];
                    double spanCross = stackVertical
                        ? compMaxX[c] - compMinX[c]
                        : compMaxY[c] - compMinY[c];
                    if (spanMain > 0.001) uniformScale = Math.Min(uniformScale, (slotSize - 2 * zoneRadius) / spanMain);
                    if (spanCross > 0.001) uniformScale = Math.Min(uniformScale, crossDraw / spanCross);
                }
                if (uniformScale >= double.MaxValue / 2) uniformScale = 1.0;
                uniformScale = Math.Max(uniformScale, 0.1);

                double cursor = pad;
                for (int c = 0; c < numComps; c++)
                {
                    var comp = components[c];
                    double spanMain = stackVertical
                        ? compMaxY[c] - compMinY[c]
                        : compMaxX[c] - compMinX[c];
                    double spanCross = stackVertical
                        ? compMaxX[c] - compMinX[c]
                        : compMaxY[c] - compMinY[c];

                    double mainExtent = spanMain * uniformScale;
                    double crossExtent = spanCross * uniformScale;

                    double mainStart = cursor + (slotSize - mainExtent) / 2.0;
                    double crossStart = pad + (crossDraw - crossExtent) / 2.0;

                    for (int i = 0; i < comp.Count; i++)
                    {
                        double mainVal = stackVertical ? compPy[c][i] : compPx[c][i];
                        double crossVal = stackVertical ? compPx[c][i] : compPy[c][i];
                        double mainOff = stackVertical ? compMinY[c] : compMinX[c];
                        double crossOff = stackVertical ? compMinX[c] : compMinY[c];

                        double px2 = stackVertical
                            ? crossStart + (crossVal - crossOff) * uniformScale
                            : mainStart + (mainVal - mainOff) * uniformScale;
                        double py2 = stackVertical
                            ? mainStart + (mainVal - mainOff) * uniformScale
                            : crossStart + (crossVal - crossOff) * uniformScale;

                        positions[zones[comp[i]].Name] = new SKPoint((float)px2, (float)py2);
                    }

                    cursor += slotSize + interGap;
                }
            }

            return positions;
        }

        /// <summary>
        /// Classic ring layout for structured topologies. Hub zone (if present) goes centre.
        /// Multiple Hub-* zones trigger the multi-hub cluster layout.
        /// </summary>
        private static Dictionary<string, SKPoint> LayoutZonesRing(List<Zone> zones, List<Connection> connections)
        {
            var positions = new Dictionary<string, SKPoint>(StringComparer.Ordinal);
            int n = zones.Count;
            if (n == 0)
            {
                _zoneRadius = ZoneRadiusMax;
                return positions;
            }

            var multiHubs = zones
                .Where(z => z.Name.StartsWith("Hub-", StringComparison.Ordinal))
                .ToList();

            if (multiHubs.Count >= 2)
                return LayoutZonesMultiHub(zones, connections, multiHubs);

            const double margin = 18;
            double ringRadius0 = Width / 2.0 - margin;
            double chord0 = 2.0 * ringRadius0 * Math.Sin(Math.PI / Math.Max(1, n));
            const double minGap = 6;
            double zoneRadius = Math.Min(ZoneRadiusMax, (chord0 - minGap) / 2.0);
            _zoneRadius = zoneRadius;

            Zone? hub = zones.FirstOrDefault(z => string.Equals(z.Name, "Hub", StringComparison.Ordinal));
            var outer = hub is null ? zones : zones.Where(z => z != hub).ToList();
            int outerN = Math.Max(1, outer.Count);

            double ringRadius = Math.Max(
                HubRadiusMin + zoneRadius + minGap,
                Math.Min(ringRadius0, Width / 2.0 - zoneRadius - margin));
            var center = new SKPoint(Width / 2.0f, Height / 2.0f);

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
                positions[outer[i].Name] = new SKPoint(
                    (float)(center.X + Math.Cos(angle) * ringRadius),
                    (float)(center.Y + Math.Sin(angle) * ringRadius));
            }

            return positions;
        }

        /// <summary>
        /// Multi-hub cluster layout (tournament hub-and-spoke). Each Hub-* sits at the centre
        /// of its cluster with its direct neighbours (spokes) ringed around it.
        /// </summary>
        private static Dictionary<string, SKPoint> LayoutZonesMultiHub(
            List<Zone> zones,
            List<Connection> connections,
            List<Zone> multiHubs)
        {
            var positions = new Dictionary<string, SKPoint>(StringComparer.Ordinal);
            const double margin = 18;
            const double minGap = 6;

            var hubSpokes = new Dictionary<string, List<Zone>>(StringComparer.Ordinal);
            var zoneByName = zones.ToDictionary(z => z.Name, StringComparer.Ordinal);

            foreach (var hub in multiHubs)
            {
                var spokes = connections
                    .Where(c => !string.Equals(c.ConnectionType, "Proximity", StringComparison.Ordinal)
                             && !string.Equals(c.ConnectionType, "Portal", StringComparison.Ordinal))
                    .Select(c => string.Equals(c.From, hub.Name, StringComparison.Ordinal) ? c.To : (
                                 string.Equals(c.To, hub.Name, StringComparison.Ordinal) ? c.From : null))
                    .Where(name => name != null && zoneByName.ContainsKey(name!))
                    .Select(name => zoneByName[name!])
                    .Distinct()
                    .ToList();
                hubSpokes[hub.Name] = spokes;
            }

            int maxSpokes = hubSpokes.Values.Max(s => s.Count);
            int numHubs = multiHubs.Count;

            double canvasHalf = Width / 2.0 - margin;
            double sinB = numHubs > 1 ? Math.Sin(Math.PI / numHubs) : 0.0;
            double sinA = maxSpokes > 1 ? Math.Sin(Math.PI / maxSpokes) : 1.0;

            double hubRingRadius = numHubs > 1
                ? (canvasHalf + minGap / 2.0) / (1.0 + sinB)
                : 0.0;

            double radialLeft = canvasHalf - hubRingRadius;
            double minSpokeR = HubRadiusMin + minGap;
            double zoneRadius = Math.Min(ZoneRadiusMax, (radialLeft * sinA - minGap / 2.0) / (1.0 + sinA));
            zoneRadius = Math.Max(1.0, zoneRadius);
            double spokeRingRadius = Math.Max(radialLeft - zoneRadius, minSpokeR + zoneRadius);

            _zoneRadius = zoneRadius;

            var canvasCenter = new SKPoint(Width / 2.0f, Height / 2.0f);

            for (int h = 0; h < numHubs; h++)
            {
                var hub = multiHubs[h];

                double hubAngle = -Math.PI / 2.0 + h * Math.PI * 2.0 / numHubs;
                var hubPos = numHubs == 1
                    ? canvasCenter
                    : new SKPoint(
                        (float)(canvasCenter.X + Math.Cos(hubAngle) * hubRingRadius),
                        (float)(canvasCenter.Y + Math.Sin(hubAngle) * hubRingRadius));

                positions[hub.Name] = hubPos;

                var spokes = hubSpokes[hub.Name];
                int s = spokes.Count;
                if (s == 0) continue;

                double spokeBaseAngle = numHubs == 1 ? -Math.PI / 2.0 : hubAngle;
                for (int i = 0; i < s; i++)
                {
                    double spokeAngle = spokeBaseAngle + i * Math.PI * 2.0 / s;
                    positions[spokes[i].Name] = new SKPoint(
                        (float)(hubPos.X + Math.Cos(spokeAngle) * spokeRingRadius),
                        (float)(hubPos.Y + Math.Sin(spokeAngle) * spokeRingRadius));
                }
            }

            foreach (var zone in zones)
            {
                if (!positions.ContainsKey(zone.Name))
                    positions[zone.Name] = canvasCenter;
            }

            return positions;
        }
    }
}
