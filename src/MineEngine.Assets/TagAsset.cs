using System.Text.RegularExpressions;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>What a tag groups (tags.md): the registry its entries come from.</summary>
public enum TagRegistry
{
    Blocks,
    Items,
    Functions,
    PaintingVariants,
    DamageTypes,
    Enchantments,
    EntityTypes,
    Biomes,
    GameEvents,
    Structures,
    CatVariants,
    BannerPatterns,
    WorldPresets,
    Potions,
    Dialogs,
    Fluids,
}

/// <summary>
/// Tag of the Tags tab (V0.7.1, like the Elements Tag of MCreator): a name for a group of blocks,
/// items, entities, biomes... that recipes, graphs and other tags use instead of each element. A tag
/// of another namespace (minecraft:logs, c:ores) adds the entries to that tag.
/// </summary>
public sealed class TagAsset : Asset
{
    /// <summary>Namespace that stands for the mod itself (its id is used when the mod is generated).</summary>
    public const string ModNamespace = "";

    private TagRegistry _registry = TagRegistry.Items;
    private string _namespace = ModNamespace;
    private string _path = string.Empty;
    private bool _replace;
    private IReadOnlyList<string> _values = [];

    public TagAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
        _path = resourceId.Value;
    }

    public override AssetType Type => AssetType.Tag;

    public TagRegistry Registry
    {
        get => _registry;
        set => SetField(ref _registry, value);
    }

    /// <summary>"minecraft", "forge", "neoforge", "c"... or <see cref="ModNamespace"/> for the mod.</summary>
    public string Namespace
    {
        get => _namespace;
        set => SetField(ref _namespace, (value ?? string.Empty).Trim().ToLowerInvariant());
    }

    /// <summary>Name of the tag in its namespace ("ores/ruby").</summary>
    public string TagPath
    {
        get => _path;
        set => SetField(ref _path, (value ?? string.Empty).Trim().ToLowerInvariant());
    }

    /// <summary>The tag replaces the entries given by the other mods and by Minecraft instead of adding to them.</summary>
    public bool Replace
    {
        get => _replace;
        set => SetField(ref _replace, value);
    }

    /// <summary>
    /// Entries: an element ("minecraft:stone", "ruby" for the mod), another tag ("#minecraft:logs"),
    /// or an optional entry of another mod ("othermod:thing?").
    /// </summary>
    public IReadOnlyList<string> Values
    {
        get => _values;
        set => SetField(ref _values, value ?? []);
    }

    /// <summary>"#minecraft:logs" for a tag of Minecraft, "#mymod:ores/ruby" for a tag of the mod.</summary>
    public string Reference(string modId) => $"#{(Namespace.Length == 0 ? modId : Namespace)}:{TagPath}";

    /// <summary>Name of the registry, as in tags.md ("blocks", "entity_type").</summary>
    public static string RegistryLabel(TagRegistry registry) => registry switch
    {
        TagRegistry.Blocks => "blocks",
        TagRegistry.Items => "items",
        TagRegistry.Functions => "functions",
        TagRegistry.PaintingVariants => "painting_variants",
        TagRegistry.DamageTypes => "damage_types",
        TagRegistry.Enchantments => "enchantments",
        TagRegistry.EntityTypes => "entity_type",
        TagRegistry.Biomes => "biome",
        TagRegistry.GameEvents => "game_event",
        TagRegistry.Structures => "structure",
        TagRegistry.CatVariants => "entities_variant",
        TagRegistry.BannerPatterns => "banner_pattern",
        TagRegistry.WorldPresets => "world_preset",
        TagRegistry.Potions => "potion",
        TagRegistry.Dialogs => "dialog",
        _ => "fluid",
    };

    /// <summary>What the registry holds (tooltip).</summary>
    public static string RegistryDescription(TagRegistry registry) => registry switch
    {
        TagRegistry.Blocks => Tr.T("Blocks"),
        TagRegistry.Items => Tr.T("Items"),
        TagRegistry.Functions => Tr.T("Minecraft functions (.mcfunction)"),
        TagRegistry.PaintingVariants => Tr.T("Variants of paintings"),
        TagRegistry.DamageTypes => Tr.T("Types of damage"),
        TagRegistry.Enchantments => Tr.T("Enchantments"),
        TagRegistry.EntityTypes => Tr.T("Entities (types of entities)"),
        TagRegistry.Biomes => Tr.T("Biomes"),
        TagRegistry.GameEvents => Tr.T("Events of the game (vibrations)"),
        TagRegistry.Structures => Tr.T("Structures of the world generation"),
        TagRegistry.CatVariants => Tr.T("Variants of entities (cats)"),
        TagRegistry.BannerPatterns => Tr.T("Patterns of banners"),
        TagRegistry.WorldPresets => Tr.T("Presets of worlds"),
        TagRegistry.Potions => Tr.T("Potions (depending on the version)"),
        TagRegistry.Dialogs => Tr.T("Dialogs (Minecraft 1.21.6 and later)"),
        _ => Tr.T("Fluids"),
    };

    /// <summary>
    /// Folder of the tags of the registry under data/&lt;namespace&gt;/tags; Minecraft 1.21 uses
    /// singular names for the folders that were plural ("block" instead of "blocks").
    /// </summary>
    public static string Folder(TagRegistry registry, bool singular) => registry switch
    {
        TagRegistry.Blocks => singular ? "block" : "blocks",
        TagRegistry.Items => singular ? "item" : "items",
        TagRegistry.Functions => singular ? "function" : "functions",
        TagRegistry.EntityTypes => singular ? "entity_type" : "entity_types",
        TagRegistry.GameEvents => singular ? "game_event" : "game_events",
        TagRegistry.Fluids => singular ? "fluid" : "fluids",
        TagRegistry.PaintingVariants => "painting_variant",
        TagRegistry.DamageTypes => "damage_type",
        TagRegistry.Enchantments => "enchantment",
        TagRegistry.Biomes => "worldgen/biome",
        TagRegistry.Structures => "worldgen/structure",
        TagRegistry.CatVariants => "cat_variant",
        TagRegistry.BannerPatterns => "banner_pattern",
        TagRegistry.WorldPresets => "worldgen/world_preset",
        TagRegistry.Potions => "potion",
        _ => "dialog",
    };

    /// <summary>Namespaces offered in the tab (empty: the mod).</summary>
    public static IReadOnlyList<string> CommonNamespaces { get; } = [ModNamespace, "minecraft", "c", "forge", "neoforge", "fabric"];

    private static readonly Regex PathPattern = new("^[a-z0-9_.-]+(/[a-z0-9_.-]+)*$", RegexOptions.Compiled);

    private static readonly Regex EntryPattern = new(@"^#?([a-z0-9_.-]+:)?[a-z0-9_./-]+\??$", RegexOptions.Compiled);

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        if (!PathPattern.IsMatch(TagPath))
        {
            diagnostics.Error(Tr.T("Name of the tag: lowercase letters, digits, _ - . and / between the folders (ores/ruby)."), source, Id);
        }

        if (Namespace.Length > 0 && !Regex.IsMatch(Namespace, "^[a-z0-9_.-]+$"))
        {
            diagnostics.Error(Tr.T("Namespace of the tag: lowercase letters, digits, _ - and ."), source, Id);
        }

        if (Values.Count == 0)
        {
            diagnostics.Warning(Tr.T("The tag is empty."), source, Id);
        }

        foreach (string value in Values.Where(v => !EntryPattern.IsMatch(v)))
        {
            diagnostics.Error(Tr.F("Entry \"{0}\": an identifier (minecraft:stone), a tag (#minecraft:logs), or an optional entry (othermod:thing?).", value), source, Id);
        }

        if (Registry == TagRegistry.Dialogs)
        {
            diagnostics.Info(Tr.T("Dialogs only exist from Minecraft 1.21.6: the tag is ignored by older versions."), source, Id);
        }
    }
}
