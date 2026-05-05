using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace Olden_Era___Template_Editor.Tests;

public class VisualTemplateDesignerTests
{
    [Fact]
    public void VisualTemplateDocument_SerializesVersionAndCanvasCoordinates()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Zones[0].CanvasX = 123;
        document.Zones[0].CanvasY = 456;

        string json = JsonSerializer.Serialize(document);
        VisualTemplateDocument? deserialized = JsonSerializer.Deserialize<VisualTemplateDocument>(json);

        Assert.Contains("\"version\":", json, StringComparison.Ordinal);
        Assert.NotNull(deserialized);
        Assert.Equal(VisualTemplateDocument.CurrentVersion, deserialized.Version);
        Assert.Equal(123, deserialized.Zones[0].CanvasX);
        Assert.Equal(456, deserialized.Zones[0].CanvasY);
    }

    [Fact]
    public void Validate_DuplicatePlayerSpawnFails()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Zones.Add(new VisualZone
        {
            Id = "p1b",
            ExportLetter = "C",
            ZoneType = VisualZoneType.PlayerSpawn,
            PlayerSlot = 1,
            CanvasX = 350,
            CanvasY = 500
        });

        VisualValidationResult result = VisualTemplateValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Player 1 has more than one spawn", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_MissingContiguousPlayerSpawnFails()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Zones[1].PlayerSlot = 3;

        VisualValidationResult result = VisualTemplateValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Player 2 is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_DisconnectedGraphFails()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Zones.Add(Neutral("C", "n1", 350, 500));
        document.Connections.Add(new VisualConnection { FromZoneId = "p1", ToZoneId = "p2" });

        VisualValidationResult result = VisualTemplateValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("All zones must be connected", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_CrossingDirectConnectionsFailButPortalsMayCross()
    {
        VisualTemplateDocument direct = CrossingDocument(VisualConnectionType.Direct);
        VisualValidationResult directResult = VisualTemplateValidator.Validate(direct);

        VisualTemplateDocument portal = CrossingDocument(VisualConnectionType.Portal);
        VisualValidationResult portalResult = VisualTemplateValidator.Validate(portal);

        Assert.False(directResult.IsValid);
        Assert.Contains(directResult.Errors, error => error.Contains("Crossing direct connections", StringComparison.Ordinal));
        Assert.True(portalResult.IsValid);
    }

    [Fact]
    public void Validate_InvalidFactionMatchTargetFails()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Zones[0].Castles[0] = new VisualCastle
        {
            FactionMode = VisualCastleFactionMode.MatchPlayer,
            MatchPlayerSlot = 8
        };

        VisualValidationResult result = VisualTemplateValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Match-to-player", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_ExportsVisualZonesConnectionsGuardStrengthAndFactions()
    {
        VisualTemplateDocument document = BasicDocument();
        VisualZone neutral = Neutral("C", "n1", 350, 350);
        neutral.ZoneType = VisualZoneType.NeutralLow;
        neutral.CastleCount = 1;
        neutral.Castles = [new VisualCastle { FactionMode = VisualCastleFactionMode.MatchPlayer, MatchPlayerSlot = 1 }];
        document.Zones.Add(neutral);
        document.Zones[0].Castles[0] = new VisualCastle
        {
            FactionMode = VisualCastleFactionMode.Restricted,
            AllowedFactions = ["Temple", "Grove"]
        };
        document.Connections.Add(new VisualConnection
        {
            FromZoneId = "p1",
            ToZoneId = "n1",
            ConnectionType = VisualConnectionType.Direct,
            GuardStrengthPercent = 150
        });
        document.Connections.Add(new VisualConnection
        {
            FromZoneId = "n1",
            ToZoneId = "p2",
            ConnectionType = VisualConnectionType.Portal,
            GuardStrengthPercent = 50
        });

        RmgTemplate template = VisualTemplateGenerator.Generate(document);
        Variant variant = Assert.Single(template.Variants ?? []);
        var zones = (variant.Zones ?? []).ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var connections = variant.Connections ?? [];

        Assert.Contains("Spawn-A", zones.Keys);
        Assert.Contains("Spawn-B", zones.Keys);
        Assert.Contains("Neutral-C", zones.Keys);
        Assert.Equal("zone_layout_sides", zones["Neutral-C"].Layout);
        Assert.Equal(["Human", "Nature"], zones["Spawn-A"].MainObjects?[0].Faction?.Args);
        Assert.Equal("Match", zones["Neutral-C"].MainObjects?[0].Faction?.Type);
        Assert.Equal(["0", "Spawn-A"], zones["Neutral-C"].MainObjects?[0].Faction?.Args);
        Assert.Contains(connections, connection => connection.ConnectionType == "Direct" && connection.GuardValue == 45000);
        Assert.Contains(connections, connection => connection.ConnectionType == "Portal" && connection.GuardValue == 12500);
    }

    [Fact]
    public void Generate_CityHoldMarksNeutralCastleAsHoldTarget()
    {
        VisualTemplateDocument document = BasicDocument();
        document.VictoryCondition = "win_condition_5";
        VisualZone neutral = Neutral("C", "n1", 350, 350);
        neutral.CastleCount = 1;
        neutral.Castles = [new VisualCastle()];
        document.Zones.Add(neutral);
        document.Connections.Add(new VisualConnection { FromZoneId = "p1", ToZoneId = "n1" });
        document.Connections.Add(new VisualConnection { FromZoneId = "n1", ToZoneId = "p2" });

        Zone holdZone = Assert.Single(VisualTemplateGenerator.Generate(document).Variants?[0].Zones ?? [],
            zone => zone.Name == "Neutral-C");

        Assert.True(holdZone.MainObjects?[0].HoldCityWinCon);
    }

    [Fact]
    public void CopyPaste_DuplicatesZoneSettingsWithoutConnectionsAndWithNewLetter()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Connections.Add(new VisualConnection { FromZoneId = "p1", ToZoneId = "p2" });

        VisualTemplateClipboardPayload payload = VisualTemplateOperations.CopyZones(document, ["p1"]);
        List<VisualZone> pasted = VisualTemplateOperations.PasteZones(document, payload);

        VisualZone zone = Assert.Single(pasted);
        Assert.NotEqual("p1", zone.Id);
        Assert.Equal(VisualZoneType.PlayerSpawn, zone.ZoneType);
        Assert.Equal(1, zone.PlayerSlot);
        Assert.Equal("C", zone.ExportLetter);
        Assert.Single(document.Connections);
    }

    [Fact]
    public void VisualTemplatePreviewPngWriter_RendersSevenHundredPixelPreview()
    {
        VisualTemplateDocument document = BasicDocument();
        document.Connections.Add(new VisualConnection { FromZoneId = "p1", ToZoneId = "p2" });

        BitmapSource bitmap = RunOnStaThread(() => VisualTemplatePreviewPngWriter.Render(document));

        Assert.Equal(700, bitmap.PixelWidth);
        Assert.Equal(700, bitmap.PixelHeight);
    }

    private static VisualTemplateDocument BasicDocument() => new()
    {
        TemplateName = "Visual Test",
        Zones =
        [
            new VisualZone
            {
                Id = "p1",
                ExportLetter = "A",
                ZoneType = VisualZoneType.PlayerSpawn,
                PlayerSlot = 1,
                CanvasX = 120,
                CanvasY = 120,
                CastleCount = 1,
                Castles = [new VisualCastle()]
            },
            new VisualZone
            {
                Id = "p2",
                ExportLetter = "B",
                ZoneType = VisualZoneType.PlayerSpawn,
                PlayerSlot = 2,
                CanvasX = 580,
                CanvasY = 580,
                CastleCount = 1,
                Castles = [new VisualCastle()]
            }
        ]
    };

    private static VisualZone Neutral(string letter, string id, double x, double y) => new()
    {
        Id = id,
        ExportLetter = letter,
        ZoneType = VisualZoneType.NeutralMedium,
        CanvasX = x,
        CanvasY = y,
        CastleCount = 0,
        Castles = []
    };

    private static VisualTemplateDocument CrossingDocument(VisualConnectionType crossingType)
    {
        VisualTemplateDocument document = BasicDocument();
        document.Zones[0].CanvasX = 100;
        document.Zones[0].CanvasY = 100;
        document.Zones[1].CanvasX = 600;
        document.Zones[1].CanvasY = 600;
        document.Zones.Add(Neutral("C", "n1", 100, 600));
        document.Zones.Add(Neutral("D", "n2", 600, 100));
        document.Connections.Add(new VisualConnection { FromZoneId = "p1", ToZoneId = "p2", ConnectionType = VisualConnectionType.Direct });
        document.Connections.Add(new VisualConnection { FromZoneId = "n1", ToZoneId = "n2", ConnectionType = crossingType });
        document.Connections.Add(new VisualConnection { FromZoneId = "p1", ToZoneId = "n1", ConnectionType = VisualConnectionType.Direct });
        return document;
    }

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T? result = default;
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (thrown is not null)
            throw new InvalidOperationException("The STA action failed.", thrown);

        return result!;
    }
}
