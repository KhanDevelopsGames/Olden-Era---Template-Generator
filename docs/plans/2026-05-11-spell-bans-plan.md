# Spell bans (#24) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let users ban spells in either host. Banning a spell produces `globalBans.magics: ["spell_id", ...]` in the generated `.rmg.json` and round-trips through `.oetgs`.

**Architecture:** Add a `BannedSpells` list to `GeneratorSettings.HeroSettings` and `SettingsFile`, wire it through `SettingsMapper`, emit it from `TemplateGenerator` next to the existing items/heroes ban blocks, and add a parallel ban UI to `HeroSettingsPanel.razor` (Web) and `HeroesPanel.xaml(.cs)` (WPF) that mirrors the existing hero-bans surface.

**Tech Stack:** .NET 10, xUnit (`tests/OldenEra.Generator.Tests`), Blazor WASM, WPF.

**Working tree:** `.worktrees/heroes-bans-ui` on branch `feature/heroes-bans-ui`. Use this worktree for all work.

**Conventions checked:**
- Hero/item bans currently emit without id validation — spells follow suit (no `KnownValues` change).
- Hero ban checkboxes are read at save time with no per-control dirty tracking wired in MainWindow — spell bans mirror this.
- `CommunityCatalog.Default.Spells` (`src/OldenEra.Generator/Services/CommunityCatalog.cs:111`) is the canonical spell source; reuse it.
- Tests live in `tests/OldenEra.Generator.Tests`. Run with `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`.
- macOS can build Generator + Web; WPF host build is CI-only.

Reference: design doc at `docs/plans/2026-05-11-spell-bans-design.md`.

---

## Task 1: Add `BannedSpells` to the generator settings model

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs:41` (add property next to `HeroBans` inside `HeroSettings`)

**Step 1: Edit `HeroSettings`**

Add this property immediately after the `HeroBans` property block (after line 41):

```csharp
/// <summary>
/// Spell IDs (e.g. <c>"spell.fly"</c>) banned from the template.
/// Emitted into <c>globalBans.magics</c>; sibling of <see cref="HeroBans"/>.
/// </summary>
public List<string> BannedSpells { get; set; } = new();
```

**Step 2: Verify build**

Run: `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj --nologo`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs
git commit -m "feat(model): add HeroSettings.BannedSpells

Sibling of HeroBans; will be emitted into globalBans.magics.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: Generator emits `globalBans.magics` (TDD)

**Files:**
- Test: `tests/OldenEra.Generator.Tests/SpellBansTests.cs` (create)
- Modify: `src/OldenEra.Generator/Services/TemplateGenerator.cs` (add block after line 135, after the hero-bans block)

**Step 1: Look at how existing global-ban tests are structured**

Search to understand the test idiom:

```bash
grep -rn "GlobalBans\.Heroes\|GlobalBans\.Items\|globalBans" tests/OldenEra.Generator.Tests/ | head -20
```

If no direct tests exist for the heroes/items ban emission, use the `TemplateGenerator` entry-point pattern visible in other tests (e.g. `SettingsValidatorTests.cs`). Build a minimal `GeneratorSettings`, run the generator, assert on `template.GlobalBans.Magics`.

**Step 2: Write failing tests**

Create `tests/OldenEra.Generator.Tests/SpellBansTests.cs` with these four tests:

```csharp
using System.Collections.Generic;
using System.Linq;
using OldenEra.Generator.Models.Generator;
using OldenEra.Generator.Services;
using Xunit;

namespace OldenEra.Generator.Tests;

public class SpellBansTests
{
    private static GeneratorSettings MakeSettings()
    {
        // Mirror the minimal-settings pattern used by other tests in this project.
        // If existing tests use a helper / fixture, replace this body to call it.
        return new GeneratorSettings();
    }

    [Fact]
    public void BannedSpells_EmitsGlobalBansMagics()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string> { "spell.fly", "spell.town_portal" };

        var template = new TemplateGenerator().Generate(s);

