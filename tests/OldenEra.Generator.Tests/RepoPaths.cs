namespace OldenEra.Generator.Tests;

internal static class RepoPaths
{
    public static string GeneratorDataRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "src", "OldenEra.TemplateEditor", "GameData", "GeneratorData");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "Could not locate src/OldenEra.TemplateEditor/GameData/GeneratorData by walking up from AppContext.BaseDirectory.");
    }
}
