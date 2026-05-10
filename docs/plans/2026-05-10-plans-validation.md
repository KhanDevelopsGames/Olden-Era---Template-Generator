# Plan validation against `main` (post-refactor commit `30de0b0`)

Date: 2026-05-10. Validated against current `main` after the layout refactor merged in PR #6.

## Summary

All three thread plans remain **executable** with path updates. Generator line numbers held; UI label line numbers held. The refactor introduced one helpful change (a cross-platform test project) that improves Thread 1.

## Repo layout changes (apply to all three plans)

| Old path (in plans) | New path on `main` |
|---|---|
| `Olden Era - Template Editor.slnx` | `OldenEra.slnx` |
| `OldenEra.Generator/` | `src/OldenEra.Generator/` |
| `Olden Era - Template Editor/` | `src/OldenEra.TemplateEditor/` |
| `OldenEra.Web/` | `src/OldenEra.Web/` |
| `Olden Era - Template Editor.Tests/` | `tests/OldenEra.TemplateEditor.Tests/` (`net10.0-windows`) |
| (did not exist) | `tests/OldenEra.Generator.Tests/` (`net10.0`, cross-platform) — **new** |
| `Olden Era - Template Editor/GameData/` | `src/OldenEra.TemplateEditor/GameData/` |
| `Olden Era - Template Editor/Views/*.xaml` | `src/OldenEra.TemplateEditor/Views/*.xaml` (confirmed: `HeroesPanel.xaml`, `WinConditionsPanel.xaml`, `MapPanel.xaml`, `TopologyPanel.xaml`, `ZonesPanel.xaml`, `ExperimentalPanel.xaml`) |

Anything in a plan that says `OldenEra.Generator/...`, `OldenEra.Web/...`, or `Olden Era - Template Editor/...` should be read with `src/` prepended (or `tests/` for the test projects).

## Key anchor verification (all confirmed still valid)

- `src/OldenEra.Generator/Services/TemplateGenerator.cs`
  - `:30` — `public static RmgTemplate Generate(GeneratorSettings settings)` ✓
  - `:184` / `:233` — `ExtractZoneLetter` ✓ (Thread 1 Item 4c)
  - `:1000` — Tournament dispatch (silent fallback bug site) ✓ (Thread 1 Item 4a)
  - `:1023` — `BuildVariantTournament` ✓
  - `:2441` — `GeneralResourcesPoor` literal ✓ (Thread 1 Item 1; Thread 2 Item 2)
- `src/OldenEra.TemplateEditor/MainWindow.xaml.cs`
  - `:23-24` — `SimpleModeMaxZones` / `AdvancedModeMaxZones` ✓ (Thread 1 Item 2 — still need to move to shared lib)
  - `:379` — `private bool Validate()` ✓ (Thread 1 Item 2)
- `src/OldenEra.Web/Components/HeroSettingsPanel.razor` lines 6/11/16 ✓ (Thread 3 Item 1)
- `src/OldenEra.TemplateEditor/Views/HeroesPanel.xaml` lines 22/32/42 ✓ (Thread 3 Item 1)
- `src/OldenEra.Web/Components/WinConditionsPanel.razor` lines 7/11/40/57/65 ✓ (Thread 3 Items 2 & 3)
- `src/OldenEra.TemplateEditor/Views/WinConditionsPanel.xaml` lines 26/36/48/66/69 ✓

## Per-plan adjustments

### Thread 1 (generation correctness)

- **Improvement available:** `tests/OldenEra.Generator.Tests/` is now `net10.0` (cross-platform). Put the new tests there, not in `tests/OldenEra.TemplateEditor.Tests/` (which stays `net10.0-windows`). This was already a recommendation in the plan; the refactor made it the obvious choice.
  - `EmittedIdValidationTests.cs` → `tests/OldenEra.Generator.Tests/`
  - `SettingsValidatorTests.cs` → `tests/OldenEra.Generator.Tests/`
  - `ReferenceTemplateParityTests.cs` → `tests/OldenEra.Generator.Tests/`
  - `TopologyInvariantTests.cs` → `tests/OldenEra.Generator.Tests/`
  - All four can now run on macOS/Linux CI.
- GameData copy-to-output: pattern in `tests/OldenEra.Generator.Tests/SmokeTests.cs` should be the template — check there before inventing a new resolution helper.
- Item 2 caller update on the Web side: search for `TemplateGenerator.Generate(` under `src/OldenEra.Web/` to find the Generate-button handler.

### Thread 2 (GameData catalog)

- Project file path is now `src/OldenEra.Generator/OldenEra.Generator.csproj`. The proposed `<Content Include="...\GameData\GeneratorData\**\*.json"...>` element needs the path updated to `..\OldenEra.TemplateEditor\GameData\GeneratorData\**\*.json` (relative to the new csproj location).
- Heroes precondition still applies — confirmed no `*hero*` files under `src/OldenEra.TemplateEditor/GameData/`.
- New tests for `GameDataCatalog` should go in `tests/OldenEra.Generator.Tests/` (cross-platform), same reasoning as Thread 1.

### Thread 3 (text clarity)

- WPF UserControls split is confirmed: labels live in `src/OldenEra.TemplateEditor/Views/*.xaml`, not in `MainWindow.xaml`. The plan's assumption was correct.
- WPF panel-name mapping (note these differ from the Razor names):
  - Web `AdvancedPanel.razor` + `ZoneConfigPanel.razor` → WPF `Views/ZonesPanel.xaml`
  - Web `ExperimentalZonePanel.razor` + `PerTierOverridesPanel.razor` → WPF `Views/ExperimentalPanel.xaml`
  - Web `HeroSettingsPanel.razor` → WPF `Views/HeroesPanel.xaml`
  - Web `WinConditionsPanel.razor` → WPF `Views/WinConditionsPanel.xaml`
  - Web `MapSettingsPanel.razor` → WPF `Views/MapPanel.xaml`
  - Web `TopologyPicker.razor` → WPF `Views/TopologyPanel.xaml`
- e2e suite path was already correct in the plan: `tests/e2e/specs/`.

## Build/test commands (updated)

- `dotnet build OldenEra.slnx`
- `dotnet test OldenEra.slnx`
- `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` (runs on macOS now)
- `dotnet run --project src/OldenEra.Web/OldenEra.Web.csproj`
- `dotnet run --project src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj` (Windows only)

## Net effect

- Thread 1: **easier to execute** — cross-platform test project removes the macOS dev blocker for new tests.
- Thread 2: same effort, just one csproj-relative path adjustment.
- Thread 3: same effort, WPF file mapping is now confirmed.

No plan needs rewriting; plans are still valid with the path map above.