        Assert.NotNull(template.GlobalBans);
        Assert.NotNull(template.GlobalBans!.Magics);
        Assert.Contains("spell.fly", template.GlobalBans.Magics!);
        Assert.Contains("spell.town_portal", template.GlobalBans.Magics!);
    }

    [Fact]
    public void BannedSpells_Empty_DoesNotCreateMagicsArray()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string>();

        var template = new TemplateGenerator().Generate(s);

        Assert.True(template.GlobalBans is null || template.GlobalBans.Magics is null);
    }

    [Fact]
    public void BannedSpells_DedupesIds()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string> { "spell.fly", "spell.fly", "spell.town_portal" };

        var template = new TemplateGenerator().Generate(s);

        Assert.Equal(2, template.GlobalBans!.Magics!.Count);
        Assert.Equal(1, template.GlobalBans.Magics.Count(x => x == "spell.fly"));
    }

    [Fact]
    public void BannedSpells_IgnoresWhitespaceIds()
    {
        var s = MakeSettings();
        s.HeroSettings.BannedSpells = new List<string> { "spell.fly", "   ", "" };

        var template = new TemplateGenerator().Generate(s);

        Assert.Single(template.GlobalBans!.Magics!);
        Assert.Equal("spell.fly", template.GlobalBans.Magics![0]);
    }
}
```

Note: if `MakeSettings()` produces a generator that fails before reaching the bans block (e.g., requires zone config), look at one of the existing `*Tests.cs` files in `tests/OldenEra.Generator.Tests/` for a "minimum viable settings" helper and reuse it.

**Step 3: Run tests, expect failure**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SpellBansTests" --nologo`
Expected: All 4 tests FAIL — `BannedSpells_EmitsGlobalBansMagics` fails because `GlobalBans.Magics` is null; `BannedSpells_Empty_*` should already pass; dedup/whitespace tests fail.

**Step 4: Implement emission**

In `src/OldenEra.Generator/Services/TemplateGenerator.cs`, after the hero-bans block (after line 135 `}`), add:

```csharp
            // Spell bans — emitted as globalBans.magics. Mirrors the hero-bans
            // block; ids are passed through without validation, same as hero/item bans.
            if (settings.HeroSettings.BannedSpells.Count > 0)
            {
                template.GlobalBans ??= new GlobalBans();
                template.GlobalBans.Magics ??= new List<string>();
                foreach (var sid in settings.HeroSettings.BannedSpells)
                    if (!string.IsNullOrWhiteSpace(sid)
                        && !template.GlobalBans.Magics.Contains(sid))
                        template.GlobalBans.Magics.Add(sid);
            }
```

**Step 5: Run tests, expect pass**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SpellBansTests" --nologo`
Expected: 4/4 PASS.

Then run the full suite to catch regressions:

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --nologo`
Expected: 0 failed (skipped tests are fine).

**Step 6: Commit**

```bash
git add tests/OldenEra.Generator.Tests/SpellBansTests.cs src/OldenEra.Generator/Services/TemplateGenerator.cs
git commit -m "feat(generator): emit globalBans.magics from BannedSpells

Mirrors the existing hero-ban block. Tests cover happy path,
empty-list no-op, dedup, and whitespace id stripping.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: `.oetgs` round-trip for `BannedSpells`

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/SettingsFile.cs:50` (add property next to `heroBans`)
- Modify: `src/OldenEra.Generator/Services/SettingsMapper.cs:82` and `:188` (forward the list both directions)
- Test: `tests/OldenEra.Generator.Tests/SpellBansTests.cs` (extend)

**Step 1: Add the failing round-trip test**

Append to `SpellBansTests.cs`:

```csharp
    [Fact]
    public void SettingsFile_RoundTrips_BannedSpells()
    {
        var g = new GeneratorSettings();
        g.HeroSettings.BannedSpells = new List<string> { "spell.fly", "spell.town_portal" };

        var file = SettingsMapper.ToFile(g);
        var roundTripped = SettingsMapper.FromFile(file);

        Assert.Equal(
            new[] { "spell.fly", "spell.town_portal" },
            roundTripped.HeroSettings.BannedSpells.ToArray());
    }
```

If `SettingsMapper`'s public method names differ, look them up and adjust:

```bash
grep -n "public static.*ToFile\|public static.*FromFile\|public.*GeneratorSettings\|public.*SettingsFile" src/OldenEra.Generator/Services/SettingsMapper.cs
```

**Step 2: Run, expect failure**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SettingsFile_RoundTrips_BannedSpells" --nologo`
Expected: FAIL — list comes back empty.

**Step 3: Add the JSON property**

In `src/OldenEra.Generator/Models/Generator/SettingsFile.cs`, add immediately after line 50 (`heroBans`):

