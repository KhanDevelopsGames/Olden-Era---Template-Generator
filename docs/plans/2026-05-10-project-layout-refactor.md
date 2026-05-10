# Project Layout Refactor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Rename the two spaced project names to `OldenEra.TemplateEditor[.Tests]`, move all four projects under `src/` and `tests/`, rename the solution to `OldenEra.slnx`, and add a new `OldenEra.Generator.Tests` project with a smoke test.

**Architecture:** Pure structural refactor. No production behavior changes. Source code edits are limited to `RootNamespace`/`AssemblyName` properties, namespace declarations, `x:Class` attributes, `ProjectReference` paths, and CI/docs path references. The `OldenEraTemplateGenerator` AssemblyName is preserved so the released artifact filename does not change.

**Tech Stack:** .NET 10, C#, WPF (net10.0-windows), Blazor WASM, xUnit, GitHub Actions, Playwright (existing E2E).

**Branch:** `refactor/project-layout` (already created, design doc already committed).

**Design reference:** `docs/plans/2026-05-10-project-layout-refactor-design.md`.

---

## Pre-flight context

The repository today has:

- `Olden Era - Template Editor/Olden Era - Template Editor.csproj` — WPF app. `RootNamespace = Olden_Era___Template_Editor`, `AssemblyName = OldenEraTemplateGenerator`.
- `Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj` — xUnit tests for the WPF app, also references `OldenEra.Generator`.
- `OldenEra.Generator/OldenEra.Generator.csproj` — netstandard-style class library, the shared core.
- `OldenEra.Web/OldenEra.Web.csproj` — Blazor WASM app.
- `Olden Era - Template Editor.slnx` — solution file at root.

The WPF C# code and XAML use the MSBuild-mangled namespace `Olden_Era___Template_Editor`. The tests project uses `Olden_Era___Template_Editor.Tests`. Both must change.

Workflows that reference old paths: `.github/workflows/release.yml`, `tests.yml`, `deploy-web.yml`, `e2e.yml`. Playwright config also references `OldenEra.Web` by relative path: `tests/e2e/playwright.config.ts`.

Docs that mention old paths: `README.md`, `AGENTS.md`, every file under `docs/plans/`.

---

## Task 1: Verify clean working tree and baseline build

**Files:** none — environment check only.

**Step 1: Confirm branch and clean status**

Run:
```bash
git status
git rev-parse --abbrev-ref HEAD
```

Expected: branch is `refactor/project-layout`. Working tree clean (only the design doc commit ahead of main).

**Step 2: Baseline build on macOS to confirm starting point**

Run:
```bash
dotnet build "Olden Era - Template Editor.slnx" -c Release 2>&1 | tail -20
```

Expected: `OldenEra.Generator` and `OldenEra.Web` build successfully. `Olden Era - Template Editor` and its `.Tests` project fail or are skipped on macOS — that's fine and documented in the macOS workflow notes.

Record any unexpected failures before proceeding. **Do not continue if the Generator or Web project fails to build on the baseline.**

**Step 3: No commit**

This is a check, not a change.

---

## Task 2: Create `src/` and `tests/` directories and move `OldenEra.Generator`

We move the cleanly-named projects first because they require no rename — only relocation. Each move is its own commit so any breakage bisects cleanly.

**Files:**
- Move: `OldenEra.Generator/` → `src/OldenEra.Generator/`

**Step 1: Create src/ directory**

Run:
```bash
mkdir -p src
```

**Step 2: Move the project with `git mv`**

Run:
```bash
git mv OldenEra.Generator src/OldenEra.Generator
```

**Step 3: Update solution file path**

Edit `Olden Era - Template Editor.slnx`. Change:

```xml
<Project Path="OldenEra.Generator/OldenEra.Generator.csproj" />
```

to:

```xml
<Project Path="src/OldenEra.Generator/OldenEra.Generator.csproj" />
```

**Step 4: Update `ProjectReference` paths in the two consumers**

In `Olden Era - Template Editor/Olden Era - Template Editor.csproj`, change:
```xml
<ProjectReference Include="..\OldenEra.Generator\OldenEra.Generator.csproj" />
```
to:
```xml
<ProjectReference Include="..\src\OldenEra.Generator\OldenEra.Generator.csproj" />
```

