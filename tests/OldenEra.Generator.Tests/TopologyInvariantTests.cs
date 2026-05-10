using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class TopologyInvariantTests
{
    // ── 4a: Tournament must be exactly 2 players ──────────────────────────

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void Tournament_WithMoreThanTwoPlayers_Throws(int playerCount)
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = playerCount,
            Topology = MapTopology.Default,
            GameEndConditions = new GameEndConditions { VictoryCondition = "win_condition_6" },
            TournamentRules = new TournamentRules { Enabled = true },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 4 },
        };

        var ex = Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
        Assert.Contains("Tournament", ex.Message);
        Assert.Contains(playerCount.ToString(), ex.Message);
    }

    [Fact]
    public void Tournament_WithExactlyTwoPlayers_Succeeds()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 2,
            Topology = MapTopology.Default,
            GameEndConditions = new GameEndConditions { VictoryCondition = "win_condition_6" },
            TournamentRules = new TournamentRules { Enabled = true },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 4 },
        };

        var template = TemplateGenerator.Generate(settings);
        Assert.NotNull(template);
    }

    // ── 4b: HubAndSpoke + CityHold → hub becomes the hold city ────────────

    [Fact]
    public void HubAndSpoke_WithCityHold_MarksHubAsHoldCity()
    {
        var template = GenerateWithCityHold(MapTopology.HubAndSpoke);

        var zones = template.Variants![0].Zones!;
        var hub = zones.Single(z => z.Name == "Hub");
        Assert.True(HasHoldCityMarker(hub), "Hub zone should have HoldCityWinCon = true.");

        foreach (var z in zones.Where(z => z.Name != "Hub"))
        {
            Assert.False(HasHoldCityMarker(z), $"Non-hub zone {z.Name} should not be marked as hold city.");
        }
    }

    [Fact]
    public void HubAndSpoke_WithoutCityHold_HasNoHoldCityMarker()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 4,
            Topology = MapTopology.HubAndSpoke,
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 0 },
        };

        var template = TemplateGenerator.Generate(settings);
        var zones = template.Variants![0].Zones!;
        Assert.All(zones, z => Assert.False(HasHoldCityMarker(z), $"Zone {z.Name}"));
    }

    [Fact]
    public void Chain_WithCityHold_MarksANeutralZone_NotAPlayerZone()
    {
        var template = GenerateWithCityHold(MapTopology.Chain);

        var zones = template.Variants![0].Zones!;
        var marked = zones.Where(HasHoldCityMarker).ToList();
        Assert.Single(marked);
        Assert.StartsWith("Neutral-", marked[0].Name);
    }

    // ── 4c: tier override resolves by zone letter (regression for 59b98ce) ─

    [Fact]
    public void NeutralTierOverride_ResolvesByLetter_LowAndHighDifferOnGuardWeeklyIncrement()
    {
        // Player count 4 ⇒ neutral letters start at E.
        // With Advanced enabled and one Low-no-castle + one High-castle, plan is:
        //   Neutral-E = Low,  Neutral-F = High.
        const double lowInc = 0.07;
        const double highInc = 0.21;

        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 4,
            Topology = MapTopology.Default,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 0,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    NeutralLowNoCastleCount = 1,
                    NeutralHighCastleCount = 1,
                    LowTier = new TierOverrides { GuardWeeklyIncrement = lowInc },
                    HighTier = new TierOverrides { GuardWeeklyIncrement = highInc },
                },
            },
        };

        var template = TemplateGenerator.Generate(settings);
        var zones = template.Variants![0].Zones!;
        var e = zones.Single(z => z.Name == "Neutral-E");
        var f = zones.Single(z => z.Name == "Neutral-F");

        Assert.Equal(lowInc, e.GuardWeeklyIncrement);
        Assert.Equal(highInc, f.GuardWeeklyIncrement);
    }

    [Fact]
    public void NonPrefixedZoneName_DoesNotCrashTierResolution()
    {
        // HubAndSpoke produces a zone literally named "Hub" with no -letter suffix.
        // ExtractZoneLetter returns null for it; the code must skip tier resolution.
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 4,
            Topology = MapTopology.HubAndSpoke,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 0,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    LowTier = new TierOverrides { GuardWeeklyIncrement = 0.5 },
                    HighTier = new TierOverrides { GuardWeeklyIncrement = 0.99 },
                },
            },
        };

        // Should not throw.
        var template = TemplateGenerator.Generate(settings);
        var hub = template.Variants![0].Zones!.Single(z => z.Name == "Hub");
        Assert.NotNull(hub);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static RmgTemplate GenerateWithCityHold(MapTopology topology)
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "test",
            PlayerCount = 4,
            Topology = topology,
            GameEndConditions = new GameEndConditions { CityHold = true },
            ZoneCfg = new ZoneConfiguration { NeutralZoneCount = 4 },
        };
        return TemplateGenerator.Generate(settings);
    }

    private static bool HasHoldCityMarker(Zone z) =>
        z.MainObjects?.Any(o => o.HoldCityWinCon == true) ?? false;
}
