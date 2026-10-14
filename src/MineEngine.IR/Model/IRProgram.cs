using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;

namespace MineEngine.IR.Model;

/// <summary>
/// Définition de programme du mod (fonction, classe, structure, interface, énumération),
/// indépendante du loader. <see cref="OwnerId"/> désigne la classe qui la contient.
/// </summary>
public abstract class IRDefinition
{
    protected IRDefinition(Guid id, ResourceId resourceId, string name, AccessLevel access, Guid? ownerId)
    {
        Id = id;
        ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        Name = string.IsNullOrWhiteSpace(name) ? resourceId.Value : name;
        Access = access;
        OwnerId = ownerId;
    }

    /// <summary>Asset qui définit l'élément (pour relier les erreurs Java au graphe).</summary>
    public Guid Id { get; }

    public ResourceId ResourceId { get; }

    /// <summary>Nom donné par l'utilisateur, d'où vient le nom Java.</summary>
    public string Name { get; }

    public AccessLevel Access { get; }

    public Guid? OwnerId { get; }
}

/// <summary>Corps d'une fonction, d'une procédure ou d'un constructeur.</summary>
public sealed record IRBody(IReadOnlyList<IRLocal> Locals, IReadOnlyList<IRStatement> Statements)
{
    public static IRBody Empty { get; } = new([], []);
}

/// <summary>Fonction (valeurs renvoyées) ou procédure ; méthode quand elle appartient à une classe.</summary>
public sealed class IRFunction : IRDefinition
{
    public IRFunction(Guid id, ResourceId resourceId, string name, AccessLevel access, Guid? ownerId, IRBody body)
        : base(id, resourceId, name, access, ownerId)
    {
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public bool IsProcedure { get; init; }

    /// <summary>Méthode statique d'une classe (sans "Moi").</summary>
    public bool IsStatic { get; init; }

    public bool IsPure { get; init; }

    public IReadOnlyList<VariableDefinition> Parameters { get; init; } = [];

    public IReadOnlyList<VariableDefinition> Returns { get; init; } = [];

    /// <summary>Variables locales (réinitialisées à chaque appel).</summary>
    public IReadOnlyList<IRVariable> Locals { get; init; } = [];

    /// <summary>Variables globales (conservées d'un appel à l'autre).</summary>
    public IReadOnlyList<IRVariable> Globals { get; init; } = [];

    public IRBody Body { get; }

    /// <summary>Méthode d'une classe sur un objet.</summary>
    public bool IsInstanceMethod => OwnerId is not null && !IsStatic;
}

/// <summary>Classe : champs, constructeur, classe parente, interfaces implémentées.</summary>
public sealed class IRClass : IRDefinition
{
    public IRClass(Guid id, ResourceId resourceId, string name, AccessLevel access, Guid? ownerId, IRBody constructor)
        : base(id, resourceId, name, access, ownerId)
    {
        Constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
    }

    public Guid? BaseClassId { get; init; }

    public IReadOnlyList<Guid> InterfaceIds { get; init; } = [];

    public IReadOnlyList<IRVariable> Fields { get; init; } = [];

    public IReadOnlyList<VariableDefinition> ConstructorParameters { get; init; } = [];

    /// <summary>Variables locales du constructeur.</summary>
    public IReadOnlyList<IRVariable> ConstructorLocals { get; init; } = [];

    /// <summary>Corps du constructeur ; s'il commence par <see cref="IRSuperStatement"/>, c'est l'appel du constructeur parent.</summary>
    public IRBody Constructor { get; }
}

/// <summary>Structure : des champs, créée avec la valeur de chacun.</summary>
public sealed class IRStruct : IRDefinition
{
    public IRStruct(Guid id, ResourceId resourceId, string name, AccessLevel access, Guid? ownerId, IReadOnlyList<IRVariable> fields)
        : base(id, resourceId, name, access, ownerId)
    {
        Fields = fields ?? [];
    }

    public IReadOnlyList<IRVariable> Fields { get; }
}

/// <summary>Interface : des méthodes à fournir.</summary>
public sealed class IRInterface : IRDefinition
{
    public IRInterface(Guid id, ResourceId resourceId, string name, AccessLevel access, Guid? ownerId, IReadOnlyList<MethodSignature> methods)
        : base(id, resourceId, name, access, ownerId)
    {
        Methods = methods ?? [];
    }

    public IReadOnlyList<MethodSignature> Methods { get; }
}

/// <summary>Énumération : une liste de valeurs nommées.</summary>
public sealed class IREnum : IRDefinition
{
    public IREnum(Guid id, ResourceId resourceId, string name, AccessLevel access, Guid? ownerId, IReadOnlyList<EnumConstant> constants)
        : base(id, resourceId, name, access, ownerId)
    {
        Constants = constants ?? [];
    }

    public IReadOnlyList<EnumConstant> Constants { get; }
}
