using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Puppeteer;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Minecraft.Java;

namespace MineEngine.Editor.ViewModels.Creation;

/// <summary>What a choice of the creation window makes.</summary>
public enum NewElementAction
{
    /// <summary>An element of the project (<see cref="NewElementEntry.AssetType"/>).</summary>
    Asset,

    /// <summary>A graph of visual programming (<see cref="NewElementEntry.GraphKind"/>).</summary>
    Graph,

    /// <summary>A Java file written by the user (<see cref="NewElementEntry.JavaKind"/>).</summary>
    JavaClass,

    /// <summary>A folder of the explorer.</summary>
    Folder,

    /// <summary>Not available yet: shown so that the list says what is planned.</summary>
    Coming,
}

/// <summary>A starting content offered for a choice (Sparkle systems, Puppeteer trees, armor pieces).</summary>
public sealed record NewElementTemplate(string Title, string Description, Action<Asset> Apply);

/// <summary>A choice of the creation window and of the Add menu.</summary>
public sealed record NewElementEntry(string Title, string Description, string Icon, NewElementAction Action)
{
    public AssetType? AssetType { get; init; }

    public GraphKind? GraphKind { get; init; }

    public JavaClassKind? JavaKind { get; init; }

    /// <summary>Name proposed in the window.</summary>
    public string DefaultName { get; init; } = string.Empty;

    public IReadOnlyList<NewElementTemplate> Templates { get; init; } = [];

    /// <summary>Applied to the new element when there is no template (an armor is an item of kind chestplate...).</summary>
    public Action<Asset>? Prepare { get; init; }

    public bool IsAvailable => Action != NewElementAction.Coming;

    /// <summary>Title shown in the lists: "(coming later)" for the planned ones.</summary>
    public string Label => IsAvailable ? Title : Tr.F("{0} (coming later)", Title);
}

/// <summary>A group of choices: the 9 categories of the Add menu.</summary>
public sealed record NewElementCategory(string Title, string Icon, IReadOnlyList<NewElementEntry> Entries);

/// <summary>Everything that can be created in a project, by category (V0.7.14).</summary>
public static class NewElementCatalog
{
    private static readonly Lazy<IReadOnlyList<NewElementCategory>> LazyCategories = new(Build);

    public static IReadOnlyList<NewElementCategory> Categories => LazyCategories.Value;

    public static IEnumerable<NewElementEntry> Entries => Categories.SelectMany(c => c.Entries);

    public static NewElementEntry? ForAsset(AssetType type) =>
        Entries.FirstOrDefault(e => e.Action == NewElementAction.Asset && e.AssetType == type && e.Prepare is null);

    public static NewElementEntry? ForGraph(GraphKind kind) =>
        Entries.FirstOrDefault(e => e.Action == NewElementAction.Graph && e.GraphKind == kind);

    public static NewElementEntry? ForJava(JavaClassKind kind) =>
        Entries.FirstOrDefault(e => e.Action == NewElementAction.JavaClass && e.JavaKind == kind);

    public static NewElementEntry Folder => Entries.First(e => e.Action == NewElementAction.Folder);

    private static NewElementEntry Asset(AssetType type, string title, string description, string icon, string defaultName) =>
        new(title, description, icon, NewElementAction.Asset) { AssetType = type, DefaultName = defaultName };

    private static NewElementEntry Coming(string title, string description, string icon) =>
        new(title, description, icon, NewElementAction.Coming);

    private static NewElementEntry Graph(GraphKind kind, string title, string description, string icon) =>
        new(title, description, icon, NewElementAction.Graph) { GraphKind = kind, DefaultName = ProgramPalette.DefaultName(kind) };

    private static NewElementEntry Java(JavaClassKind kind, string title, string description, string defaultName) =>
        new(title, description, "Glyph.Code", NewElementAction.JavaClass) { JavaKind = kind, DefaultName = defaultName };

