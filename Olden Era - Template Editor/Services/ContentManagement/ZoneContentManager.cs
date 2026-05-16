using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;
using System;
using System.Linq;
namespace OldenEraTemplateEditor.Services.ContentManagement
{
/* Content manager for defining zone-specific content */
public static class ZoneContentManager
{
    /// <summary>
    /// Player spawn zone — guaranteed starter mines anchored near the castle, a full rare mine
    /// set spread along roads, utility/economic buildings, tier-split hiring picks, one hero
    /// stat trainer, guarded resource banks, and starter loot.
    /// Grounded in the consensus across Exodus, Staircase, Kerberos, Blitz, Universe, and
    /// Yin Yang spawn zones from the example template corpus.
    /// </summary>
    public static List<ContentItem> BuildPlayerZoneMandatoryContent(GeneratorSettings settings)
    {
        var content = new List<ContentItem>();

        if (settings.SpawnRemoteFootholds)
            content.Add(ContentPresets.RemoteFoothold(settings.ZoneCfg.PlayerZoneCastles));

        content.AddRange(settings.PlayerZoneMandatoryContent);

        return content;
    }

    /// <summary>
    /// Low-quality neutral zone — t2 pools, intentionally lean mandatory content.
    /// The t2 content pools supply most of the variety; mandatory content only guarantees
    /// the essential items that every low zone must have: a few rare mines, basic utility
    /// buildings, and a handful of random hires. Modelled after Universe side zones,
    /// Kerberos connector zones, and Madness side zones from the template corpus.
    /// No high-end encounters (no dragon utopias, research labs, unstable ruins).
    /// </summary>
    public static List<ContentItem> BuildLowNeutralMandatoryContent(GeneratorSettings settings)
    {
        var content = new List<ContentItem>();

        if (settings.SpawnRemoteFootholds)
            content.Add(ContentPresets.RemoteFoothold(settings.ZoneCfg.PlayerZoneCastles));

        content.AddRange(settings.LowNeutralMandatoryContent);       

        return content;
    }

    /// <summary>
    /// Medium-quality neutral zone — t3 pools, tier-3 resource banks, medium hires, stat buildings,
    /// epic+legendary loot, pandora boxes, gold and rare mines.
    /// Based on t3-pool side/treasure zones found in Staircase, Shamrock, Blitz, Kerberos, and similar templates.
    /// No high-end encounters (no dragon utopias, research labs, unstable ruins).
    /// </summary>
    public static List<ContentItem> BuildMediumNeutralMandatoryContent(GeneratorSettings settings)
    {
        var content = new List<ContentItem>();

        if (settings.SpawnRemoteFootholds)
            content.Add(ContentPresets.RemoteFoothold(settings.ZoneCfg.PlayerZoneCastles));

        content.AddRange(settings.MediumNeutralMandatoryContent);

        return content;
    }

    /// <summary>
    /// High-quality neutral zone — t4/t5 pools, highest-challenge encounters: dragon utopias,
    /// unstable ruins, research labs, mythic scroll boxes, tier-3 hero stats, many unit banks
    /// (including biome-restricted), legendary loot, many pandora boxes, gold-heavy mines.
    /// Based on t4/t5-pool treasure zones found in Staircase, Symphony, Blitz, Crossroads, and
    /// high-zone mandatory content across the example template corpus.
    /// Only this tier spawns dragon utopias, unstable ruins, and research laboratories.
    /// </summary>
    public static List<ContentItem> BuildHighNeutralMandatoryContent(GeneratorSettings settings)
    {
        var content = new List<ContentItem>();

        if (settings.SpawnRemoteFootholds)
            content.Add(ContentPresets.RemoteFoothold(settings.ZoneCfg.PlayerZoneCastles));

        content.AddRange(settings.HighNeutralMandatoryContent);

        return content;
    }
    
        // ── Content count limits ─────────────────────────────────────────────────

