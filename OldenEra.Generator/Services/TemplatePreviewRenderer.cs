using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        // ── Colours — Olden Era parchment skin ───────────────────────────────────
        // Parchment gradient stops
        private static readonly SKColor ParchmentLight = new SKColor(0xE7, 0xD6, 0xA8);
        private static readonly SKColor ParchmentMid   = new SKColor(0xCD, 0xB6, 0x85);
        private static readonly SKColor ParchmentDark  = new SKColor(0x9C, 0x87, 0x57);
        private static readonly SKColor StainColor     = new SKColor(0x3A, 0x20, 0x10);
        private static readonly SKColor InkBrown       = new SKColor(0x2A, 0x1A, 0x0A);
        private static readonly SKColor FrameOuter     = new SKColor(0x8F, 0x73, 0x3F);
        private static readonly SKColor FrameInner     = new SKColor(0x5A, 0x3A, 0x14);

        // Coin
        private static readonly SKColor CoinFillCenter = new SKColor(0x7A, 0x55, 0x30);
        private static readonly SKColor CoinFillEdge   = new SKColor(0x1F, 0x13, 0x0A);
        private static readonly SKColor CoinRimDark    = new SKColor(0x5A, 0x3A, 0x1A);
        private static readonly SKColor CoinInnerRing  = new SKColor(0x1C, 0x0F, 0x06);

        // Rim accents (top stop of the rim gradient)
        private static readonly SKColor RimSpawn       = new SKColor(0xD8, 0xA8, 0x5A);
        private static readonly SKColor RimHub         = new SKColor(0xCF, 0xA8, 0x6B);
        private static readonly SKColor RimBronze      = new SKColor(0xCD, 0x7F, 0x32);
        private static readonly SKColor RimSilver      = new SKColor(0xC0, 0xC0, 0xC0);
        private static readonly SKColor RimGold        = new SKColor(0xFF, 0xD2, 0x32);
        private static readonly SKColor HoldCityGold   = new SKColor(0xFF, 0xD7, 0x00);

        // Numeral / icon ink
        private static readonly SKColor NumeralCream   = new SKColor(0xF1, 0xD9, 0x90);

        // Connection lines
        private static readonly SKColor DirectLineColor = new SKColor(0x2A, 0x1A, 0x0A);
        private static readonly SKColor PortalLineColor = new SKColor(0x2C, 0x4F, 0x6A, 200);

        // Lazy-loaded serif typeface (Cinzel, embedded)
        private static SKTypeface? _serifTypeface;
        private static SKTypeface SerifTypeface
        {
            get
            {
                if (_serifTypeface != null) return _serifTypeface;
                try
                {
                    var asm = typeof(TemplatePreviewRenderer).Assembly;
                    using var stream = asm.GetManifestResourceStream("OldenEra.Generator.Resources.Cinzel.ttf");
                    if (stream != null)
                    {
                        _serifTypeface = SKTypeface.FromStream(stream);
                    }
                }
                catch
                {
                    // fall through to default
                }
                _serifTypeface ??= SKTypeface.Default;
                return _serifTypeface;
            }
        }

        /// <summary>
        /// Renders a 700x700 PNG preview of the template. Returns the encoded PNG bytes.
        /// </summary>
        public static byte[] RenderPng(RmgTemplate template, MapTopology topology = MapTopology.Default)
        {
            var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            DrawPreview(canvas, template, topology);
            canvas.Flush();
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        // ── Main draw ────────────────────────────────────────────────────────────
        private static void DrawPreview(SKCanvas canvas, RmgTemplate template, MapTopology topology)
        {
            int seed = (template.Name ?? string.Empty).GetHashCode();
            DrawParchmentBackground(canvas, seed);
            DrawCompassRose(canvas);
            DrawFrame(canvas);

            Variant? variant = template.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? new List<Zone>();
            if (zones.Count == 0)
            {
                using var font = new SKFont(SerifTypeface, 24f) { Edging = SKFontEdging.Antialias, Subpixel = true };
                using var paint = new SKPaint { Color = InkBrown, IsAntialias = true };
                DrawText(canvas, template.Name ?? string.Empty, Width / 2f, Height / 2f, font, paint, centered: true);
                return;
            }

            var orderedZones = OrderZones(zones, variant?.Orientation?.ZeroAngleZone);
            var connections = variant?.Connections ?? new List<Connection>();
            var positions = LayoutZones(orderedZones, connections, topology);

            // Connections (under zones)
            DrawConnections(canvas, connections, positions, _zoneRadius);

            // Non-spawn zones first, spawn on top
            foreach (Zone zone in orderedZones.Where(z => !z.Name.StartsWith("Spawn-", StringComparison.Ordinal)))
                DrawZone(canvas, zone, positions[zone.Name]);
            foreach (Zone zone in orderedZones.Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal)))
                DrawZone(canvas, zone, positions[zone.Name]);
        }

        // ── Background, frame, compass ───────────────────────────────────────────

        private static void DrawParchmentBackground(SKCanvas canvas, int seed)
        {
            // Base radial gradient — slightly above center to feel like top-lit parchment
            var center = new SKPoint(Width / 2f, Height * 0.45f);
            float radius = (float)Math.Sqrt(Width * Width + Height * Height) / 2f;
            using (var shader = SKShader.CreateRadialGradient(
                center, radius,
                new[] { ParchmentLight, ParchmentMid, ParchmentDark },
                new[] { 0f, 0.6f, 1f },
                SKShaderTileMode.Clamp))
            using (var paint = new SKPaint { Shader = shader, IsAntialias = true })
            {
                canvas.DrawRect(new SKRect(0, 0, Width, Height), paint);
            }

            // Stains — deterministic from seed
            var rng = new Random(seed == 0 ? 1 : seed);
            int stainCount = 8;
            for (int i = 0; i < stainCount; i++)
            {
                float cx = (float)(rng.NextDouble() * Width);
                float cy = (float)(rng.NextDouble() * Height);
                float r  = 60f + (float)(rng.NextDouble() * 80f);
                byte alpha = (byte)(20 + rng.Next(70)); // 20..89
                using var paint = new SKPaint
                {
                    Color = StainColor.WithAlpha(alpha),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 28f),
                };
                canvas.DrawCircle(cx, cy, r, paint);
            }

            // Vignette — transparent center to dark edges
            using (var vig = SKShader.CreateRadialGradient(
                new SKPoint(Width / 2f, Height / 2f),
                radius,
                new[] { new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 115) },
                new[] { 0f, 0.6f, 1f },
                SKShaderTileMode.Clamp))
            using (var paint = new SKPaint { Shader = vig, IsAntialias = true })
            {
                canvas.DrawRect(new SKRect(0, 0, Width, Height), paint);
            }
        }

        private static void DrawFrame(SKCanvas canvas)
        {
            using (var p = new SKPaint
            {
                Color = FrameOuter, IsAntialias = true,
                Style = SKPaintStyle.Stroke, StrokeWidth = 1.0f,
            })
                canvas.DrawRect(new SKRect(8, 8, Width - 8, Height - 8), p);

            using (var p = new SKPaint
            {
                Color = FrameInner, IsAntialias = true,
                Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f,
            })
                canvas.DrawRect(new SKRect(22, 22, Width - 22, Height - 22), p);

            using (var p = new SKPaint
            {
                Color = FrameInner, IsAntialias = true,
                Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f,
            })
                canvas.DrawRect(new SKRect(30, 30, Width - 30, Height - 30), p);
        }

        private static void DrawCompassRose(SKCanvas canvas)
        {
            float cx = Width - 90f;
            float cy = 90f;
            float r  = 38f;
            byte a   = 64; // ~25%

            using (var p = new SKPaint
            {
                Color = StainColor.WithAlpha(a),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
            })
                canvas.DrawCircle(cx, cy, r, p);

            using (var p = new SKPaint
            {
                Color = StainColor.WithAlpha(a),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.6f,
            })
                canvas.DrawCircle(cx, cy, r * 0.7f, p);

            // Vertical N-S diamond
            using (var p = new SKPaint { Color = StainColor.WithAlpha(140), IsAntialias = true, Style = SKPaintStyle.Fill })
            using (var path = new SKPath())
            {
                path.MoveTo(cx, cy - r * 0.95f);
                path.LineTo(cx + 4, cy);
                path.LineTo(cx, cy + r * 0.95f);
                path.LineTo(cx - 4, cy);
                path.Close();
                canvas.DrawPath(path, p);
            }

            // Horizontal E-W diamond
            using (var p = new SKPaint { Color = StainColor.WithAlpha(100), IsAntialias = true, Style = SKPaintStyle.Fill })
            using (var path = new SKPath())
            {
                path.MoveTo(cx - r * 0.95f, cy);
                path.LineTo(cx, cy - 4);
                path.LineTo(cx + r * 0.95f, cy);
                path.LineTo(cx, cy + 4);
                path.Close();
                canvas.DrawPath(path, p);
            }

            // "N" glyph above outer ring
            using (var font = new SKFont(SerifTypeface, 13f) { Edging = SKFontEdging.Antialias, Subpixel = true })
            using (var p = new SKPaint { Color = StainColor.WithAlpha(160), IsAntialias = true })
            {
                DrawText(canvas, "N", cx, cy - r - 6, font, p, centered: true);
            }
        }

        // ── Coin token primitive ─────────────────────────────────────────────────

        private static void DrawCoin(SKCanvas canvas, SKPoint center, float radius,
                                     SKColor rimTopColor, float rimWidth)
        {
            // Drop shadow
            using (var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 90),
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.8f),
            })
            {
                canvas.DrawOval(new SKRect(center.X - radius * 0.95f, center.Y + radius - 3,
                                           center.X + radius * 0.95f, center.Y + radius + 3), shadowPaint);
            }

            // Base radial-fill: 35% offset from center for the highlight
            var fillCenter = new SKPoint(center.X - radius * 0.30f, center.Y - radius * 0.35f);
            using (var shader = SKShader.CreateRadialGradient(
                fillCenter, radius * 1.2f,
                new[] { CoinFillCenter, CoinFillEdge },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp))
            using (var paint = new SKPaint { Shader = shader, IsAntialias = true, Style = SKPaintStyle.Fill })
            {
                canvas.DrawCircle(center, radius, paint);
            }

            // Inner engraved ring
            using (var inner = new SKPaint
            {
                Color = CoinInnerRing.WithAlpha(150),
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.8f,
            })
                canvas.DrawCircle(center, radius * 0.77f, inner);

            // Rim — vertical linear gradient (rimTopColor → CoinRimDark)
            using (var rimShader = SKShader.CreateLinearGradient(
                new SKPoint(center.X, center.Y - radius),
                new SKPoint(center.X, center.Y + radius),
                new[] { rimTopColor, CoinRimDark },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp))
            using (var rim = new SKPaint
            {
                Shader = rimShader,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = rimWidth,
            })
                canvas.DrawCircle(center, radius, rim);
        }

        // ── Connections ──────────────────────────────────────────────────────────

        private static void DrawConnections(SKCanvas canvas, List<Connection> connections,
            Dictionary<string, SKPoint> positions, double zoneRadius)
        {
            double r = 0;
            double curveThreshold = zoneRadius * 1.3;
            double curveOffset    = zoneRadius * 2.0;

            const int    ArcSamples   = 32;
            const int    MaxArcIter   = 20;
            const double ArcClearance = 6.0;

            SKPoint RefineCtrl(SKPoint p1, SKPoint ctrl, SKPoint p2, string fromName, string toName)
            {
                for (int arcIter = 0; arcIter < MaxArcIter; arcIter++)
                {
                    bool arcClean = true;
                    SKPoint? worstZone = null;
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
                    ctrl = new SKPoint((float)(ctrl.X + nx3 * nudge), (float)(ctrl.Y + ny3 * nudge));
                }
                return ctrl;
            }

            var drawnRaw = new List<(SKPoint P1, SKPoint Ctrl, SKPoint P2, bool IsPortal, bool HasCurve, string From, string To)>();

            foreach (Connection conn in connections)
            {
                if (string.Equals(conn.ConnectionType, "Proximity", StringComparison.Ordinal)) continue;
                if (!positions.TryGetValue(conn.From, out SKPoint from)) continue;
                if (!positions.TryGetValue(conn.To,   out SKPoint to))   continue;

                bool isPortal = string.Equals(conn.ConnectionType, "Portal", StringComparison.Ordinal);

                double dx = to.X - from.X, dy = to.Y - from.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1) continue;
                double ux = dx / dist, uy = dy / dist;

                SKPoint p1 = new SKPoint((float)(from.X + ux * r), (float)(from.Y + uy * r));
                SKPoint p2 = new SKPoint((float)(to.X   - ux * r), (float)(to.Y   - uy * r));

                double bestPerp  = curveThreshold;
                double bestT     = 0.5;
                SKPoint? blockZone = null;

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

                bool hasCurve = blockZone.HasValue;
                SKPoint ctrl;
                if (hasCurve)
                {
                    SKPoint mid = new SKPoint((float)(from.X + bestT * dx), (float)(from.Y + bestT * dy));
                    double px = mid.X - blockZone!.Value.X;
                    double py = mid.Y - blockZone!.Value.Y;
                    double pl = Math.Sqrt(px * px + py * py);
                    if (pl < 0.001) { px = -uy; py = ux; pl = 1; }
                    ctrl = new SKPoint((float)(mid.X + px / pl * curveOffset), (float)(mid.Y + py / pl * curveOffset));
                    ctrl = RefineCtrl(p1, ctrl, p2, conn.From, conn.To);
                }
                else
                {
                    ctrl = new SKPoint((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f);
                }

                drawnRaw.Add((p1, ctrl, p2, isPortal, hasCurve, conn.From, conn.To));
            }

            // Shared-endpoint untangling
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

                            double DeflectionSq(SKPoint a, SKPoint ctrl0, SKPoint b)
                            {
                                double mx = (a.X + b.X) / 2, my = (a.Y + b.Y) / 2;
                                double ox = ctrl0.X - mx, oy = ctrl0.Y - my;
                                return ox * ox + oy * oy;
                            }

                            SKPoint FlipCtrl(SKPoint a, SKPoint ctrl0, SKPoint b)
                            {
                                double chx = b.X - a.X, chy = b.Y - a.Y;
                                double len2 = chx * chx + chy * chy;
                                if (len2 < 0.001) return ctrl0;
                                double t2 = ((ctrl0.X - a.X) * chx + (ctrl0.Y - a.Y) * chy) / len2;
                                double footX = a.X + t2 * chx, footY = a.Y + t2 * chy;
                                return new SKPoint((float)(2 * footX - ctrl0.X), (float)(2 * footY - ctrl0.Y));
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

            const int CurveSamples = 32;
            var polylines = drawn.Select(d => SamplePath(d.P1, d.Ctrl, d.P2, d.HasCurve, CurveSamples)).ToList();

            var crossings = new List<(int I, int J, SKPoint At, int SegI, int SegJ)>();
            for (int i = 0; i < drawn.Count; i++)
                for (int j = i + 1; j < drawn.Count; j++)
                    if (PolylinesIntersect(polylines[i], polylines[j], out SKPoint pt, out int si, out int sj))
                        crossings.Add((i, j, pt, si, sj));

            const double GapHalfPx = 7.0;
            var overSet    = new HashSet<int>();
            var gapsByPath = new Dictionary<int, List<(double Lo, double Hi)>>();

            foreach (var (i, j, at, si, sj) in crossings)
            {
                double lenI = PolylineLength(polylines[i]);
                double lenJ = PolylineLength(polylines[j]);
                bool iIsOver = lenI <= lenJ;
                int overIdx  = iIsOver ? i : j;
                int underIdx = iIsOver ? j : i;
                int segUnder = iIsOver ? sj : si;

                overSet.Add(overIdx);

                if (!gapsByPath.TryGetValue(underIdx, out var gaps))
                    gapsByPath[underIdx] = gaps = new List<(double, double)>();

                var poly = polylines[underIdx];
                double arcTotal = PolylineLength(poly);
                if (arcTotal < 0.001) continue;

                double arcBefore = 0;
                for (int s = 0; s < segUnder; s++)
                    arcBefore += PointDist(poly[s], poly[s + 1]);

                double segLen = PointDist(poly[segUnder], poly[segUnder + 1]);
                double fracOnSeg = 0;
                if (segLen > 0.001)
                {
                    double dx = poly[segUnder + 1].X - poly[segUnder].X;
                    double dy = poly[segUnder + 1].Y - poly[segUnder].Y;
                    double t = ((at.X - poly[segUnder].X) * dx + (at.Y - poly[segUnder].Y) * dy) / (segLen * segLen);
                    fracOnSeg = Math.Clamp(t, 0, 1);
                }

                double arcAt = arcBefore + fracOnSeg * segLen;
                double tAt   = arcAt / arcTotal;
                double tGap  = GapHalfPx / arcTotal;
                gaps.Add((Math.Max(0, tAt - tGap), Math.Min(1, tAt + tGap)));
            }

            // First pass: paths never "over"
            for (int k = 0; k < drawn.Count; k++)
            {
                if (overSet.Contains(k)) continue;
                var (p1, ctrl, p2, isPortal, hasCurve) = drawn[k];
                if (gapsByPath.TryGetValue(k, out var gaps2))
                    DrawConnectionPathWithGaps(canvas, polylines[k], gaps2, isPortal);
                else
                    DrawConnectionPath(canvas, p1, ctrl, p2, hasCurve, isPortal);
            }

            // Second pass: "over" paths
            for (int k = 0; k < drawn.Count; k++)
            {
                if (!overSet.Contains(k)) continue;
                var (p1, ctrl, p2, isPortal, hasCurve) = drawn[k];
                if (gapsByPath.TryGetValue(k, out var gaps2))
                    DrawConnectionPathWithGaps(canvas, polylines[k], gaps2, isPortal);
                else
                    DrawConnectionPath(canvas, p1, ctrl, p2, hasCurve, isPortal);
            }
        }

        private static void DrawConnectionPathWithGaps(SKCanvas canvas, SKPoint[] poly,
            List<(double Lo, double Hi)> gaps, bool isPortal)
        {
            SKColor baseColor = isPortal ? PortalLineColor : DirectLineColor;
            float strokeW     = isPortal ? 2.0f : 2.5f;

            double totalLen = PolylineLength(poly);
            if (totalLen < 0.001) return;

            const double FadeExtraPx = 18.0;
            const int    FadeSteps   = 20;

            var zones = gaps.Select(g => (
                FadeStart: Math.Max(0, g.Lo - FadeExtraPx / totalLen),
                GapLo:     g.Lo,
                GapHi:     g.Hi,
                FadeEnd:   Math.Min(1, g.Hi + FadeExtraPx / totalLen)
            )).ToList();

            double AlphaAt(double t)
            {
                double alpha = 1.0;
                foreach (var z in zones)
                {
                    if (t <= z.FadeStart || t >= z.FadeEnd) continue;
                    if (t >= z.GapLo && t <= z.GapHi) return 0.0;
                    if (t < z.GapLo)
                    {
                        double ramp = (z.FadeStart < z.GapLo)
                            ? 1.0 - (t - z.FadeStart) / (z.GapLo - z.FadeStart)
                            : 0.0;
                        alpha = Math.Min(alpha, ramp);
                    }
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

            SKPoint EvalPoly(double t)
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
                        return new SKPoint(
                            (float)(poly[s].X + frac * (poly[s + 1].X - poly[s].X)),
                            (float)(poly[s].Y + frac * (poly[s + 1].Y - poly[s].Y)));
                    }
                    arc += len;
                }
                return poly[^1];
            }

            var tEvents = new SortedSet<double> { 0.0, 1.0 };
            foreach (var z in zones)
            {
                tEvents.Add(z.FadeStart); tEvents.Add(z.GapLo);
                tEvents.Add(z.GapHi);     tEvents.Add(z.FadeEnd);
            }
            {
                double arc2 = 0;
                for (int s = 0; s < poly.Length - 1; s++)
                {
                    tEvents.Add(arc2 / totalLen);
                    arc2 += PointDist(poly[s], poly[s + 1]);
                }
                tEvents.Add(1.0);
            }

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeW,
                StrokeCap = SKStrokeCap.Round,
            };

            var tList = tEvents.ToList();
            for (int seg = 0; seg < tList.Count - 1; seg++)
            {
                double tA = tList[seg], tB = tList[seg + 1];
                if (tB - tA < 1e-6) continue;

                double tMid = (tA + tB) / 2.0;
                double aMid = AlphaAt(tMid);
                if (aMid < 0.01) continue;

                bool needsFade = zones.Any(z => tA < z.FadeEnd && tB > z.FadeStart);

                if (!needsFade)
                {
                    byte a = (byte)Math.Round(baseColor.Alpha * aMid);
                    paint.Color = new SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, a);
                    var pa = EvalPoly(tA); var pb = EvalPoly(tB);
                    canvas.DrawLine(pa.X, pa.Y, pb.X, pb.Y, paint);
                }
                else
                {
                    for (int step = 0; step < FadeSteps; step++)
                    {
                        double t0 = tA + (tB - tA) * step       / FadeSteps;
                        double t1 = tA + (tB - tA) * (step + 1) / FadeSteps;
                        double a  = AlphaAt((t0 + t1) / 2.0);
                        if (a < 0.01) continue;
                        byte ab = (byte)Math.Round(baseColor.Alpha * a);
                        paint.Color = new SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, ab);
                        var pa = EvalPoly(t0); var pb = EvalPoly(t1);
                        canvas.DrawLine(pa.X, pa.Y, pb.X, pb.Y, paint);
                    }
                }
            }
        }

        private static void DrawConnectionPath(SKCanvas canvas, SKPoint p1, SKPoint ctrl, SKPoint p2,
            bool hasCurve, bool isPortal)
        {
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = isPortal ? PortalLineColor : DirectLineColor,
                StrokeWidth = isPortal ? 2f : 2.5f,
                StrokeCap = SKStrokeCap.Round,
            };

            if (!hasCurve)
            {
                canvas.DrawLine(p1.X, p1.Y, p2.X, p2.Y, paint);
                return;
            }

            using var path = new SKPath();
            path.MoveTo(p1);
            path.QuadTo(ctrl, p2);
            canvas.DrawPath(path, paint);
        }

        private static SKPoint[] SamplePath(SKPoint p1, SKPoint ctrl, SKPoint p2, bool hasCurve, int steps)
        {
            var pts = new SKPoint[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                double t = (double)s / steps;
                if (hasCurve)
                {
                    double mt = 1.0 - t;
                    pts[s] = new SKPoint(
                        (float)(mt * mt * p1.X + 2 * mt * t * ctrl.X + t * t * p2.X),
                        (float)(mt * mt * p1.Y + 2 * mt * t * ctrl.Y + t * t * p2.Y));
                }
                else
                {
                    pts[s] = new SKPoint(
                        (float)(p1.X + t * (p2.X - p1.X)),
                        (float)(p1.Y + t * (p2.Y - p1.Y)));
                }
            }
            return pts;
        }

        private static bool PolylinesIntersect(SKPoint[] a, SKPoint[] b, out SKPoint intersection, out int segA, out int segB)
        {
            intersection = default; segA = 0; segB = 0;
            for (int i = 0; i < a.Length - 1; i++)
                for (int j = 0; j < b.Length - 1; j++)
                    if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1], out intersection))
                    { segA = i; segB = j; return true; }
            return false;
        }

        private static double PolylineLength(SKPoint[] pts)
        {
            double len = 0;
            for (int i = 0; i < pts.Length - 1; i++) len += PointDist(pts[i], pts[i + 1]);
            return len;
        }

        private static bool SegmentsIntersect(SKPoint p1, SKPoint p2, SKPoint p3, SKPoint p4, out SKPoint intersection)
        {
            intersection = default;
            double d1x = p2.X - p1.X, d1y = p2.Y - p1.Y;
            double d2x = p4.X - p3.X, d2y = p4.Y - p3.Y;
            double cross = d1x * d2y - d1y * d2x;
            if (Math.Abs(cross) < 1e-8) return false;
            double t = ((p3.X - p1.X) * d2y - (p3.Y - p1.Y) * d2x) / cross;
            double u = ((p3.X - p1.X) * d1y - (p3.Y - p1.Y) * d1x) / cross;
            if (t <= 0.0 || t >= 1.0 || u <= 0.0 || u >= 1.0) return false;
            intersection = new SKPoint((float)(p1.X + t * d1x), (float)(p1.Y + t * d1y));
            return true;
        }

        private static double PointDist(SKPoint a, SKPoint b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ── Zone drawing ─────────────────────────────────────────────────────────

        private static void DrawZone(SKCanvas canvas, Zone zone, SKPoint pt)
        {
            bool isSpawn    = zone.Name.StartsWith("Spawn-",   StringComparison.Ordinal);
            bool isHub      = string.Equals(zone.Name, "Hub", StringComparison.Ordinal)
                           || zone.Name.StartsWith("Hub-",    StringComparison.Ordinal);
            bool isNeutral  = zone.Name.StartsWith("Neutral-", StringComparison.Ordinal);
            bool isHoldCity = IsHoldCityZone(zone);
            int  castles    = CastleCount(zone);

            SKColor rimColor;
            float   rimWidth = 2.5f;

            if (isHoldCity)
            {
                rimColor = HoldCityGold;
                rimWidth = 3.5f;
            }
            else if (isNeutral)
            {
                rimColor = NeutralTierRim(zone);
            }
            else if (isHub)
            {
                rimColor = RimHub;
                rimWidth = 2.5f;
            }
            else // spawn
            {
                rimColor = RimSpawn;
            }

            double drawRadius = isHub ? Math.Max(_zoneRadius, HubRadiusMin) : _zoneRadius;

            DrawCoin(canvas, pt, (float)drawRadius, rimColor, rimWidth);

            if (isHoldCity)
            {
                DrawHoldCityIcon(canvas, pt, drawRadius);
            }
            else if (isSpawn)
            {
                DrawPlayerNumber(canvas, zone, pt, drawRadius);
                if (castles > 1)
                    DrawCastleBadge(canvas, pt, drawRadius, castles);
            }
            else if (isNeutral)
            {
                if (castles > 0)
                    DrawNeutralCastleContent(canvas, pt, castles);
            }
            else if (isHub)
            {
                using var font = new SKFont(SerifTypeface, (float)(drawRadius * 0.85)) { Edging = SKFontEdging.Antialias, Subpixel = true };
                using var paint = new SKPaint { Color = NumeralCream, IsAntialias = true };
                DrawText(canvas, "Hub", pt.X, pt.Y, font, paint, centered: true);
            }
        }

        private static bool IsHoldCityZone(Zone zone) =>
            zone.MainObjects?.Any(o => o.HoldCityWinCon == true) == true;

        private static void DrawHoldCityIcon(SKCanvas canvas, SKPoint centre, double r)
        {
            // Crest pediment: small triangle above the house roof
            float pedW = (float)(r * 0.8);
            float pedH = (float)(r * 0.28);
            float pedTop = (float)(centre.Y - r * 0.95);
            using (var paint = new SKPaint { Color = HoldCityGold, IsAntialias = true, Style = SKPaintStyle.Fill })
            using (var path = new SKPath())
            {
                path.MoveTo(centre.X, pedTop);
                path.LineTo(centre.X + pedW / 2f, pedTop + pedH);
                path.LineTo(centre.X - pedW / 2f, pedTop + pedH);
                path.Close();
                canvas.DrawPath(path, paint);
            }

            // House
            double iconSize = r * 1.25;
            DrawHouseIcon(canvas, new SKPoint(centre.X, (float)(centre.Y + r * 0.10)), iconSize, HoldCityGold);
        }

        private static void DrawCastleBadge(SKCanvas canvas, SKPoint zoneCentre, double r, int castles)
        {
            float bx = (float)(zoneCentre.X + r * 0.72);
            float by = (float)(zoneCentre.Y + r * 0.72);
            float br = (float)(r * 0.70);

            using (var bg = new SKPaint { Color = CoinFillEdge, IsAntialias = true, Style = SKPaintStyle.Fill })
                canvas.DrawCircle(bx, by, br, bg);
            using (var bord = new SKPaint { Color = RimSpawn, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f })
                canvas.DrawCircle(bx, by, br, bord);

            float iconSize = br * 0.60f;
            float fontSize = br * 1.0f;

            DrawHouseIcon(canvas, new SKPoint(bx - br * 0.32f, by + 0.5f), iconSize, NumeralCream);

            using var font = new SKFont(SerifTypeface, fontSize) { Edging = SKFontEdging.Antialias, Subpixel = true };
            using var paint = new SKPaint { Color = NumeralCream, IsAntialias = true };
            DrawText(canvas, castles.ToString(CultureInfo.InvariantCulture), bx + br * 0.40f, by + 0.5f, font, paint, centered: true);
        }

        private static void DrawNeutralCastleContent(SKCanvas canvas, SKPoint pt, int castles)
        {
            string countStr = castles.ToString(CultureInfo.InvariantCulture);

            float iconW   = (float)(_zoneRadius * 0.55);
            float fontSize = (float)(_zoneRadius * 0.62);
            float gap     = (float)(_zoneRadius * 0.12);

            using var font = new SKFont(SerifTypeface, fontSize) { Edging = SKFontEdging.Antialias, Subpixel = true };
            float textW  = font.MeasureText(countStr);
            float totalW = iconW + gap + textW;

            float startX = pt.X - totalW / 2f;

            DrawHouseIcon(canvas, new SKPoint(startX + iconW / 2f, pt.Y + 0.5f), iconW, NumeralCream);

            using var paint = new SKPaint { Color = NumeralCream, IsAntialias = true };
            DrawText(canvas, countStr, startX + iconW + gap + textW / 2f, pt.Y + 0.5f, font, paint, centered: true);
        }

        private static void DrawHouseIcon(SKCanvas canvas, SKPoint centre, double size, SKColor color)
        {
            double w  = size * 0.9;
            double h  = size;
            double rh = h * 0.45;
            double bh = h - rh;

            float left   = (float)(centre.X - w / 2);
            float right  = (float)(centre.X + w / 2);
            float top    = (float)(centre.Y - h / 2);
            float roofBt = (float)(top + rh);
            float bottom = (float)(top + h);

            using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

            // Roof triangle
            using (var roof = new SKPath())
            {
                roof.MoveTo((float)centre.X, top);
                roof.LineTo((float)(right + w * 0.1), roofBt);
                roof.LineTo((float)(left  - w * 0.1), roofBt);
                roof.Close();
                canvas.DrawPath(roof, paint);
            }

            // Body rectangle
            canvas.DrawRect(new SKRect(left, roofBt, right, bottom), paint);
        }

        private static SKColor NeutralTierRim(Zone zone)
        {
            var pool = zone.GuardedContentPool?.FirstOrDefault() ?? string.Empty;
            if (pool.Contains("_t4_") || pool.Contains("_t5_"))
                return RimGold;
            if (pool.Contains("_t2_") || pool.Contains("_t1_"))
                return RimBronze;
            return RimSilver;
        }

        private static void DrawPlayerNumber(SKCanvas canvas, Zone zone, SKPoint centre, double r)
        {
            string label = "?";
            string? spawnValue = zone.MainObjects?
                .FirstOrDefault(o => o.Type == "Spawn")?.Spawn;
            if (spawnValue is not null && spawnValue.StartsWith("Player", StringComparison.Ordinal))
            {
                string number = spawnValue["Player".Length..];
                if (int.TryParse(number, out _))
                    label = number;
            }

            using var font = new SKFont(SerifTypeface, (float)(r * 0.95)) { Edging = SKFontEdging.Antialias, Subpixel = true };
            using var paint = new SKPaint { Color = NumeralCream, IsAntialias = true };
            DrawText(canvas, label, centre.X, centre.Y, font, paint, centered: true);
        }

        private static int CastleCount(Zone zone)
        {
            int count = 0;
            foreach (MainObject obj in zone.MainObjects ?? new List<MainObject>())
                if (obj.Type is "City" or "Spawn")
                    count++;
            return count;
        }

        // Centred text drawing helper using SKFont metrics.
        private static void DrawText(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint, bool centered)
        {
            if (!centered)
            {
                canvas.DrawText(text, x, y, font, paint);
                return;
            }
            font.MeasureText(text, out SKRect bounds);
            float baselineX = x - bounds.MidX;
            float baselineY = y - bounds.MidY;
            canvas.DrawText(text, baselineX, baselineY, font, paint);
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
