# Olden Era — Simple Template Generator

> **⚠️ Disclaimer**  
> This project was mostly built with GitHub Copilot. The goal was to quickly create a tool for generating random map templates with custom settings to play with friends — not to produce a polished, production-ready application. As a result, some edge cases may not be handled perfectly and bugs are to be expected. Features marked as **[EXPERIMENTAL]** in the UI have not been thoroughly tested and may produce broken or unplayable maps.

---

## What is this?

A small Windows desktop tool that generates `.rmg.json` random map templates for **Heroes of Might and Magic: Olden Era**. Instead of editing JSON by hand, you configure your map settings through a simple UI and click **Generate & Save**.

---

## Features

- Configure **player count**, **map size**, **neutral zones**, and **castle counts**
- Multiple **map topologies** to change how zones are connected:
  - **Random** — zones are placed at random positions and connected to all bordering zones (Delaunay-based)
  - **Ring** — zones arranged in a circle, each connected to its two neighbours
  - **Chain** — zones connected in a straight line
  - **Hub & Spoke** *(Experimental)* — all zones connect to a shared central hub
  - **Shared Web** *(Experimental)* — players connect through shared neutral zones
- **Isolate player zones** — players can only reach each other through neutral zones
- **Spawn portals** — adds portal connections between non-adjacent zones
- **Spawn remote footholds** — places remote foothold objects in each zone
- **Hero settings** — configure min/max hero count and the per-castle increment
- Automatically detects your **Olden Era install path** via Steam registry and opens the Save dialog directly in the correct `map_templates` folder
- **Auto update check** — notifies you on startup if a newer version is available on GitHub

---

## Installation

1. Download the latest release from the [Releases page](https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases)
2. Extract and run `Olden Era - Template Editor.exe`
3. No installation required — it's a single self-contained executable

> **Requirements:** Windows 10/11 with [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed

---

## How to use

1. Fill in the settings you want
2. Click **Generate & Save…**
3. Save the `.rmg.json` file to your Olden Era templates folder:  
   `<Olden Era install folder>\HeroesOldenEra_Data\StreamingAssets\map_templates`  
   *(The tool tries to open this folder automatically if it can find your Steam installation)*
4. Launch Olden Era and select your template in the map generator

---

## Experimental features

The following topologies are marked **[EXPERIMENTAL]** in the UI because they have not been thoroughly tested and may produce maps with game-breaking issues:

| Topology | Known risks |
|---|---|
| **Hub & Spoke** | The central hub zone may cause pathing or balance issues |
| **Shared Web** | Zone connectivity and guard placement are not well-tested with all player/neutral counts |

Use these at your own risk.

---

## Where are templates stored?

```
<Olden Era install folder>\HeroesOldenEra_Data\StreamingAssets\map_templates
```


---

## Building from source

Requirements: Visual Studio 2022+ or the .NET 10 SDK

```powershell
git clone https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator.git
cd "Olden Era - Template Editor"
dotnet build
```

---

## Contributing

Issues and pull requests are welcome. Keep in mind this is a hobby project — response times may vary.

---

## License

This project is not affiliated with or endorsed by the developers of Heroes of Might and Magic: Olden Era.  
Use generated templates at your own risk.
