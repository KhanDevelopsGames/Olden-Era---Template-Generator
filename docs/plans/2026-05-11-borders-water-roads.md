# Map Borders & Roads Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Surface `Variant.Border.{CornerRadius, ObstaclesWidth, WaterWidth}` and per-map `Road.Type` as opt-in experimental settings, with Web + WPF parity and byte-identical defaults.

**Architecture:** Follow the established post-process pattern from the 2026-05-10 experimental rollout. Add a `BordersRoadsSettings` group to `GeneratorSettings`, a parallel `*File`-suffixed schema in `SettingsFile`, bidirectional mapping in `SettingsMapper`, an extra branch in `ApplyExperimentalSettings`, and one new card in each host's Experimental section. Defaults: all overrides null/false → no writes happen → output unchanged.

**Tech Stack:** .NET 10, C#, Blazor WebAssembly, WPF (`net10.0-windows`, build-only on macOS), xUnit. SkiaSharp is irrelevant — preview is unaffected.

**Reference design:** `docs/plans/2026-05-11-borders-water-roads-design.md`

**Build commands:**
- Build everything (macOS): `/opt/homebrew/bin/dotnet build`
- Run tests (macOS skip — see `feedback_macos_dotnet_workflow.md`): `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj` (this test project targets `net10.0` and runs on Mac; the WPF tests do not).

---

### Task 1: Add `BordersRoadsSettings` to `GeneratorSettings`

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs`

**Step 1: Add the new class above `GeneratorSettings` (around line 175)**

Insert before `public class GeneratorSettings`:

```csharp
public class BordersRoadsSettings
{
    /// <summary>Variant.Border.CornerRadius override. null = generator default (0.0).</summary>
    public double? CornerRadius { get; set; }
    /// <summary>Variant.Border.ObstaclesWidth override. null = generator default (3).</summary>
    public int? ObstaclesWidth { get; set; }
    /// <summary>If true, water border is applied with WaterWidth. Default WaterType "water grass".</summary>
    public bool WaterBorderEnabled { get; set; } = false;
    /// <summary>Width of water border. Only used when WaterBorderEnabled is true.</summary>
    public int WaterWidth { get; set; } = 4;
    /// <summary>Road.Type override applied to every road. null = generator default ("Dirt").</summary>
    public string? RoadType { get; set; }
}
```

**Step 2: Add property to `GeneratorSettings` next to other experimental groups (after line 213)**

In the experimental section block, after `public StartingBonusSettings Bonuses ... = new();`, add:

```csharp
public BordersRoadsSettings BordersRoads { get; set; } = new BordersRoadsSettings();
```

**Step 3: Build to verify**

Run: `/opt/homebrew/bin/dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj`
Expected: Build succeeded, 0 errors.

**Step 4: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/GeneratorSettings.cs
git commit -m "feat(generator): add BordersRoadsSettings model"
```

---

### Task 2: Add failing test for default no-op behavior

**Files:**
- Modify: `tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs`

**Step 1: Locate the test file's existing helper**

Run: `grep -n "GeneratorSettings\|Generate(" tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs | head -10`

