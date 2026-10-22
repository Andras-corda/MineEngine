using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Noms Java des définitions de programme du mod, rangées dans le package "code" :
/// une définition hors de toute classe devient une classe (une fonction devient une classe
/// qui porte une méthode statique du même nom), une définition intégrée à une classe en
/// devient un membre (méthode, type imbriqué "Parente.Imbriquee"). Les paramètres, variables
/// et champs sont nommés d'après leur nom saisi, sans entrer en conflit avec les mots-clés
/// ni avec "level" (le monde, premier paramètre de toute méthode générée).
/// </summary>
public sealed class JavaProgramNames
{
    /// <summary>Paramètre "monde" ajouté en tête de chaque fonction, procédure, méthode et constructeur.</summary>
    public const string LevelParameter = "level";

    private static readonly HashSet<string> ReservedLocals = new(StringComparer.Ordinal)
    {
        LevelParameter, "player", "position", "event", "result", "random",
    };

    private readonly Dictionary<Guid, IRDefinition> _definitions;
    private readonly Dictionary<Guid, string> _variables = [];

    public JavaProgramNames(JavaModNaming naming, IEnumerable<IRDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(naming);
        Package = naming.RootPackage + ".code";
        _definitions = definitions.ToDictionary(d => d.Id);
        foreach (IRDefinition definition in _definitions.Values)
        {
            foreach (VariableDefinition variable in Variables(definition))
            {
                _variables[variable.Id] = Safe(ProgramNames.Identifier(variable.Name));
            }
        }

        // Les variables globales d'une méthode sont des champs statiques de sa classe : préfixées par la méthode.
        foreach (IRFunction method in _definitions.Values.OfType<IRFunction>().Where(f => f.OwnerId is { } owner && _definitions.ContainsKey(owner)))
        {
            foreach (IRVariable global in method.Globals)
            {
                _variables[global.Id] = MethodName(method).TrimEnd('_') + ProgramNames.Identifier(global.Definition.Name, upperFirst: true);
            }
        }
    }

    public string Package { get; }

    public IEnumerable<IRDefinition> Definitions => _definitions.Values;

    /// <summary>Définitions écrites chacune dans son fichier (celles qui n'appartiennent à aucune classe).</summary>
    public IEnumerable<IRDefinition> TopLevel => _definitions.Values.Where(d => d.OwnerId is null || !_definitions.ContainsKey(d.OwnerId.Value));

    public IRDefinition? Find(Guid id) => _definitions.GetValueOrDefault(id);

    public IEnumerable<IRDefinition> Members(Guid owner) => _definitions.Values.Where(d => d.OwnerId == owner).OrderBy(d => d.ResourceId);

    /// <summary>Nom simple du type ou de la classe d'une définition ("Golem").</summary>
    public string SimpleName(IRDefinition definition) => ProgramNames.Identifier(definition.Name, upperFirst: true);

    /// <summary>Nom du type tel qu'écrit dans le code du package "code" ("Golem", "Golem.Etat").</summary>
    public string TypeName(Guid id)
    {
        if (Find(id) is not { } definition)
        {
            return "Object";
        }

        string name = SimpleName(definition);
        return definition.OwnerId is { } owner && Find(owner) is not null ? $"{TypeName(owner)}.{name}" : name;
    }

    /// <summary>Nom d'une méthode, d'une fonction ou d'une procédure ("attaquer").</summary>
    public string MethodName(IRDefinition function) => Safe(ProgramNames.Identifier(function.Name));

    /// <summary>Nom Java d'un paramètre, d'une variable ou d'un champ.</summary>
    public string Variable(Guid id) => _variables.TryGetValue(id, out string? name) ? name : "unknown" + id.ToString("N")[..6];

    /// <summary>Nom d'une méthode d'interface.</summary>
    public string InterfaceMethod(Guid interfaceId, Guid method) =>
        Find(interfaceId) is IRInterface contract && contract.Methods.FirstOrDefault(m => m.Id == method) is { } signature
            ? Safe(ProgramNames.Identifier(signature.Name))
            : "unknown";

