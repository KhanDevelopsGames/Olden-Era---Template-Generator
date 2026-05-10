# Project Layout Refactor — Design

**Date:** 2026-05-10
**Branch:** `refactor/project-layout`
**Status:** Design approved, ready for implementation plan

## Motivation

The repository grew through a port from a WPF-only app to a shared-library + WPF + Blazor WASM layout. The result has several non-idiomatic structural smells:

1. Two projects use spaced, dashed names (`Olden Era - Template Editor`, `Olden Era - Template Editor.Tests`) while two use standard `OldenEra.X` PascalCase. Spaces in project names produce mangled root namespaces (e.g. `Olden_Era___Template_Editor`) and require quoting in every CLI invocation.
2. The solution file shares the spaced name.
3. There is both a top-level `tests/` directory (Playwright E2E) and an `Olden Era - Template Editor.Tests` project at the root. Two test homes is one too many.
4. Only the WPF app has a unit-test project. The shared `OldenEra.Generator` library — the most valuable thing to test — has none.
5. All four projects sit at the repo root with no `src/` separation, the dominant .NET convention.

The Blazor port also reinforced that Generator, Web, and the desktop editor are peer projects, not "the editor's dependencies." The current flat layout obscures that relationship.

## Target layout

```
OldenEra/                              (repo root)
├── OldenEra.slnx
├── Directory.Build.props
├── README.md
├── AGENTS.md
├── .github/
├── docs/
├── src/
│   ├── OldenEra.Generator/            (moved, contents unchanged)
│   ├── OldenEra.Web/                  (moved, contents unchanged)
│   └── OldenEra.TemplateEditor/       (renamed from "Olden Era - Template Editor")
└── tests/
    ├── OldenEra.Generator.Tests/      (NEW — xUnit, smoke test only)
    ├── OldenEra.TemplateEditor.Tests/ (renamed + moved)
    └── e2e/                           (existing Playwright suite — unchanged)
```

### Naming

- `OldenEra.Generator` — unchanged (shared core).
- `OldenEra.Web` — unchanged (Blazor WASM).
- `OldenEra.TemplateEditor` — renamed from `Olden Era - Template Editor`. "Template" is retained because the WPF app is a template editor, not a direct map editor.
- `OldenEra.TemplateEditor.Tests` — renamed accordingly.
- `OldenEra.Generator.Tests` — new xUnit project, seeded with one smoke test.
- `OldenEra.slnx` — solution renamed from the spaced name.

## Migration steps

The work is mechanical. It can be done as one commit or split move-then-rename; either is acceptable. All work occurs on the `refactor/project-layout` branch.

1. **Move and rename projects** with `git mv` to preserve history:
   - `Olden Era - Template Editor` → `src/OldenEra.TemplateEditor`
   - `Olden Era - Template Editor.Tests` → `tests/OldenEra.TemplateEditor.Tests`
   - `OldenEra.Generator` → `src/OldenEra.Generator`
   - `OldenEra.Web` → `src/OldenEra.Web`
   - Rename the two `.csproj` files inside the moved TemplateEditor folders.
   - Set explicit `RootNamespace` and `AssemblyName` in both renamed `.csproj`s to `OldenEra.TemplateEditor` / `OldenEra.TemplateEditor.Tests`.

2. **Rename the solution** to `OldenEra.slnx` and rewrite the four `<Project Path="...">` entries to the new paths.

3. **Update C# namespaces** inside TemplateEditor. The current code uses the MSBuild-mangled `Olden_Era___Template_Editor` namespace. Replace project-wide with `OldenEra.TemplateEditor`. Also update `x:Class` attributes in XAML files.

4. **Update touchpoints** (full grep pass required, but at minimum):
   - `.github/workflows/*.yml` — build paths, release workflow paths, artifact paths
   - `tests/e2e/` Playwright config — any path references to the web project
   - `README.md`, `AGENTS.md`, `docs/plans/*.md`
   - `.gitignore` — path-specific ignores
   - Auto-memory files referencing old paths (`project_orientation.md`, `codebase_map.md`, `feedback_macos_dotnet_workflow.md`)

5. **Add `OldenEra.Generator.Tests`**:
   - New xUnit project at `tests/OldenEra.Generator.Tests/`.
   - `ProjectReference` to `src/OldenEra.Generator`.
   - Seed with one happy-path smoke test for `TemplateGenerator` so CI exercises it.

## Verification gates

All must pass before merging:

1. `dotnet build OldenEra.slnx` clean on macOS (Generator + Web build; TemplateEditor failure on Mac is expected and documented).
2. `dotnet build OldenEra.slnx` clean on Windows (all four projects).
3. `dotnet test` runs both `OldenEra.TemplateEditor.Tests` and `OldenEra.Generator.Tests` green.
4. WPF app launches on Windows and loads a template (manual smoke test).
5. `dotnet watch run --project src/OldenEra.Web` starts the Blazor app and renders the home page.
6. Playwright E2E suite passes against the renamed web project paths.
7. Release workflow dry-run on a throwaway tag — its paths break silently otherwise.

## Rollout

- Single PR off `refactor/project-layout`. Splitting it produces intermediate commits with a half-renamed solution, which is harder to review.
- Land it when no other in-flight feature branches exist, or coordinate rebases. Every existing branch will conflict heavily.

## Out of scope (YAGNI)

- Real coverage in `OldenEra.Generator.Tests` beyond the smoke test. Coverage is a separate, ongoing effort.
- `OldenEra.Web.Tests` (bUnit). Playwright covers the web.
- Code-style or namespace cleanup beyond what the rename forces.
- CI overhaul. Update paths; do not restructure workflows.
- Central package management (`Directory.Packages.props`).
- Moving `docs/` or `.github/`.

## Follow-ups to consider later

- The `OldenEra.Generator.Models.Generator` namespace is redundant. Flattening it is a separate, reviewable change.
- Grow real test coverage in `OldenEra.Generator.Tests`.
