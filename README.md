# Mine Engine

<!-- Badges -->
[![GitHub License](https://img.shields.io/github/license/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/blob/main/LICENSE)
[![GitHub Repository Size](https://img.shields.io/github/repo-size/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine)
[![GitHub Open Issues](https://img.shields.io/github/issues/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/issues)
[![GitHub Stars](https://img.shields.io/github/stars/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/stargazers)
![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
[![Architecture diagram](https://gitdiagram.com/diagram-badge.svg)](https://gitdiagram.com/andras-corda/mineengine?utm_source=readme&utm_medium=badge)

Minecraft mod creation environment, written in C# / WPF. Current version: **V0.4, nodal prototype** (Blueprint-style behavior graphs, translated into Java for Forge and NeoForge).

Progress from version to version is tracked in [ROADMAP.md](ROADMAP.md).

## Requirements

- Windows 10 or 11
- .NET SDK 9
- JDK 17 or newer to run Gradle. Ideally, the version required by the MDK (17 for Minecraft 1.20.1, 21 for 1.21.1); otherwise, Gradle will download it itself to compile the mod.
- An Internet connection to download the MDKs and for the first build (Gradle and Minecraft).

## Install a release
  > Unreleased yet

## Get Started with code src

```bash
dotnet run --project src/MineEngine.App
```
In Visual Studio: Right-click **MineEngine.App** > **Set as Startup Project**, then press F5.

Tests:

```bash
dotnet test
```

Full-stack tests (actual MDK and Gradle builds, several minutes each) run only if `MINEENGINE_E2E=1`.

## MDK

> An MDK (Mod Development Kit) is the official Gradle project for Forge or NeoForge for a specific version of Minecraft. Each Mine Engine project selects an MDK, which determines its loader and Minecraft version.

### **Tools > MDK Manager** allows you to:

- download an official MDK: Forge 1.20.1, Forge 1.21.1, NeoForge 1.21.1;
- import an MDK `.zip` archive or an already extracted folder;
- delete an MDK.

> Users can also manually place a `.zip` archive or MDK folder in `%LOCALAPPDATA%\MineEngine\mdks`, then click **Refresh**.

Any Forge, NeoForge, or Fabric MDK can be installed, but the generator only produces code for the three versions listed above. The others appear as “Not supported.”

## Usage

1. **Tools > MDK Manager**: Install at least one MDK.
2. The **Home** screen lists your projects (search, sort, recent): double-click to open one, or select **Add Project...** to reference an existing one. **File > Close Project** takes you back to the Home screen.
3. **New Project...**: name, ID, version, authors, website, license, description, and MDK. Everything can be edited in **Project > Project Settings**, except for the ID.
4. Add content from the **Dashboard** or the **Project** menu, then configure it in the **Details** panel:
   - **Item**: Visuals, Properties, Food;
   - **Block**: Visuals, Properties, Harvest, Item;
   - **Mob**: Appearance, Attributes, AI (behaviors in order of priority), Spawn (egg, biomes), Sounds, and Loot;
   - **Recipe**: crafted with or without a workbench, or in a furnace;
   - **Graph**: in-game behaviors, drawn in the node editor (see below);
   - **Import**: PNG images (Texture assets) and OGG sounds (Sound assets), reusable anywhere.
5. **Build > Generate Mod** (Ctrl+Shift+B): The `.jar` file is copied to the project’s `Build/` folder.
6. **Build > Launch Minecraft** (F5): Launches Minecraft with the mod. The content can be found in a Creative tab named after the mod.

## Editor

### Interface
- **Layout**: Explorer on the left, tabs in the center, Details on the right, Output and Diagnostics at the bottom. Panels can be moved, stacked, or detached; the layout is saved. **View > Reset Layout** restores the default layout. Panels adapt to a narrow window.
- **Explorer** (VS Code-style): project tree, file search, creation, renaming, and trash; updates when files change on disk. The Structure section lists the classes, methods, or keys of the open file.
- **Dashboard**: a permanent tab that lists the mod’s elements (like MCreator), either as a list or as thumbnails, with filters by type, search, validation status, and number of uses.
- **Code tabs**: editor with syntax highlighting, line numbers, and breadcrumb trail. Generated files and Project.json are read-only; an asset opens in JSON preview. Ctrl+W closes the tab; within the tab, Ctrl+Z applies to the text.
- **Details** (Unreal and Unity style): the selected target, followed by its properties: an asset’s inspector, or the properties of an open file.
- **Undo / Redo** (Ctrl+Z / Ctrl+Y): any changes to the project, including adding and removing assets and project settings.
- **References**: Textures, sounds, and items are identified by their internal IDs; you can rename an asset without breaking anything that uses it. The Inspector displays “Used by,” and deleting
- **Old Projects**: A V0.1 or V0.2 project is converted upon opening (its textures become assets); save to write the new format.
- **Diagnostics**: Updated with every change and after every build. A Java compilation error is linked to the asset that caused it; double-clicking on it takes you there.
- **Backups**: Automatic saving (every 5 minutes by default) and backup copies in `.mineengine\backups` within the project.
- **Log**: The Output console is also written to `%LOCALAPPDATA%\MineEngine\logs`.

### Nodal Graphs

A **graph** describes behaviors without writing any Java code, similar to Unreal’s Blueprints: an **event** (right-clicking a block, using an item, a player logging in, block broken) triggers a sequence of **conditions** (owns the item, is holding the item, is crouching, chance, if) and **actions** (spawn, give or take an item, send a message, play a sound, place a block, explosion, cancel the event). **Value** nodes calculate numbers, text, positions, or conditions.

- **+ Graph** in the Dashboard creates a graph and opens it; double-clicking a graph reopens it.
- The white wire specifies the execution order; the colored wires carry values (red: Boolean, green: number, pink: text, blue: player, orange: position). An unconnected input is set directly on the node.
- **Right-click**: add a node (search). **Drag a pin** toward another: connect; into empty space: select a node to connect to. **Alt+click**: disconnect. **Right-click and drag** or hold the scroll wheel: move the view; **scroll wheel**: zoom; **F**: show all; **Delete**, **Ctrl+D**, **Ctrl+A**.
- Incomplete nodes are highlighted in red (error) or orange (warning); “Details” and “Diagnostics” list the issues, and clicking on a node displays it. Use Ctrl+Z to undo changes, and changes are saved with the project.
- Upon compilation, each graph becomes a Java class (MyGraphBehavior) subscribed to the Forge or NeoForge event bus.

### Settings
**Tools > Settings** (Ctrl+,): **Appearance** tab for light, dark, or Windows-style themes; **Backup** tab for the automatic backup interval and the number of backup copies. The selection takes effect immediately and is saved in `%LOCALAPPDATA%\MineEngine\settings.json`.

## Architecture

### Editor structure

```
src/
  MineEngine.Core        identifiers, Asset, undo/redo history, diagnostics, logs, JSON, files
  MineEngine.Assets      Item, Block, Mob (AI), Recipe, Texture, Sound, Graph (nodes, validation), AssetRegistry, serialization
  MineEngine. Project     ModProject, ProjectLayout, ProjectRepository, AssetFileImporter
  MineEngine.IR          Mod IR and asset translation (ModIRBuilder), GraphCompiler (graph to IRBehavior)
  MineEngine.Minecraft   MDK (library, inspection, catalog), Forge and NeoForge backends, resources
  MineEngine.Generator   ModGenerator: Mod IR to Gradle project
  MineEngine.Build       BuildPipeline, JdkLocator, GradleRunner
  MineEngine.Editor      WPF (ModernWpf, AvalonDock): views, panels, ViewModels (MVVM), services
  MineEngine.App         executable and CompositionRoot
tests/
  MineEngine.Tests       xUnit tests
```

Rules:

- Only `MineEngine.Editor` and `MineEngine.App` depend on WPF.
- The generator reads only the IR mod, never the assets directly.
- Everything specific to a loader is handled by `IModLoaderBackend`; `ModLoaderBackendRegistry` selects the backend appropriate for the project’s MDK.
- Dependencies are passed via the constructor; they are only assembled in `CompositionRoot`.

### Structure of a mod project

```
MyMod/
  Project.json      project settings (including the selected MDK)
  Content/          one .asset.json file per asset, organized by type (Items/, Blocks/, Mobs/, Recipes/, Textures/, Sounds/, Graphs/)
  Textures/         imported PNG images
  Sounds/           imported OGG sounds
  Scripts/          reserved for user Java code (V1.1)
  Generated/        Gradle project generated from the MDK, rewritten with each build (the `run/` directory is preserved)
  Build/            generated .jar mods
```

### Program Structure Diagram
[![Architecture diagram of andras-corda/mineengine](https://gitdiagram.com/andras-corda/mineengine/diagram.png)](https://gitdiagram.com/andras-corda/mineengine?utm_source=readme&utm_medium=picture)

> Translated with DeepL.com