        /// <summary>
        /// Builds the full set of contentCountLimits derived from all example templates.
        /// Counts reflect the typical maximum values observed across templates.
        /// </summary>
        public static List<ContentCountLimit> BuildAllContentCountLimits(GeneratorSettings settings)
        {
            var sidLimits = new List<ContentSidLimit>
            {
                // ── Banned in generated zones ────────────────────────────────────
                // black_tower sid is missing from the known values content list. To be investigated...
                new() { Sid = "black_tower",          MaxCount = 0 }, // tier-1 resource bank; too weak/out-of-place in neutral zones
                // ── Utility / buff buildings ─────────────────────────────────────
                new() { Sid = ContentIds.Fountain.Sid,             MaxCount = 2 },
                new() { Sid = ContentIds.Fountain2.Sid,           MaxCount = 2 },
                new() { Sid = ContentIds.ManaWell.Sid,            MaxCount = 2 },
                new() { Sid = ContentIds.BeerFountain.Sid,        MaxCount = 2 },
                new() { Sid = ContentIds.Market.Sid,               MaxCount = 1 },
                new() { Sid = ContentIds.Forge.Sid,                MaxCount = 2 },
                new() { Sid = ContentIds.Stables.Sid,              MaxCount = 1 },
                new() { Sid = ContentIds.Watchtower.Sid,           MaxCount = 2 },
                new() { Sid = ContentIds.WindRose.Sid,            MaxCount = 1 },
                new() { Sid = ContentIds.QuixsPath.Sid,           MaxCount = 2 },
                new() { Sid = ContentIds.CrystalTrail.Sid,        MaxCount = 3 },
                new() { Sid = ContentIds.MysteriousStone.Sid,     MaxCount = 2 },

                // ── Learning / XP buildings ──────────────────────────────────────
                new() { Sid = ContentIds.University.Sid,           MaxCount = 2 },
                new() { Sid = ContentIds.WiseOwl.Sid,             MaxCount = 4 },
                new() { Sid = ContentIds.CelestialSphere.Sid,     MaxCount = 2 },
                new() { Sid = ContentIds.PileOfBooks.Sid,        MaxCount = 2 },
                new() { Sid = ContentIds.InsarasEye.Sid,          MaxCount = 2 },
                new() { Sid = ContentIds.TearOfTruth.Sid,        MaxCount = 3 },
                new() { Sid = ContentIds.TreeOfAbundance.Sid,    MaxCount = 2 },

                // ── Hire buildings ───────────────────────────────────────────────
                new() { Sid = ContentIds.HuntsmansCamp.Sid,       MaxCount = 2 },
                new() { Sid = ContentIds.ShadyDen.Sid,            MaxCount = 2 },
                new() { Sid = ContentIds.RandomHire1.Sid,        MaxCount = 6 },
                new() { Sid = ContentIds.RandomHire2.Sid,        MaxCount = 6 },
                new() { Sid = ContentIds.RandomHire3.Sid,        MaxCount = 6 },
                new() { Sid = ContentIds.RandomHire4.Sid,        MaxCount = 6 },
                new() { Sid = ContentIds.RandomHire5.Sid,        MaxCount = 6 },
                new() { Sid = ContentIds.RandomHire6.Sid,        MaxCount = 6 },
                new() { Sid = ContentIds.RandomHire7.Sid,        MaxCount = 6 },

                // ── Combat / encounter buildings ─────────────────────────────────
                new() { Sid = ContentIds.Arena.Sid,                MaxCount = 2 },
                new() { Sid = ContentIds.SacrificialShrine.Sid,   MaxCount = 2 },
                new() { Sid = ContentIds.Chimerologist.Sid,        MaxCount = 2 },
                new() { Sid = ContentIds.Circus.Sid,               MaxCount = 2 },
                new() { Sid = ContentIds.InfernalCirque.Sid,      MaxCount = 2 },
                new() { Sid = ContentIds.FlatteringMirror.Sid,    MaxCount = 2 },
                new() { Sid = ContentIds.FickleShrine.Sid,        MaxCount = 1 },
                new() { Sid = ContentIds.PointOfBalance.Sid,     MaxCount = 3 },

                // ── Special / loot ───────────────────────────────────────────────
                new() { Sid = ContentIds.PandoraBox.Sid,          MaxCount = 4 },

                // ── Map-feature objects (typically 0 = disabled, 99 = unlimited;
                //    we cap at a sensible value so they can occasionally appear) ──
                new() { Sid = ContentIds.RitualPyre.Sid,          MaxCount = 3 },
                new() { Sid = ContentIds.BorealCall.Sid,          MaxCount = 3 },
                new() { Sid = ContentIds.JoustingRange.Sid,       MaxCount = 1 },
                new() { Sid = ContentIds.UnforgottenGrave.Sid,    MaxCount = 1 },
                new() { Sid = ContentIds.PetrifiedMemorial.Sid,   MaxCount = 1 },
                new() { Sid = ContentIds.TheGorge.Sid,            MaxCount = 1 },
            };

            // If player-zone mandatory content contains a SID more times than the default limit,
            // lift that limit so the generated template can legally place all configured items.
            var mandatorySidCounts = settings.PlayerZoneMandatoryContent
                .Where(item => !string.IsNullOrWhiteSpace(item.Sid))
                .GroupBy(item => item.Sid!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var sidLimit in sidLimits)
            {
                if (mandatorySidCounts.TryGetValue(sidLimit.Sid, out int configuredCount))
                {
                    sidLimit.MaxCount = Math.Max(sidLimit.MaxCount, configuredCount);
                }
            }

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
