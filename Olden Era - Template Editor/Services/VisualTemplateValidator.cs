using Olden_Era___Template_Editor.Models;

namespace Olden_Era___Template_Editor.Services
{
    public static class VisualTemplateValidator
    {
        public static VisualValidationResult Validate(VisualTemplateDocument document)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(document.TemplateName))
                errors.Add("Template name is required.");

            if (document.Zones.Count > 32)
                errors.Add("Visual templates can contain at most 32 zones.");

            ValidateExportLetters(document, errors);
            ValidatePlayers(document, errors);
            ValidateWinConditionSupport(document, errors);
            ValidateConnections(document, errors);
            ValidateFactions(document, errors);

            if (document.MapSize > OldenEraTemplateEditor.Models.KnownValues.MaxOfficialMapSize)
                warnings.Add("Experimental map sizes above 240x240 may fail or behave unpredictably in game.");

            return new VisualValidationResult(errors, warnings);
        }

        private static void ValidateExportLetters(VisualTemplateDocument document, List<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VisualZone zone in document.Zones)
            {
                if (string.IsNullOrWhiteSpace(zone.ExportLetter))
                {
                    errors.Add("Every zone must have an export letter.");
                    continue;
                }

                string letter = zone.ExportLetter.Trim().ToUpperInvariant();
                if (!VisualTemplateOperations.ZoneLetters.Contains(letter))
                    errors.Add($"Zone letter {zone.ExportLetter} is outside the supported A-AF range.");
                if (!seen.Add(letter))
                    errors.Add($"Zone letter {letter} is used more than once.");
            }
        }

        private static void ValidatePlayers(VisualTemplateDocument document, List<string> errors)
        {
            var spawns = document.Zones
                .Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn)
                .ToList();

            if (spawns.Count == 0)
            {
                errors.Add("At least two player spawn zones are required.");
                return;
            }

            var grouped = spawns
                .Where(zone => zone.PlayerSlot is >= 1 and <= 8)
                .GroupBy(zone => zone.PlayerSlot!.Value)
                .ToDictionary(group => group.Key, group => group.Count());

            if (spawns.Any(zone => zone.PlayerSlot is null or < 1 or > 8))
                errors.Add("Every player spawn zone must target Player 1 through Player 8.");

            int maxPlayer = grouped.Keys.Count == 0 ? 0 : grouped.Keys.Max();
            if (maxPlayer < 2)
                errors.Add("Visual templates require at least Player 1 and Player 2.");
            if (maxPlayer > 8)
                errors.Add("Visual templates support at most 8 players.");

            for (int player = 1; player <= maxPlayer; player++)
            {
                if (!grouped.TryGetValue(player, out int count))
                    errors.Add($"Player {player} is missing a spawn zone.");
                else if (count > 1)
                    errors.Add($"Player {player} has more than one spawn zone.");
            }

            if ((document.Tournament || document.VictoryCondition == "win_condition_6") && maxPlayer != 2)
                errors.Add("Tournament mode only supports exactly 2 players.");
        }

        private static void ValidateWinConditionSupport(VisualTemplateDocument document, List<string> errors)
        {
            bool usesCityHold = document.CityHold || document.VictoryCondition == "win_condition_5";
            if (usesCityHold && !document.Zones.Any(zone => zone.ZoneType != VisualZoneType.PlayerSpawn && zone.CastleCount > 0))
                errors.Add("City Hold requires at least one neutral zone with a castle.");
        }

        private static void ValidateConnections(VisualTemplateDocument document, List<string> errors)
        {
            var zonesById = document.Zones.ToDictionary(zone => zone.Id, StringComparer.Ordinal);
            var edgePairs = new HashSet<string>(StringComparer.Ordinal);

            foreach (VisualConnection connection in document.Connections)
            {
                bool hasFrom = zonesById.ContainsKey(connection.FromZoneId);
                bool hasTo = zonesById.ContainsKey(connection.ToZoneId);
                if (!hasFrom || !hasTo)
                {
                    errors.Add("Every connection must reference existing zones.");
                    continue;
                }

                if (connection.FromZoneId == connection.ToZoneId)
                {
                    errors.Add("Connections cannot link a zone to itself.");
                    continue;
                }

                string pair = OrderedPair(connection.FromZoneId, connection.ToZoneId);
                if (!edgePairs.Add(pair))
                    errors.Add("Duplicate connections between the same two zones are not allowed.");
            }

            ValidateGraphConnected(document, zonesById, errors);
            ValidateDirectCrossings(document, zonesById, errors);
        }

        private static void ValidateGraphConnected(
            VisualTemplateDocument document,
            Dictionary<string, VisualZone> zonesById,
            List<string> errors)
        {
            if (document.Zones.Count <= 1)
                return;

            var adjacency = document.Zones.ToDictionary(
                zone => zone.Id,
                _ => new List<string>(),
                StringComparer.Ordinal);

            foreach (VisualConnection connection in document.Connections)
            {
                if (!zonesById.ContainsKey(connection.FromZoneId) || !zonesById.ContainsKey(connection.ToZoneId))
                    continue;
                if (connection.FromZoneId == connection.ToZoneId)
                    continue;

                adjacency[connection.FromZoneId].Add(connection.ToZoneId);
                adjacency[connection.ToZoneId].Add(connection.FromZoneId);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(document.Zones[0].Id);
            visited.Add(document.Zones[0].Id);

            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                foreach (string next in adjacency[id])
                {
                    if (visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            if (visited.Count != document.Zones.Count)
                errors.Add("All zones must be connected by direct links or portals.");
        }

        private static void ValidateDirectCrossings(
            VisualTemplateDocument document,
            Dictionary<string, VisualZone> zonesById,
            List<string> errors)
        {
            var direct = document.Connections
                .Where(connection => connection.ConnectionType == VisualConnectionType.Direct)
                .Where(connection => zonesById.ContainsKey(connection.FromZoneId) && zonesById.ContainsKey(connection.ToZoneId))
                .ToList();

            for (int i = 0; i < direct.Count; i++)
            {
                for (int j = i + 1; j < direct.Count; j++)
                {
                    VisualConnection a = direct[i];
                    VisualConnection b = direct[j];
                    if (SharesEndpoint(a, b))
                        continue;

                    VisualZone a1 = zonesById[a.FromZoneId];
                    VisualZone a2 = zonesById[a.ToZoneId];
                    VisualZone b1 = zonesById[b.FromZoneId];
                    VisualZone b2 = zonesById[b.ToZoneId];
                    if (SegmentsIntersect(a1.CanvasX, a1.CanvasY, a2.CanvasX, a2.CanvasY, b1.CanvasX, b1.CanvasY, b2.CanvasX, b2.CanvasY))
                        errors.Add("Crossing direct connections are not valid; convert one crossing edge to a portal.");
                }
            }
        }

        private static void ValidateFactions(VisualTemplateDocument document, List<string> errors)
        {
            var playerSlots = document.Zones
                .Where(zone => zone.ZoneType == VisualZoneType.PlayerSpawn && zone.PlayerSlot is >= 1 and <= 8)
                .Select(zone => zone.PlayerSlot!.Value)
                .ToHashSet();

            foreach (VisualZone zone in document.Zones)
            {
                int castleCount = Math.Clamp(zone.CastleCount, zone.ZoneType == VisualZoneType.PlayerSpawn ? 1 : 0, 4);
                for (int i = 0; i < Math.Min(castleCount, zone.Castles.Count); i++)
                {
                    VisualCastle castle = zone.Castles[i];
                    if (castle.FactionMode == VisualCastleFactionMode.MatchPlayer)
                    {
                        if (castle.MatchPlayerSlot is null || !playerSlots.Contains(castle.MatchPlayerSlot.Value))
                            errors.Add("Match-to-player castle restrictions must reference a player with a valid spawn zone.");
                    }

                    if (castle.FactionMode == VisualCastleFactionMode.Restricted)
                    {
                        foreach (string faction in castle.AllowedFactions)
                        {
                            if (!VisualTemplateGenerator.FactionCodeByGameName.ContainsKey(faction))
                                errors.Add($"Unknown faction label: {faction}.");
                        }
                    }
                }
            }
        }

        private static bool SharesEndpoint(VisualConnection a, VisualConnection b) =>
            a.FromZoneId == b.FromZoneId ||
            a.FromZoneId == b.ToZoneId ||
            a.ToZoneId == b.FromZoneId ||
            a.ToZoneId == b.ToZoneId;

        private static string OrderedPair(string a, string b) =>
            string.Compare(a, b, StringComparison.Ordinal) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

        private static bool SegmentsIntersect(
            double ax, double ay, double bx, double by,
            double cx, double cy, double dx, double dy)
        {
            static double Direction(double ax, double ay, double bx, double by, double cx, double cy) =>
                (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

            double d1 = Direction(cx, cy, dx, dy, ax, ay);
            double d2 = Direction(cx, cy, dx, dy, bx, by);
            double d3 = Direction(ax, ay, bx, by, cx, cy);
            double d4 = Direction(ax, ay, bx, by, dx, dy);

            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }
    }

    public sealed record VisualValidationResult(List<string> Errors, List<string> Warnings)
    {
        public bool IsValid => Errors.Count == 0;
    }
}