```csharp
        [JsonPropertyName("bannedSpells")]      public List<string> BannedSpells      { get; set; } = new();
```

**Step 4: Wire `SettingsMapper`**

In `src/OldenEra.Generator/Services/SettingsMapper.cs`, the `HeroSettings` block at line 77–86 (file → in-memory) — add inside the object initializer after `HeroBans = ...`:

```csharp
                BannedSpells = s.BannedSpells is null ? new() : new List<string>(s.BannedSpells),
```

Then in the in-memory → file block around line 188 — add right after `HeroBans = new List<string>(g.HeroSettings.HeroBans),`:

```csharp
            BannedSpells = new List<string>(g.HeroSettings.BannedSpells),
```

**Step 5: Run, expect pass**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter "FullyQualifiedName~SpellBansTests" --nologo`
Expected: 5/5 PASS.

Full suite:

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --nologo`
Expected: 0 failed.

**Step 6: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/SettingsFile.cs src/OldenEra.Generator/Services/SettingsMapper.cs tests/OldenEra.Generator.Tests/SpellBansTests.cs
git commit -m "feat(model): round-trip BannedSpells through .oetgs

Adds bannedSpells JSON field and wires SettingsMapper both directions.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: Web UI — spell-ban `<details>` block

**Files:**
- Modify: `src/OldenEra.Web/Components/HeroSettingsPanel.razor`

**Step 1: Add the markup**

After the closing `</details>` of the "Pin starting hero per faction" block (after current line 89, before the closing `</div>` at line 90), insert:

```razor
    <details class="oe-stacked" style="margin-top: 12px;">
        <summary class="oe-label" style="cursor: pointer;">
            Ban specific spells (@Settings.HeroSettings.BannedSpells.Count selected)
        </summary>
        <div class="oe-hint">
            Banned spells are removed from mage guilds, shrines, and pandora boxes
            (emitted as <code>globalBans.magics</code>).
        </div>

        <div class="oe-tier-tabs" role="tablist">
            @foreach (var school in SpellSchools)
            {
                <button type="button"
                        class="@($"oe-tier-tab{(ActiveSchool == school ? " active" : "")}")"
                        role="tab"
                        aria-selected="@(ActiveSchool == school ? "true" : "false")"
                        @onclick="@(() => ActiveSchool = school)">
                    @FriendlySchool(school)
                </button>
            }
        </div>

        <div class="oe-ban-chips">
            @foreach (var spell in SpellsBySchool(ActiveSchool))
            {
                bool banned = Settings.HeroSettings.BannedSpells.Contains(spell.Id);
                var tooltip = string.IsNullOrWhiteSpace(spell.Description)
                    ? $"T{spell.Tier} · {FriendlySchool(spell.School)}"
                    : spell.Description;
                <button type="button"
                        class="@($"oe-ban-chip{(banned ? " selected" : "")}")"
                        title="@tooltip"
                        @onclick="@(() => ToggleSpellBan(spell.Id))">
                    @($"T{spell.Tier}. {spell.Name}")
                </button>
            }
        </div>
    </details>
```

**Step 2: Add the code-behind helpers**

Inside the `@code { ... }` block, add (place these after the existing `OnFixedHeroChanged` method):

```csharp
    private string ActiveSchool { get; set; } = SpellSchools.FirstOrDefault() ?? "day";

    private static readonly string[] SpellSchools =
        CommunityCatalog.Default.Spells
            .Select(s => s.School ?? "")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SchoolOrder)
            .ToArray();

    private static IEnumerable<CommunityCatalog.SpellEntry> SpellsBySchool(string school) =>
        CommunityCatalog.Default.Spells
            .Where(s => string.Equals(s.School, school, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Tier)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase);

    private static int SchoolOrder(string school) => school?.ToLowerInvariant() switch
    {
        "day" => 0,
        "night" => 1,
        "arcane" => 2,
        "primal" => 3,
        _ => 99,
    };

    private static string FriendlySchool(string school) => school?.ToLowerInvariant() switch
    {
        "day" => "Day",
        "night" => "Night",
        "arcane" => "Arcane",
        "primal" => "Primal",
        _ => string.IsNullOrEmpty(school) ? "Other" : char.ToUpper(school[0]) + school[1..],
    };

    private async Task ToggleSpellBan(string spellId)
    {
        if (!Settings.HeroSettings.BannedSpells.Remove(spellId))
            Settings.HeroSettings.BannedSpells.Add(spellId);
        await NotifyChanged();
    }
```

