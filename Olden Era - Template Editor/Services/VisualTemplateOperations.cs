using Olden_Era___Template_Editor.Models;
using System.Text.Json;

namespace Olden_Era___Template_Editor.Services
{
    public static class VisualTemplateOperations
    {
        public static readonly string[] ZoneLetters =
        [
            "A", "B", "C", "D", "E", "F", "G", "H",
            "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X",
            "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF"
        ];

        private static readonly JsonSerializerOptions CloneOptions = new()
        {
            WriteIndented = false
        };

        public static VisualTemplateDocument CreateDefaultDocument()
        {
            var document = new VisualTemplateDocument();
            document.Zones.Add(new VisualZone
            {
                Id = Guid.NewGuid().ToString("N"),
                ExportLetter = "A",
                ZoneType = VisualZoneType.PlayerSpawn,
                PlayerSlot = 1,
                CanvasX = 210,
                CanvasY = 350,
                CastleCount = 1,
                Castles = [new VisualCastle()]
            });
            document.Zones.Add(new VisualZone
            {
                Id = Guid.NewGuid().ToString("N"),
                ExportLetter = "B",
                ZoneType = VisualZoneType.PlayerSpawn,
                PlayerSlot = 2,
                CanvasX = 490,
                CanvasY = 350,
                CastleCount = 1,
                Castles = [new VisualCastle()]
            });

            return document;
        }

        public static VisualTemplateClipboardPayload CopyZones(VisualTemplateDocument document, IEnumerable<string> zoneIds)
        {
            var idSet = zoneIds.ToHashSet(StringComparer.Ordinal);
            return new VisualTemplateClipboardPayload
            {
                Zones = document.Zones
                    .Where(zone => idSet.Contains(zone.Id))
                    .Select(Clone)
                    .ToList()
            };
        }

        public static List<VisualZone> PasteZones(VisualTemplateDocument document, VisualTemplateClipboardPayload payload, double offset = 30)
        {
            var pasted = new List<VisualZone>();
            foreach (VisualZone copied in payload.Zones)
            {
                VisualZone zone = Clone(copied);
                zone.Id = Guid.NewGuid().ToString("N");
                zone.ExportLetter = NextAvailableLetter(document);
                zone.CanvasX = Math.Clamp(zone.CanvasX + offset, 20, VisualTemplatePreviewPngWriter.CanvasWidth - 20);
                zone.CanvasY = Math.Clamp(zone.CanvasY + offset, 20, VisualTemplatePreviewPngWriter.CanvasHeight - 20);
                NormalizeCastles(zone);
                document.Zones.Add(zone);
                pasted.Add(zone);
            }

            return pasted;
        }

        public static string NextAvailableLetter(VisualTemplateDocument document)
        {
            var used = document.Zones
                .Select(zone => zone.ExportLetter)
                .Where(letter => !string.IsNullOrWhiteSpace(letter))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string letter in ZoneLetters)
            {
                if (!used.Contains(letter))
                    return letter;
            }

            return ZoneLetters[^1];
        }

        public static void NormalizeCastles(VisualZone zone)
        {
            int min = zone.ZoneType == VisualZoneType.PlayerSpawn ? 1 : 0;
            zone.CastleCount = Math.Clamp(zone.CastleCount, min, 4);
            while (zone.Castles.Count < zone.CastleCount)
                zone.Castles.Add(new VisualCastle());
            if (zone.Castles.Count > zone.CastleCount)
                zone.Castles.RemoveRange(zone.CastleCount, zone.Castles.Count - zone.CastleCount);
        }

        public static T Clone<T>(T value)
        {
            string json = JsonSerializer.Serialize(value, CloneOptions);
            return JsonSerializer.Deserialize<T>(json, CloneOptions)
                ?? throw new InvalidOperationException("Unable to clone visual template data.");
        }
    }

    public sealed class VisualTemplateClipboardPayload
    {
        public List<VisualZone> Zones { get; set; } = [];
    }
}
