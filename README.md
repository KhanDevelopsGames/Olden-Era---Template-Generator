# Olden Era — Simple Template Generator

> **⚠️ Disclaimer**  
> This project was mostly built with GitHub Copilot. The goal was to quickly create a tool for generating random map templates with custom settings to play with friends — not to produce a polished, production-ready application. As a result, some edge cases may not be handled perfectly and bugs are to be expected. Features marked as **[EXPERIMENTAL]** in the UI have not been thoroughly tested and may produce broken or unplayable maps.

---

## What is this?

A small Windows desktop tool that generates `.rmg.json` random map templates for **Heroes of Might and Magic: Olden Era**. Instead of editing JSON by hand, you configure your map settings through a simple UI and click **Generate Template**.

---

## Features

### General

- Configure **template name**, **player count** (2–8), and **map size**
- Experimental map sizes available via an opt-in checkbox
- **Reproducible generation** — set an optional integer **seed** to make output deterministic; the same settings + seed always produce the same `.rmg.json`. Click 🎲 to roll a fresh seed, leave blank for non-deterministic output. The seed used is shown after generation so you can copy and share it.
- **Auto update check** — notifies you on startup if a newer version is available on GitHub
- Automatically detects your **Olden Era install path** via Steam registry and opens the Save dialog in the correct `map_templates` folder
- **Preview image** — generate a visual overview of the zone layout before saving; optionally save it in the template directory for in-game display

### Hero Settings

- Configure **minimum hero count**, **maximum hero count**, and the **per-castle increment**  
  *(defaults: 4 min / 8 max / 1 increment)*

### Map Layout (Topology)

- **Random** — zones placed at random and connected to all neighbouring zones (Delaunay-based)
- **Ring** — zones arranged in a circle, each connected to its two neighbours
- **Chain** — zones connected in a straight line
- **Hub** *(Experimental)* — all zones connect to a shared central hub
  - Configurable **hub zone size** (0.25× – 3×)

### Zone Configuration

- Set **castles per player zone** and **castles per neutral zone**
- Configure **resource spawn rate**, **structure density**, **neutral stack strength**, and **border guard strength**
- **Match player castle factions** — player zones receive a faction-locked castle to match their chosen faction

#### Advanced Settings

Unlock additional controls via the **Advanced settings** checkbox:

- **Player zone size** and **neutral zone size** multipliers (0.5× – 2×)
- **Guard strength randomization** percentage
- **Advanced neutral zone breakdown** — independently configure counts of low/medium/high quality neutral zones, with and without castles (up to 32 total zones in advanced mode)

### Portal & Connectivity Options

- **Always spawn portals** — adds portal connections between non-adjacent zones, with a configurable **max portal connections** limit
- **Generate roads** — adds road objects between connected zones
- **Spawn remote footholds** — places remote footholds in each castle zone
- **Isolate player zones** *(Random layout only)* — players can only reach each other through neutral zones
- **Balanced zone placement** — attempts to distribute zones more evenly across the map

### Game Rules

- **Victory condition** — choose between standard and alternative win conditions
- **Faction Laws experience** and **Astrology experience** multipliers

### Win Conditions

#### Lose when starting city is lost
#### Lose when starting hero is lost

#### City Hold
Players must capture and hold a designated city for a set number of days to win.

- The city location is chosen automatically based on topology:
  - **Hub** — the hub zone becomes the hold city
  - **Other layouts** — the highest-quality neutral zone that is maximally equidistant from all players is attempted to be selected
- The hold city is highlighted with a **golden house icon** in the preview image
- Generation is blocked if no valid city zone can be determined

#### Tournament Mode
A special competitive mode designed for 1v1 play with an isolated preparation phase.

- **Only available with exactly 2 players** — generation is blocked otherwise
- Each player starts in a **completely isolated cluster** of zones; it is impossible to reach the opponent until the tournament begins
- Neutral zones are **balanced by quality tier** across both players (each side gets the same mix of low/medium/high zones)
- Zone order within each cluster is **randomised** but **mirrored** — both players experience the same layout
- Supports multiple topologies:
  - **Chain / Ring** — two mirrored isolated chains
  - **Random** — two mirrored isolated Delaunay-like clusters
  - **Hub** — each player gets their own private hub cluster (respects hub zone size setting)
