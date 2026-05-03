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
        private const int Width = 512;
        private const int Height = 512;

        public static string GetSidecarPath(string rmgJsonPath) =>
            rmgJsonPath.EndsWith(".rmg.json", StringComparison.OrdinalIgnoreCase)
                ? rmgJsonPath[..^".rmg.json".Length] + ".png"
                : Path.ChangeExtension(rmgJsonPath, ".png");

        public static void Save(RmgTemplate template, string previewPath)
        {
            string? directory = Path.GetDirectoryName(previewPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                DrawPreview(dc, template);
            }

            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string tempPath = $"{previewPath}.{Guid.NewGuid():N}.tmp";
            using (var stream = File.Create(tempPath))
                encoder.Save(stream);

            File.Move(tempPath, previewPath, overwrite: true);
        }

        private static void DrawPreview(DrawingContext dc, RmgTemplate template)
        {
            var background = new SolidColorBrush(Color.FromRgb(28, 22, 16));
            var border = new Pen(new SolidColorBrush(Color.FromRgb(143, 115, 63)), 3);
            dc.DrawRectangle(background, null, new Rect(0, 0, Width, Height));
            dc.DrawRoundedRectangle(null, border, new Rect(8, 8, Width - 16, Height - 16), 8, 8);

            Variant? variant = template.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? [];
            if (zones.Count == 0)
            {
                DrawText(dc, template.Name, new Point(Width / 2.0, Height / 2.0), 24, Brushes.White, centered: true);
                return;
            }

            var orderedZones = OrderZones(zones, variant?.Orientation?.ZeroAngleZone);
            var positions = LayoutZones(orderedZones);
            DrawConnections(dc, variant?.Connections ?? [], positions);

            foreach (Zone zone in orderedZones)
                DrawZone(dc, zone, positions[zone.Name]);

            DrawText(dc, template.Name, new Point(Width / 2.0, 30), 18, new SolidColorBrush(Color.FromRgb(239, 220, 178)), centered: true);
        }

        private static List<Zone> OrderZones(List<Zone> zones, string? zeroAngleZone)
        {
            var ordered = zones.ToList();
            int zeroIndex = !string.IsNullOrWhiteSpace(zeroAngleZone)
                ? ordered.FindIndex(zone => string.Equals(zone.Name, zeroAngleZone, StringComparison.Ordinal))
                : -1;

            if (zeroIndex <= 0) return ordered;
            return ordered.Skip(zeroIndex).Concat(ordered.Take(zeroIndex)).ToList();
        }

        private static Dictionary<string, Point> LayoutZones(List<Zone> zones)
        {
            var positions = new Dictionary<string, Point>(StringComparer.Ordinal);
            Zone? hub = zones.FirstOrDefault(zone => string.Equals(zone.Name, "Hub", StringComparison.Ordinal));
            var outerZones = hub is null ? zones : zones.Where(zone => zone != hub).ToList();

            if (hub is not null)
                positions[hub.Name] = new Point(Width / 2.0, Height / 2.0);

            double radius = outerZones.Count <= 6 ? 180 : 205;
            var center = new Point(Width / 2.0, Height / 2.0 + 12);

            for (int i = 0; i < outerZones.Count; i++)
            {
                double angle = -Math.PI / 2 + i * Math.PI * 2 / Math.Max(1, outerZones.Count);
                positions[outerZones[i].Name] = new Point(
                    center.X + Math.Cos(angle) * radius,
                    center.Y + Math.Sin(angle) * radius);
            }

            return positions;
        }

        private static void DrawConnections(DrawingContext dc, List<Connection> connections, Dictionary<string, Point> positions)
        {
            foreach (Connection connection in connections)
            {
                if (!positions.TryGetValue(connection.From, out Point from)) continue;
                if (!positions.TryGetValue(connection.To, out Point to)) continue;

                var brush = connection.ConnectionType == "Portal"
                    ? new SolidColorBrush(Color.FromRgb(125, 186, 196))
                    : new SolidColorBrush(Color.FromRgb(122, 97, 55));
                var pen = new Pen(brush, connection.ConnectionType == "Proximity" ? 1 : 2);
                if (connection.ConnectionType == "Portal")
                    pen.DashStyle = DashStyles.Dash;

                dc.DrawLine(pen, from, to);
            }
        }

        private static void DrawZone(DrawingContext dc, Zone zone, Point point)
        {
            bool isSpawn = zone.Name.StartsWith("Spawn-", StringComparison.Ordinal);
            bool isHub = string.Equals(zone.Name, "Hub", StringComparison.Ordinal);
            bool hasCity = zone.MainObjects?.Any(mainObject => mainObject.Type == "City") == true;

            Brush fill = isHub
                ? new SolidColorBrush(Color.FromRgb(83, 113, 125))
                : isSpawn
                    ? new SolidColorBrush(Color.FromRgb(184, 142, 58))
                    : hasCity
                        ? new SolidColorBrush(Color.FromRgb(112, 143, 84))
                        : new SolidColorBrush(Color.FromRgb(86, 101, 76));

            var outline = new Pen(new SolidColorBrush(Color.FromRgb(245, 225, 179)), 2);
            dc.DrawEllipse(fill, outline, point, 24, 24);
            DrawText(dc, ShortZoneLabel(zone.Name), point, 13, Brushes.White, centered: true);
        }

        private static string ShortZoneLabel(string zoneName)
        {
            if (string.Equals(zoneName, "Hub", StringComparison.Ordinal)) return "Hub";
            int dash = zoneName.LastIndexOf('-');
            return dash >= 0 && dash < zoneName.Length - 1 ? zoneName[(dash + 1)..] : zoneName;
        }

        private static void DrawText(DrawingContext dc, string text, Point point, double size, Brush brush, bool centered)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush,
                1.0);

            var origin = centered
                ? new Point(point.X - formatted.Width / 2, point.Y - formatted.Height / 2)
                : point;
            dc.DrawText(formatted, origin);
        }
    }
}
