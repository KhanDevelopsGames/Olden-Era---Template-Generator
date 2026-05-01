using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using System.Collections.Generic;
using System.Linq;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Generates an RmgTemplate based on <see cref="GeneratorSettings"/>,
    /// using Jebus Cross Classic as a structural base.
    /// </summary>
    public static class TemplateGenerator
    {
        // Each player zone gets 5 guard connections to the center (same as JCC).
        private const int ConnectionsPerZone = 5;

        // Letters used to name zones: A, B, C … up to 8 players + 8 neutral zones = 16 max.
        private static readonly string[] ZoneLetters =
            ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P"];

        public static RmgTemplate Generate(GeneratorSettings settings)
        {
            var playerLetters = ZoneLetters.Take(settings.PlayerCount).ToList();

            // Hub & Spoke handles isolation via hub; for all other topologies the
            // isolation is done by skipping player–player connections, so no extra
            // neutral zones need to be auto-created.
            int neutralCount = settings.NeutralZoneCount;

            var neutralLetters = ZoneLetters.Skip(settings.PlayerCount).Take(neutralCount).ToList();

            var template = new RmgTemplate
            {
                Name = settings.TemplateName,
                GameMode = settings.GameMode,
                Description = "custom_generated_template",
                DisplayWinCondition = settings.VictoryCondition,
                SizeX = settings.MapSize,
                SizeZ = settings.MapSize,
                GameRules = BuildGameRules(settings),
                Variants = [BuildVariant(settings, playerLetters, neutralLetters)],
                ZoneLayouts = BuildZoneLayouts(),
                MandatoryContent = BuildAllMandatoryContent(playerLetters, neutralLetters, settings),
                ContentCountLimits = BuildAllContentCountLimits(),
                ContentPools = [],
                ContentLists = []
            };

            return template;
        }

        // ── Game rules ───────────────────────────────────────────────────────────

        private static GameRules BuildGameRules(GeneratorSettings settings) => new()
        {
            HeroCountMin = settings.HeroCountMin,
            HeroCountMax = settings.HeroCountMax,
            HeroCountIncrement = settings.HeroCountIncrement,
            HeroHireBan = false,
            EncounterHoles = false,
            FactionLawsExpModifier = 1.0,
            AstrologyExpModifier = 1.0,
            Bonuses =
            [
                new Bonus
                {
                    Sid = "add_bonus_hero_stat",
                    ReceiverSide = -1,
                    ReceiverFilter = "all_heroes",
                    Parameters = ["movementBonus", "0"]
                }
            ],
            WinConditions = new WinConditions
            {
                Classic = true,
                Desertion = true,
                DesertionDay = 3,
                DesertionValue = 3000,
                HeroLighting = false,
                HeroLightingDay = 1,
                LostStartCity = false,
                LostStartCityDay = 3,
                LostStartHero = false,
                CityHold = false,
                CityHoldDays = 6
            }
        };

        // ── Variant ──────────────────────────────────────────────────────────────

        private static Variant BuildVariant(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            return settings.Topology switch
            {
                MapTopology.HubAndSpoke => BuildVariantHubAndSpoke(settings, playerLetters, neutralLetters),
                MapTopology.Chain       => BuildVariantChain(settings, playerLetters, neutralLetters),
                MapTopology.SharedWeb   => BuildVariantSharedWeb(settings, playerLetters, neutralLetters),
                MapTopology.Random      => BuildVariantRandom(settings, playerLetters, neutralLetters),
                _                       => BuildVariantDefault(settings, playerLetters, neutralLetters),
            };
        }

        // ── Topology: Default (Ring) ──────────────────────────────────────────────

        private static Variant BuildVariantDefault(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            var orderedLetters = playerLetters.Concat(neutralLetters).ToList();
            int outerCount = orderedLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

            // Pre-compute ring connection names, but only for pairs that are actually connected.
            // A pair is skipped when isolation is on and both zones are player zones.
            var ringConnRight = new string[outerCount]; // name of the connection from i to i+1
            var ringConnLeft  = new string[outerCount]; // name of the connection from i-1 to i
            for (int i = 0; i < outerCount; i++)
            {
                int next = (i + 1) % outerCount;
                bool bothPlayers = playerLetters.Contains(orderedLetters[i])
                                && playerLetters.Contains(orderedLetters[next]);
                if (isolate && bothPlayers) continue; // leave entries as null — no connection here
                string name = $"Ring-{orderedLetters[i]}-{orderedLetters[next]}";
                ringConnRight[i]    = name;
                ringConnLeft[next]  = name;
            }

            var zones = new List<Zone>();
            for (int i = 0; i < outerCount; i++)
            {
                string letter = orderedLetters[i];
                // Only include non-null connection names for this zone's roads.
                var myConns = new[] { ringConnLeft[i], ringConnRight[i] }
                    .Where(c => c != null).Distinct().ToArray();

                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns, settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));
                else
                    zones.Add(BuildNeutralZone(letter, myConns, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));
            }

            var connections = new List<Connection>();
            connections.AddRange(BuildRingConnections(playerLetters, orderedLetters, isolate));
            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, orderedLetters));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections);
            return MakeVariant(playerLetters, orderedLetters[0], outerCount, zones, connections);
        }

        // ── Topology: Random Proximity ────────────────────────────────────────────

        private static Variant BuildVariantRandom(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            var rng = new Random();
            // Shuffle zones so player/neutral order is random.
            var allLetters = playerLetters.Concat(neutralLetters).OrderBy(_ => rng.Next()).ToList();
            int count = allLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

            // Assign random 2D positions in the unit square.
            // Jitter positions slightly apart to avoid degenerate collinear/cocircular cases.
            var pos = new List<(double X, double Y)>();
            for (int i = 0; i < count; i++)
                pos.Add((rng.NextDouble() * 0.9 + 0.05, rng.NextDouble() * 0.9 + 0.05));

            // Compute Delaunay triangulation — every edge is a true zone-to-zone border.
            // With only ≤16 points this is fast enough to do with Bowyer-Watson.
            var pairs = DelaunayEdges(pos);

            // Build connection name lookup per zone index.
            var connsByZone = Enumerable.Range(0, count).ToDictionary(i => i, _ => new List<string>());
            var connections = new List<Connection>();

            foreach (var (a, b) in pairs)
            {
                string fromLetter = allLetters[a];
                string toLetter   = allLetters[b];
                if (isolate && playerLetters.Contains(fromLetter) && playerLetters.Contains(toLetter))
                    continue;

                string connName = $"Rnd-{fromLetter}-{toLetter}";
                connsByZone[a].Add(connName);
                connsByZone[b].Add(connName);

                string fromZone = playerLetters.Contains(fromLetter) ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = playerLetters.Contains(toLetter)   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connName,
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = 30000,
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"rnd_guard_{fromLetter}_{toLetter}"
                });
            }

            var zones = new List<Zone>();
            for (int i = 0; i < count; i++)
            {
                string letter = allLetters[i];
                var myConns = connsByZone[i].ToArray();
                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns, settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));
                else
                    zones.Add(BuildNeutralZone(letter, myConns, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, allLetters));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections);
            return MakeVariant(playerLetters, allLetters[0], count, zones, connections);
        }

        // ── Delaunay triangulation (Bowyer-Watson) ────────────────────────────────

        /// <summary>
        /// Returns the unique undirected edges of the Delaunay triangulation of the given points.
        /// Each edge (A, B) has A &lt; B.
        /// </summary>
        private static List<(int A, int B)> DelaunayEdges(List<(double X, double Y)> pts)
        {
            int n = pts.Count;
            if (n == 1) return [];
            if (n == 2) return [(0, 1)];

            // Super-triangle large enough to contain all points.
            double minX = pts.Min(p => p.X), minY = pts.Min(p => p.Y);
            double maxX = pts.Max(p => p.X), maxY = pts.Max(p => p.Y);
            double dx = maxX - minX, dy = maxY - minY;
            double delta = Math.Max(dx, dy) * 10;
            var superPts = new List<(double X, double Y)>(pts)
            {
                (minX - delta,     minY - delta * 3),
                (minX + delta * 3, minY - delta),
                (minX,             minY + delta * 3)
            };
            int s0 = n, s1 = n + 1, s2 = n + 2;

            // Each triangle is (i0, i1, i2) into superPts.
            var triangles = new List<(int I0, int I1, int I2)> { (s0, s1, s2) };

            for (int p = 0; p < n; p++)
            {
                double px = superPts[p].X, py = superPts[p].Y;

                // Find all triangles whose circumcircle contains this point.
                var bad = triangles.Where(t => InCircumcircle(superPts, t, px, py)).ToList();

                // Collect the boundary polygon of the bad triangles (edges not shared by two bad triangles).
                var boundary = new List<(int A, int B)>();
                foreach (var t in bad)
                {
                    (int A, int B)[] edges = [(t.I0, t.I1), (t.I1, t.I2), (t.I2, t.I0)];
                    foreach (var e in edges)
                    {
                        bool shared = bad.Any(other => other != t &&
                            ((other.I0 == e.A && other.I1 == e.B) || (other.I1 == e.A && other.I0 == e.B) ||
                             (other.I1 == e.A && other.I2 == e.B) || (other.I2 == e.A && other.I1 == e.B) ||
                             (other.I2 == e.A && other.I0 == e.B) || (other.I0 == e.A && other.I2 == e.B)));
                        if (!shared) boundary.Add(e);
                    }
                }

                foreach (var t in bad) triangles.Remove(t);
                foreach (var (a, b) in boundary)
                    triangles.Add((a, b, p));
            }

            // Remove triangles that share a vertex with the super-triangle.
            triangles.RemoveAll(t => t.I0 >= n || t.I1 >= n || t.I2 >= n);

            // Extract unique edges from real points only.
            var edgeSet = new HashSet<(int, int)>();
            foreach (var t in triangles)
            {
                edgeSet.Add((Math.Min(t.I0, t.I1), Math.Max(t.I0, t.I1)));
                edgeSet.Add((Math.Min(t.I1, t.I2), Math.Max(t.I1, t.I2)));
                edgeSet.Add((Math.Min(t.I2, t.I0), Math.Max(t.I2, t.I0)));
            }
            return [.. edgeSet];
        }

        private static bool InCircumcircle(List<(double X, double Y)> pts, (int I0, int I1, int I2) t, double px, double py)
        {
            double ax = pts[t.I0].X - px, ay = pts[t.I0].Y - py;
            double bx = pts[t.I1].X - px, by = pts[t.I1].Y - py;
            double cx = pts[t.I2].X - px, cy = pts[t.I2].Y - py;
            double det = ax * (by * (cx * cx + cy * cy) - cy * (bx * bx + by * by))
                       - ay * (bx * (cx * cx + cy * cy) - cx * (bx * bx + by * by))
                       + (ax * ax + ay * ay) * (bx * cy - by * cx);
            return det > 0;
        }

        // ── Topology: Hub & Spoke ─────────────────────────────────────────────────

        private static Variant BuildVariantHubAndSpoke(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            // A central "Hub" zone connects to every outer zone.
            // Outer zones are players + neutrals arranged around the hub.
            var outerLetters = playerLetters.Concat(neutralLetters).ToList();
            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Hub zone (neutral, no castle, high loot).
            var hubConns = outerLetters.Select(l => $"Hub-{l}").ToArray();
            zones.Add(BuildHubZone(hubConns));

            // Outer zones each connect only to the hub.
            for (int i = 0; i < outerLetters.Count; i++)
            {
                string letter = outerLetters[i];
                var spokeConns = new[] { $"Hub-{letter}" };
                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", spokeConns, settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));
                else
                    zones.Add(BuildNeutralZone(letter, spokeConns, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));
            }

            // Hub → each outer zone: Direct guarded connections (multiple per zone like JCC).
            foreach (var letter in outerLetters)
            {
                string outerZone = playerLetters.Contains(letter) ? $"Spawn-{letter}" : $"Neutral-{letter}";
                // Main named connection.
                connections.Add(new Connection
                {
                    Name = $"Hub-{letter}",
                    From = "Hub",
                    To = outerZone,
                    ConnectionType = "Direct",
                    GuardZone = "Hub",
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = 30000,
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"hub_guard_{letter}"
                });
                // Extra unnamed connections (same as JCC pattern of 5 per zone).
                for (int e = 1; e < ConnectionsPerZone; e++)
                    connections.Add(new Connection { From = "Hub", To = outerZone, ConnectionType = "Direct" });
            }

            // Proximity between players that are adjacent in the outer ring (visual only).
            if (!settings.NoDirectPlayerConnections)
            {
                for (int i = 0; i < playerLetters.Count; i++)
                {
                    int next = (i + 1) % playerLetters.Count;
                    connections.Add(new Connection
                    {
                        Name = $"Pseudo-{playerLetters[i]}-{playerLetters[next]}",
                        From = $"Spawn-{playerLetters[i]}",
                        To = $"Spawn-{playerLetters[next]}",
                        ConnectionType = "Proximity"
                    });
                }
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, outerLetters));

            return MakeVariant(playerLetters, outerLetters[0], outerLetters.Count + 1, zones, connections);
        }

        // ── Topology: Chain ───────────────────────────────────────────────────────

        private static Variant BuildVariantChain(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            var orderedLetters = playerLetters.Concat(neutralLetters).ToList();
            int count = orderedLetters.Count;
            bool isolate = settings.NoDirectPlayerConnections && playerLetters.Count > 1;

            // Pre-compute connection names for each adjacent pair, skipping player–player
            // pairs when isolation is enabled. null means no connection at that position.
            var connNames = new string[count - 1];
            for (int i = 0; i < count - 1; i++)
            {
                bool bothPlayers = playerLetters.Contains(orderedLetters[i])
                                && playerLetters.Contains(orderedLetters[i + 1]);
                if (isolate && bothPlayers) continue;
                connNames[i] = $"Chain-{orderedLetters[i]}-{orderedLetters[i + 1]}";
            }

            var zones = new List<Zone>();
            for (int i = 0; i < count; i++)
            {
                string letter = orderedLetters[i];
                var myConns = new List<string>();
                if (i > 0         && connNames[i - 1] != null) myConns.Add(connNames[i - 1]);
                if (i < count - 1 && connNames[i]     != null) myConns.Add(connNames[i]);

                int playerIdx = playerLetters.IndexOf(letter);
                if (playerIdx >= 0)
                    zones.Add(BuildSpawnZone(letter, $"Player{playerIdx + 1}", myConns.ToArray(), settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));
                else
                    zones.Add(BuildNeutralZone(letter, myConns.ToArray(), settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));
            }

            var connections = new List<Connection>();
            for (int i = 0; i < count - 1; i++)
            {
                if (connNames[i] == null) continue;
                string fromLetter = orderedLetters[i];
                string toLetter   = orderedLetters[i + 1];
                string fromZone = playerLetters.Contains(fromLetter) ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = playerLetters.Contains(toLetter)   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";
                connections.Add(new Connection
                {
                    Name = connNames[i],
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = 30000,
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"chain_guard_{fromLetter}_{toLetter}"
                });
            }

            if (settings.RandomPortals)
                connections.AddRange(BuildRandomPortalConnections(playerLetters, orderedLetters));

            if (isolate) EnsurePlayerZonesConnected(playerLetters, zones, connections);
            return MakeVariant(playerLetters, orderedLetters[0], count, zones, connections);
        }

        // ── Topology: Shared Web ──────────────────────────────────────────────────

        private static Variant BuildVariantSharedWeb(GeneratorSettings settings, List<string> playerLetters, List<string> neutralLetters)
        {
            // Each player connects to two neutral zones.
            // Neutral zones are arranged in a ring connecting all players.
            // If there are fewer neutrals than needed, we wrap around (multiple players share a neutral).
            // Requires at least 1 neutral zone.
            var neutrals = neutralLetters.Count > 0
                ? neutralLetters
                : ZoneLetters.Skip(settings.PlayerCount).Take(1).ToList();

            int p = playerLetters.Count;
            int n = neutrals.Count;

            // Pre-compute neutral ring connection names.
            var neutralRingConns = new string[n];
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                neutralRingConns[i] = $"NRing-{neutrals[i]}-{neutrals[next]}";
            }

            var zones = new List<Zone>();
            var connections = new List<Connection>();

            // Build neutral zones — each connects to its two ring neighbours.
            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                string[] nConns = n > 1
                    ? new[] { neutralRingConns[prev], neutralRingConns[i] }.Distinct().ToArray()
                    : [];
                zones.Add(BuildNeutralZone(neutrals[i], nConns, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));
            }

            // Build player zones — each connects to two neutrals (evenly distributed).
            for (int i = 0; i < p; i++)
            {
                // Spread players across neutral ring evenly.
                int n1 = (i * n / p) % n;
                int n2 = ((i * n / p) + 1) % n;

                var spokeConns = new List<string> { $"Web-{playerLetters[i]}-{neutrals[n1]}" };
                if (n1 != n2)
                    spokeConns.Add($"Web-{playerLetters[i]}-{neutrals[n2]}");

                zones.Add(BuildSpawnZone(playerLetters[i], $"Player{i + 1}", spokeConns.ToArray(), settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));

                // Player → neutral Direct connections.
                foreach (var connName in spokeConns)
                {
                    string neutralLetter = connName.Split('-')[2]; // "Web-A-I" → index 2 = "I"
                    string neutralZone = $"Neutral-{neutralLetter}";
                    connections.Add(new Connection
                    {
                        Name = connName,
                        From = $"Spawn-{playerLetters[i]}",
                        To = neutralZone,
                        ConnectionType = "Direct",
                        GuardZone = neutralZone,
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = 30000,
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"web_guard_{playerLetters[i]}_{neutralLetter}"
                    });
                }
            }

            // Neutral ring connections.
            if (n > 1)
            {
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    connections.Add(new Connection
                    {
                        Name = neutralRingConns[i],
                        From = $"Neutral-{neutrals[i]}",
                        To = $"Neutral-{neutrals[next]}",
                        ConnectionType = "Direct",
                        GuardZone = $"Neutral-{neutrals[i]}",
                        GuardEscape = false,
                        SimTurnSquad = true,
                        GuardValue = 20000,
                        GuardWeeklyIncrement = 0.15,
                        GuardMatchGroup = $"nring_guard_{neutrals[i]}_{neutrals[next]}"
                    });
                }
            }

            if (settings.RandomPortals)
            {
                var allLetters = playerLetters.Concat(neutrals).ToList();
                connections.AddRange(BuildRandomPortalConnections(playerLetters, allLetters));
            }

            bool isolateWeb = settings.NoDirectPlayerConnections && playerLetters.Count > 1;
            if (isolateWeb) EnsurePlayerZonesConnected(playerLetters, zones, connections);

            var allZoneLettersOrdered = neutrals.Concat(playerLetters).ToList();
            return MakeVariant(playerLetters, playerLetters[0], zones.Count, zones, connections);
        }

        // ── Hub zone (only used by Hub & Spoke topology) ─────────────────────────

        private static Zone BuildHubZone(string[] spokeConns) => new()
        {
            Name = "Hub",
            Size = 1.0,
            Layout = "zone_layout_spawns",
            GuardCutoffValue = 2000,
            GuardRandomization = 0.05,
            GuardMultiplier = 1.5,
            GuardWeeklyIncrement = 0.20,
            GuardReactionDistribution = [0, 10, 10, 20, 10, 0],
            DiplomacyModifier = -0.5,
            GuardedContentPool = ["template_pool_jebus_cross_guarded_side_zone"],
            UnguardedContentPool = ["template_pool_jebus_cross_unguarded_side_zone"],
            ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
            MandatoryContent = [],
            ContentCountLimits = BuildSideContentLimits(),
            GuardedContentValue = 300000,
            GuardedContentValuePerArea = 0,
            UnguardedContentValue = 50000,
            UnguardedContentValuePerArea = 0,
            ResourcesValue = 80000,
            ResourcesValuePerArea = 0,
            MainObjects = [],
            ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
            ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
            MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
            CrossroadsPosition = 0,
            Roads = spokeConns.Select(c => PlainRoad(ConnectionEndpoint(c), ConnectionEndpoint(c))).ToList()
        };

        // ── Isolation failsafe ───────────────────────────────────────────────────

        /// <summary>
        /// When "isolate player zones" is active, player zones that ended up with no connections
        /// (because all their neighbours were also player zones) must still be reachable.
        /// This method adds a minimal direct player–player fallback connection for each such zone.
        /// </summary>
        private static void EnsurePlayerZonesConnected(
            List<string> playerLetters, List<Zone> zones, List<Connection> connections)
        {
            if (playerLetters.Count < 2) return;

            // Collect the set of connection names already present in the connection list.
            var connNames = connections.Select(c => c.Name).Where(n => n != null).ToHashSet();

            foreach (var letter in playerLetters)
            {
                string zoneName = $"Spawn-{letter}";
                var zone = zones.FirstOrDefault(z => z.Name == zoneName);
                if (zone == null) continue;

                // Check whether this zone has any named road pointing to an existing connection.
                bool hasConnection = zone.Roads != null && zone.Roads
                    .Any(r => r.To?.Type == "Connection" && connNames.Contains(r.To.Args?[0]));

                if (hasConnection) continue;

                // Find another player zone that is also disconnected, or any other player zone.
                string? partnerLetter = playerLetters
                    .Where(pl => pl != letter)
                    .OrderBy(pl =>
                    {
                        var pZone = zones.FirstOrDefault(z => z.Name == $"Spawn-{pl}");
                        bool partnerHasConn = pZone?.Roads != null && pZone.Roads
                            .Any(r => r.To?.Type == "Connection" && connNames.Contains(r.To.Args?[0]));
                        return partnerHasConn ? 1 : 0; // prefer pairing two isolated players together
                    })
                    .FirstOrDefault();

                if (partnerLetter == null) continue;

                // Only add if we don't already have a fallback connection between these two.
                string pair = (string.Compare(letter, partnerLetter, StringComparison.Ordinal) < 0)
                    ? letter + "-" + partnerLetter
                    : partnerLetter + "-" + letter;
                string fallbackName = $"Fallback-{pair}";
                if (connNames.Contains(fallbackName)) continue;

                // Register the connection.
                connections.Add(new Connection
                {
                    Name = fallbackName,
                    From = $"Spawn-{letter}",
                    To = $"Spawn-{partnerLetter}",
                    ConnectionType = "Direct",
                    GuardZone = $"Spawn-{letter}",
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = 30000,
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"fallback_guard_{fallbackName}"
                });
                connNames.Add(fallbackName);

                // Add road endpoints to both zones.
                foreach (var pLetter in new[] { letter, partnerLetter })
                {
                    var pZone = zones.FirstOrDefault(z => z.Name == $"Spawn-{pLetter}");
                    if (pZone != null)
                    {
                        pZone.Roads ??= [];
                        pZone.Roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(fallbackName)));
                    }
                }
            }
        }

        // ── Variant factory helper ────────────────────────────────────────────────

        private static Variant MakeVariant(List<string> playerLetters, string firstLetter, int totalZones, List<Zone> zones, List<Connection> connections) => new()
        {
            Orientation = new Orientation
            {
                ZeroAngleZone = playerLetters.Count > 0 ? $"Spawn-{firstLetter}" : $"Neutral-{firstLetter}",
                BaseAngleMin = 45,
                BaseAngleMax = 45,
                RandomAngleAmplitude = 360,
                RandomAngleStep = 360.0 / totalZones
            },
            Border = new Border
            {
                CornerRadius = 0.0,
                ObstaclesWidth = 3,
                ObstaclesNoise = [new NoiseEntry { Amp = 1, Freq = 12 }],
                WaterWidth = 0,
                WaterNoise = [new NoiseEntry { Amp = 1, Freq = 12 }],
                WaterType = "water grass"
            },
            Zones = zones,
            Connections = connections
        };

        // ── Spawn zone ───────────────────────────────────────────────────────────

        private static Zone BuildSpawnZone(string letter, string player, string[] ringConns, int castleCount, bool spawnFootholds)
        {
            // Index 0 = Spawn (player town), indices 1..castleCount = extra same-faction cities.
            var mainObjects = new List<MainObject>
            {
                new()
                {
                    Type = "Spawn",
                    Spawn = player,
                    RemoveGuardIfHasOwner = true,
                    GuardChance = 0.5,
                    GuardValue = 5000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "default_buildings_construction",
                    Placement = "Uniform",
                    PlacementArgs = ["true", "0.7", "0"]
                }
            };

            for (int i = 1; i < castleCount; i++)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    Faction = new TypedSelector { Type = "Random", Args = [] },
                    GuardChance = 0.5,
                    GuardValue = 2500,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "poor_buildings_construction",
                    Placement = "Uniform",
                    PlacementArgs = ["false", "-0.8", "3"]
                });
            }

            return new Zone
            {
                Name = $"Spawn-{letter}",
                Size = 1.0,
                Layout = "zone_layout_spawns",
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.0,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_start_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_start_zone"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
                MandatoryContent = [$"mandatory_content_side_{letter}"],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = 200000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 50000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 80000,
                ResourcesValuePerArea = 0,
                MainObjects = mainObjects,
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds)
            };
        }

        // ── Neutral zone ─────────────────────────────────────────────────────────

        private static Zone BuildNeutralZone(string letter, string[] ringConns, int castleCount, bool spawnFootholds)
        {
            // Index 0 = primary neutral city; indices 1..castleCount-1 = extra neutral cities.
            // All cities use FromList [] (neutral faction, not biome-matched).
            var mainObjects = new List<MainObject>
            {
                new()
                {
                    Type = "City",
                    GuardChance = 0.5,
                    GuardValue = 10000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "rich_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Uniform",
                    PlacementArgs = ["true", "0.8", "2"]
                }
            };

            for (int i = 1; i < castleCount; i++)
            {
                mainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 0.5,
                    GuardValue = 5000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "poor_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Uniform",
                    PlacementArgs = ["false", "-0.8", "3"]
                });
            }

            return new Zone
            {
                Name = $"Neutral-{letter}",
                Size = 1.0,
                Layout = "zone_layout_spawns",
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = 1.3,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [0, 10, 10, 10, 10, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["template_pool_jebus_cross_guarded_side_zone"],
                UnguardedContentPool = ["template_pool_jebus_cross_unguarded_side_zone"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_rich"],
                MandatoryContent = [$"mandatory_content_neutral_{letter}"],
                ContentCountLimits = BuildSideContentLimits(),
                GuardedContentValue = 260000,
                GuardedContentValuePerArea = 0,
                UnguardedContentValue = 40000,
                UnguardedContentValuePerArea = 0,
                ResourcesValue = 60000,
                ResourcesValuePerArea = 0,
                MainObjects = mainObjects,
                ZoneBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
                CrossroadsPosition = 0,
                Roads = BuildOuterZoneRoads(ringConns, castleCount, spawnFootholds)
            };
        }

        // ── Connections

        /// <summary>
        /// Creates guarded Direct connections between every adjacent pair of outer zones
        /// arranged in a ring (zone[i] ↔ zone[i+1], wrapping around).
        /// When <paramref name="isolatePlayers"/> is true, player–player adjacent pairs are skipped.
        /// </summary>
        private static IEnumerable<Connection> BuildRingConnections(
            List<string> playerLetters, List<string> orderedLetters, bool isolatePlayers = false)
        {
            int count = orderedLetters.Count;
            if (count < 2) yield break;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                string fromLetter = orderedLetters[i];
                string toLetter   = orderedLetters[next];

                if (isolatePlayers && playerLetters.Contains(fromLetter) && playerLetters.Contains(toLetter))
                    continue;

                string fromZone = playerLetters.Contains(fromLetter) ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone   = playerLetters.Contains(toLetter)   ? $"Spawn-{toLetter}"   : $"Neutral-{toLetter}";

                yield return new Connection
                {
                    Name = $"Ring-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Direct",
                    GuardZone = fromZone,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = 30000,
                    GuardWeeklyIncrement = 0.15,
                    GuardMatchGroup = $"ring_guard_{fromLetter}_{toLetter}"
                };
            }
        }

        /// <summary>
        /// Pairs each zone with a random non-adjacent zone and emits a Portal connection.
        /// Uses a shuffled derangement so every zone gets exactly one outgoing portal
        /// that does NOT lead to its immediate ring neighbours.
        /// </summary>
        private static IEnumerable<Connection> BuildRandomPortalConnections(
            List<string> playerLetters, List<string> orderedLetters)
        {
            int count = orderedLetters.Count;
            if (count < 2) yield break; // Need at least 2 zones for a portal.

            // Build a derangement where zone[i] -> zone[dest[i]] and dest[i] is never an
            // immediate neighbour (i-1, i, i+1 mod count).
            var rng = new Random();
            int[] dest = BuildNonAdjacentDerangement(count, rng);

            for (int i = 0; i < count; i++)
            {
                string fromLetter = orderedLetters[i];
                string toLetter   = orderedLetters[dest[i]];

                string fromZone = playerLetters.Contains(fromLetter)
                    ? $"Spawn-{fromLetter}" : $"Neutral-{fromLetter}";
                string toZone = playerLetters.Contains(toLetter)
                    ? $"Spawn-{toLetter}" : $"Neutral-{toLetter}";

                yield return new Connection
                {
                    Name = $"Portal-{fromLetter}-{toLetter}",
                    From = fromZone,
                    To = toZone,
                    ConnectionType = "Portal",
                    PortalPlacementRulesFrom = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 2 }],
                    PortalPlacementRulesTo   = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 2 }],
                    Road = true,
                    GuardEscape = false,
                    GuardValue = 25000,
                    GuardWeeklyIncrement = 0.15
                };
            }
        }

        /// <summary>
        /// Returns an array where result[i] != i and every index appears exactly once as a destination.
        /// Prefers non-adjacent targets; falls back to adjacent neighbours when no better option exists.
        /// </summary>
        private static int[] BuildNonAdjacentDerangement(int count, Random rng)
        {
            int[] dest = new int[count];
            int attempts = 0;
            while (true)
            {
                attempts++;
                var candidates = Enumerable.Range(0, count).OrderBy(_ => rng.Next()).ToList();
                bool valid = true;
                for (int i = 0; i < count; i++)
                {
                    // Prefer non-adjacent; fall back to adjacent (but never self).
                    int found = -1;
                    for (int j = 0; j < candidates.Count; j++)
                    {
                        int c = candidates[j];
                        if (c != i && c != (i + 1) % count && c != (i - 1 + count) % count)
                        { found = j; break; }
                    }
                    if (found < 0)
                    {
                        // No non-adjacent candidate left — accept any non-self candidate.
                        for (int j = 0; j < candidates.Count; j++)
                        {
                            if (candidates[j] != i) { found = j; break; }
                        }
                    }
                    if (found < 0) { valid = false; break; }
                    dest[i] = candidates[found];
                    candidates.RemoveAt(found);
                }
                if (valid) return dest;
                if (attempts > 100) break; // Should never happen, but guard against infinite loop.
            }
            // Ultimate fallback: simple rotation by half.
            int shift = Math.Max(1, count / 2);
            return Enumerable.Range(0, count).Select(i => (i + shift) % count).ToArray();
        }

        // ── Content limit helpers ────────────────────────────────────────────────

        private static List<string> BuildCenterContentLimits(int playerCount)
        {
            // JCC has limits for each player-count variant from 1..6.
            return Enumerable.Range(1, playerCount)
                .Select(n => $"content_limits_center_{n}")
                .ToList();
        }

        private static List<string> BuildSideContentLimits()
        {
            // Identical list used by every outer zone in JCC.
            var limits = new List<string>();
            for (int a = 1; a <= 5; a++)
                for (int b = a + 1; b <= 6; b++)
                    limits.Add($"content_limits_side_{a}_{b}");
            return limits;
        }

        // ── Road / endpoint factories ────────────────────────────────────────────

        /// <summary>
        /// Builds the road list for a spawn or neutral outer zone:
        /// roads to each adjacent ring connection, to the secondary city (index 1),
        /// and to the remote foothold.
        /// </summary>
        private static List<Road> BuildOuterZoneRoads(string[] ringConns, int castleCount, bool includeFoothold)
        {
            var roads = new List<Road>();
            for (int i = 1; i < castleCount; i++)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MainObjectEndpoint(i.ToString())));

            if (includeFoothold)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), MandatoryContentEndpoint("name_remote_foothold_1")));
            foreach (var rc in ringConns)
                roads.Add(PlainRoad(MainObjectEndpoint("0"), ConnectionEndpoint(rc)));
            return roads;
        }

        private static Road PlainRoad(RoadEndpoint from, RoadEndpoint to) =>
            new() { From = from, To = to };

        private static RoadEndpoint MainObjectEndpoint(string index) =>
            new() { Type = "MainObject", Args = [index] };

        private static RoadEndpoint ConnectionEndpoint(string name) =>
            new() { Type = "Connection", Args = [name] };

        private static RoadEndpoint MandatoryContentEndpoint(string name) =>
            new() { Type = "MandatoryContent", Args = [name] };

        // ── Zone layouts ─────────────────────────────────────────────────────────

        private static List<ZoneLayout> BuildZoneLayouts() =>
        [
            new ZoneLayout
            {
                Name = "zone_layout_spawns",
                ObstaclesFill = 0.24,
                ObstaclesFillVoid = 0.48,
                LakesFill = 0.3,
                MinLakeArea = 16,
                ElevationClusterScale = 0.16,
                ElevationModes =
                [
                    new ElevationMode { Weight = 2, MinElevatedFraction = 0.2, MaxElevatedFraction = 0.4 },
                    new ElevationMode { Weight = 1, MinElevatedFraction = 0.6, MaxElevatedFraction = 0.8 }
                ],
                RoadClusterArea = 160,
                GuardedEncounterResourceFractions = new GuardedEncounterResourceFractions
                {
                    CountBounds = [],
                    Fractions = [0.66]
                },
                AmbientPickupDistribution = new AmbientPickupDistribution
                {
                    Repulsion = 1.0,
                    Noise = 0.4,
                    RoadAttraction = -0.30,
                    ObstacleAttraction = 0.0,
                    GroupSizeWeights = [20, 2, 1]
                }
            }
        ];

        // ── Mandatory content ────────────────────────────────────────────────────

        private static List<MandatoryContentGroup> BuildAllMandatoryContent(
            List<string> playerLetters, List<string> neutralLetters, GeneratorSettings settings)
        {
            var groups = new List<MandatoryContentGroup>();

            foreach (var letter in playerLetters)
                groups.Add(BuildSpawnMandatoryContent(letter, settings.PlayerZoneCastles, settings.SpawnRemoteFootholds));

            foreach (var letter in neutralLetters)
                groups.Add(BuildNeutralMandatoryContent(letter, settings.NeutralZoneCastles, settings.SpawnRemoteFootholds));

            return groups;
        }

        private static MandatoryContentGroup BuildSpawnMandatoryContent(string letter, int castleCount, bool spawnFootholds)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_side_{letter}",
                Content = BuildOuterZoneMandatoryContent(isNeutral: false, castleCount, spawnFootholds)
            };
        }

        private static MandatoryContentGroup BuildNeutralMandatoryContent(string letter, int castleCount, bool spawnFootholds)
        {
            return new MandatoryContentGroup
            {
                Name = $"mandatory_content_neutral_{letter}",
                Content = BuildOuterZoneMandatoryContent(isNeutral: true, castleCount, spawnFootholds)
            };
        }

        private static List<ContentItem> BuildOuterZoneMandatoryContent(bool isNeutral, int castleCount, bool spawnFootholds)
        {
            var footholdRules = new List<ContentPlacementRule>
            {
                new() { Type = "Crossroads", Args = [], TargetMin = 0.2, TargetMax = 0.3, Weight = 0 },
                new() { Type = "MainObject", Args = ["0"], TargetMin = 0.2, TargetMax = 0.4, Weight = 0 },
            };
            if (castleCount > 1)
                footholdRules.Add(new ContentPlacementRule { Type = "MainObject", Args = ["1"], TargetMin = 0.5, TargetMax = 0.5, Weight = 2 });

            var content = new List<ContentItem>();

            if (spawnFootholds)
                content.Add(new ContentItem { Name = "name_remote_foothold_1", Sid = "remote_foothold", IsGuarded = false, Rules = footholdRules });

            content.AddRange(new List<ContentItem>
            {
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_base"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { IncludeLists = ["template_pool_jebus_cross_guarded_resource_banks_tier_3_pro"], Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.3, TargetMax = 0.6, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.5, Weight = 1 }] },
                new() { Sid = "fountain", IsGuarded = false },
                new() { Sid = "watchtower", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "mana_well", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "market" },
                new() { Sid = "forge" },
                new() { Sid = "tavern", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.2, TargetMax = 0.4, Weight = 1 }] },
                new() { Sid = "stables", IsGuarded = false, Rules = [new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.15, TargetMax = 0.30, Weight = 1 }] },
                new() { Sid = "university" },
                new() { IncludeLists = ["basic_content_list_building_hero_stats_and_skills_tier_2"] },
                new() { Sid = "mystical_tower" },
                new() { Sid = "prison", Variant = 0, IsGuarded = false },
                new() { Sid = "random_hire_5" },
                new() { IncludeLists = ["basic_content_list_building_random_hires"] },
                new() { IncludeLists = ["basic_content_list_building_guarded_units_banks_no_biome_restriction"], IsGuarded = isNeutral ? null : false },
                new() { Sid = "pandora_box", Variant = 0, SoloEncounter = true },
                new() { Sid = "mine_wood", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_ore", IsMine = true, Rules = [new ContentPlacementRule { Type = "MainObject", Args = ["0"], TargetMin = 0.0, TargetMax = 0.0, Weight = 1 }, new ContentPlacementRule { Type = "Road", Args = [], TargetMin = 0.1, TargetMax = 0.4, Weight = 2 }] },
                new() { Sid = "mine_gold", IsMine = true, Rules = [new ContentPlacementRule { Type = "Crossroads", Args = [], TargetMin = 0.1, TargetMax = 0.3, Weight = 1 }] },
                new() { Sid = "mine_crystals", IsMine = true },
                new() { Sid = "mine_mercury", IsMine = true },
                new() { Sid = "mine_gemstones", IsMine = true },
                new() { Sid = "alchemy_lab", IsMine = true },
            });

            return content;
        }

        // ── Content count limits ─────────────────────────────────────────────────

        /// <summary>
        /// Replicates the full set of contentCountLimits from Jebus Cross Classic verbatim.
        /// These are referenced by name from zones and must exist at the root level.
        /// </summary>
        private static List<ContentCountLimit> BuildAllContentCountLimits()
        {
            var sidLimits = new List<ContentSidLimit>
            {
                new() { Sid = "fountain", MaxCount = 2 },
                new() { Sid = "fountain_2", MaxCount = 2 },
                new() { Sid = "mana_well", MaxCount = 2 },
                new() { Sid = "huntsmans_camp", MaxCount = 1 },
                new() { Sid = "market", MaxCount = 1 },
                new() { Sid = "forge", MaxCount = 2 },
                new() { Sid = "celestial_sphere", MaxCount = 2 },
                new() { Sid = "arena", MaxCount = 1 },
                new() { Sid = "sacrificial_shrine", MaxCount = 1 },
                new() { Sid = "chimerologist", MaxCount = 1 },
                new() { Sid = "wise_owl", MaxCount = 3 },
                new() { Sid = "circus", MaxCount = 2 },
                new() { Sid = "infernal_cirque", MaxCount = 2 },
                new() { Sid = "university", MaxCount = 2 },
                new() { Sid = "tree_of_abundance", MaxCount = 1 },
                new() { Sid = "fickle_shrine", MaxCount = 1 },
                new() { Sid = "insaras_eye", MaxCount = 2 },
                new() { Sid = "watchtower", MaxCount = 1 },
                new() { Sid = "flattering_mirror", MaxCount = 2 },
                new() { Sid = "wind_rose", MaxCount = 1 },
                new() { Sid = "jousting_range", MaxCount = 0 },
                new() { Sid = "unforgotten_grave", MaxCount = 0 },
                new() { Sid = "petrified_memorial", MaxCount = 0 },
                new() { Sid = "the_gorge", MaxCount = 0 },
                new() { Sid = "ritual_pyre", MaxCount = 99 },
                new() { Sid = "boreal_call", MaxCount = 99 },
                new() { Sid = "point_of_balance", MaxCount = 3 },
            };

            var limits = new List<ContentCountLimit>();

            limits.Add(new ContentCountLimit { Name = "content_limits_side", Limits = sidLimits });
            limits.Add(new ContentCountLimit { Name = "content_limits_side_0_0", PlayerMin = 0, PlayerMax = 0, Limits = sidLimits });

            for (int a = 1; a <= 5; a++)
                for (int b = a + 1; b <= 6; b++)
                    limits.Add(new ContentCountLimit { Name = $"content_limits_side_{a}_{b}", PlayerMin = a, PlayerMax = b, Limits = sidLimits });

            return limits;
        }
    }
}
