using System.IO.Compression;
using System.Reflection;
using System.Text;
using OldenEra.Generator.Constants;

namespace OldenEra.Generator.Services;

public static class InstallerPackager
{
    public static byte[] BuildPlainZip(string templateName, byte[] rmgJson, byte[] png)
    {
        string safeName = SanitizeTemplateName(templateName);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, $"{safeName}.rmg.json", rmgJson);
            WriteEntry(archive, $"{safeName}.png", png);
        }
        return ms.ToArray();
    }

    public static byte[] BuildInstallerZip(string templateName, byte[] rmgJson, byte[] png)
    {
        string safeName = SanitizeTemplateName(templateName);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, $"{safeName}.rmg.json", rmgJson);
            WriteEntry(archive, $"{safeName}.png", png);
            WriteEntry(archive, "install.bat", LoadResource("install.bat", safeName));
            // PowerShell single-quoted literal: ' becomes ''.
            WriteEntry(archive, "install.ps1", LoadResource("install.ps1", safeName.Replace("'", "''")));
            WriteEntry(archive, "README.txt", LoadResource("README.txt", safeName));
        }
        return ms.ToArray();
    }

    /// <summary>Strip filename-hostile characters and PowerShell single-quote
    /// escapes so the name is safe as a zip entry, a Windows filename, and
    /// a PowerShell single-quoted literal in the embedded install script.</summary>
    public static string SanitizeTemplateName(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            return "Custom Template";
        char[] invalid = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0'];
        var sb = new StringBuilder(templateName.Length);
        foreach (char c in templateName)
            sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
        string trimmed = sb.ToString().Trim().TrimEnd('.');
        return trimmed.Length == 0 ? "Custom Template" : trimmed;
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] data)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    private static byte[] LoadResource(string fileName, string templateName)
    {
        var asm = typeof(InstallerPackager).Assembly;
        string resourceName = $"OldenEra.Generator.Resources.Installer.{fileName}";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string text = reader.ReadToEnd()
            .Replace("{TEMPLATE_NAME}", templateName)
            .Replace("{STEAM_APP_ID}", OldenEraSteamInfo.AppId)
            .Replace("{STEAM_FOLDER_NAME}", OldenEraSteamInfo.SteamFolderName)
            .Replace("{TEMPLATES_SUBPATH}", OldenEraSteamInfo.TemplatesSubpath);
        return Encoding.UTF8.GetBytes(text);
    }
}
