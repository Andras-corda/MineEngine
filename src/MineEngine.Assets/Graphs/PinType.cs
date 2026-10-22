namespace MineEngine.Assets.Graphs;

/// <summary>
/// Famille d'une entrée ou d'une sortie de nœud. Exec porte l'ordre d'exécution (le fil
/// blanc) ; les autres portent une valeur. Object et Enum désignent un type défini dans
/// le projet (classe, structure, interface ou énumération) : voir <see cref="DataType"/>.
/// </summary>
public enum PinType
{
    Exec,
    Boolean,
    Number,
    Text,
    Player,
    Position,
    Item,
    Block,
    Sound,

    /// <summary>A stack of items (an item, a count, damage, enchantments...), like the one held in hand.</summary>
    ItemStack,

    /// <summary>Instance d'une classe, d'une structure ou d'une interface du projet.</summary>
    Object,

    /// <summary>Valeur d'une énumération du projet.</summary>
    Enum,

    // ----- V0.6.7: types of the global variables (and of the graphs) -----

    /// <summary>Number with decimals (Java double).</summary>
    Decimal,

    /// <summary>One of the six directions: up, down, north, south, east, west.</summary>
    Direction,

    /// <summary>A block with its properties (facing, lit...), as placed in the world.</summary>
    BlockState,

    /// <summary>Any entity: a player, a mob, a dropped item...</summary>
    Entity,

    /// <summary>Two whole numbers (X, Y).</summary>
    Vector2i,

    /// <summary>Three whole numbers (X, Y, Z); a position is one.</summary>
    Vector3i,

    /// <summary>Two decimal numbers (X, Y).</summary>
    Vector2f,

    /// <summary>Three decimal numbers (X, Y, Z), like a precise point of the world.</summary>
    Vector3f,

    // ----- Collections (their element type, and the key type of a dictionary) -----

    /// <summary>Values in order, with a size fixed when it is filled (get and set by index).</summary>
    Array,

    /// <summary>Values in order, that grow and shrink.</summary>
    List,

    /// <summary>Values without order and without duplicates.</summary>
    Set,

    /// <summary>Values found by their key.</summary>
    Map,

    // ----- More Java and Minecraft types (TypesVar.md) -----

    /// <summary>One character (Java char): 'a', '7', '#'...</summary>
    Char,

    /// <summary>Whole number on 64 bits (Java long), like the time of the world in ticks.</summary>
    Long,

    /// <summary>Number with decimals, less precise (Java float), often used for the rendering.</summary>
    Float,

    /// <summary>Whole number from -128 to 127 (Java byte).</summary>
    Byte,

    /// <summary>Unique identifier (Java UUID) of a player or an entity.</summary>
    Uuid,

    /// <summary>A living entity: a player, a mob, an animal (it has health and effects).</summary>
    LivingEntity,

    /// <summary>A Minecraft text with its style (colors, bold...): Component.</summary>
    TextComponent,

    /// <summary>A namespaced identifier ("minecraft:stone"): ResourceLocation.</summary>
    Identifier,

    /// <summary>An equipment slot: main hand, off hand, head, chest, legs, feet.</summary>
    EquipmentSlot,

    /// <summary>The main hand or the off hand: InteractionHand.</summary>
    Hand,

    /// <summary>Rarity of an item: common, uncommon, rare, epic.</summary>
    Rarity,

    /// <summary>A kind of fluid (water, lava, a fluid of the mod).</summary>
    Fluid,

    /// <summary>A box of the world, between two corners (Java AABB), as used for collisions.</summary>
    BoundingBox,

    /// <summary>Where some damage comes from (fall, fire, an entity...).</summary>
    DamageSource,

    /// <summary>A status effect with its duration and its level (MobEffectInstance).</summary>
    EffectInstance,

    /// <summary>NBT data (CompoundTag): named values saved with an item, a block or an entity.</summary>
    NbtTag,

    /// <summary>A world: the overworld, the Nether, the End or a dimension of the mod (Level).</summary>
    World,
}