    /// <summary>Constante d'énumération ("TRES_RARE").</summary>
    public string Constant(Guid enumId, Guid constant)
    {
        string name = Find(enumId) is IREnum type && type.Constants.FirstOrDefault(c => c.Id == constant) is { } value
            ? ProgramNames.Constant(value.Name)
            : "UNKNOWN";
        return $"{TypeName(enumId)}.{name}";
    }

    /// <summary>Expression qui appelle une fonction ou une méthode (sans les arguments) : "Soin.soin", "target.attaquer", "attaquer".</summary>
    public string CallPrefix(IRFunction function, string? target)
    {
        string method = MethodName(function);
        if (target is not null)
        {
            return $"{target}.{method}";
        }

        if (function.OwnerId is { } owner && Find(owner) is not null)
        {
            return function.IsStatic ? $"{TypeName(owner)}.{method}" : method;
        }

        return $"{TypeName(function.Id)}.{method}";
    }

    /// <summary>
    /// Record des valeurs renvoyées par une fonction qui en renvoie plusieurs ("AttaquerResult"),
    /// déclaré par la première définition de la méthode (une redéfinition renvoie le même type).
    /// </summary>
    public string ResultRecordName(IRFunction function) => ProgramNames.Identifier(function.Name, upperFirst: true) + "Result";

    /// <summary>Nom complet du record des valeurs renvoyées, vu depuis le package "code".</summary>
    public string ResultRecordType(IRFunction function)
    {
        IRFunction root = RootDeclaration(function);
        string holder = root.OwnerId is { } owner && Find(owner) is not null ? TypeName(owner) : TypeName(root.Id);
        return $"{holder}.{ResultRecordName(root)}";
    }

    /// <summary>Vrai si la fonction déclare son record (elle ne redéfinit pas une méthode d'une classe parente).</summary>
    public bool DeclaresResultRecord(IRFunction function) => ReferenceEquals(RootDeclaration(function), function);

    /// <summary>Méthode de la classe parente redéfinie par cette méthode, ou null.</summary>
    public IRFunction? Overridden(IRFunction function)
    {
        if (function.IsStatic || function.OwnerId is not { } owner || Find(owner) is not IRClass type)
        {
            return null;
        }

        string name = MethodName(function);
        var seen = new HashSet<Guid> { type.Id };
        for (IRClass? parent = type.BaseClassId is { } id ? Find(id) as IRClass : null;
             parent is not null && seen.Add(parent.Id);
             parent = parent.BaseClassId is { } next ? Find(next) as IRClass : null)
        {
            if (Members(parent.Id).OfType<IRFunction>().FirstOrDefault(m => !m.IsStatic && MethodName(m) == name) is { } overridden)
            {
                return overridden;
            }
        }

        return null;
    }

