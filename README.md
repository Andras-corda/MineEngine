# Mine Engine

<!-- Badges -->
[![GitHub License](https://img.shields.io/github/license/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/blob/main/LICENSE)
[![GitHub Repository Size](https://img.shields.io/github/repo-size/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine)
[![GitHub Open Issues](https://img.shields.io/github/issues/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/issues)
[![GitHub Stars](https://img.shields.io/github/stars/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/stargazers)
![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
[![Architecture diagram](https://gitdiagram.com/diagram-badge.svg)](https://gitdiagram.com/andras-corda/mineengine?utm_source=readme&utm_medium=badge)

Minecraft mod creation environment, written in C# / WPF. Current version: **V0.4.5** (redesigned interface: contrasted grays, Roboto font, vector icons, English or French; visual programming with graphs: functions, procedures, classes, structures, interfaces and enumerations; MCreator-style project layout; integrated Java code editor; dark blue theme).

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
4. Add content from the **Dashboard** or the **Project** menu: each element opens in its own editor tab (double-click to reopen it):
   - **Item**: Visuals, Type (simple, tools, weapons, armor), Properties, Food (with effects), Data (fuel, tags, crafting remainder), Events (linked to a graph);
   - **Block**: Visuals, Properties, Harvest, Item;
   - **Mob**: Appearance, Attributes, AI (behaviors in order of priority), Spawn (egg, biomes), Sounds, and Loot;
   - **Recipe**: crafted with or without a workbench, or in a furnace;
   - **Graph**: in-game behaviors, or a function, procedure, class, structure, interface or enumeration, drawn in the node editor (see below);
   - **Import**: PNG images (Texture assets) and OGG sounds (Sound assets), reusable anywhere.
5. **Build > Generate Mod** (Ctrl+Shift+B): The `.jar` file is produced in the project’s `build/libs/` folder.
6. **Build > Launch Minecraft** (F5): Launches Minecraft with the mod. The content can be found in a Creative tab named after the mod.

## Editor

### Interface
- **Look**: Roboto font (bundled), contrasted neutral grays with a blue accent. Icons come from `Themes/Icons.xaml` (font glyphs and vector icons for blocks, items, mobs, recipes, graph kinds and loaders); rules in `docs/ICONS.md`.
- **Home**: the sidebar holds New / Open / Add project and the pages (Home, My projects, Settings, MDKs, Help); the ☰ button collapses it to icons only.
- **Layout**: Explorer on the left, tabs in the center, Details on the right, Output and Diagnostics at the bottom. Panels can be moved, stacked, or detached; the layout is saved. **View > Reset Layout** restores the default layout. Panels adapt to a narrow window.
- **Explorer** (VS Code-style): project tree, file search, creation, renaming, and trash; updates when files change on disk. The Structure section lists the classes, methods, or keys of the open file.
- **Dashboard**: a permanent tab that lists the mod’s elements (like MCreator), either as a list or as thumbnails, with filters by type, search, validation status, and number of uses. Elements can be organized in **folders and subfolders**: breadcrumb trail, double-click to enter a folder, Backspace to go up, **New folder**, rename, delete (the content moves up), **Move to** in the context menu, or drag an element onto a folder or a breadcrumb segment. New elements are created in the open folder; the search covers all folders. Folders only organize the editor: they change neither the game nor the files.
- **Layout buttons** (top right of the title bar, like VS Code): customize the layout (focus mode, show all, reset), show or hide the Explorer (Ctrl+B), Output and Diagnostics (Ctrl+J), Details (Ctrl+Alt+B).
- **Code tabs**: Java editor (see below) with syntax highlighting, line numbers, breadcrumb trail and toolbar (Save, Search, Format). Ctrl+W closes the tab; within the tab, Ctrl+Z applies to the text.
- **Details** (Unreal and Unity style): the selected target, followed by its properties: an asset’s inspector, or the properties of an open file.
- **Undo / Redo** (Ctrl+Z / Ctrl+Y): any changes to the project, including adding and removing assets and project settings.
- **References**: Textures, sounds, and items are identified by their internal IDs; you can rename an asset without breaking anything that uses it. The Inspector displays “Used by,” and deleting
- **Old Projects**: A project from an earlier version is converted upon opening: `Project.json` becomes `<Name>.mineengine`, `Content/` becomes `elements/`, Java files from `Scripts/` move to `src/main/java` (in their package folder), `Generated/` is removed except its test worlds (`run/`). A zip of the old project is kept in `.mineengine/backups`.
- **Diagnostics**: Updated with every change and after every build. A Java compilation error is linked to the asset that caused it, or to the line of the Java file; double-clicking on it takes you there.
- **Backups**: Automatic saving (every 5 minutes by default) and backup copies in `.mineengine\backups` within the project.
- **Log**: The Output console is also written to `%LOCALAPPDATA%\MineEngine\logs`.

### Nodal Graphs

A **graph** describes behaviors without writing any Java code, similar to Unreal’s Blueprints: an **event** (right-clicking a block, using an item, a player logging in, block broken) triggers a sequence of **conditions** (owns the item, is holding the item, is crouching, chance, if) and **actions** (spawn, give or take an item, send a message, play a sound, place a block, explosion, cancel the event). **Value** nodes calculate numbers, text, positions, or conditions.

- **+ Graph** in the Dashboard creates a graph and opens it; double-clicking a graph reopens it.
- The white wire specifies the execution order; the colored wires carry values (red: Boolean, green: number, pink: text, blue: player, orange: position). An unconnected input is set directly on the node.
- **Right-click**: add a node (search). **Drag a pin** toward another: connect; into empty space: select a node to connect to. **Alt+click**: disconnect. **Right-click and drag** or hold the scroll wheel: move the view; **scroll wheel**: zoom; **F**: show all; **Delete**, **Ctrl+D**, **Ctrl+A**.
- Incomplete nodes are highlighted in red (error) or orange (warning); “Details” and “Diagnostics” list the issues, and clicking on a node displays it. Use Ctrl+Z to undo changes, and changes are saved with the project.
- Upon compilation, each event graph becomes a Java class (MyGraphBehavior) subscribed to the Forge or NeoForge event bus.
- **Backspace** or **Delete** removes the selected nodes (the entry node of a function cannot be removed).

### Programming with graphs

**+ Graph** in the Dashboard (or **Project > Add a graph**) asks what the graph defines:

| Kind | Definition panel | Node canvas |
| --- | --- | --- |
| Event graph | graph variables (kept while the game runs) | events and what they trigger |
| Function | parameters, return values (one or more), local or global variables, accessibility, pure | body, from the entry node to the Return nodes |
| Procedure | parameters, local or global variables, accessibility | body (no return value) |
| Class | constructor parameters, fields (public, protected, private), parent class, implemented interfaces, methods and nested types | constructor body (with "Parent constructor" first when the parent needs parameters) |
| Structure | fields | none |
| Interface | methods to provide (parameters, at most one return value) | none |
| Enumeration | values | none |

- Every definition becomes available in the add-node menu of the graphs that can see it: **Call** (function, procedure, method, interface method), **Create** (class or structure), **Read / Modify** a field or a variable, an enumeration value, **Self** inside a class.
- **Visibility**: a public definition can be used everywhere. A private definition outside any class can only be used from graphs of the same Dashboard folder; a definition nested in a class (method, nested type) can only be used inside that class (private) or also in its subclasses (protected).
- **Inheritance**: a class receives the fields and methods of its parent; a method with the same name and signature overrides it; an object of a subclass can be connected wherever the parent class or an implemented interface is expected. Missing interface methods, inheritance cycles and signature mismatches are reported in Diagnostics.
- **Java**: definitions are generated in the `code` package of the mod: a top-level function becomes a class with a static method, a class becomes a Java class (`extends`, `implements`, `@Override`), several return values become a `record`. Every function, method and constructor receives the world (`Level level`) as first parameter, like Unreal's *World Context*, so game actions work everywhere. The generated classes can be called from hand-written Java code.

### Java code

Like in MCreator, a project is a regular Gradle project: Java code generated by Mine Engine and Java code written by hand live side by side in `src/main/java` and are compiled together.

- **Project > New Java class** (or the `{}` button of the Explorer): event listener (`@SubscribeEvent` methods called by the game, with the right annotation for Forge or NeoForge), utility class or empty class. The class is created in the package of the selected folder, or in `<mod package>.custom`.
- **Generated code** is read-only: it is rewritten at every build. **Lock to edit** (banner of the tab) keeps Mine Engine from rewriting the file, so it can be edited by hand (also works for `build.gradle`); **Unlock** gives it back to Mine Engine. Locking is undoable and saved in the project file.
- Mine Engine deletes only the generated files it no longer produces (list in `.mineengine/generated.json`); files written by hand are never touched.
- **Editor**: foldable blocks, automatic indentation, closing of brackets and quotes, completion (Ctrl+Space or while typing: keywords, common Minecraft and loader classes, generated mod classes, words of the file; accepting a class adds its `import`), **Format** (Ctrl+Shift+F), search and replace (Ctrl+F), errors of the last build underlined in the code with a dot in the margin.

### Settings
**Tools > Settings** (Ctrl+,): **Appearance** tab for light, dark (contrasted neutral grays, blue accent), or Windows-style themes, and the interface **language** (English by default, or French; applied at the next start); **Backup** tab for the automatic backup interval and the number of backup copies. The selection takes effect immediately and is saved in `%LOCALAPPDATA%\MineEngine\settings.json`.

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

The project folder is the Gradle project of the mod, like an MCreator workspace:

```
MyMod/
  MyMod.mineengine     project settings (MDK, Dashboard folders, locked files)
  elements/            one .asset.json file per element, organized by type (items/, blocks/, mobs/, recipes/, textures/, sounds/, graphs/)
  Textures/            imported PNG images
  Sounds/              imported OGG sounds
  src/main/java        Java code: generated by Mine Engine and written by hand
  src/main/resources   generated resources (models, languages, recipes, loot tables...)
  build.gradle...      Gradle files copied from the MDK (generated, can be locked)
  build/libs/          generated .jar mods
  run/                 Minecraft game folder (test worlds)
  .mineengine/         local cache: list of generated files, backups
```

### Program Structure Diagram
[![Architecture diagram of andras-corda/mineengine](https://gitdiagram.com/andras-corda/mineengine/diagram.png)](https://gitdiagram.com/andras-corda/mineengine?utm_source=readme&utm_medium=picture)

> Translated with DeepL.com