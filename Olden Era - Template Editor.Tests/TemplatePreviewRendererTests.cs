using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace Olden_Era___Template_Editor.Tests;

public class TemplatePreviewRendererTests
{
    private static GeneratorSettings BuildSettings(MapTopology topology) => new()
    {
        TemplateName = "Renderer Layout Test",
        GameMode = "Classic",
        MapSize = 200,
        PlayerCount = 2,
        Topology = topology,
    };

    [Fact]
    public void ComputeLayout_AssignsPositionToEveryZone()
    {
        var settings = BuildSettings(MapTopology.Random);
        RmgTemplate template = TemplateGenerator.Generate(settings);

        var layout = TemplatePreviewRenderer.ComputeLayout(template, settings.Topology);

        var zones = template.Variants?.FirstOrDefault()?.Zones ?? new List<Zone>();
        Assert.NotEmpty(zones);
        Assert.Equal(zones.Count, layout.Count);
        foreach (var zone in zones)
            Assert.True(layout.ContainsKey(zone.Name), $"Missing position for zone '{zone.Name}'");
    }

    [Fact]
    public void ComputeLayout_PositionsAreInsideCanvas()
    {
        var settings = BuildSettings(MapTopology.Random);
        RmgTemplate template = TemplateGenerator.Generate(settings);

        var layout = TemplatePreviewRenderer.ComputeLayout(template, settings.Topology);

        Assert.NotEmpty(layout);
        foreach (var (name, pos) in layout)
        {
            Assert.True(pos.X >= 0 && pos.X <= TemplatePreviewRenderer.Width,
                $"Zone '{name}' X={pos.X} outside [0,{TemplatePreviewRenderer.Width}]");
            Assert.True(pos.Y >= 0 && pos.Y <= TemplatePreviewRenderer.Height,
                $"Zone '{name}' Y={pos.Y} outside [0,{TemplatePreviewRenderer.Height}]");
        }
    }

    [Theory]
    [InlineData(MapTopology.Random)]
    [InlineData(MapTopology.Default)]
    [InlineData(MapTopology.HubAndSpoke)]
    [InlineData(MapTopology.Chain)]
    public void ComputeLayout_HandlesAllTopologies(MapTopology topology)
    {
        var settings = BuildSettings(topology);
        RmgTemplate template = TemplateGenerator.Generate(settings);

        var layout = TemplatePreviewRenderer.ComputeLayout(template, topology);

        Assert.NotEmpty(layout);
    }

    [Fact]
    public void RenderPng_ReturnsValidPngBytes()
    {
        var settings = BuildSettings(MapTopology.Default);
        RmgTemplate template = TemplateGenerator.Generate(settings);

        byte[] png = TemplatePreviewRenderer.RenderPng(template, settings.Topology);

        Assert.NotNull(png);
        Assert.True(png.Length >= 8, "PNG must be at least 8 bytes (signature length)");
        // PNG magic: 89 50 4E 47 0D 0A 1A 0A
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]);
        Assert.Equal(0x4E, png[2]);
        Assert.Equal(0x47, png[3]);
        Assert.Equal(0x0D, png[4]);
        Assert.Equal(0x0A, png[5]);
        Assert.Equal(0x1A, png[6]);
        Assert.Equal(0x0A, png[7]);
    }
}