    /// <summary>Vrai si la méthode implémente une méthode d'une interface de sa classe (ou d'une parente).</summary>
    public bool ImplementsInterface(IRFunction function)
    {
        if (function.IsStatic || function.OwnerId is not { } owner || Find(owner) is not IRClass type)
        {
            return false;
        }

        string name = MethodName(function);
        var seen = new HashSet<Guid>();
        for (IRClass? current = type; current is not null && seen.Add(current.Id); current = current.BaseClassId is { } next ? Find(next) as IRClass : null)
        {
            if (current.InterfaceIds.Select(Find).OfType<IRInterface>().Any(i => i.Methods.Any(m => Safe(ProgramNames.Identifier(m.Name)) == name)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Type Java d'une valeur.</summary>
    public string JavaType(DataType type) => type.Kind switch
    {
        PinType.Boolean => "boolean",
        PinType.Number => "int",
        PinType.Decimal => "double",
        PinType.Text => "String",
        PinType.Player => "Player",
        PinType.Entity => "Entity",
        PinType.Position => "BlockPos",
        PinType.Direction => "Direction",
        PinType.Item => "Item",
        PinType.Block => "Block",
        PinType.BlockState => "BlockState",
        PinType.Sound => "SoundEvent",
        PinType.ItemStack => "ItemStack",
        PinType.Vector2i => "Vector2i",
        PinType.Vector3i => "Vec3i",
        PinType.Vector2f => "Vec2",
        PinType.Vector3f => "Vec3",
        PinType.Char => "char",
        PinType.Long => "long",
        PinType.Float => "float",
        PinType.Byte => "byte",
        PinType.Uuid => "UUID",
        PinType.LivingEntity => "LivingEntity",
        PinType.TextComponent => "Component",
        PinType.Identifier => "ResourceLocation",
        PinType.EquipmentSlot => "EquipmentSlot",
        PinType.Hand => "InteractionHand",
        PinType.Rarity => "Rarity",
        PinType.Fluid => "Fluid",
        PinType.BoundingBox => "AABB",
        PinType.DamageSource => "DamageSource",
        PinType.EffectInstance => "MobEffectInstance",
        PinType.NbtTag => "CompoundTag",
        PinType.World => "Level",
        PinType.Array or PinType.List => $"List<{BoxedType(type.Element!)}>",
        PinType.Set => $"Set<{BoxedType(type.Element!)}>",
        PinType.Map => $"Map<{BoxedType(type.Key!)}, {BoxedType(type.Element!)}>",
        PinType.Object or PinType.Enum => type.Definition is { } id ? TypeName(id) : "Object",
        _ => "Object",
    };

    /// <summary>Java type of a value inside a collection (Integer for int...).</summary>
    public string BoxedType(DataType type) => type.Kind switch
    {
        PinType.Boolean => "Boolean",
        PinType.Number => "Integer",
        PinType.Decimal => "Double",
        PinType.Char => "Character",
        PinType.Long => "Long",
        PinType.Float => "Float",
        PinType.Byte => "Byte",
        _ => JavaType(type),
    };

    /// <summary>Imports Minecraft (et java.util) nécessaires pour écrire ce type et ceux qui le composent.</summary>
    public static IEnumerable<string> ImportsFor(DataType type) => type.Parts().SelectMany(OwnImports).Distinct();

    private static IEnumerable<string> OwnImports(DataType type) => type.Kind switch
    {
        PinType.Player => ["net.minecraft.world.entity.player.Player"],
        PinType.Entity => ["net.minecraft.world.entity.Entity"],
        PinType.Position => ["net.minecraft.core.BlockPos"],
        PinType.Direction => ["net.minecraft.core.Direction"],
        PinType.Item => ["net.minecraft.world.item.Item"],
        PinType.Block => ["net.minecraft.world.level.block.Block"],
        PinType.BlockState => ["net.minecraft.world.level.block.state.BlockState"],
        PinType.Sound => ["net.minecraft.sounds.SoundEvent"],
        PinType.ItemStack => ["net.minecraft.world.item.ItemStack"],
        PinType.Vector2i => ["org.joml.Vector2i"],
        PinType.Vector3i => ["net.minecraft.core.Vec3i"],
        PinType.Vector2f => ["net.minecraft.world.phys.Vec2"],
        PinType.Vector3f => ["net.minecraft.world.phys.Vec3"],
        PinType.Uuid => ["java.util.UUID"],
        PinType.LivingEntity => ["net.minecraft.world.entity.LivingEntity"],
        PinType.TextComponent => ["net.minecraft.network.chat.Component"],
        PinType.Identifier => ["net.minecraft.resources.ResourceLocation"],
        PinType.EquipmentSlot => ["net.minecraft.world.entity.EquipmentSlot"],
        PinType.Hand => ["net.minecraft.world.InteractionHand"],
        PinType.Rarity => ["net.minecraft.world.item.Rarity"],
        PinType.Fluid => ["net.minecraft.world.level.material.Fluid"],
        PinType.BoundingBox => ["net.minecraft.world.phys.AABB"],
        PinType.DamageSource => ["net.minecraft.world.damagesource.DamageSource"],
        PinType.EffectInstance => ["net.minecraft.world.effect.MobEffectInstance"],
        PinType.NbtTag => ["net.minecraft.nbt.CompoundTag"],
        PinType.World => ["net.minecraft.world.level.Level"],
        PinType.Array or PinType.List => ["java.util.List", "java.util.ArrayList"],
        PinType.Set => ["java.util.Set", "java.util.HashSet"],
        PinType.Map => ["java.util.Map", "java.util.HashMap"],
        _ => [],
    };

    // ----- Global variables (class MineEngineGlobals of the package "code") -----

    public const string GlobalsClass = "MineEngineGlobals";

    private readonly Dictionary<Guid, (IRGlobalVariable Global, string Name)> _globals = [];

    /// <summary>Declares the global variables: each one gets a Java name unique in the class (score, score2...).</summary>
    public void AddGlobals(IEnumerable<IRGlobalVariable> globals)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (IRGlobalVariable global in globals)
        {
            string name = Safe(ProgramNames.Identifier(global.Name));
            if (name.Length == 0)
            {
                name = "variable";
            }

            string unique = name;
            for (int i = 2; !used.Add(unique); i++)
            {
                unique = name + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            _globals[global.Id] = (global, unique);
        }
    }

    public IEnumerable<(IRGlobalVariable Global, string Name)> Globals => _globals.Values;

    public (IRGlobalVariable Global, string Name)? FindGlobal(Guid id) => _globals.TryGetValue(id, out var found) ? found : null;

    /// <summary>"Score" from "score": the accessors are getScore and setScore.</summary>
    public static string Accessor(string name) => char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>Type renvoyé par une fonction : void, le type de sa seule valeur, ou son record.</summary>
    public string ReturnType(IRFunction function) => function.Returns.Count switch
    {
        0 => "void",
        1 => JavaType(function.Returns[0].Type),
        _ => ResultRecordType(function),
    };

    /// <summary>Type renvoyé par une méthode d'interface (au plus une valeur).</summary>
    public string ReturnType(MethodSignature method) => method.Returns.Count == 0 ? "void" : JavaType(method.Returns[0].Type);

    /// <summary>Liste des paramètres Java : le monde, puis ceux de la définition.</summary>
    public string ParameterList(IEnumerable<VariableDefinition> parameters, bool withLevel = true) =>
        string.Join(", ", (withLevel ? [$"Level {LevelParameter}"] : Array.Empty<string>())
            .Concat(parameters.Select(p => $"{JavaType(p.Type)} {Variable(p.Id)}")));

    public static string Modifier(AccessLevel access) => access switch
    {
        AccessLevel.Public => "public",
        AccessLevel.Protected => "protected",
        _ => "private",
    };

    private IRFunction RootDeclaration(IRFunction function)
    {
        IRFunction current = function;
        var seen = new HashSet<Guid>();
        while (seen.Add(current.Id) && Overridden(current) is { } parent)
        {
            current = parent;
        }

        return current;
    }

    private static IEnumerable<VariableDefinition> Variables(IRDefinition definition) => definition switch
    {
        IRFunction f => f.Parameters.Concat(f.Returns).Concat(f.Locals.Select(v => v.Definition)).Concat(f.Globals.Select(v => v.Definition)),
        IRClass c => c.Fields.Select(v => v.Definition).Concat(c.ConstructorParameters),
        IRStruct s => s.Fields.Select(v => v.Definition),
        IRInterface i => i.Methods.SelectMany(m => m.Parameters.Concat(m.Returns)),
        _ => [],
    };

    /// <summary>Évite les mots-clés de Java et les noms déjà pris par le code généré ("level"...).</summary>
    private static string Safe(string identifier) =>
        identifier.Length == 0 ? "unnamed" : JavaNames.IsKeyword(identifier) || ReservedLocals.Contains(identifier) ? identifier + "_" : identifier;

    /// <summary>Ajoute les variables des graphes d'événements (variables de comportement).</summary>
    public void AddBehaviorVariables(IEnumerable<IRVariable> variables)
    {
        foreach (IRVariable variable in variables)
        {
            _variables[variable.Id] = Safe(ProgramNames.Identifier(variable.Definition.Name));
        }
    }
}