If `CommunityCatalog.SpellEntry` is in a different namespace, adjust the using directives at the top of the file or fully qualify the type. The component already imports `CommunityCatalog` (it uses `Catalog.Factions`).

**Step 3: Build the Web project**

Run: `dotnet build src/OldenEra.Web/OldenEra.Web.csproj --nologo`
Expected: Build succeeded, 0 errors.

**Step 4: Manual smoke (best effort on Mac)**

Per `feedback_blazor_dev_server`:

Run: `cd src/OldenEra.Web && dotnet watch run`
Then in a browser open the page, expand Heroes section, expand "Ban specific spells (0 selected)", click some chips on different school tabs, verify count updates, reload to confirm localStorage round-trip. Stop the dev server when done. If you cannot run a browser in this environment, skip and note that visual verification belongs to the user / CI.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Components/HeroSettingsPanel.razor
git commit -m "feat(web): spell-bans UI in HeroSettingsPanel

Third <details> block mirroring the hero-bans pattern. School tabs
(Day/Night/Arcane/Primal) with chips sorted by tier then name,
sourced from CommunityCatalog.Default.Spells.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: WPF UI — spell-ban Expander + tab content

**Files:**
- Modify: `src/OldenEra.TemplateEditor/Views/HeroesPanel.xaml` (add `<Expander>` after line 80)
- Modify: `src/OldenEra.TemplateEditor/Views/HeroesPanel.xaml.cs` (add helpers + getter/applier)
- Modify: `src/OldenEra.TemplateEditor/MainWindow.xaml.cs` (call new getter at line 1208 area; new applier near line 872)

**Step 1: XAML — add the Expander**

In `HeroesPanel.xaml`, after the closing `</Expander>` of "Pin starting hero per faction" (after line 80, before the final `</StackPanel>` at line 82), add:

```xml
                <Expander Header="Ban specific spells" Margin="0,4,0,4">
                    <StackPanel Margin="8,4,0,8">
                        <TextBlock Style="{StaticResource HintText}">
                            Banned spells are removed from mage guilds, shrines, and pandora boxes (emitted as globalBans.magics).
                        </TextBlock>
                        <TabControl x:Name="TcSpellBans" Margin="0,4,0,0" MinHeight="180"/>
                    </StackPanel>
                </Expander>
```

No inline `<Style>` blocks → the `Style.Triggers`-after-`Setter` rule (`feedback_wpf_style_child_order`) does not apply here.

**Step 2: Code-behind — add the spell-ban builder, getter, applier**

In `HeroesPanel.xaml.cs`:

Add a new field next to `_heroBanCheckBoxes`:

```csharp
    // Per-school CheckBox map for spell bans, keyed by spell id.
    private readonly Dictionary<string, CheckBox> _spellBanCheckBoxes = new();
```

In the constructor, after `BuildFixedHeroRows();` add:

```csharp
        BuildSpellBanTabs();
```

Add the new method, modeled on `BuildHeroBanTabs`:

```csharp
    private void BuildSpellBanTabs()
    {
        TcSpellBans.Items.Clear();
        _spellBanCheckBoxes.Clear();

        var schools = CommunityCatalog.Default.Spells
            .Select(s => s.School ?? "")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .OrderBy(SchoolOrder)
            .ToList();

        foreach (var school in schools)
        {
            var stack = new StackPanel { Margin = new Thickness(6) };
            var spells = CommunityCatalog.Default.Spells
                .Where(s => string.Equals(s.School, school, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Tier)
                .ThenBy(s => s.Name, System.StringComparer.OrdinalIgnoreCase);

            foreach (var spell in spells)
            {
                var cb = new CheckBox
                {
                    Content = $"T{spell.Tier}. {spell.Name}",
                    Margin = new Thickness(2),
                    ToolTip = string.IsNullOrWhiteSpace(spell.Description)
                        ? $"T{spell.Tier} · {FriendlySchool(spell.School)}"
                        : spell.Description,
                    Tag = spell.Id,
                };
                _spellBanCheckBoxes[spell.Id] = cb;
                stack.Children.Add(cb);
            }
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Content = stack,
            };
            TcSpellBans.Items.Add(new TabItem { Header = FriendlySchool(school), Content = scroll });
        }
    }

    private static int SchoolOrder(string school) => school?.ToLowerInvariant() switch
    {
        "day" => 0,
        "night" => 1,
        "arcane" => 2,
        "primal" => 3,
        _ => 99,
    };

    private static string FriendlySchool(string school) => school?.ToLowerInvariant() switch
    {
        "day" => "Day",
        "night" => "Night",
        "arcane" => "Arcane",
        "primal" => "Primal",
        _ => string.IsNullOrEmpty(school) ? "Other" : char.ToUpper(school[0]) + school[1..],
    };

    /// <summary>Read the UI state into a flat list of banned spell ids.</summary>
    public List<string> GetBannedSpells()
    {
        var bans = new List<string>();
        foreach (var (id, cb) in _spellBanCheckBoxes)
            if (cb.IsChecked == true) bans.Add(id);
        return bans;
    }

    /// <summary>Apply persisted spell bans to the UI state.</summary>
    public void ApplyBannedSpells(IEnumerable<string>? bannedSpells)
    {
        var bans = new HashSet<string>(bannedSpells ?? System.Array.Empty<string>(),
                                       System.StringComparer.OrdinalIgnoreCase);
        foreach (var (id, cb) in _spellBanCheckBoxes)
            cb.IsChecked = bans.Contains(id);
    }
```

**Step 3: Wire MainWindow**

In `src/OldenEra.TemplateEditor/MainWindow.xaml.cs`:

At the `BuildSettings`-style block around line 1208, inside the `HeroSettings = new HeroSettings { ... }` initializer, add after `FixedStartingHeroByFaction = ...`:

```csharp
                BannedSpells = PnlHeroes.GetBannedSpells(),
```

Around line 872 — after `PnlHeroes.ApplyHeroSelection(s.HeroBans, s.FixedStartingHeroByFaction);` add:

```csharp
            PnlHeroes.ApplyBannedSpells(s.BannedSpells);
```

Around line 741 — `BuildSettings`-file-shape block, after `HeroBans = PnlHeroes.GetHeroBans(),` line, add:

```csharp
            BannedSpells          = PnlHeroes.GetBannedSpells(),
```

**Step 4: Build (best effort on Mac)**

Run: `dotnet build src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj --nologo` if the host can build it; otherwise mark "validated by CI" per `feedback_macos_dotnet_workflow`. Always run the Generator + Web builds + tests before committing:

Run: `dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj --nologo && dotnet build src/OldenEra.Web/OldenEra.Web.csproj --nologo && dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --nologo`
Expected: builds succeed, tests pass.

**Step 5: Commit**

```bash
git add src/OldenEra.TemplateEditor/Views/HeroesPanel.xaml src/OldenEra.TemplateEditor/Views/HeroesPanel.xaml.cs src/OldenEra.TemplateEditor/MainWindow.xaml.cs
git commit -m "feat(wpf): spell-bans Expander in HeroesPanel

Third Expander modeled on the hero-ban TabControl. Tabs by school,
checkboxes per spell. Wired into BuildSettings/ApplySettings.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: Final verification

**Step 1: Run the full Generator test suite + Web build**

Run: `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --nologo`
Expected: 73 passed (was 69; +4 spell tests), 0 failed, skipped tests unchanged.

Run: `dotnet build src/OldenEra.Web/OldenEra.Web.csproj --nologo`
Expected: 0 errors.

**Step 2: Use the verification-before-completion skill**

Before declaring done, invoke `superpowers:verification-before-completion` to confirm: tests passing, builds clean, no stray uncommitted files (`git status`), branch ready for code review.

**Step 3: Hand off**

Push and open a PR per `superpowers:finishing-a-development-branch`. Title suggestion: `feat: spell bans (#24) — emit globalBans.magics`.

---

## Out of scope (intentionally not in this plan)

- **Issue #21** — dropped during brainstorming. Tooltip "parity gap" doesn't exist (WPF panel views have no `ToolTip` attributes); FixedStartingHero picker is already implemented in `HeroSettingsPanel.razor:61–89`. The issue should be closed as stale.
- **Issue #22** — separate surface (GameMode picker un-hide + installer-zip button on WPF). Zero file overlap with this branch.
- **Spell-id validation in `KnownValues`** — items and heroes are not validated today; spells follow suit. Add later only if we tighten all three together.
