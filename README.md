# Mine Engine

<!-- Badges -->
[![GitHub License](https://img.shields.io/github/license/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/blob/main/LICENSE)
[![GitHub Repository Size](https://img.shields.io/github/repo-size/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine)
[![GitHub Open Issues](https://img.shields.io/github/issues/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/issues)
[![GitHub Stars](https://img.shields.io/github/stars/Andras-corda/MineEngine)](https://github.com/Andras-corda/MineEngine/stargazers)
![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
[![Architecture diagram](https://gitdiagram.com/diagram-badge.svg)](https://gitdiagram.com/andras-corda/mineengine?utm_source=readme&utm_medium=badge)

Minecraft mod creation environment, written in C# / WPF. Current version: **V0.5.4** (reroute points on graph wires; drop guides in the theme colors; themes: dark, light, midnight blue, crimson red, forest green, gold, purple, high contrast, plus imported themes; accent color, interface size and editor options; importable interface languages; redesigned interface: contrasted grays, Roboto font, vector icons; visual programming with graphs: functions, procedures, classes, structures, interfaces and enumerations; MCreator-style project layout; integrated Java code editor; dark blue theme).

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
3. **New Project...**: name, ID, version, authors, website, license, description, and MDK. Everything can be edited in **Workspace > Project Settings**, except for the ID.
4. Add content from the **Dashboard** or the **Project** menu: each element opens in its own editor tab (double-click to reopen it):
   - **Item**: Visuals, Type (simple, tools, weapons, armor), Properties, Food (with effects), Data (fuel, tags, crafting remainder), Events (linked to a graph);
   - **Block**: Visuals, Properties, Harvest, Item;
   - **Mob**: Appearance, Attributes, AI (behaviors in order of priority), Spawn (egg, biomes), Sounds, and Loot;
   - **Recipe**: crafted with or without a workbench, or in a furnace;
   - **Gameplay** (*Assets > Add > Gameplay*): command (`/name` typed in its tab, a permission level, an optional argument and a feedback message; **Create its graph** makes an event graph ready with "On command", and then opens it), creative tab (icon and items), keybind (default key; a graph event runs when it is pressed), enchantment, fluid (flow, light, textures, optional bucket), game rule (true/false or number) and potion effect;
   - **Graph**: in-game behaviors, or a function, procedure, class, structure, interface or enumeration, drawn in the node editor (see below);
   - **Import**: PNG images (Texture assets) and OGG sounds (Sound assets), reusable anywhere.
5. **Build & Run > Build the mod** (Ctrl+Shift+B): The `.jar` file is produced in the project’s `build/libs/` folder; **Export the mod (.jar)...** builds it and copies it where you choose.
6. **Build & Run > Launch Minecraft** (F5): Launches Minecraft with the mod. The content can be found in a Creative tab named after the mod.

## Editor

### Interface
- **Look**: Roboto font (bundled), contrasted neutral grays with a blue accent. Icons come from `Themes/Icons.xaml` (font glyphs and vector icons for blocks, items, mobs, recipes, graph kinds and loaders); rules in `docs/ICONS.md`.
- **Home**: the sidebar holds New / Open / Add project and the pages (Home, My projects, Settings, MDKs, Help); the ☰ button collapses it to icons only.
- **Menus**: Mine Engine (GitHub repository, website), File, Edit, Workspace (project settings, folders, Java classes, validation), Assets (add, import textures, sounds or `.asset.json` elements, export the selected element), Tools (MDK Manager, Settings), Build & Run (build, export the `.jar`, launch Minecraft, reload Gradle), Window (panels and layout).
- **Toolbar** (under the menus): on the left, **+** (menu to add an item, block, mob, recipe, any kind of graph, or import textures and sounds), then for the selected element **Duplicate** (Ctrl+D), **Delete**, **Edit**, **Open graph**, **View Java code** (generates the mod code without compiling and opens the element's Java file at its line), and **New folder**; on the right, the **search** (Ctrl+F: it filters the Dashboard, or the Resources and Languages tabs when they are shown), **List** / **Tiles** (hidden over Resources and Languages), **Validate**, **Build** and **Run**.
- **Layout**: Explorer on the left, tabs in the center, Details on the right, Output and Diagnostics at the bottom. Panels can be moved, stacked, or detached (the drop guides use the theme colors); the layout is saved. **Window > Reset Layout** restores the default layout. Panels adapt to a narrow window.
- **Explorer** (VS Code-style): project tree, file search, creation, renaming, and trash; updates when files change on disk. The Structure section lists the classes, methods, or keys of the open file.
- **Dashboard**: a permanent tab that lists the mod’s elements (like MCreator), either as a list or as thumbnails, with search (toolbar), validation status, and number of uses. Elements can be organized in **folders and subfolders**: double-click to enter a folder, Backspace to go up, **New folder**, rename, delete (the content moves up), **Move to** in the context menu, or drag an element onto a folder. New elements are created in the open folder; the search covers all folders. Folders only organize the editor: they change neither the game nor the files. **Right-click in the empty part**: up to the parent folder, back to the root, new folder, undo, redo, validate, build and run. **Ctrl+click** and **Shift+click** select several elements: Delete, Move to and drag-and-drop then act on all of them (one step to undo). In the tile view, **Ctrl+wheel** makes the tiles bigger or smaller.
- **Layout buttons** (top right of the title bar, like VS Code): customize the layout (focus mode, show all, reset), show or hide the Explorer (Ctrl+B), Output and Diagnostics (Ctrl+J), Details (Ctrl+Alt+B).
- **Code tabs**: Java editor (see below) with syntax highlighting, line numbers, breadcrumb trail and toolbar (Save, Search, Format). Ctrl+W closes the tab; within the tab, Ctrl+Z applies to the text.
- **Properties** (Unreal and Unity style): for an element selected in the Dashboard, a summary only (type, folder, state, main facts, what uses it and what it uses) with **Edit the element**: it is edited in its own tab, whose name and identifier are in Properties. For an open file, its properties; for a graph, its summary and its **definition** (name, variables, parameters...).
- **Undo / Redo** (Ctrl+Z / Ctrl+Y): any changes to the project, including adding and removing assets and project settings.
- **References**: Textures, sounds, and items are identified by their internal IDs; you can rename an asset without breaking anything that uses it. The Properties panel displays “Used by,” and deleting
- **Old Projects**: A project from an earlier version is converted upon opening: `Project.json` becomes `<Name>.mineengine`, `Content/` becomes `elements/`, Java files from `Scripts/` move to `src/main/java` (in their package folder), `Generated/` is removed except its test worlds (`run/`). A zip of the old project is kept in `.mineengine/backups`.
- **Diagnostics**: Updated with every change and after every build. A Java compilation error is linked to the asset that caused it, or to the line of the Java file; double-clicking on it takes you there.
- **Backups**: Automatic saving (every 5 minutes by default) and backup copies in `.mineengine\backups` within the project.
- **Log**: The Output console is also written to `%LOCALAPPDATA%\MineEngine\logs`.

### Nodal Graphs

A **graph** describes behaviors without writing any Java code, similar to Unreal’s Blueprints: an **event** (right-clicking a block, using an item, a player logging in, block broken) triggers a sequence of **conditions** (owns the item, is holding the item, is crouching, chance, if) and **actions** (spawn, give or take an item, send a message, play a sound, place a block, explosion, cancel the event). **Value** nodes calculate numbers, text, positions, or conditions.

- **+ Graph** in the Dashboard creates a graph and opens it; double-clicking a graph reopens it.
- The white wire specifies the execution order; the colored wires carry values (red: Boolean, green: number, pink: text, blue: player, orange: position). An unconnected input is set directly on the node.
- The **Nodes** window (**Window > Nodes**, next to the Explorer by default) lists the nodes of the open graph by domain, as in MCreator: events, logic, math, text, player, items, blocks, world and program (functions, objects, variables). Search by name or description, then **drag** a node onto the graph, or **double-click** it to add it at the center of the view. Like every panel, it can be detached or docked elsewhere (with Properties, for example).
- **Block** nodes: remove, replace (keeping the state and the data), update, break, use (right-click), turn the block, read and write its properties and its NBT data (numbers, texts, booleans), and read the block, its direction, opacity, hardness, light level, enchanting bonus or solidity. **Item** nodes work on **item stacks** (green-teal wire), for example the item in the main hand: cooldown, enchantments, custom name, count, durability, rarity, food, smelting, harvesting a block... The Java follows the Minecraft version (1.20.1 or 1.21.1).
- **Logic and loops**: and, or, different, greater or equal, less or equal, same text; **Repeat** (number of turns, gives the current turn), **While** (10,000 turns at most so that the game does not freeze) and **For each player**. A loop has two exec outputs: **Loop body**, run at each turn, and **Completed**, run after the last one.
- **Math and text**: division and modulo (0 when dividing by 0), power, square root, minimum, maximum, absolute value; text length, contains, replace, text to number, number to text.
- **World and players**: dimension, time of day, day, rain, storm, set the time and the weather, spawn a creature or particles; health, maximum health, food, alive, set health, heal, deal damage, teleport, armor piece, off hand, block looked at; item in a slot of the inventory, empty a slot, count an item; numbers and texts stored in the custom data of an item stack (NBT in 1.20.1, data component from 1.20.5).
- **Positions**: X, Y, Z, position from coordinates, distance, same position, in an area, move in a direction.
- **Events**: also player leaves, every tick (per player), player hurt (cancellable, gives the damage), creature appears (cancellable) and creature dies (give the identifier of the creature).
- **Flow** (like Blueprints): **Switch on number** / **Switch on text** (one output per case, then Default; cases in Properties, one per line, or **Add pin**), **Sequence** (outputs run from top to bottom; **Add pin**) and **Flip Flop** (A, then B, then A...; gives *Is A*). Right-click on a Switch or a Sequence removes its last pin. An execution **reroute** can send several wires: they run from top to bottom, like a Sequence.
- **Commands**: run a command of Minecraft or of the mod as the player (with the player's permission) or as the server (every permission, silently, at a position).
- **Ray casts and physics**: does the ray hit a block, the block hit, does the player look at a creature and which one (hidden by blocks); set the velocity, push, launch forward (blocks per second), gravity on or off, on the ground, fall distance.
- **Appearances** (textures and 3D models switched while the game runs): an item, a block or a mob has a list of **Appearances** in its tab (numbered from 1; 0 is the normal look), each one a texture or, for an item or a block, a 3D model. **Set item appearance** (on an item stack: its custom model data, the item model lists one override per appearance), **Set block appearance** (one block at a time: the block has a state property `appearance`), **Set creature appearance** (the mobs of the mod around a position, by identifier or all; a mob only changes its texture), and the nodes that read the number back.
- **Sounds and music**: play a sound with volume and pitch, play a sound to one player only, stop the sounds, play a music (a music of the game, a disc or a sound of the mod) and stop it.
- **Right-click on a node**: align the selected nodes in a row or in a column, align their edges, distribute them, add a comment or a tooltip, duplicate, delete (one step to undo).
- **Comments** (**C**, like in Blueprints): a titled, colored box around the selected nodes, drawn behind them; moving its title moves the nodes inside; its edges and corners resize it; **double-click** its title to rename it on the canvas. Color and an optional tooltip are set in **Properties**.
- **Tooltips** (**T**): a small bubble for the selected nodes (or comment); its message, written in **Properties**, shows only when the mouse is over the bubble or over those nodes (they show a small *i*). Comments and tooltips change nothing in the Java.
- **Grid** button (top right of the graph): moved nodes and resized comments snap to the grid, so that they stay aligned and the wires straight (same option as Settings > Editors).
- **Right-click**: add a node (search). **Drag a pin** toward another: connect; into empty space: select a node to connect to. **Alt+click**: disconnect. **Right-click and drag** or hold the scroll wheel: move the view; **scroll wheel**: zoom; **F**: show all; **Delete**, **Ctrl+D**, **Ctrl+A**. **Double-click a wire**: add a **reroute** point (like Unreal) to route the wire elsewhere; it can be moved like a node, and deleting it keeps the wire. Reroutes change nothing in the generated Java.
- **Wires**: the wire under the mouse is highlighted; **Backspace** (or Delete) breaks it. Right-click on a node: **Refresh nodes** (they follow the functions, variables and types that changed; wires that no longer fit are removed) and **Break all connections**.
- **Reroute references** (like the named reroutes of Unreal): right-click on a value reroute, **Create a reference**; the hollow point gives the value of the reroute anywhere in the graph, without a wire across it. Deleting the reroute connects its references to its source.
- **Casts**: the **Conversions** domain holds *To whole number*, *To long*, *To float*, *To decimal*, *To byte*, *To character*, *To text*, *To rich text*, *To identifier*, *To UUID*, *To living entity* and *To player*; their input takes the type of the wire. Connecting two pins of different types inserts the right cast between them (an int on a float input gives *To float*).
- **Types** (see `TypesVar.md`): besides numbers, decimals, texts, players, positions, items, blocks and the V0.6.7 types, values can be a **character**, a **long**, a **float**, a **byte**, a **UUID**, a **living entity**, a **rich text** (colors, bold), an **identifier** (`minecraft:stone`), an **equipment slot**, a **hand**, a **rarity**, a **fluid**, a **box** (AABB), a **damage source**, an **effect instance**, **NBT data** or a **world**, in the graphs, the parameters and the global variables, with nodes to make and use them.
- Incomplete nodes are highlighted in red (error) or orange (warning); “Details” and “Diagnostics” list the issues, and clicking on a node displays it. Use Ctrl+Z to undo changes, and changes are saved with the project.
- Upon compilation, each event graph becomes a Java class (MyGraphBehavior) subscribed to the Forge or NeoForge event bus.
- Validation keeps intermediate results: the problems of an element are computed again only when it changes (or, for a graph, when a definition of the program changes), and the list of references and the program model are shared instead of being rebuilt by each element.
- **Backspace** or **Delete** removes the selected nodes (the entry node of a function cannot be removed).

### Programming with graphs

**+** in the toolbar (or **Assets > Add a graph**) asks what the graph defines. The definition is edited in the Details panel; a structure, interface or enumeration has no canvas, so its definition fills the tab:

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
- **Function library** (*Assets > Add > Visual programming > Function library*): a graph that holds functions and procedures, always public and usable in every graph (like Blueprint Function Libraries). Its tab lists them, with **Add a function** / **Add a procedure**; a function can be **pure** (no execution wire). In the add menu and in the **Nodes** window, they have a domain of their own named after the library, and their nodes are gray. In Java, a library becomes a class of static methods (`Library.function(level, ...)`).
- **Visibility**: a public definition can be used everywhere. A private definition outside any class can only be used from graphs of the same Dashboard folder; a definition nested in a class (method, nested type) can only be used inside that class (private) or also in its subclasses (protected).
- **Inheritance**: a class receives the fields and methods of its parent; a method with the same name and signature overrides it; an object of a subclass can be connected wherever the parent class or an implemented interface is expected. Missing interface methods, inheritance cycles and signature mismatches are reported in Diagnostics.
- **Java**: definitions are generated in the `code` package of the mod: a top-level function becomes a class with a static method, a class becomes a Java class (`extends`, `implements`, `@Override`), several return values become a `record`. Every function, method and constructor receives the world (`Level level`) as first parameter, like Unreal's *World Context*, so game actions work everywhere. The generated classes can be called from hand-written Java code.

### Global variables

The **Global** view of the toolbar (between Resources and Languages, **Ctrl+4**) works like the global variables of MCreator: each row is a variable readable and changeable from every graph, through its **Get** and **Set** nodes (domain *Global variables* of the add menu and of the Nodes window).

- **Type**: number, decimal number, text, true/false, player, entity, position, direction, 2D and 3D vectors (whole or decimal), item, item stack, block, block state, sound, or a class, structure or enumeration of the project.
- **Container**: a single value, or a collection of values of that type: **Array**, **List**, **Set** (HashSet) or **Dictionary** (with the type of its keys). Your own collections are lists or dictionaries of your structures, classes or enumerations.
- **Scope** (names never translated, see `scopeGlobalVariable.md`): `player_persistent`, `player_session`, `player_lifetime`, `player_dimension`, `player_account` (shared by every save), `player_guild` (the guild is set by a node), `player_team` (scoreboard teams); `entity_type`, `entity_owner` (tamed animals), `entity_group` (set by a node), `entity_dimension`; `global_map`, `global_dimension`, `global_session`, `global_chunk`, `global_server` (kept in the config folder, shared by every save); `world_structure`, `world_biome`, `world_dimension`. The nodes of a variable ask for what its scope needs: the player, the entity or the position.
- **Starting value** for a number, a decimal, a text, a boolean or a direction.
- Entities, players and objects cannot be written in the save: in a saved scope they are kept only until the server stops (a warning tells it).
- **Collection nodes** (size, contains, add, get or set at an index, remove, get or set a key, set the size of an array, **For each value**) take the type of the wire connected to them, like Unreal's wildcard pins; dropping a wire in empty space offers them already typed. Vector, decimal, direction, block state and entity nodes complete them.
- **Java**: a `code/MineEngineGlobals.java` class holds the values (`SavedData` for Map and World, player data saved with the player), with `getX` / `setX` accessors usable from hand-written code.

### Puppeteer (behavior trees)

**Puppeteer** is the AI sub-program of Mine Engine: the behavior of a mob is described as a **behavior tree**, like Unreal's Behavior Trees. **Assets > Add > Behavior tree (Puppeteer)** (or **+** in the toolbar) creates a tree from an example (spot the nearest player, attack it, otherwise wander) and opens it in its own tab. In the **AI** tab of a mob, **Driven by** chooses between the list of behaviors and a Puppeteer tree (**Create a Puppeteer tree** makes one for this mob); the tree then replaces the list, and the mob still swims so it does not drown.

- **Root**: the tree runs on every game tick; when its branch ends, it starts again.
- **Composites**: **Selector** (tries its children until one succeeds), **Sequence** (runs its children until one fails), **Random selector**, **Simple parallel** (a main task with others running in the background). Children run **from left to right**: moving a node changes its priority, and the numbers on the nodes give the execution order.
- **Tasks**: wait, move to, wander, melee attack, look at, flee, find the nearest creature, choose a random position, set a boolean or a number, clear a key, play a sound, leap, shoot a projectile (arrow, snowball, small fireball), heal, teleport.
- **Decorators** (blue, above a node): conditions (key is set, boolean is true, compare a number, distance, health, time of day, in water, sees the creature, random chance) with **Invert** and **Check again** (Unreal's *Observer aborts*: self, lower priority, both), and modifiers (cooldown, loop, time limit, always succeed, invert the result).
- **Services** (green, below a node): spot the nearest creature, remember the attacker, measure a distance, at the chosen interval while the node runs.
- **Blackboard**: the memory of the mob (creature, position, number or boolean keys); each mob has its own. Nodes refer to keys by identifier, so renaming a key keeps the links.
- **Execution order**: a numbered badge on the top right corner of each node, like Unreal's numbers.
- **Comments and tooltips**: **C** and **T**, as in the graphs (resize by the edges, double-click to rename, settings in Properties); they are not part of the tree and change nothing in the AI.
- **Editing**: right-click to add a node (search), or on a node for its menu (add a decorator or a service, a comment or a tooltip, detach, duplicate, delete). **Drag the bottom pin** of a composite onto a node to make it a child, or into empty space to add one; **Alt+click** on a pin detaches. **Arrange** lays the tree out without changing its order. The selected node, decorator or service and the blackboard are edited in **Properties** (double-click a node to show it); Ctrl+Z undoes everything.
- **Public goals**: tick **Public goal** on a tree to reuse it in other trees with the **Run a goal** task (like Unreal's Run Behavior); it shares the blackboard of the mob. A goal that is not public, a tree that runs itself or a loop of goals is reported.
- **Diagnostics**: empty composites, missing or wrongly typed keys, invalid values and nodes not connected to the root are reported in Properties and in Diagnostics; clicking a problem shows the node.
- **Java**: the mod receives a small engine (`ai/Puppeteer.java`) and one class per tree (`ai/MyTree.java`, method `build()`); the mob registers `Puppeteer.TreeGoal`, which ticks its own copy of the tree. Checked with Forge 1.20.1, Forge 1.21.1 and NeoForge 1.21.1.

### Work area, resources and project

- **Asset browser**: **double-click the preview** of any texture slot, or **Browse...** next to a 3D model, to see the textures or the 3D models as a grid of tiles, with a search (name or folder), the size of the tiles, what uses each one, and **Import...**; double-click a tile to choose it. **Minecraft** shows the textures and models of the game itself (read from the launcher or from the MDKs): the one chosen is copied into the project, folder *Minecraft*.
- **Blocks** (like the block editor of MCreator): besides the shape, textures, hardness, sounds and drops, the tabs **Advanced** (rotation toward the player, on walls and ceilings, water, gravity, glow, connected sides, replaceable, no collision, light opacity, random offset, map color, note block instrument, pistons, ladder, plants, enchanting bonus, beacon beam color), **Redstone and fire**, **Shape** (boxes in pixels, subtracted boxes), **States** (properties of the block state: true/false, whole numbers, choices, read by the graphs) and **Ore** (generation in the world: replaced blocks, biomes, groups per chunk, size, heights); an item texture and a particle texture.
- **Tabs**: right-click a tab of the work area to close it, the others, those on the right or all of them, save, copy the path, show the file in Windows, open it with another program, select the element in the Dashboard, split the work area into tab groups or float the tab. Element tabs show the settings of the element, centered; its name and identifier are edited in Properties.
- **Resources tab**: the asset files only (textures, sounds, 3D models), with import buttons.

### 3D models (Blockbench)

- **Import**: *Assets > Add > Import > 3D models...*, the Resources tab, or **Import a model...** next to the 3D model of an item, a block or a mob. A Blockbench project (`.bbmodel`) or a Minecraft Java JSON model opens the import window; GeckoLib (.geo.json), OBJ and glTF files are kept in the project as sources.
- **Binding textures** in the import window: each texture of the model keeps its image (embedded in the Blockbench file) or takes a texture of the project or a PNG file. **Variants** are other textures for the whole model: each mob takes one at random and keeps it (a cat with five coats: *Add the other images of the file* or *Add PNG files...*). **Layers** are drawn over the model with the same UVs (a collar, a saddle); an **emissive** layer glows in the dark, like the eyes of a spider. The window can also create the item, block or mob that uses the model. Everything can be changed later in the tab of the model.
- **Item**: *3D model* in the Visual tab. **Block**: shape *Imported 3D model*. The model is written into the mod as `<mod>:custom/<id>` (a Blockbench model is converted to a Java model; models made for an entity are centered in the block) with its textures in the block atlas.
- **Mob**: model *3D model (Blockbench)*. The model becomes a Java entity model (one part per group, box UV, Forge/NeoForge 1.20.1 and 1.21.1) with a renderer for its texture, variants and layers. Bones named *head* follow the look of the mob; *leg* and *arm* bones swing when it walks.
- What a Java model cannot keep is reported: turned groups (block and item models only turn cubes, by steps of 22.5 degrees), cubes beyond -16 to 32, elements that are not cubes.
- **Mod icon**: **Workspace > Project Settings > Icon** chooses a PNG, copied into the project as `icon.png` and written as `logoFile` in the mod metadata (in-game mod list).
- **Languages**: the texts of Mine Engine are written in English; French is a translation (`Localization/fr.json`). Language files made for older versions (keyed by French texts) are converted when imported.

### Java code

Like in MCreator, a project is a regular Gradle project: Java code generated by Mine Engine and Java code written by hand live side by side in `src/main/java` and are compiled together.

- **Workspace > New Java class** (or the `{}` button of the Explorer): event listener (`@SubscribeEvent` methods called by the game, with the right annotation for Forge or NeoForge), utility class or empty class. The class is created in the package of the selected folder, or in `<mod package>.custom`.
- **Generated code** is read-only: it is rewritten at every build. **Lock to edit** (banner of the tab) keeps Mine Engine from rewriting the file, so it can be edited by hand (also works for `build.gradle`); **Unlock** gives it back to Mine Engine. Locking is undoable and saved in the project file.
- Mine Engine deletes only the generated files it no longer produces (list in `.mineengine/generated.json`); files written by hand are never touched.
- **Editor**: foldable blocks, automatic indentation, closing of brackets and quotes, completion (Ctrl+Space or while typing: keywords, common Minecraft and loader classes, generated mod classes, words of the file; accepting a class adds its `import`), **Format** (Ctrl+Shift+F), search and replace (Ctrl+F), errors of the last build underlined in the code with a dot in the margin.

### Settings
**File > Settings...** or **Tools > Settings** (Ctrl+,):

- **General**: reopen the last project at startup, automatic backup interval, number of backup copies, and a button to restore the defaults.
- **Appearance**: the theme (same as Windows, dark, light, midnight blue, crimson red, forest green, gold, purple, high contrast, or an imported theme), the accent color and the interface size (80 % to 150 %). The theme and the accent are previewed immediately; Cancel restores the previous ones. **Import...** adds a theme file, **Export...** writes the current theme with all its colors, ready to be edited and imported again.
- **Language**: English (default), French, or an imported language. **Export a template...** writes every interface text with its English version, to translate and import. Texts an imported language does not translate stay in English. A new language is used after a restart, which Mine Engine offers to do right away.
- **Editors**: code editor text size and word wrap, graph grid display and snapping of moved nodes.
- **Debug**: the debug mode (see below).

### Debug mode and crash reports

- **Debug mode** (*Settings > Debug*): the Output and the log file also show the detailed messages (duration of each step of the validation, of the generation of each file group and of the build), Gradle gives the full stack of its errors (`--stacktrace`), the status bar shows the memory used, and a **Debug** menu appears: write the state of the engine in the Output (project, caches of the validation, undo history, memory), copy the system information, collect memory, open the logs or the crash reports folder, write a test crash report.
- **Crash reports**, whatever the mode: every unexpected error of Mine Engine (interface, background task, generation or build of the engine) writes a report in `%LOCALAPPDATA%\MineEngine\crashes` with the version, the system, the open project, the last 200 messages of the log and the error. A session that did not close normally (killed process, power cut) is reported at the next start with the end of its log. The 30 newest reports are kept. **Mine Engine > Crash reports** opens the folder: join a report to a bug report on GitHub.

Settings are saved in `%LOCALAPPDATA%\MineEngine\settings.json`; imported themes and languages are copied to `themes\` and `languages\` in the same folder.

Theme file (`.json`): `{ "formatVersion": 1, "name": "My theme", "base": "dark", "colors": { "AccentBrush": "#2F7FD8", ... } }`. `base` is `dark` or `light`; missing colors come from that base, unknown keys are ignored.

Language file (`.json`): `{ "formatVersion": 1, "code": "de", "name": "Deutsch", "culture": "de-DE", "translations": { "New block": "Neuer Block", ... } }`. Keys are the English source texts (older files keyed by the French texts are still read); a translation that is empty or does not keep the same `{0}`, `{1}`... values is skipped.

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
  elements/            one .asset.json file per element, organized by type (items/, blocks/, mobs/, recipes/, textures/, sounds/, graphs/, ai/)
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

## License

Mine Engine is released under the [MIT License](LICENSE).

> Translated with DeepL.com