In `Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj`, change:
```xml
<ProjectReference Include="..\OldenEra.Generator\OldenEra.Generator.csproj" />
```
to:
```xml
<ProjectReference Include="..\src\OldenEra.Generator\OldenEra.Generator.csproj" />
```

In `OldenEra.Web/OldenEra.Web.csproj`, find any `ProjectReference` to Generator and update it identically. (Read the file first; if it has no such reference, skip.)

**Step 5: Verify build**

Run:
```bash
dotnet build "Olden Era - Template Editor.slnx" -c Release 2>&1 | tail -20
```

Expected: same result as Task 1's baseline. Generator and Web build; WPF skipped on macOS.

**Step 6: Commit**

```bash
git add -A
git commit -m "refactor: move OldenEra.Generator under src/"
```

---

## Task 3: Move `OldenEra.Web` under `src/`

**Files:**
- Move: `OldenEra.Web/` → `src/OldenEra.Web/`

**Step 1: Move with `git mv`**

```bash
git mv OldenEra.Web src/OldenEra.Web
```

**Step 2: Update solution file path**

In `Olden Era - Template Editor.slnx`, change:
```xml
<Project Path="OldenEra.Web/OldenEra.Web.csproj" />
```
to:
```xml
<Project Path="src/OldenEra.Web/OldenEra.Web.csproj" />
```

**Step 3: Verify Web builds**

```bash
dotnet build src/OldenEra.Web/OldenEra.Web.csproj -c Release 2>&1 | tail -10
```

Expected: build succeeds.

**Step 4: Commit**

```bash
git add -A
git commit -m "refactor: move OldenEra.Web under src/"
```

---

## Task 4: Rename and move the WPF editor project

This is the biggest single step. Do it as one commit because every C#/XAML file in the project references the old namespace and they must change atomically.

**Files:**
- Move: `Olden Era - Template Editor/` → `src/OldenEra.TemplateEditor/`
- Rename inside: `Olden Era - Template Editor.csproj` → `OldenEra.TemplateEditor.csproj`
- Edit: `RootNamespace`, `ProjectReference` paths in csproj
- Edit: namespace declarations in every `.cs` and `x:Class`/namespace attributes in every `.xaml` (15+ files)

**Step 1: Move the directory**

```bash
git mv "Olden Era - Template Editor" src/OldenEra.TemplateEditor
```

**Step 2: Rename the .csproj file**

```bash
git mv "src/OldenEra.TemplateEditor/Olden Era - Template Editor.csproj" src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj
```

**Step 3: Edit the .csproj**

In `src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj`:

- Change `<RootNamespace>Olden_Era___Template_Editor</RootNamespace>` to `<RootNamespace>OldenEra.TemplateEditor</RootNamespace>`.
- **Keep** `<AssemblyName>OldenEraTemplateGenerator</AssemblyName>` — this is the released `.exe` filename and changing it would alter the artifact users download.
- Update the Generator `ProjectReference` path from `..\src\OldenEra.Generator\...` to `..\OldenEra.Generator\...` (the projects are now siblings under `src/`).

**Step 4: Replace the namespace across all C# and XAML files**

Run from repo root:
```bash
grep -rl "Olden_Era___Template_Editor" src/OldenEra.TemplateEditor --include="*.cs" --include="*.xaml" \
  | xargs sed -i '' 's/Olden_Era___Template_Editor/OldenEra.TemplateEditor/g'
```

Note: `sed -i ''` is the macOS form. On Linux use `sed -i`.

**Step 5: Verify no occurrences of the old namespace remain in source**

```bash
grep -rn "Olden_Era___Template_Editor" src/OldenEra.TemplateEditor --include="*.cs" --include="*.xaml"
```

Expected: no output.

**Step 6: Update the solution file**

In `Olden Era - Template Editor.slnx`, change:
```xml
<Project Path="Olden Era - Template Editor/Olden Era - Template Editor.csproj" />
```
to:
```xml
<Project Path="src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj" />
```