/// <summary>
/// Type d'une valeur : un type intégré (nombre, texte, joueur...) ou un type défini dans le
/// projet (<see cref="Definition"/> est alors l'asset qui le définit). Immuable, comparable.
/// </summary>
public sealed record DataType(PinType Kind, Guid? Definition = null)
{
    /// <summary>Type of the values of a collection, or null.</summary>
    public DataType? Element { get; init; }

    /// <summary>Type of the keys of a dictionary, or null.</summary>
    public DataType? Key { get; init; }

    public static readonly DataType Exec = new(PinType.Exec);
    public static readonly DataType Boolean = new(PinType.Boolean);
    public static readonly DataType Number = new(PinType.Number);
    public static readonly DataType Text = new(PinType.Text);
    public static readonly DataType Player = new(PinType.Player);
    public static readonly DataType Position = new(PinType.Position);
    public static readonly DataType Item = new(PinType.Item);
    public static readonly DataType Block = new(PinType.Block);
    public static readonly DataType Sound = new(PinType.Sound);
    public static readonly DataType ItemStack = new(PinType.ItemStack);
    public static readonly DataType Decimal = new(PinType.Decimal);
    public static readonly DataType Direction = new(PinType.Direction);
    public static readonly DataType BlockState = new(PinType.BlockState);
    public static readonly DataType Entity = new(PinType.Entity);
    public static readonly DataType Vector2i = new(PinType.Vector2i);
    public static readonly DataType Vector3i = new(PinType.Vector3i);
    public static readonly DataType Vector2f = new(PinType.Vector2f);
    public static readonly DataType Vector3f = new(PinType.Vector3f);
    public static readonly DataType Char = new(PinType.Char);
    public static readonly DataType Long = new(PinType.Long);
    public static readonly DataType Float = new(PinType.Float);
    public static readonly DataType Byte = new(PinType.Byte);
    public static readonly DataType Uuid = new(PinType.Uuid);
    public static readonly DataType LivingEntity = new(PinType.LivingEntity);
    public static readonly DataType TextComponent = new(PinType.TextComponent);
    public static readonly DataType Identifier = new(PinType.Identifier);
    public static readonly DataType EquipmentSlot = new(PinType.EquipmentSlot);
    public static readonly DataType Hand = new(PinType.Hand);
    public static readonly DataType Rarity = new(PinType.Rarity);
    public static readonly DataType Fluid = new(PinType.Fluid);
    public static readonly DataType BoundingBox = new(PinType.BoundingBox);
    public static readonly DataType DamageSource = new(PinType.DamageSource);
    public static readonly DataType EffectInstance = new(PinType.EffectInstance);
    public static readonly DataType NbtTag = new(PinType.NbtTag);
    public static readonly DataType World = new(PinType.World);

    /// <summary>Types intégrés proposés pour une variable ou un paramètre (sans l'exécution).</summary>
    public static IReadOnlyList<DataType> BuiltIn { get; } =
    [
        Boolean, Number, Decimal, Float, Long, Byte, Char, Text, TextComponent, Player, LivingEntity, Entity, Uuid,
        Position, Direction, Vector2i, Vector3i, Vector2f, Vector3f, BoundingBox,
        Item, ItemStack, Rarity, EquipmentSlot, Hand, Block, BlockState, Fluid, Sound, Identifier,
        EffectInstance, DamageSource, NbtTag, World,
    ];

    /// <summary>Values offered for a type chosen in a list (equipment slot, hand, rarity), or null.</summary>
    public static IReadOnlyList<string>? NamedValues(PinType kind) => kind switch
    {
        PinType.EquipmentSlot => ["mainhand", "offhand", "head", "chest", "legs", "feet"],
        PinType.Hand => ["main_hand", "off_hand"],
        PinType.Rarity => ["common", "uncommon", "rare", "epic"],
        _ => null,
    };

    /// <summary>Numbers (whole or decimal) and characters, between which the casts convert.</summary>
    public bool IsNumeric => Kind is PinType.Number or PinType.Decimal or PinType.Float or PinType.Long or PinType.Byte;

