using System.IO.Compression;
using System.Text;
using System.Text.Json;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Tests;

public class InstallerPackagerTests
{
    private const string TemplateName = "My Test Template";

    private static (byte[] json, byte[] png) Fixture()
    {
        var rmg = new RmgTemplate { Name = TemplateName };
        var json = JsonSerializer.SerializeToUtf8Bytes(rmg);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        return (json, png);
    }

    [Fact]
    public void BuildPlainZip_ContainsJsonAndPng()
    {
        var (json, png) = Fixture();
        byte[] zip = InstallerPackager.BuildPlainZip(TemplateName, json, png);

        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.Equal(2, archive.Entries.Count);
        Assert.NotNull(archive.GetEntry($"{TemplateName}.rmg.json"));
        Assert.NotNull(archive.GetEntry($"{TemplateName}.png"));
    }

    [Fact]
    public void BuildInstallerZip_ContainsAllFivePiecesWithSubstitutions()
    {
        var (json, png) = Fixture();
        byte[] zip = InstallerPackager.BuildInstallerZip(TemplateName, json, png);

        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.Equal(5, archive.Entries.Count);

        var jsonEntry = archive.GetEntry($"{TemplateName}.rmg.json");
        Assert.NotNull(jsonEntry);
        using (var s = jsonEntry!.Open())
        {
            var roundTrip = JsonSerializer.Deserialize<RmgTemplate>(s);
            Assert.Equal(TemplateName, roundTrip!.Name);
        }

        var pngEntry = archive.GetEntry($"{TemplateName}.png");
        Assert.NotNull(pngEntry);
        var pngBytes = new byte[8];
        using (var s = pngEntry!.Open())
            s.ReadExactly(pngBytes);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, pngBytes);

        foreach (var name in new[] { "install.bat", "install.ps1", "README.txt" })
        {
            var entry = archive.GetEntry(name);
            Assert.NotNull(entry);
            using var sr = new StreamReader(entry!.Open(), Encoding.UTF8);
            string text = sr.ReadToEnd();
            Assert.DoesNotContain("{TEMPLATE_NAME}", text);
            Assert.DoesNotContain("{STEAM_APP_ID}", text);
            Assert.DoesNotContain("{STEAM_FOLDER_NAME}", text);
            Assert.DoesNotContain("{TEMPLATES_SUBPATH}", text);
        }

        var psEntry = archive.GetEntry("install.ps1")!;
        using var psReader = new StreamReader(psEntry.Open(), Encoding.UTF8);
        string psText = psReader.ReadToEnd();
        Assert.Contains(TemplateName, psText);
        Assert.Contains("3105440", psText);
    }

    [Theory]
    [InlineData("Foo/Bar", "Foo_Bar")]
    [InlineData("a:b*c?d", "a_b_c_d")]
    [InlineData("trailing.", "trailing")]
    [InlineData("   ", "Custom Template")]
    [InlineData("", "Custom Template")]
    [InlineData("OK Name", "OK Name")]
    public void SanitizeTemplateName_ScrubsHostileCharacters(string input, string expected)
    {
        Assert.Equal(expected, InstallerPackager.SanitizeTemplateName(input));
    }

    [Fact]
    public void BuildInstallerZip_EscapesSingleQuoteForPowerShell()
    {
        var (json, png) = Fixture();
        byte[] zip = InstallerPackager.BuildInstallerZip("O'Brien", json, png);

        using var ms = new MemoryStream(zip);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        Assert.NotNull(archive.GetEntry("O'Brien.rmg.json"));

        var psEntry = archive.GetEntry("install.ps1")!;
        using var sr = new StreamReader(psEntry.Open(), Encoding.UTF8);
        string ps = sr.ReadToEnd();
        // PowerShell single-quoted literal: ' must be doubled to embed safely.
        Assert.Contains("'O''Brien'", ps);
    }
}