**Step 7: Update the test project's `ProjectReference` to the editor**

In `Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj`, change:
```xml
<ProjectReference Include="..\Olden Era - Template Editor\Olden Era - Template Editor.csproj" />
```
to:
```xml
<ProjectReference Include="..\..\src\OldenEra.TemplateEditor\OldenEra.TemplateEditor.csproj" />
```

(Note: this path assumes the tests project will be moved to `tests/` in Task 5. We use the eventual path now to avoid an extra edit. Until Task 5 the tests project is at `Olden Era - Template Editor.Tests/` so the relative path is wrong temporarily. **That is fine — we will not build the tests project until after Task 5.**)

Also update the Generator reference in the same file from `..\src\OldenEra.Generator\...` to `..\..\src\OldenEra.Generator\...`.

**Step 8: Verify Generator and Web still build**

```bash
dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj -c Release 2>&1 | tail -10
dotnet build src/OldenEra.Web/OldenEra.Web.csproj -c Release 2>&1 | tail -10
```

Expected: both succeed. WPF cannot be built on macOS so we don't try.

**Step 9: Commit**

```bash
git add -A
git commit -m "refactor: rename WPF project to OldenEra.TemplateEditor and move under src/"
```

---

## Task 5: Rename and move the test project

**Files:**
- Move: `Olden Era - Template Editor.Tests/` → `tests/OldenEra.TemplateEditor.Tests/`
- Rename inside: `Olden Era - Template Editor.Tests.csproj` → `OldenEra.TemplateEditor.Tests.csproj`
- Edit: `RootNamespace`, namespace declarations in `.cs` files

**Step 1: Create tests/ directory and move**

```bash
mkdir -p tests
git mv "Olden Era - Template Editor.Tests" tests/OldenEra.TemplateEditor.Tests
```

**Step 2: Rename the .csproj**

```bash
git mv "tests/OldenEra.TemplateEditor.Tests/Olden Era - Template Editor.Tests.csproj" tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj
```

**Step 3: Edit the .csproj**

In `tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj`:

- Change `<RootNamespace>Olden_Era___Template_Editor.Tests</RootNamespace>` to `<RootNamespace>OldenEra.TemplateEditor.Tests</RootNamespace>`.
- Confirm both `ProjectReference` entries use `..\..\src\...` paths (already set in Task 5/Step 7 — verify, do not duplicate). They should be:
  ```xml
  <ProjectReference Include="..\..\src\OldenEra.TemplateEditor\OldenEra.TemplateEditor.csproj" />
  <ProjectReference Include="..\..\src\OldenEra.Generator\OldenEra.Generator.csproj" />
  ```

**Step 4: Update namespaces in test source files**

```bash
grep -rl "Olden_Era___Template_Editor" tests/OldenEra.TemplateEditor.Tests --include="*.cs" \
  | xargs sed -i '' 's/Olden_Era___Template_Editor/OldenEra.TemplateEditor/g'
```

**Step 5: Verify**

```bash
grep -rn "Olden_Era___Template_Editor" tests/OldenEra.TemplateEditor.Tests
```

Expected: no output.

**Step 6: Update solution file**

In `Olden Era - Template Editor.slnx`, change:
```xml
<Project Path="Olden Era - Template Editor.Tests/Olden Era - Template Editor.Tests.csproj" />
```
to:
```xml
<Project Path="tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj" />
```

**Step 7: Commit**

```bash
git add -A
git commit -m "refactor: rename test project to OldenEra.TemplateEditor.Tests and move under tests/"
```

---

## Task 6: Rename the solution file

**Files:**
- Move: `Olden Era - Template Editor.slnx` → `OldenEra.slnx`

**Step 1: Rename**

```bash
git mv "Olden Era - Template Editor.slnx" OldenEra.slnx
```

**Step 2: Verify Generator+Web still build via the solution**

```bash
dotnet build OldenEra.slnx -c Release 2>&1 | tail -20
```

Expected: Generator and Web build; WPF + Tests fail on macOS as documented.

**Step 3: Commit**

```bash
git add -A
git commit -m "refactor: rename solution to OldenEra.slnx"
```