Note the conventional way fixtures construct settings (you'll need at least `PlayerCount`, `MapSize`, `Topology` set). Mirror an existing simple test case.

**Step 2: Add a new fact at the end of the file's `BordersAndRoads` region**

Add (or create at end of class):

```csharp
[Fact]
public void BordersAndRoads_DefaultSettings_KeepsHardcodedBorderAndRoadValues()
{
    var settings = new GeneratorSettings
    {
        PlayerCount = 2,
        MapSize = 160,
        Topology = MapTopology.Random
    };

    var template = TemplateGenerator.Generate(settings);

    Assert.NotNull(template.Variants);
    foreach (var variant in template.Variants!)
    {
        Assert.NotNull(variant.Border);
        Assert.Equal(0.0, variant.Border!.CornerRadius);
        Assert.Equal(3, variant.Border.ObstaclesWidth);
        Assert.Equal(0, variant.Border.WaterWidth);
        Assert.Equal("water grass", variant.Border.WaterType);

        Assert.NotNull(variant.Zones);
        foreach (var zone in variant.Zones!)
        {
            if (zone.Roads is null) continue;
            foreach (var road in zone.Roads)
                Assert.Equal("Dirt", road.Type);
        }
    }
}
```

**Step 3: Run test, expect PASS (this is the lock-in baseline)**

Run: `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter FullyQualifiedName~BordersAndRoads_DefaultSettings`

Expected: PASS. This test guards byte-identical defaults *before* we add the override branch.

**Step 4: Commit**

```bash
git add tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs
git commit -m "test(generator): lock baseline border/road defaults"
```

---

### Task 3: Add failing test for `CornerRadius` override

**Files:**
- Modify: `tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs`

**Step 1: Add the test**

```csharp
[Fact]
public void BordersAndRoads_CornerRadiusOverride_AppliedToEveryVariant()
{
    var settings = new GeneratorSettings
    {
        PlayerCount = 2,
        MapSize = 160,
        Topology = MapTopology.Random,
        BordersRoads = { CornerRadius = 0.5 }
    };

    var template = TemplateGenerator.Generate(settings);

    foreach (var variant in template.Variants!)
        Assert.Equal(0.5, variant.Border!.CornerRadius);
}
```

**Step 2: Run, expect FAIL**

Run: `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter FullyQualifiedName~BordersAndRoads_CornerRadiusOverride`

Expected: FAIL — `CornerRadius` is still hardcoded at `0.0`.

**Step 3: Do not commit yet — implementation comes in Task 4.**

---

### Task 4: Implement `ApplyBordersAndRoads` post-pass

**Files:**
- Modify: `src/OldenEra.Generator/Services/TemplateGenerator.cs`

**Step 1: Add a call inside `ApplyExperimentalSettings`**

Locate the method (line ~89). At the end of the method (after the last existing branch — keep ordering: scalar overrides first, then per-zone overlays already loop variants), add:

```csharp
            ApplyBordersAndRoads(template, settings.BordersRoads);
```

**Step 2: Add the helper method below `ApplyExperimentalToZone`**

```csharp
        private static void ApplyBordersAndRoads(RmgTemplate template, BordersRoadsSettings br)
        {
            if (template.Variants is null) return;
            foreach (var variant in template.Variants)
            {
                if (variant.Border is { } border)
                {
                    if (br.CornerRadius.HasValue) border.CornerRadius = br.CornerRadius.Value;
                    if (br.ObstaclesWidth.HasValue) border.ObstaclesWidth = br.ObstaclesWidth.Value;
                    if (br.WaterBorderEnabled) border.WaterWidth = br.WaterWidth;
                }

                if (br.RoadType is { Length: > 0 } rt && variant.Zones is not null)
                {
                    foreach (var zone in variant.Zones)
                    {
                        if (zone.Roads is null) continue;
                        foreach (var road in zone.Roads) road.Type = rt;
                    }
                }
            }
        }
```

**Step 3: Run all border/road tests, expect PASS**

Run: `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter FullyQualifiedName~BordersAndRoads`

Expected: 2/2 PASS.

**Step 4: Commit**

```bash
git add src/OldenEra.Generator/Services/TemplateGenerator.cs tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs
git commit -m "feat(generator): apply BordersRoads overrides post-generation"
```

---

### Task 5: Add tests for the remaining override branches

**Files:**
- Modify: `tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs`

**Step 1: Add four tests in a single edit**

```csharp
[Fact]
public void BordersAndRoads_ObstaclesWidthOverride_Applied()
{
    var settings = new GeneratorSettings
    {
        PlayerCount = 2, MapSize = 160, Topology = MapTopology.Random,
        BordersRoads = { ObstaclesWidth = 7 }
    };
    var template = TemplateGenerator.Generate(settings);
    foreach (var v in template.Variants!) Assert.Equal(7, v.Border!.ObstaclesWidth);
}

[Fact]
public void BordersAndRoads_WaterEnabled_AppliesWidthAndKeepsWaterType()
{
    var settings = new GeneratorSettings
    {
        PlayerCount = 2, MapSize = 160, Topology = MapTopology.Random,
        BordersRoads = { WaterBorderEnabled = true, WaterWidth = 6 }
    };
    var template = TemplateGenerator.Generate(settings);
    foreach (var v in template.Variants!)
    {
        Assert.Equal(6, v.Border!.WaterWidth);
        Assert.Equal("water grass", v.Border.WaterType);
    }
}

[Fact]
public void BordersAndRoads_WaterDisabled_IgnoresWidthSlider()
{
    var settings = new GeneratorSettings
    {
        PlayerCount = 2, MapSize = 160, Topology = MapTopology.Random,
        BordersRoads = { WaterBorderEnabled = false, WaterWidth = 6 }
    };
    var template = TemplateGenerator.Generate(settings);
    foreach (var v in template.Variants!) Assert.Equal(0, v.Border!.WaterWidth);
}

[Theory]
[InlineData(MapTopology.Random)]
[InlineData(MapTopology.HubAndSpoke)]
public void BordersAndRoads_RoadTypeStone_AppliedToEveryRoad(MapTopology topology)
{
    var settings = new GeneratorSettings
    {
        PlayerCount = 2, MapSize = 160, Topology = topology,
        BordersRoads = { RoadType = "Stone" }
    };
    var template = TemplateGenerator.Generate(settings);
    int roadCount = 0;
    foreach (var v in template.Variants!)
    foreach (var z in v.Zones!)
        if (z.Roads is not null)
            foreach (var r in z.Roads) { Assert.Equal("Stone", r.Type); roadCount++; }
    Assert.True(roadCount > 0, "expected at least one road");
}
```

**Step 2: Run all border/road tests**

Run: `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter FullyQualifiedName~BordersAndRoads`

Expected: 6/6 PASS (the Theory counts as 2).

**Step 3: Commit**

```bash
git add tests/OldenEra.Generator.Tests/TemplateGeneratorTests.cs
git commit -m "test(generator): cover ObstaclesWidth, water enable/disable, RoadType"
```

---

### Task 6: Persist new fields in `SettingsFile`

**Files:**
- Modify: `src/OldenEra.Generator/Models/Generator/SettingsFile.cs`

**Step 1: Add fields next to the existing `terrainObstaclesFill` block (~line 88)**

```csharp
        [JsonPropertyName("borderCornerRadius")]   public double? BorderCornerRadius   { get; set; }
        [JsonPropertyName("borderObstaclesWidth")] public int?    BorderObstaclesWidth { get; set; }
        [JsonPropertyName("waterBorderEnabled")]   public bool    WaterBorderEnabled   { get; set; } = false;
        [JsonPropertyName("waterWidth")]           public int     WaterWidth           { get; set; } = 4;
        [JsonPropertyName("roadType")]             public string  RoadType             { get; set; } = "";
```

**Step 2: Build**

Run: `/opt/homebrew/bin/dotnet build src/OldenEra.Generator/OldenEra.Generator.csproj`
Expected: 0 errors.

**Step 3: Commit**

```bash
git add src/OldenEra.Generator/Models/Generator/SettingsFile.cs
git commit -m "feat(settings): persist BordersRoads fields in .oetgs schema"
```

---

### Task 7: Map between `SettingsFile` and `GeneratorSettings`

**Files:**
- Modify: `src/OldenEra.Generator/Services/SettingsMapper.cs`

**Step 1: Inspect existing terrain mapping for the pattern**

Run: `grep -n "Terrain\b\|TerrainObstaclesFill\|TerrainSettings" src/OldenEra.Generator/Services/SettingsMapper.cs`

You'll see two locations: a `ToGeneratorSettings`-style method (around line 35) and a `ToFile`-style method (around line 221). Mirror these.

**Step 2: In the `SettingsFile → GeneratorSettings` direction (around line 35)**

After the `Terrain = new TerrainSettings { ... }` block, add:

```csharp
            BordersRoads = new BordersRoadsSettings
            {
                CornerRadius = s.BorderCornerRadius,
                ObstaclesWidth = s.BorderObstaclesWidth,
                WaterBorderEnabled = s.WaterBorderEnabled,
                WaterWidth = s.WaterWidth,
                RoadType = string.IsNullOrEmpty(s.RoadType) ? null : s.RoadType
            },
```

**Step 3: In the `GeneratorSettings → SettingsFile` direction (around line 221)**

After the `TerrainObstaclesFill = g.Terrain.ObstaclesFill,` line, add:

```csharp
            BorderCornerRadius   = g.BordersRoads.CornerRadius,
            BorderObstaclesWidth = g.BordersRoads.ObstaclesWidth,
            WaterBorderEnabled   = g.BordersRoads.WaterBorderEnabled,
            WaterWidth           = g.BordersRoads.WaterWidth,
            RoadType             = g.BordersRoads.RoadType ?? "",
```

**Step 4: Build**

Run: `/opt/homebrew/bin/dotnet build`
Expected: 0 errors.

**Step 5: Commit**

```bash
git add src/OldenEra.Generator/Services/SettingsMapper.cs
git commit -m "feat(settings): map BordersRoads in both directions"
```

---

### Task 8: Add round-trip test in `HostParityTests`

**Files:**
- Modify: `tests/OldenEra.Generator.Tests/HostParityTests.cs` (or whichever test class covers `SettingsFile` round-trips — `grep -rn "SettingsMapper\|\.ToFile\|ToGeneratorSettings" tests/` to find it)

**Step 1: Find the existing round-trip helper**

Run: `grep -rn "TerrainObstaclesFill\|FromFile\|ToFile" tests/`

**Step 2: Add a test mirroring the existing pattern**

Pseudocode (adapt to the actual API):

```csharp
[Fact]
public void SettingsMapper_BordersRoads_RoundTrips()
{
    var original = new GeneratorSettings
    {
        BordersRoads = new BordersRoadsSettings
        {
            CornerRadius = 0.3,
            ObstaclesWidth = 5,
            WaterBorderEnabled = true,
            WaterWidth = 7,
            RoadType = "Stone"
        }
    };

    var file = SettingsMapper.ToFile(original);
    var (restored, _, _, _) = SettingsMapper.FromFile(file); // adjust tuple shape per existing call sites

    Assert.Equal(0.3, restored.BordersRoads.CornerRadius);
    Assert.Equal(5, restored.BordersRoads.ObstaclesWidth);
    Assert.True(restored.BordersRoads.WaterBorderEnabled);
    Assert.Equal(7, restored.BordersRoads.WaterWidth);
    Assert.Equal("Stone", restored.BordersRoads.RoadType);
}
```

**Step 3: Run, expect PASS**

Run: `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj --filter FullyQualifiedName~BordersRoads_RoundTrips`

**Step 4: Commit**

```bash
git add tests/OldenEra.Generator.Tests/
git commit -m "test(settings): round-trip BordersRoads through .oetgs"
```

---

### Task 9: Web UI — `MapBordersRoadsPanel.razor`

**Files:**
- Create: `src/OldenEra.Web/Components/MapBordersRoadsPanel.razor`

**Step 1: Inspect a sibling panel for parameter conventions**

Run: `cat src/OldenEra.Web/Components/ExperimentalZonePanel.razor | head -20`

Note: panels take `[Parameter] GeneratorSettings Settings` and `[Parameter] EventCallback OnChanged` (or call a `NotifyChanged()` method — match what other panels do).

**Step 2: Create the file**

```razor
@using OldenEra.Generator.Models
@using OldenEra.Generator.Models.Unfrozen

<ExperimentalCard Key="map-borders-roads" Title="Map borders & roads">
    <div class="oe-hint">
        Override the generator's border + road defaults. Empty/off = generator default.
    </div>

    <SliderRow Label="Corner radius" WideLabel="true"
               Min="0" Max="100" Step="5" Suffix="%"
               Value="(int)Math.Round((Settings.BordersRoads.CornerRadius ?? 0) * 100)"
               ValueChanged="@(v => { Settings.BordersRoads.CornerRadius = v == 0 ? (double?)null : v / 100.0; NotifyChanged(); })" />

    <SliderRow Label="Obstacle width" WideLabel="true"
               Min="0" Max="10" Step="1"
               Value="Settings.BordersRoads.ObstaclesWidth ?? 3"
               ValueChanged="@(v => { Settings.BordersRoads.ObstaclesWidth = v == 3 ? (int?)null : v; NotifyChanged(); })" />

    <div class="oe-stacked">
        <label class="oe-label">
            <input type="checkbox"
                   checked="@Settings.BordersRoads.WaterBorderEnabled"
                   @onchange="@(e => { Settings.BordersRoads.WaterBorderEnabled = (bool)e.Value!; NotifyChanged(); })" />
            Water border
        </label>
    </div>

    @if (Settings.BordersRoads.WaterBorderEnabled)
    {
        <SliderRow Label="Water width" WideLabel="true"
                   Min="1" Max="10" Step="1"
                   Value="Settings.BordersRoads.WaterWidth"
                   ValueChanged="@(v => { Settings.BordersRoads.WaterWidth = v; NotifyChanged(); })" />
    }

    <div class="oe-stacked">
        <label class="oe-label">Road type</label>
        <select value="@(Settings.BordersRoads.RoadType ?? "")"
                @onchange="@(e => { var s = (string?)e.Value; Settings.BordersRoads.RoadType = string.IsNullOrEmpty(s) ? null : s; NotifyChanged(); })">
            <option value="">Default (Dirt)</option>
            @foreach (var rt in KnownValues.RoadTypes)
            {
                <option value="@rt">@rt</option>
            }
        </select>
    </div>
</ExperimentalCard>

@code {
    [Parameter, EditorRequired] public GeneratorSettings Settings { get; set; } = default!;
    [Parameter] public EventCallback OnChanged { get; set; }
    private async Task NotifyChanged() => await OnChanged.InvokeAsync();
}
```

**Step 3: Verify the parameter contract**

If `ExperimentalZonePanel` uses a different convention (e.g., bare `NotifyChanged()` without `OnChanged` parameter, or `EventCallback<GeneratorSettings>`), adapt the `@code` block to match exactly.

Run: `grep -A3 "@code {" src/OldenEra.Web/Components/ExperimentalZonePanel.razor`

**Step 4: Build**

Run: `/opt/homebrew/bin/dotnet build src/OldenEra.Web/OldenEra.Web.csproj`
Expected: 0 errors.

**Step 5: Commit**

```bash
git add src/OldenEra.Web/Components/MapBordersRoadsPanel.razor
git commit -m "feat(web): MapBordersRoadsPanel component"
```

---

### Task 10: Mount the panel in `Home.razor`

**Files:**
- Modify: `src/OldenEra.Web/Pages/Home.razor`

**Step 1: Find where `ExperimentalZonePanel` is rendered**

Run: `grep -n "ExperimentalZonePanel\|MapBordersRoadsPanel" src/OldenEra.Web/Pages/Home.razor`

**Step 2: Add `<MapBordersRoadsPanel />` adjacent to it**

Use the same parameter pattern as the sibling panel (e.g., `<MapBordersRoadsPanel Settings="@settings" OnChanged="HandleChanged" />`).

**Step 3: Build the web project + run dev server briefly to confirm it renders**

Run: `/opt/homebrew/bin/dotnet build src/OldenEra.Web/OldenEra.Web.csproj`

Optional smoke test (per `feedback_blazor_dev_server.md`): `/opt/homebrew/bin/dotnet watch --project src/OldenEra.Web run` and confirm the card appears. If `dotnet watch` is unavailable, skip — Task 11 (WPF) and Task 12 (manual smoke) suffice.

**Step 4: Commit**

```bash
git add src/OldenEra.Web/Pages/Home.razor
git commit -m "feat(web): mount MapBordersRoadsPanel in Experimental section"
```

---

### Task 11: WPF UI — Expander in `ExperimentalPanel.xaml`

**Files:**
- Modify: `src/OldenEra.TemplateEditor/Views/ExperimentalPanel.xaml`
- Modify: `src/OldenEra.TemplateEditor/MainWindow.xaml.cs`

**Step 1: Add an Expander after the Terrain density block (~line 214)**

Insert before the `<!-- ── Building presets ─` comment:

```xml
                <!-- ── Map borders & roads ─────────────────────────────── -->
                <Expander Header="Map borders &amp; roads" Margin="0,4,0,4">
                    <StackPanel Margin="8,4,0,8">
                        <TextBlock Style="{StaticResource HintText}">
                            Override the generator's border + road defaults. Off / "Default" = unchanged.
                        </TextBlock>
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="185"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="40"/>
                            </Grid.ColumnDefinitions>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>
                            <Label    Grid.Row="0" Grid.Column="0" Content="Corner radius (%)" Padding="5,2"/>
                            <Slider   Grid.Row="0" Grid.Column="1" x:Name="SldBorderCornerRadius" Minimum="0" Maximum="100" Value="0" TickFrequency="5" IsSnapToTickEnabled="True"/>
                            <TextBlock Grid.Row="0" Grid.Column="2" x:Name="TxtBorderCornerRadius" Text="0" HorizontalAlignment="Right" VerticalAlignment="Center" Style="{StaticResource ValueText}"/>

                            <Label    Grid.Row="1" Grid.Column="0" Content="Obstacle width" Padding="5,2"/>
                            <Slider   Grid.Row="1" Grid.Column="1" x:Name="SldBorderObstaclesWidth" Minimum="0" Maximum="10" Value="3" TickFrequency="1" IsSnapToTickEnabled="True"/>
                            <TextBlock Grid.Row="1" Grid.Column="2" x:Name="TxtBorderObstaclesWidth" Text="3" HorizontalAlignment="Right" VerticalAlignment="Center" Style="{StaticResource ValueText}"/>

                            <CheckBox Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" x:Name="ChkWaterBorderEnabled" Content="Water border" Margin="5,4,0,2"/>

                            <Label    Grid.Row="3" Grid.Column="0" Content="Water width" Padding="5,2"/>
                            <Slider   Grid.Row="3" Grid.Column="1" x:Name="SldWaterWidth" Minimum="1" Maximum="10" Value="4" TickFrequency="1" IsSnapToTickEnabled="True"/>
                            <TextBlock Grid.Row="3" Grid.Column="2" x:Name="TxtWaterWidth" Text="4" HorizontalAlignment="Right" VerticalAlignment="Center" Style="{StaticResource ValueText}"/>

                            <Label    Grid.Row="4" Grid.Column="0" Content="Road type" Padding="5,2"/>
                            <ComboBox Grid.Row="4" Grid.Column="1" x:Name="CmbRoadType"/>
                        </Grid>
                    </StackPanel>
                </Expander>
```

**Step 2: Build to catch BAML errors immediately**

Per `feedback_wpf_style_child_order.md`, BAML errors only surface on Mac via build. Run:

`/opt/homebrew/bin/dotnet build src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj`

Expected: 0 errors.

**Step 3: Wire up `MainWindow.xaml.cs`**

Find `BuildSettings()` (around the `TerrainObstaclesFill` line — `grep -n "TerrainObstaclesFill" src/OldenEra.TemplateEditor/MainWindow.xaml.cs`).

There are three sites: `BuildSettings()` returning `SettingsFile` (line ~777), `ApplySettings(SettingsFile)` (line ~916), and another `BuildSettings`-flavored site for the in-memory `GeneratorSettings` (line ~1281). Find them all with the grep above and add parallel lines to each.

**In the `SettingsFile`-builder (~line 777):** add after `TerrainObstaclesFill = ... / 100.0,`:

```csharp
            BorderCornerRadius   = PnlExperimental.SldBorderCornerRadius.Value == 0 ? (double?)null : PnlExperimental.SldBorderCornerRadius.Value / 100.0,
            BorderObstaclesWidth = PnlExperimental.SldBorderObstaclesWidth.Value == 3 ? (int?)null : (int)PnlExperimental.SldBorderObstaclesWidth.Value,
            WaterBorderEnabled   = PnlExperimental.ChkWaterBorderEnabled.IsChecked == true,
            WaterWidth           = (int)PnlExperimental.SldWaterWidth.Value,
            RoadType             = (PnlExperimental.CmbRoadType.SelectedItem as string) ?? "",
```

**In `ApplySettings(SettingsFile s)` (~line 916):** add after `PnlExperimental.SldTerrainObstacles.Value = ...`:

```csharp
            PnlExperimental.SldBorderCornerRadius.Value   = Math.Clamp((s.BorderCornerRadius ?? 0) * 100.0, 0, 100);
            PnlExperimental.SldBorderObstaclesWidth.Value = s.BorderObstaclesWidth ?? 3;
            PnlExperimental.ChkWaterBorderEnabled.IsChecked = s.WaterBorderEnabled;
            PnlExperimental.SldWaterWidth.Value           = s.WaterWidth;
            PnlExperimental.CmbRoadType.SelectedItem      = string.IsNullOrEmpty(s.RoadType) ? null : s.RoadType;
```

**In the `GeneratorSettings`-builder (~line 1281):** add after `ObstaclesFill = ... / 100.0,`:

```csharp
            BordersRoads = new BordersRoadsSettings
            {
                CornerRadius = PnlExperimental.SldBorderCornerRadius.Value == 0 ? (double?)null : PnlExperimental.SldBorderCornerRadius.Value / 100.0,
                ObstaclesWidth = PnlExperimental.SldBorderObstaclesWidth.Value == 3 ? (int?)null : (int)PnlExperimental.SldBorderObstaclesWidth.Value,
                WaterBorderEnabled = PnlExperimental.ChkWaterBorderEnabled.IsChecked == true,
                WaterWidth = (int)PnlExperimental.SldWaterWidth.Value,
                RoadType = (PnlExperimental.CmbRoadType.SelectedItem as string) is { Length: > 0 } rt ? rt : null
            },
```

**Step 4: Initialize `CmbRoadType` items + slider value labels**

Find the `Loaded`/init handler that populates `CmbPlayerPreset` (`grep -n "CmbPlayerPreset.Items\|CmbPlayerPreset.ItemsSource" src/OldenEra.TemplateEditor/MainWindow.xaml.cs`). Mirror that pattern:

```csharp
PnlExperimental.CmbRoadType.ItemsSource = new[] { (string?)null }.Concat(KnownValues.RoadTypes).ToList();
```

Or simpler: just add hard-coded items inline. Choose what matches the existing `CmbPlayerPreset` style.

Find the value-text-binding wiring (`grep -n "TxtTerrainObstacles" src/OldenEra.TemplateEditor/MainWindow.xaml.cs`) and add three parallel handlers for `TxtBorderCornerRadius`, `TxtBorderObstaclesWidth`, `TxtWaterWidth`.

**Step 5: Build**

Run: `/opt/homebrew/bin/dotnet build src/OldenEra.TemplateEditor/OldenEra.TemplateEditor.csproj`
Expected: 0 errors.

**Step 6: Commit**

```bash
git add src/OldenEra.TemplateEditor/Views/ExperimentalPanel.xaml src/OldenEra.TemplateEditor/MainWindow.xaml.cs
git commit -m "feat(wpf): map borders & roads experimental panel"
```

---

### Task 12: Final sweep — full build + full test run

**Step 1: Full solution build**

Run: `/opt/homebrew/bin/dotnet build`
Expected: 0 errors. Verifies WPF XAML compiles via `EnableWindowsTargeting`.

**Step 2: Full Mac-runnable test pass**

Run: `/opt/homebrew/bin/dotnet test tests/OldenEra.Generator.Tests/OldenEra.Generator.Tests.csproj`
Expected: All green, including the 6 new BordersAndRoads tests + 1 round-trip.

**Step 3: Visual smoke (web)**

Run `dotnet watch --project src/OldenEra.Web run` and confirm:
- Toggle Experimental on → "Map borders & roads" card appears
- Tweak corner radius, water checkbox, road type dropdown
- Open browser DevTools console — no errors
- Confirm settings persist after page refresh

**Step 4: WPF tests run on CI (`windows-latest`)**

After pushing the branch, the GitHub Actions `tests.yml` workflow will execute the WPF-typed tests. Local Mac cannot run them.

**Step 5: Final commit (if any cleanup remains)**

```bash
git status
# If clean, nothing to commit. Otherwise stage and commit any leftover wiring fixes.
```

---

### Task 13: Open PR via finishing-a-development-branch

After all tasks pass, switch to `superpowers:finishing-a-development-branch` to decide on merge / PR.