    private static IReadOnlyList<NewElementCategory> Build() =>
    [
        new(Tr.T("Game content"), "BlockGeometry",
        [
            Asset(AssetType.Item, Tr.T("Item"), Tr.T("Simple item, tool, weapon or food, with its texture and its behavior"), "ItemGeometry", Tr.T("New item")),
            Asset(AssetType.Block, Tr.T("Block"), Tr.T("Block with its textures, its shape and what it drops"), "BlockGeometry", Tr.T("New block")),
            Asset(AssetType.Mob, Tr.T("Mob"), Tr.T("Creature with its model, its behavior and its spawns"), "MobGeometry", Tr.T("New mob")),
            Asset(AssetType.Item, Tr.T("Armor"), Tr.T("Piece of armor: an item worn on the head, the chest, the legs or the feet"), "ItemGeometry", Tr.T("New armor")) with
            {
                Templates =
                [
                    ArmorPiece(ItemKind.Helmet, Tr.T("Helmet")),
                    ArmorPiece(ItemKind.Chestplate, Tr.T("Chestplate")),
                    ArmorPiece(ItemKind.Leggings, Tr.T("Leggings")),
                    ArmorPiece(ItemKind.Boots, Tr.T("Boots")),
                ],
                Prepare = asset => ((ItemAsset)asset).Kind = ItemKind.Chestplate,
            },
            Coming(Tr.T("Painting"), Tr.T("Painting that players can hang on walls"), "Glyph.Texture"),
        ]),
        new(Tr.T("World"), "Glyph.Explorer",
        [
            Asset(AssetType.Biome, Tr.T("Biome"), Tr.T("Biome of the overworld, with its climate and its colors"), "Glyph.Explorer", Tr.T("New biome")),
            Coming(Tr.T("Dimension"), Tr.T("World of its own, reached through a portal"), "Glyph.Explorer"),
            Coming(Tr.T("Structure"), Tr.T("Village, dungeon or ruin generated in the world"), "BlockGeometry"),
            Coming(Tr.T("Feature"), Tr.T("Tree, rock or small structure scattered in the biomes"), "Glyph.Star"),
            Asset(AssetType.Fluid, Tr.T("Fluid"), Tr.T("Fluid with its block and its bucket"), "FluidGeometry", Tr.T("New fluid")),
            Asset(AssetType.Plant, Tr.T("Plant"), Tr.T("Flower, tall plant, growing plant or crop, with its patches in the world"), "Glyph.Star", Tr.T("New plant")),
        ]),
        new(Tr.T("Gameplay"), "CommandGeometry",
        [
            Coming(Tr.T("Achievement"), Tr.T("Advancement unlocked by an action of the player"), "Glyph.Star"),
            Asset(AssetType.CreativeTab, Tr.T("Creative tab"), Tr.T("Tab of the creative inventory with the items you choose"), "Glyph.Tab", Tr.T("New tab")),
            Asset(AssetType.Command, Tr.T("Command"), Tr.T("Chat command (/heal) run by event graphs"), "CommandGeometry", Tr.T("New command")),
            Asset(AssetType.Effect, Tr.T("Status effect"), Tr.T("Effect like Speed or Poison, with its potion"), "PotionGeometry", Tr.T("New effect")),
            Asset(AssetType.Enchantment, Tr.T("Enchantment"), Tr.T("Enchantment for weapons, armor or tools"), "Glyph.Star", Tr.T("New enchantment")),
            Coming(Tr.T("Projectile"), Tr.T("Thrown or shot entity, like an arrow or a snowball"), "Glyph.Forward"),
            Asset(AssetType.Recipe, Tr.T("Recipe"), Tr.T("Crafting, smelting or other recipe"), "RecipeGeometry", Tr.T("New recipe")),
        ]),
        new(Tr.T("Game data"), "Glyph.Settings",
        [
            Asset(AssetType.GameRule, Tr.T("Game rule"), Tr.T("Rule changed with /gamerule"), "Glyph.Settings", Tr.T("New rule")),
            Asset(AssetType.Keybind, Tr.T("Keybind"), Tr.T("Key of the Controls that runs event graphs"), "Glyph.Keyboard", Tr.T("New key")),
            Asset(AssetType.LootTable, Tr.T("Drop table"), Tr.T("What a block, a mob or a chest gives: pools of items drawn at random"), "Glyph.Package", Tr.T("New drop table")),
            Asset(AssetType.DamageType, Tr.T("Damage type"), Tr.T("Kind of damage with its death message, used by the graphs and the immunities of the mobs"), "Glyph.Warning", Tr.T("New damage type")),
            Coming(Tr.T("Villager trade"), Tr.T("Trades offered by a villager profession"), "Glyph.Sync"),
            Coming(Tr.T("Villager profession"), Tr.T("Job of a villager, with its workstation"), "MobGeometry"),
        ]),
        new(Tr.T("Particles (Sparkle)"), "SparkleGeometry",
        [
            Asset(AssetType.Sparkle, Tr.T("Particle system"), Tr.T("Sparkle particle system: emitters of particles played by the graphs"), "SparkleGeometry", Tr.T("New particles")) with
            {
                Templates = [new(Tr.T("Sparks"), Tr.T("A burst of stars that rise and fall."), asset => ((SparkleAsset)asset).ApplyStarterSystem()),
                    .. SparkleTemplates.All.Select(info => new NewElementTemplate(info.Title, info.Description, asset => SparkleSystem((SparkleAsset)asset, info)))],
            },
        ]),
        new(Tr.T("AI trees (Puppeteer)"), "BehaviorTreeGeometry",
        [
            Asset(AssetType.BehaviorTree, Tr.T("Behavior tree"), Tr.T("AI of a mob as nodes: selectors, sequences, tasks, decorators and services, like Unreal's Behavior Trees"), "BehaviorTreeGeometry", Tr.T("New behavior")) with
            {
                Templates = [.. BehaviorTreeTemplates.All.Select(info => new NewElementTemplate(info.Title, info.Description,
                    asset => BehaviorTreeTemplates.Apply((BehaviorTreeAsset)asset, info.Template)))],
            },
        ]),
        new(Tr.T("Java programming"), "Glyph.Code",
        [
            Java(JavaClassKind.Empty, Tr.T("Class"), Tr.T("Empty Java class"), "MyClass"),
            Java(JavaClassKind.Utility, Tr.T("Utility class"), Tr.T("Java class of static methods"), "MyUtils"),
            Java(JavaClassKind.Enum, Tr.T("Enumeration"), Tr.T("Java list of named values"), "MyEnum"),
            Java(JavaClassKind.Record, Tr.T("Structure"), Tr.T("Java record that groups fields"), "MyRecord"),
            Java(JavaClassKind.EventListener, Tr.T("Game event listener"), Tr.T("Java class whose @SubscribeEvent methods react to the events of the game"), "MyEvents"),
        ]),
        new(Tr.T("Visual programming"), "NodeGraphGeometry",
        [
            Graph(GraphKind.Events, Tr.T("Event graph"), Tr.T("Reactions to game events (right-click, player join...)"), "NodeGraphGeometry"),
            Graph(GraphKind.Function, Tr.T("Function"), Tr.T("Sequence of instructions that returns values"), "FunctionGeometry"),
            Graph(GraphKind.Procedure, Tr.T("Procedure"), Tr.T("Sequence of instructions that returns nothing"), "ProcedureGeometry"),
            Graph(GraphKind.Library, Tr.T("Function library"), Tr.T("Functions and procedures usable in every graph of the project, grouped under the name of the library in the Nodes window"), "LibraryGeometry"),
            Graph(GraphKind.Class, Tr.T("Class"), Tr.T("Type with fields, a constructor and methods; inheritance and interfaces"), "ClassGeometry"),
            Graph(GraphKind.Enum, Tr.T("Enumeration"), Tr.T("List of named values"), "EnumGeometry"),
            Graph(GraphKind.Struct, Tr.T("Struct"), Tr.T("Type that groups fields"), "StructGeometry"),
            Graph(GraphKind.Interface, Tr.T("Interface"), Tr.T("Methods that the implementing classes must provide"), "InterfaceGeometry"),
        ]),
        new(Tr.T("Folder"), "Glyph.Folder",
        [
            new(Tr.T("New folder"), Tr.T("Folder of the explorer, to sort the elements"), "Glyph.NewFolder", NewElementAction.Folder) { DefaultName = Tr.T("New folder") },
        ]),
    ];

    private static NewElementTemplate ArmorPiece(ItemKind kind, string title) =>
        new(title, Tr.F("Worn as {0}.", title.ToLowerInvariant()), asset => ((ItemAsset)asset).Kind = kind);

    private static void SparkleSystem(SparkleAsset asset, SparkleTemplateInfo info)
    {
        int id = 0;
        asset.Emitters = [.. SparkleTemplates.Create(info.Template, info.Title, () => ++id, 0, 0)];
    }
}