---

## Task 7: Add `OldenEra.Generator.Tests` (TDD)

A new xUnit project for the shared core. Seed with one smoke test that exercises a happy path of `TemplateGenerator`. Real coverage growth is out of scope.

**Files:**
- Create: `tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`
- Create: `tests/OldenEra.Generator.Tests/SmokeTests.cs`
- Modify: `OldenEra.slnx` (add new project)

**Step 1: Inspect TemplateGenerator's public API**

Read `src/OldenEra.Generator/Services/TemplateGenerator.cs` and `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs` to identify the simplest happy-path call. Look at `tests/OldenEra.TemplateEditor.Tests/TemplateGeneratorTests.cs` for an existing example of how the editor's tests invoke it; mirror that pattern but in a new `OldenEra.Generator.Tests` namespace and without depending on the WPF project.

The smoke test should: construct default `GeneratorSettings`, run the generator, assert it produces a non-null result with at least one zone (or whatever the simplest invariant is). Do **not** copy the full editor test suite — one test only.

**Step 2: Write the failing test (file does not exist yet, project does not exist yet)**

Create `tests/OldenEra.Generator.Tests/SmokeTests.cs`:

```csharp
using OldenEra.Generator.Models.Generator;
using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class SmokeTests
{
    [Fact]
    public void TemplateGenerator_DefaultSettings_ProducesNonEmptyResult()
    {
        var settings = new GeneratorSettings();
        var generator = new TemplateGenerator();

        var result = generator.Generate(settings);

        Assert.NotNull(result);
    }
}
```

**Adapt the test body** to whatever the actual `TemplateGenerator` API looks like — the constructor may take parameters, the method may be named differently, and the result type's most basic invariant may differ. The point is one test that calls one happy path and asserts something non-trivial.

**Step 3: Create the .csproj**

