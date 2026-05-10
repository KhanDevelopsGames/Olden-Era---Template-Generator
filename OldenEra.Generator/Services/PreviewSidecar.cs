namespace OldenEra.Generator.Services;

/// <summary>
/// Resolves the PNG sidecar path for an Olden Era `.rmg.json` template.
/// `Foo.rmg.json` → `Foo.png`; anything else → standard ChangeExtension.
/// </summary>
public static class PreviewSidecar
{
    public static string GetSidecarPath(string rmgJsonPath) =>
        rmgJsonPath.EndsWith(".rmg.json", System.StringComparison.OrdinalIgnoreCase)
            ? rmgJsonPath[..^".rmg.json".Length] + ".png"
            : System.IO.Path.ChangeExtension(rmgJsonPath, ".png");
}
