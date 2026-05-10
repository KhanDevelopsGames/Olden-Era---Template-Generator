# AGENTS.md

Guidance for agents working on this repository.

## Project Overview

This solution generates `.rmg.json` random map template files for Heroes of Might and Magic: Olden Era. It ships in two forms backed by a shared library:

```text
OldenEra.Generator/                     net10.0 class library
  Models/, Services/                    generator + SkiaSharp PNG renderer
OldenEra.Web/                           net10.0 Blazor WebAssembly host
  Pages/, Components/, Services/        deployed to GitHub Pages
Olden Era - Template Editor/            net10.0-windows WPF host
  MainWindow.xaml(.cs), Themes/         Steam auto-detect, .oetgs file dialogs
Olden Era - Template Editor.Tests/      net10.0-windows xUnit tests
```

The main solution is:

```powershell
dotnet build "Olden Era - Template Editor.slnx"
```

The test project uses xUnit and targets the generator/model layer plus the SkiaSharp renderer. It currently targets `net10.0-windows` because some pixel-sampling tests use `System.Windows.Media.Imaging.PngBitmapDecoder`.

Example templates are stored in:

```text
Olden Era - Template Editor/GameData/ExampleTemplates
```

Use those examples as the primary local reference for the `.rmg.json` shape and expected naming style.

### Where each kind of code lives

- **Generator logic, DTOs, preview rendering** → `OldenEra.Generator/`. Pure C#, no UI dependencies. Both hosts call into this.
- **Steam install detection, file-save dialogs, .oetgs file I/O, custom title bar** → `Olden Era - Template Editor/`. Anything that requires WPF, the Windows registry, or local file system access stays here.
- **Browser settings persistence (localStorage), file download via JS interop, update banner** → `OldenEra.Web/`. Anything sandbox-bound to the browser stays here.
- **Generation tests, JSON-shape tests, renderer structural tests** → `Olden Era - Template Editor.Tests/`.

When porting a new feature: implement the core in the library, expose it through public API, then wire up each host.

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

## Cross-platform development

The repo's `Directory.Build.props` sets `<EnableWindowsTargeting>true</EnableWindowsTargeting>` so the WPF and test projects compile on macOS and Linux. This is a compile-only fix, not a runtime one:

- ✅ `dotnet build "Olden Era - Template Editor.slnx"` works on macOS/Linux/Windows.
- ✅ `dotnet run --project OldenEra.Web` works on macOS/Linux/Windows. Serves at `http://localhost:5230/`.
- ❌ `dotnet run --project "Olden Era - Template Editor"` works only on Windows (`Microsoft.WindowsDesktop.App` runtime is Windows-only).
- ❌ `dotnet test` works only on Windows for the same reason — the testhost requires `Microsoft.WindowsDesktop.App`. CI runs tests on `windows-latest` via `.github/workflows/tests.yml`.

When working on macOS or Linux: treat `dotnet build` as the local validation gate. Trust CI for actual test execution. Don't try to drop the test project's `-windows` target without first removing every `System.Windows.*` reference in the test code — pixel-sampling tests in `TemplatePreviewRenderer_EncodesNeutralQualityAndCastleCounts` (and similar) use `PngBitmapDecoder`.

## Blazor WebAssembly: required wasm-tools workload

`OldenEra.Web` uses SkiaSharp via `SkiaSharp.NativeAssets.WebAssembly`. The csproj sets `<WasmBuildNative>true</WasmBuildNative>`, which **requires** the `wasm-tools` SDK workload:

```bash
dotnet workload install wasm-tools
```

This is a one-time install (~500 MB toolchain). Without it, `dotnet build OldenEra.Web` fails with `NETSDK1147`. The CI workflow runs this install before each publish.

If you ever see `System.DllNotFoundException: libSkiaSharp` at runtime in the browser, the cause is one of:

1. `<WasmBuildNative>true</WasmBuildNative>` is missing or conditional.
2. The `wasm-tools` workload wasn't installed when the build ran.
3. The deploy didn't actually upload the new `dotnet.native.*.wasm` (which should be ~8 MB Release / ~23 MB Debug — the size jump confirms native linking succeeded).

A managed-only build will still produce a deployable site, but every Skia API call throws on first use. Always exercise the actual native call path before declaring web changes done.

## Cutting a release

Releases are produced by `.github/workflows/release.yml` and are triggered by pushing a `v*` tag. The workflow runs on `windows-latest`, publishes the WPF editor as a self-contained single-file `win-x64` exe, zips it as `OldenEraTemplateGenerator-<tag>-win-x64.zip`, and creates a GitHub Release with auto-generated notes.

To cut a release:

1. Bump `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in `Olden Era - Template Editor/Olden Era - Template Editor.csproj` to the new `X.Y.Z`. Keep all three in sync. `AssemblyVersion`/`FileVersion` use the four-part `X.Y.Z.0` form.
2. Commit on `main`: `chore: bump version to vX.Y.Z`.
3. Tag the commit and push:
   ```bash
   git tag vX.Y.Z
   git push origin main vX.Y.Z
   ```
4. Watch the workflow: `gh run watch $(gh run list --workflow=release.yml --limit 1 --json databaseId -q '.[0].databaseId')`.
5. Verify the release: `gh release view vX.Y.Z`.

Rules:

- The tag MUST start with `v` and match the csproj version. `v0.6.0` ↔ `<Version>0.6.0</Version>`.
- Never re-tag an existing version. If a release is broken, bump the patch (`v0.6.1`) instead of force-pushing the tag.
- Do not edit `release.yml` as part of a version bump unless the workflow itself needs a fix; keep release-mechanism changes in their own PR.
- The `workflow_dispatch` trigger exists for re-runs and accepts a `tag` input — use it only when re-publishing an already-pushed tag, not as a substitute for tagging.

## Debugging Blazor errors

The web app's "An unhandled error has occurred. Reload" banner is a generic placeholder. The actual exception is logged to the **browser DevTools console**, not the `dotnet run` terminal. When users report errors:

1. Have them open DevTools (Cmd/Ctrl + Shift + I) → Console tab.
2. Reproduce the error.
3. Copy the red `crit:` line and the inner exception(s) underneath.

Server-side `dotnet run` logs only show server-side errors. WASM exceptions never appear there.