Create `tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>OldenEra.Generator.Tests</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\OldenEra.Generator\OldenEra.Generator.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

Note `TargetFramework` is `net10.0` (not `net10.0-windows`) because Generator is cross-platform. This is the whole point — the tests must run on Linux CI.

**Step 4: Run the test to verify it passes**

```bash
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj -c Release 2>&1 | tail -30
```

Expected: 1 test passed.

If the test fails because the API doesn't match, **fix the test, not the production code.** This task is about adding test infrastructure, not changing behavior.

**Step 5: Add the project to the solution**

Edit `OldenEra.slnx` and add a new entry:

```xml
<Project Path="tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj" />
```

**Step 6: Verify solution-wide test run**

```bash
dotnet test OldenEra.slnx -c Release --filter "FullyQualifiedName~OldenEra.Generator.Tests" 2>&1 | tail -20
```

Expected: 1 test passed (the WPF tests project will be skipped on macOS).

**Step 7: Commit**

```bash
git add tests/OldenEra.Generator.Tests OldenEra.slnx
git commit -m "test: add OldenEra.Generator.Tests with smoke test"
```

---

## Task 8: Update CI workflow paths

Four workflow files reference old paths. Update each.

**Files:**
- Modify: `.github/workflows/release.yml`
- Modify: `.github/workflows/tests.yml`
- Modify: `.github/workflows/deploy-web.yml`
- Modify: `.github/workflows/e2e.yml`

**Step 1: Update `release.yml`**

Change:
```yaml
run: dotnet publish "Olden Era - Template Editor/Olden Era - Template Editor.csproj" -c Release -r win-x64 -o publish
```
to:
```yaml
run: dotnet publish src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj -c Release -r win-x64 -o publish
```

**Step 2: Update `tests.yml`**

Change:
```yaml
run: dotnet build "Olden Era - Template Editor.slnx" --configuration Release
```
to:
```yaml
run: dotnet build OldenEra.slnx --configuration Release
```

**Step 3: Update `deploy-web.yml`**

Change:
```yaml
run: dotnet publish OldenEra.Web/OldenEra.Web.csproj -c Release -o publish
```
to:
```yaml
run: dotnet publish src/OldenEra.Web/OldenEra.Web.csproj -c Release -o publish
```

**Step 4: Update `e2e.yml`**

Change both occurrences:
```yaml
run: dotnet restore OldenEra.Web/OldenEra.Web.csproj
...
run: dotnet build OldenEra.Web/OldenEra.Web.csproj -c Release --no-restore
```
to:
```yaml
run: dotnet restore src/OldenEra.Web/OldenEra.Web.csproj
...
run: dotnet build src/OldenEra.Web/OldenEra.Web.csproj -c Release --no-restore
```

**Step 5: Verify YAML is still valid**

Eyeball each file for indentation. A simple sanity check:

```bash
grep -n "Olden Era\|Olden_Era\|OldenEra.Web/\|Olden Era - Template" .github/workflows/*.yml
```

Expected: no output (empty result means no stale references remain).

**Step 6: Commit**

```bash
git add .github/workflows
git commit -m "ci: update workflow paths for new project layout"
```

---

## Task 9: Update Playwright config

**Files:**
- Modify: `tests/e2e/playwright.config.ts`

**Step 1: Edit the webServer command**

Change:
```typescript
command: 'dotnet run --project ../../OldenEra.Web -c Release --no-launch-profile --urls http://localhost:5230',
```
to:
```typescript
command: 'dotnet run --project ../../src/OldenEra.Web -c Release --no-launch-profile --urls http://localhost:5230',
```

(The Blazor dev-server feedback memory says `dotnet watch run` is preferred for development, but the Playwright config uses `dotnet run` with `-c Release` for CI determinism. Leave that command form alone — only update the path.)

**Step 2: Verify**

```bash
grep -n "OldenEra.Web\|Olden Era" tests/e2e/playwright.config.ts
```

Expected: only the updated path.

**Step 3: Commit**

```bash
git add tests/e2e/playwright.config.ts
git commit -m "test(e2e): update Web project path in Playwright config"
```

---

## Task 10: Update README, AGENTS.md, and docs

These reference old paths in build/run instructions. Outdated commands frustrate new contributors. Plan documents under `docs/plans/` are historical records of past designs — do **not** rewrite history in those. Only fix `README.md` and `AGENTS.md`.

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md`

**Step 1: Find every old-path reference in README.md and AGENTS.md**

```bash
grep -n "Olden Era - Template Editor\|Olden_Era___Template_Editor\|OldenEra.Web/\|OldenEra.Generator/\|Olden Era - Template Editor.slnx" README.md AGENTS.md
```

Read each match in context. For each, decide the new equivalent:

| Old reference | New reference |
|---|---|
| `Olden Era - Template Editor.slnx` | `OldenEra.slnx` |
| `Olden Era - Template Editor/...csproj` | `src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj` |
| `Olden Era - Template Editor.Tests/...csproj` | `tests/OldenEra.TemplateEditor.Tests/OldenEra.TemplateEditor.Tests.csproj` |
| `OldenEra.Web/OldenEra.Web.csproj` | `src/OldenEra.Web/OldenEra.Web.csproj` |
| `OldenEra.Generator/OldenEra.Generator.csproj` | `src/OldenEra.Generator/OldenEra.Generator.csproj` |
| `Olden_Era___Template_Editor` (namespace) | `OldenEra.TemplateEditor` |

**Step 2: Apply the edits**

Use the `Edit` tool per occurrence — these are documentation files where the surrounding sentence may need adjustment, so a blanket sed is too risky. Read each line in context first.

**Step 3: Verify nothing was missed in the two files**

```bash
grep -n "Olden Era - Template Editor\|Olden_Era___Template_Editor" README.md AGENTS.md
```

Expected: no output, OR only output that is a deliberate historical reference (e.g. "renamed from Olden Era - Template Editor in 2026-05"). If unsure, leave the historical reference and proceed.

**Step 4: Commit**

```bash
git add README.md AGENTS.md
git commit -m "docs: update paths and project names in README and AGENTS"
```

---

## Task 11: Final solution-level build and test on macOS

Sanity check before Windows verification.

**Step 1: Restore and build**

```bash
dotnet restore OldenEra.slnx
dotnet build OldenEra.slnx -c Release 2>&1 | tail -30
```

Expected: Generator, Web, and `OldenEra.Generator.Tests` build. WPF + `OldenEra.TemplateEditor.Tests` skipped on macOS (documented limitation).

**Step 2: Run cross-platform tests**

```bash
dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj -c Release 2>&1 | tail -10
```

Expected: 1 test passed.

**Step 3: Smoke-run the Blazor web app**

```bash
dotnet watch run --project src/OldenEra.Web 2>&1 &
WATCH_PID=$!
sleep 20
curl -sf http://localhost:5230/ > /dev/null && echo "WEB OK" || echo "WEB FAIL"
kill $WATCH_PID 2>/dev/null
```

Expected: `WEB OK`. (The exact port may differ — check the watch output. The point is the app starts.)

**Step 4: No commit**

This is verification only.

---

## Task 12: Windows verification (manual)

The macOS environment cannot build WPF or run the WPF tests. The author must do this on Windows before merging. Document the required steps in this plan so the human running them knows what to check.

**Manual checklist for the Windows verifier:**

1. `git fetch && git checkout refactor/project-layout`
2. `dotnet build OldenEra.slnx -c Release` — all four projects build clean.
3. `dotnet test OldenEra.slnx -c Release` — both test projects run, all green.
4. `dotnet run --project src\OldenEra.TemplateEditor` — the WPF app launches and the main window renders. Open a template and confirm it loads. Close the app.
5. `dotnet publish src\OldenEra.TemplateEditor\OldenEra.TemplateEditor.csproj -c Release -r win-x64 -o publish-test` — publish succeeds, `publish-test\OldenEraTemplateGenerator.exe` exists (note the AssemblyName is preserved so the filename is unchanged).
6. Run `publish-test\OldenEraTemplateGenerator.exe` — it launches.

If any step fails, capture the error and stop. Do not merge.

**Step 1: Add a Windows verification note to the PR description**

When opening the PR (next task), include the checklist above so the reviewer knows what was verified and what still needs Windows hands.

---

## Task 13: Push branch and open PR

**Step 1: Push the branch**

```bash
git push -u origin refactor/project-layout
```

**Step 2: Open PR via `gh`**

```bash
gh pr create --title "refactor: project layout (rename, src/tests, generator tests)" --body "$(cat <<'EOF'
## Summary
- Rename `Olden Era - Template Editor` → `OldenEra.TemplateEditor`; `.Tests` likewise.
- Move all four projects under `src/` and `tests/`.
- Rename solution to `OldenEra.slnx`.
- Add `OldenEra.Generator.Tests` (xUnit) with a smoke test for the shared core.

The released `.exe` filename is unchanged: `AssemblyName=OldenEraTemplateGenerator` is preserved.

Design: `docs/plans/2026-05-10-project-layout-refactor-design.md`
Implementation plan: `docs/plans/2026-05-10-project-layout-refactor.md`

## Test plan
- [x] macOS: `dotnet build OldenEra.slnx -c Release` (Generator + Web + Generator.Tests)
- [x] macOS: `dotnet test tests/OldenEra.Generator.Tests/...` green
- [x] macOS: Blazor web app starts via `dotnet watch run`
- [ ] **Windows: `dotnet build OldenEra.slnx -c Release` clean (all 4 projects)**
- [ ] **Windows: `dotnet test OldenEra.slnx -c Release` all green**
- [ ] **Windows: WPF app launches, loads a template**
- [ ] **Windows: `dotnet publish` produces `OldenEraTemplateGenerator.exe`**
- [ ] CI: `tests.yml` green
- [ ] CI: `deploy-web.yml` green (or dry-run on this branch)
- [ ] CI: `e2e.yml` green
- [ ] CI: `release.yml` — dry-run on a throwaway tag before next real release

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Step 3: Return PR URL** so the user can review.

---

## Done

After merge:

- Update auto-memory files (`project_orientation.md`, `codebase_map.md`, `feedback_macos_dotnet_workflow.md`) to reflect new paths. This is a follow-up by the next session that touches them — not part of this refactor.
- The redundant `OldenEra.Generator.Models.Generator` namespace flattening remains a separate, explicit follow-up.
