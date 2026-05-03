# AGENTS.md

Guidance for agents working on this repository.

## Project Overview

This is a C# WPF desktop app for generating `.rmg.json` random map template files for Heroes of Might and Magic: Olden Era.

The main solution is:

```powershell
dotnet build "Olden Era - Template Editor.slnx"
```

The app project is:

```text
Olden Era - Template Editor/Olden Era - Template Editor.csproj
```

The unit test project is:

```text
Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj
```

The test project uses xUnit and targets the generator/model layer without requiring WPF UI automation.

Example templates are stored in:

```text
Olden Era - Template Editor/ExampleTemplates
```

Use those examples as the primary local reference for the `.rmg.json` shape and expected naming style.

## Discovering Olden Era `.rmg.json` Files

Olden Era map templates are almost always located under the game's install directory:

```text
[Steam Game Install Location]\HeroesOldenEra_Data\StreamingAssets\map_templates
```

The standard Steam install location is usually:

```text
C:\Program Files (x86)\Steam\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates
```

Do not assume that path always exists. Users may have installed Steam or the game on another drive, especially on systems with multiple disks or custom Steam libraries.

Useful discovery checks:

```powershell
$defaultTemplates = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates"
Test-Path $defaultTemplates
Get-ChildItem -Path $defaultTemplates -Filter "*.rmg.json" -ErrorAction SilentlyContinue
```

If the default path is missing, inspect Steam library locations. The primary Steam library file is usually:

```text
C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf
```

Additional libraries typically contain:

```text
[Steam Library]\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates
```

A quick targeted check for common Steam library roots:

```powershell
Get-PSDrive -PSProvider FileSystem | ForEach-Object {
    $candidate = Join-Path $_.Root "SteamLibrary\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates"
    if (Test-Path $candidate) {
        Get-Item $candidate
    }
}
```

Avoid broad recursive searches across entire drives unless the user asks for that; they can be slow.

## Commands And Testing

This is a C# solution. Before handing off changes, build the solution:

```powershell
dotnet build "Olden Era - Template Editor.slnx"
```

Unit tests are part of the normal development process. When changing behavior, always add or update focused unit tests that cover the change, then ensure the full unit test suite passes before handing off:

```powershell
dotnet test "Olden Era - Template Editor.slnx"
```

If `dotnet` is not on PATH in the current shell, use the standard SDK path explicitly:

```powershell
& "C:\Program Files\dotnet\dotnet.exe" test "Olden Era - Template Editor.slnx"
```

For changes to generator logic, prefer focused unit tests around the model/service behavior rather than only testing through the UI.

## Visual Testing

For UI changes, build and run the app, then visually check the affected workflow. The app should open cleanly, controls should be readable, and the expected action should work without layout breakage.

Typical local run command:

```powershell
dotnet run --project "Olden Era - Template Editor/Olden Era - Template Editor.csproj"
```

When possible, open the solution in Visual Studio as part of manual verification:

```powershell
Start-Process "Olden Era - Template Editor.slnx"
```

At minimum, verify that the configured options can create a `.rmg.json` file. Generated template validation currently stops there: do not claim that generated templates are guaranteed to generate a playable map in game. In-game validation must be left to users until the project has a reliable automated or documented game-level validation workflow.

## Development Notes

- Keep changes scoped to the app's current C# and WPF patterns.
- Add or update unit tests for every behavior change, and do not treat the work as complete until those tests pass.
- Prefer structured JSON serialization/deserialization over manual string editing for template files.
- Treat the bundled example templates as fixtures and references; do not overwrite them during ad hoc testing.
- When saving generated files during testing, use a temporary location unless the user explicitly wants files written into their game install.
- Be careful with paths containing spaces, especially the solution name, project directory, Steam install path, and template filenames.
- Assume other individuals or agents may be working elsewhere in the repository at the same time. Ignore and preserve changes you did not specifically make; do not revert, overwrite, or include them in your work unless the user explicitly asks.