    /// <summary>Collection kinds, in the order they are offered.</summary>
    public static IReadOnlyList<PinType> CollectionKinds { get; } = [PinType.Array, PinType.List, PinType.Set, PinType.Map];

    public static DataType Collection(PinType kind, DataType element, DataType? key = null)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!CollectionKinds.Contains(kind))
        {
            throw new ArgumentException(Tr.T("Not a collection."), nameof(kind));
        }

        return new DataType(kind) { Element = element, Key = kind == PinType.Map ? key ?? Text : null };
    }

    public static DataType ListOf(DataType element) => Collection(PinType.List, element);

    public bool IsCollection => Kind is PinType.Array or PinType.List or PinType.Set or PinType.Map;

    /// <summary>Every type this one is made of (itself, its key and its element, at any depth).</summary>
    public IEnumerable<DataType> Parts()
    {
        yield return this;
        foreach (DataType part in new[] { Key, Element }.OfType<DataType>().SelectMany(p => p.Parts()))
        {
            yield return part;
        }
    }

    /// <summary>Type défini dans le projet : classe, structure ou interface (Object), ou énumération (Enum).</summary>
    public static DataType UserType(Guid definition, bool isEnum) => new(isEnum ? PinType.Enum : PinType.Object, definition);

    public static implicit operator DataType(PinType kind) => kind is PinType.Object or PinType.Enum
        ? throw new ArgumentException(Tr.T("A project type must point to its definition."), nameof(kind))
        : new DataType(kind);

    public bool IsExec => Kind == PinType.Exec;

    public bool IsUserType => Definition is not null;

    /// <summary>Valeur qui se règle directement sur le nœud quand l'entrée n'est pas reliée.</summary>
    public bool HasInlineValue => Kind is PinType.Boolean or PinType.Number or PinType.Text or PinType.Item or PinType.Block
        or PinType.Sound or PinType.Enum or PinType.Decimal or PinType.Direction
        or PinType.Char or PinType.Long or PinType.Float or PinType.Byte or PinType.Identifier or PinType.Fluid
        or PinType.EquipmentSlot or PinType.Hand or PinType.Rarity;

    /// <summary>Type qui désigne un élément du jeu ou du projet (item, bloc, son).</summary>
    public bool IsContentReference => Kind is PinType.Item or PinType.Block or PinType.Sound;

    /// <summary>Libellé d'un type intégré ; un type du projet est nommé par <see cref="IGraphContext.TypeLabel"/>.</summary>
    public string BuiltInLabel => Kind switch
    {
        PinType.Exec => Tr.T("Execution"),
        PinType.Boolean => Tr.T("Boolean"),
        PinType.Number => Tr.T("Number"),
        PinType.Text => Tr.T("Text"),
        PinType.Player => Tr.T("Player"),
        PinType.Position => Tr.T("Position"),
        PinType.Item => Tr.T("Item"),
        PinType.Block => Tr.T("Block"),
        PinType.Sound => Tr.T("Sound"),
        PinType.ItemStack => Tr.T("Item stack"),
        PinType.Object => Tr.T("Object"),
        PinType.Decimal => Tr.T("Decimal number"),
        PinType.Direction => Tr.T("Direction"),
        PinType.BlockState => Tr.T("Block state"),
        PinType.Entity => Tr.T("Entity"),
        PinType.Vector2i => Tr.T("2D vector (whole)"),
        PinType.Vector3i => Tr.T("3D vector (whole)"),
        PinType.Vector2f => Tr.T("2D vector (decimal)"),
        PinType.Vector3f => Tr.T("3D vector (decimal)"),
        PinType.Char => Tr.T("Character"),
        PinType.Long => Tr.T("Long number"),
        PinType.Float => Tr.T("Float number"),
        PinType.Byte => Tr.T("Byte"),
        PinType.Uuid => Tr.T("UUID"),
        PinType.LivingEntity => Tr.T("Living entity"),
        PinType.TextComponent => Tr.T("Rich text"),
        PinType.Identifier => Tr.T("Identifier"),
        PinType.EquipmentSlot => Tr.T("Equipment slot"),
        PinType.Hand => Tr.T("Hand"),
        PinType.Rarity => Tr.T("Rarity"),
        PinType.Fluid => Tr.T("Fluid"),
        PinType.BoundingBox => Tr.T("Box (AABB)"),
        PinType.DamageSource => Tr.T("Damage source"),
        PinType.EffectInstance => Tr.T("Effect instance"),
        PinType.NbtTag => Tr.T("NBT data"),
        PinType.World => Tr.T("World"),
        PinType.Array => Tr.F("Array of {0}", Element?.BuiltInLabel ?? "?"),
        PinType.List => Tr.F("List of {0}", Element?.BuiltInLabel ?? "?"),
        PinType.Set => Tr.F("Set of {0}", Element?.BuiltInLabel ?? "?"),
        PinType.Map => Tr.F("Dictionary {0} to {1}", Key?.BuiltInLabel ?? "?", Element?.BuiltInLabel ?? "?"),
        _ => Tr.T("Enumeration"),
    };

    /// <summary>Name of a kind of collection ("List").</summary>
    public static string CollectionLabel(PinType kind) => kind switch
    {
        PinType.Array => Tr.T("Array"),
        PinType.List => Tr.T("List"),
        PinType.Set => Tr.T("Set"),
        PinType.Map => Tr.T("Dictionary"),
        _ => Tr.T("Single value"),
    };

    /// <summary>Texte enregistré dans les fichiers : "number", "object:guid", "enum:guid", "list<number>", "map<text,number>".</summary>
    public string ToStorageText() => this switch
    {
        { Kind: PinType.Map, Key: { } key, Element: { } value } => $"map<{key.ToStorageText()},{value.ToStorageText()}>",
        { IsCollection: true, Element: { } element } => $"{Kind.ToString().ToLowerInvariant()}<{element.ToStorageText()}>",
        { Definition: { } id } => $"{Kind.ToString().ToLowerInvariant()}:{id:D}",
        _ => Kind.ToString().ToLowerInvariant(),
    };

    public static DataType? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        int open = text.IndexOf('<', StringComparison.Ordinal);
        if (open > 0 && text.EndsWith('>'))
        {
            if (!Enum.TryParse(text[..open], ignoreCase: true, out PinType collection) || !CollectionKinds.Contains(collection))
            {
                return null;
            }

            IReadOnlyList<string> arguments = SplitArguments(text[(open + 1)..^1]);
            DataType?[] types = [.. arguments.Select(Parse)];
            return (collection, types) switch
            {
                (PinType.Map, [{ } key, { } value]) when !key.IsCollection => Collection(PinType.Map, value, key),
                (not PinType.Map, [{ } element]) => Collection(collection, element),
                _ => null,
            };
        }

        string[] parts = text.Split(':', 2);
        if (!Enum.TryParse(parts[0], ignoreCase: true, out PinType kind) || !Enum.IsDefined(kind) || kind == PinType.Exec
            || CollectionKinds.Contains(kind))
        {
            return null;
        }

        if (kind is PinType.Object or PinType.Enum)
        {
            return parts.Length == 2 && Guid.TryParse(parts[1], out Guid id) ? new DataType(kind, id) : null;
        }

        return parts.Length == 1 ? new DataType(kind) : null;
    }

    public override string ToString() => ToStorageText();

    /// <summary>Arguments of "map&lt;a,b&gt;" separated at depth 0 ("list&lt;map&lt;a,b&gt;&gt;" stays whole).</summary>
    private static IReadOnlyList<string> SplitArguments(string text)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            depth += text[i] switch { '<' => 1, '>' => -1, _ => 0 };
            if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }
}

public static class PinTypeExtensions
{
    public static string Label(this PinType type) => new DataType(type, type is PinType.Object or PinType.Enum ? Guid.Empty : null).BuiltInLabel;
}