- Configure: **first tournament day**, **announcement lead time**, **tournament interval**, and **points needed to win**

---

## Installation

Two ways to use the generator:

### Web version (any OS, no install)

Open [the web version](https://rannes.github.io/Olden-Era---Template-Generator/) in any modern browser. Generation runs entirely on your device — nothing is uploaded. Works on Windows, macOS, Linux, and mobile. After generating, the `.rmg.json` downloads to your usual downloads folder; drop it into the game's `map_templates` folder by hand.

### Windows desktop app (Steam auto-detect, save direct to `map_templates`)

1. Download the latest release from the [Releases page](https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases)
2. Extract and run `OldenEraTemplateGenerator.exe`
3. No installation required — it's a single self-contained executable

> **Requirements:** Windows 10/11 with [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed

---

## How to use

1. Fill in the settings you want
2. Click **Preview** to see a visual overview of the generated zone layout (optional)
3. Click **Generate & Save…**
4. Save the `.rmg.json` file to your Olden Era templates folder:  
   `<Olden Era install folder>\HeroesOldenEra_Data\StreamingAssets\map_templates`  
   *(The tool tries to open this folder automatically if it can find your Steam installation)*
5. Launch Olden Era and select your template when creating a game

---

## Where are templates stored?

```
<Olden Era install folder>\HeroesOldenEra_Data\StreamingAssets\map_templates
```

---

## Building from source

Requirements: the .NET 10 SDK. Visual Studio 2022+ or any editor with C# tooling works.

```bash
git clone https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator.git
cd "Olden-Era---Template-Generator"
dotnet workload install wasm-tools   # one-time, needed for the web project
dotnet build OldenEra.slnx
```

The `wasm-tools` workload provides the Emscripten toolchain that statically links SkiaSharp's native binary into the WebAssembly bundle. Without it, the web project builds but Generate fails at runtime with `DllNotFoundException: libSkiaSharp`.

The solution contains four projects:

- `src/OldenEra.Generator/` — `net10.0` class library with the generator and SkiaSharp PNG renderer. Builds on any OS.
- `src/OldenEra.Web/` — Blazor WebAssembly frontend. Run with `dotnet run --project src/OldenEra.Web`. Builds on any OS but requires the `wasm-tools` workload (see above).
- `src/OldenEra.TemplateEditor/` — WPF desktop app. Builds and runs on Windows. (`Directory.Build.props` enables `EnableWindowsTargeting=true` so the project compiles on macOS/Linux for cross-checking; tests still need Windows to execute.)
- `tests/OldenEra.TemplateEditor.Tests/` — xUnit tests. Currently `net10.0-windows`; runs on Windows only.

### End-to-end (web) tests

Playwright smoke tests live in `tests/e2e/`. They boot `OldenEra.Web` and exercise Generate plus the download buttons. Run locally:

```bash
cd tests/e2e
npm install
npx playwright install --with-deps chromium
npm test
```

CI runs them on Ubuntu via `.github/workflows/e2e.yml`.

---

## Deployment

The web version auto-deploys to GitHub Pages on every push to `main` via `.github/workflows/deploy-web.yml`. To enable Pages on a fresh fork:

1. Push a commit to `main`. The Actions workflow runs `dotnet publish` and pushes the build to a `gh-pages` branch.
2. **One-time:** in the repo's GitHub UI, go to **Settings → Pages**. Under "Build and deployment", set **Source** to *Deploy from a branch* and pick the `gh-pages` branch (root). Save.
3. Subsequent pushes to `main` redeploy automatically.

The deployed URL is `https://<owner>.github.io/Olden-Era---Template-Generator/`.

---

## Contributing

Issues and pull requests are welcome. Keep in mind this is a hobby project — response times may vary.

---

## Credits

The bundled hero, unit, spell, skill, subclass, and faction reference data
in `src/OldenEra.Generator/CommunityData/` is sourced from
[**alcaras/homm-olden**](https://github.com/alcaras/homm-olden), a community
datamine of the game's `Core.zip`. Re-fetch via
`python3 src/OldenEra.Generator/CommunityData/scripts/fetch-from-alcaras.py`.

---

## License

This project is not affiliated with or endorsed by the developers of Heroes of Might and Magic: Olden Era.  
Use generated templates at your own risk.
