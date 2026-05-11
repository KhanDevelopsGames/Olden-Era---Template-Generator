# Spell bans (#24) — design

Date: 2026-05-11
Branch: `feature/heroes-bans-ui`
Scope note: Issue #21 was reviewed and dropped — its part 1 (tooltip
parity) describes a gap that does not exist (WPF panel views have no
`ToolTip` attributes), and part 2 (FixedStartingHero picker) is already
implemented in `HeroSettingsPanel.razor` lines 61–89. Issue #22 is
out of scope; it touches `MainWindow.xaml` and the installer-zip
button, with zero file overlap.

## Goal

Let users ban spells in either host. Banning a spell produces
`globalBans.magics: ["spell_id", ...]` in the generated `.rmg.json`
and round-trips through `.oetgs` and the share-link.

## Data model

`GlobalBans.Magics` already exists
(`src/OldenEra.Generator/Models/Unfrozen/Miscellaneous.cs:29`). No
model change.

Add to `GeneratorSettings`
(`src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs`),
sibling of `HeroBans` (line 41) and `GlobalBans` items list
(line 147):

```csharp
public List<string> BannedSpells { get; set; } = new();
```

Add to `SettingsFile`
(`src/OldenEra.Generator/Models/Generator/SettingsFile.cs`), sibling of
the existing `globalBans` entry (line 99):

```csharp
[JsonPropertyName("bannedSpells")] public List<string> BannedSpells = new();
```

Round-tripping then flows through the existing settings serializer
without further work.

## Generator emission

Extend `TemplateGenerator.cs` near the existing item / hero ban blocks
(around lines 116–134). Add a third block:

```csharp
if (settings.BannedSpells.Count > 0)
{
    template.GlobalBans ??= new GlobalBans();
    template.GlobalBans.Magics ??= new List<string>();
    foreach (var sid in settings.BannedSpells)
        if (!template.GlobalBans.Magics.Contains(sid))
            template.GlobalBans.Magics.Add(sid);
}
```

No id validation. Items and heroes are not validated today; spells
follow suit. If the user picks something stale, it lands in the
output as-is — same contract.

No `KnownValues` change. The UI already has a richer source
(`CommunityCatalog.Default.Spells`) used by `SpellPicker.razor`.

## Web UI

Add a third `<details>` block in
`src/OldenEra.Web/Components/HeroSettingsPanel.razor`, after the
existing "Pin starting hero" block (after line 89). Mirror the
hero-bans block at lines 31–58.

Layout:

- `<summary>` shows `Ban specific spells (N selected)`.
- Hint: `Banned spells are removed from mage guilds, shrines, and
  pandora boxes (emitted as globalBans.magics).`
- One row of school tabs (Day / Night / Arcane / Primal), reusing
  the existing `oe-tier-tabs` / `oe-tier-tab` classes.
- Below the tabs, the active school's spells render as
  `oe-ban-chip` toggle buttons sorted by tier then name, labelled
  `T{tier}. {name}`.

Code-behind additions:

- `private string ActiveSchool { get; set; } = "day";`
- Static `SpellSchools` derived from
  `CommunityCatalog.Default.Spells` distinct schools, ordered via
  the same `SchoolOrder` switch used in `SpellPicker.razor:47`.
- `SpellsBySchool(school)` →
  `Spells.Where(s => s.School == school).OrderBy(s => s.Tier)
   .ThenBy(s => s.Name)`.
- `FriendlySchool(school)` — copy from `SpellPicker.razor:56`.
- `ToggleSpellBan(string id)` mirrors the existing `ToggleHeroBan`,
  acting on `Settings.BannedSpells`.

CSS reuse only. No new styles.

## WPF UI

Add a third `<Expander>` to
`src/OldenEra.TemplateEditor/Views/HeroesPanel.xaml`, after the
"Pin starting hero per faction" expander (after line 80):

```xml
<Expander Header="Ban specific spells" Margin="0,4,0,4">
    <StackPanel Margin="8,4,0,8">
        <TextBlock Style="{StaticResource HintText}">
            Banned spells are removed from mage guilds, shrines, and
            pandora boxes (emitted as globalBans.magics).
        </TextBlock>
        <TabControl x:Name="TcSpellBans" Margin="0,4,0,0" MinHeight="180"/>
    </StackPanel>
</Expander>
```

In `HeroesPanel.xaml.cs`, mirror the `TcHeroBans` population pattern.
Iterate `CommunityCatalog.Default.Spells` grouped by school; per school
build a `TabItem` containing a `WrapPanel` of `ToggleButton`s.
`IsChecked` ↔ `Settings.BannedSpells.Contains(spell.Id)`. Label
`T{tier}. {name}`. Hook `Checked` / `Unchecked` to mutate
`Settings.BannedSpells` and fire the existing dirty-tracking event.

`MainWindow.xaml.cs` `BuildSettings` / `ApplySettings`: confirm
`BannedSpells` round-trips through `.oetgs`. With `SettingsFile`
already updated, this should work without code change — verify only.

WPF Style child ordering rule (per `feedback_wpf_style_child_order`):
the new XAML is plain `Expander` + `TabControl` and uses no inline
`<Style>` blocks, so the trigger-after-setter rule does not apply.

## Tests

Add to `tests/OldenEra.Generator.Tests/`:

1. `BannedSpells_EmitsGlobalBansMagics` — given
   `settings.BannedSpells = ["spell.fly", "spell.town_portal"]`, the
   generated template has `GlobalBans.Magics` containing both ids.
2. `BannedSpells_Empty_DoesNotCreateMagicsArray` — empty list →
   `GlobalBans.Magics` stays null (mirrors current hero / item
   behavior).
3. `BannedSpells_DedupesIds` — duplicate ids in input collapse to one
   entry in output.
4. `SettingsFile_RoundTrips_BannedSpells` — write + read `.oetgs`,
   list preserved.

Locate by reading existing hero-ban / item-ban tests and following
their style.

## Build & verify (macOS)

- `dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`
- `dotnet build src/OldenEra.Web/OldenEra.Web.csproj`
- WPF host build is validated by CI per `feedback_macos_dotnet_workflow`.

## Acceptance

- Banning a spell in either UI produces
  `globalBans.magics: ["spell_id", ...]` in the output `.rmg.json`.
- Round-trips via `.oetgs` and share-link.
- New `<details>` block / `Expander` collapses by default and is
  consistent visually with the hero-bans block above it.
