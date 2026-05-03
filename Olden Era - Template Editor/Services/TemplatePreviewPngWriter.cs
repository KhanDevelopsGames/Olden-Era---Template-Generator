using OldenEraTemplateEditor.Models;
using System.Globalization;
using System.IO;
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
        // Cap; actual radius is computed per-layout by LayoutZones().
        private const double ZoneRadiusMax = 38;

        // ── Human icon (person silhouette drawn with geometry) ───────────────────
        // Drawn relative to the zone centre; scaled to fit the circle.

        public static string GetSidecarPath(string rmgJsonPath) =>
            rmgJsonPath.EndsWith(".rmg.json", StringComparison.OrdinalIgnoreCase)
                ? rmgJsonPath[..^".rmg.json".Length] + ".png"
                : Path.ChangeExtension(rmgJsonPath, ".png");

        public static void Save(RmgTemplate template, string previewPath)
        {
            string? directory = Path.GetDirectoryName(previewPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var bitmap = Render(template);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string tempPath = $"{previewPath}.{Guid.NewGuid():N}.tmp";
            using (var stream = File.Create(tempPath))
                encoder.Save(stream);

            File.Move(tempPath, previewPath, overwrite: true);
        }

        /// <summary>Renders the preview to a <see cref="BitmapSource"/> without writing any files.</summary>
        public static BitmapSource Render(RmgTemplate template)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                DrawPreview(dc, template);

            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        // ── Main draw ────────────────────────────────────────────────────────────

        private static void DrawPreview(DrawingContext dc, RmgTemplate template)
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
            var positions    = LayoutZones(orderedZones);

            // Draw connections first (below zones)
            DrawConnections(dc, variant?.Connections ?? [], positions);

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

        private static Dictionary<string, Point> LayoutZones(List<Zone> zones)
        {
            var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
            Zone? hub = zones.FirstOrDefault(z => string.Equals(z.Name, "Hub", StringComparison.Ordinal));
            var outer = hub is null ? zones : zones.Where(z => z != hub).ToList();

            if (hub is not null)
                positions[hub.Name] = new Point(Width / 2.0, Height / 2.0);

            int n = Math.Max(1, outer.Count);

            // Ring always fills to the border — zones spread as far apart as possible.
            // The circle radius then shrinks only if needed to prevent overlap.
            // chord between adjacent zone centres = 2 · ringRadius · sin(π/n)
            // require chord ≥ 2 · zoneRadius + gap  →  zoneRadius ≤ (chord/2) - gap/2
            const double margin    = 18;                          // padding from image edge
            double ringRadius      = Width / 2.0 - margin;       // always maximum — zones spread as far apart as possible

            double chord           = 2.0 * ringRadius * Math.Sin(Math.PI / n);
            const double minGap    = 6;                           // minimum gap between circle edges
            double zoneRadius      = Math.Min(ZoneRadiusMax, (chord - minGap) / 2.0);

            // Ensure circle edges don't clip the image border
            ringRadius = Math.Min(ringRadius, Width / 2.0 - zoneRadius - margin);

            var center = new Point(Width / 2.0, Height / 2.0);

            for (int i = 0; i < outer.Count; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI * 2 / n;
                positions[outer[i].Name] = new Point(
                    center.X + Math.Cos(angle) * ringRadius,
                    center.Y + Math.Sin(angle) * ringRadius);
            }

            // Store for use during drawing
            _zoneRadius = zoneRadius;
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
            bool isSpawn   = zone.Name.StartsWith("Spawn-",   StringComparison.Ordinal);
            bool isHub     = string.Equals(zone.Name, "Hub",  StringComparison.Ordinal);
            bool isNeutral = zone.Name.StartsWith("Neutral-", StringComparison.Ordinal);
            int  castles   = CastleCount(zone);

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

            dc.DrawEllipse(fillBrush, outlinePen, pt, _zoneRadius, _zoneRadius);

            if (isSpawn)
            {
                // Person icon fills the circle
                DrawPersonIcon(dc, pt, _zoneRadius);

                // Castle count badge — only shown when there is more than one castle
                if (castles > 1)
                    DrawCastleBadge(dc, pt, _zoneRadius, castles);
            }
            else if (isNeutral)
            {
                // House icon + count side by side inside the circle, or nothing if no castles
                if (castles > 0)
                    DrawNeutralCastleContent(dc, pt, castles);
            }
            else if (isHub)
            {
                DrawText(dc, "Hub", pt, 11, Brushes.White, centered: true);
            }
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

        private static (Brush Fill, Pen Outline) NeutralTierStyle(Zone zone) => zone.Layout switch
        {
            // Bronze — Low tier
            SideLayoutName => (
                new SolidColorBrush(BronzeFill),
                new Pen(new SolidColorBrush(BronzeBorder), 2.5)),
            // Gold — High tier
            CenterLayoutName => (
                new SolidColorBrush(GoldFill),
                new Pen(new SolidColorBrush(GoldBorder), 2.5)),
            // Silver — Medium tier (default / TreasureLayoutName)
            _ => (
                new SolidColorBrush(SilverFill),
                new Pen(new SolidColorBrush(SilverBorder), 2.5)),
        };

        // ── Person icon ──────────────────────────────────────────────────────────
        // Head + body silhouette centred inside the zone circle.

        private static void DrawPersonIcon(DrawingContext dc, Point centre, double r)
        {
            double s = r * 0.38;
            var iconBrush = new SolidColorBrush(Color.FromRgb(160, 230, 170));

            // Head
            dc.DrawEllipse(iconBrush, null, new Point(centre.X, centre.Y - s * 0.95), s * 0.50, s * 0.50);
            // Body
            dc.DrawEllipse(iconBrush, null, new Point(centre.X, centre.Y + s * 0.60), s * 0.70, s * 0.80);
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
