using Olden_Era___Template_Editor.Models;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Olden_Era___Template_Editor.Services
{
    public static class VisualTemplatePreviewPngWriter
    {
        public const int CanvasWidth = 700;
        public const int CanvasHeight = 700;

        private const double ZoneRadius = 34;

        private static readonly Color BackgroundColor = Color.FromRgb(28, 22, 16);
        private static readonly Color DirectLineColor = Color.FromRgb(180, 145, 60);
        private static readonly Color PortalLineColor = Color.FromArgb(190, 90, 170, 210);

        public static string GetSidecarPath(string rmgJsonPath) =>
            TemplatePreviewPngWriter.GetSidecarPath(rmgJsonPath);

        public static void Save(VisualTemplateDocument document, string previewPath)
        {
            string? directory = Path.GetDirectoryName(previewPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            BitmapSource bitmap = Render(document);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string tempPath = $"{previewPath}.{Guid.NewGuid():N}.tmp";
            using (var stream = File.Create(tempPath))
                encoder.Save(stream);

            File.Move(tempPath, previewPath, overwrite: true);
        }

        public static BitmapSource Render(VisualTemplateDocument document)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                DrawPreview(dc, document);

            var bitmap = new RenderTargetBitmap(CanvasWidth, CanvasHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawPreview(DrawingContext dc, VisualTemplateDocument document)
        {
            dc.DrawRectangle(new SolidColorBrush(BackgroundColor), null, new Rect(0, 0, CanvasWidth, CanvasHeight));
            dc.DrawRoundedRectangle(null,
                new Pen(new SolidColorBrush(Color.FromRgb(143, 115, 63)), 3),
                new Rect(8, 8, CanvasWidth - 16, CanvasHeight - 16), 8, 8);

            var zonesById = document.Zones.ToDictionary(zone => zone.Id, StringComparer.Ordinal);
            foreach (VisualConnection connection in document.Connections)
            {
                if (!zonesById.TryGetValue(connection.FromZoneId, out VisualZone? from)) continue;
                if (!zonesById.TryGetValue(connection.ToZoneId, out VisualZone? to)) continue;

                var brush = new SolidColorBrush(connection.ConnectionType == VisualConnectionType.Portal ? PortalLineColor : DirectLineColor);
                var pen = new Pen(brush, connection.ConnectionType == VisualConnectionType.Portal ? 2.2 : 3.2);
                if (connection.ConnectionType == VisualConnectionType.Portal)
                    pen.DashStyle = DashStyles.Dash;

                dc.DrawLine(pen, PointFor(from), PointFor(to));
            }

            foreach (VisualZone zone in document.Zones.Where(zone => zone.ZoneType != VisualZoneType.PlayerSpawn))
                DrawZone(dc, zone);
            foreach (VisualZone zone in document.Zones.Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn))
                DrawZone(dc, zone);
        }

        private static void DrawZone(DrawingContext dc, VisualZone zone)
        {
            Point pt = PointFor(zone);
            (Brush fill, Pen outline, string label) = ZoneStyle(zone);
            dc.DrawEllipse(fill, outline, pt, ZoneRadius, ZoneRadius);

            DrawText(dc, label, new Point(pt.X, pt.Y - 5), 22, Brushes.White, centered: true, FontWeights.Bold);
            if (zone.CastleCount > 0)
                DrawText(dc, zone.CastleCount.ToString(CultureInfo.InvariantCulture),
                    new Point(pt.X, pt.Y + 17), 14, Brushes.White, centered: true, FontWeights.SemiBold);
        }

        private static (Brush Fill, Pen Outline, string Label) ZoneStyle(VisualZone zone) => zone.ZoneType switch
        {
            VisualZoneType.PlayerSpawn => (
                new SolidColorBrush(Color.FromRgb(42, 90, 50)),
                new Pen(new SolidColorBrush(Color.FromRgb(100, 200, 120)), 2.5),
                $"P{zone.PlayerSlot ?? 0}"),
            VisualZoneType.NeutralLow => (
                new SolidColorBrush(Color.FromRgb(101, 67, 33)),
                new Pen(new SolidColorBrush(Color.FromRgb(205, 127, 50)), 2.5),
                "L"),
            VisualZoneType.NeutralHigh => (
                new SolidColorBrush(Color.FromRgb(120, 90, 20)),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 210, 50)), 2.5),
                "H"),
            _ => (
                new SolidColorBrush(Color.FromRgb(72, 76, 80)),
                new Pen(new SolidColorBrush(Color.FromRgb(192, 192, 192)), 2.5),
                "M")
        };

        private static Point PointFor(VisualZone zone) =>
            new(
                Math.Clamp(zone.CanvasX, ZoneRadius + 10, CanvasWidth - ZoneRadius - 10),
                Math.Clamp(zone.CanvasY, ZoneRadius + 10, CanvasHeight - ZoneRadius - 10));

        private static void DrawText(DrawingContext dc, string text, Point point, double size,
            Brush brush, bool centered, FontWeight? weight = null)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
                size,
                brush,
                1.0);
            var origin = centered ? new Point(point.X - ft.Width / 2, point.Y - ft.Height / 2) : point;
            dc.DrawText(ft, origin);
        }
    }
}
