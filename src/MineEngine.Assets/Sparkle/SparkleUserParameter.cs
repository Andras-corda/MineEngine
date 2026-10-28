using System.Collections.Immutable;

namespace MineEngine.Assets.Sparkle;

/// <summary>Kind of value of a user parameter of a system.</summary>
public enum SparkleUserParameterType
{
    Number,
    Vector,
    Color,
    Boolean,
}

/// <summary>
/// User parameter of a Sparkle system (V0.7.6), like Niagara's: a named value of the system that
/// module settings are linked to (the end of a beam, a color...). A linked setting stores
/// "@" and the identifier of the parameter, so that renaming the parameter keeps its links; the
/// simulation and the code of the mod read the value of the parameter instead.
/// </summary>
public sealed record SparkleUserParameter(Guid Id, string Name, SparkleUserParameterType Type, string Value)
{
    private const char BindingMark = '@';

    /// <summary>A new parameter with the default value of its type.</summary>
    public static SparkleUserParameter Create(string name, SparkleUserParameterType type) => new(Guid.NewGuid(), name, type, DefaultValue(type));

    public static string DefaultValue(SparkleUserParameterType type) => type switch
    {
        SparkleUserParameterType.Vector => "0 0 0",
        SparkleUserParameterType.Color => "#FFFFFF",
        SparkleUserParameterType.Boolean => "false",
        _ => "0",
    };

    /// <summary>Value stored in a module setting linked to this parameter.</summary>
    public string Binding => BindingMark + Id.ToString("N");

    /// <summary>Parameter a setting value is linked to, or null for a plain value.</summary>
    public static Guid? BoundId(string? value) =>
        value is { Length: > 1 } && value[0] == BindingMark && Guid.TryParseExact(value[1..], "N", out Guid id) ? id : null;

    /// <summary>True if a module setting of this type can be linked to the parameter.</summary>
    public bool Accepts(SparkleParameterType type) => (Type, type) switch
    {
        (SparkleUserParameterType.Number, SparkleParameterType.Number or SparkleParameterType.Integer) => true,
        (SparkleUserParameterType.Vector, SparkleParameterType.Vector) => true,
        (SparkleUserParameterType.Color, SparkleParameterType.Color) => true,
        (SparkleUserParameterType.Boolean, SparkleParameterType.Boolean) => true,
        _ => false,
    };

    /// <summary>
    /// The emitters with the value of the parameters in the linked settings (a link to a missing
    /// parameter, or of the wrong type, keeps the default of the setting).
    /// </summary>
    public static IReadOnlyList<SparkleEmitter> Resolve(IReadOnlyList<SparkleEmitter> emitters, IReadOnlyList<SparkleUserParameter> parameters)
    {
        if (parameters.Count == 0 && emitters.All(e => e.Modules.All(m => m.Values.Values.All(v => BoundId(v) is null))))
        {
            return emitters;
        }

        Dictionary<Guid, SparkleUserParameter> byId = parameters.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        return [.. emitters.Select(e => e with { Modules = [.. e.Modules.Select(m => Resolve(m, byId))] })];
    }

    private static SparkleModule Resolve(SparkleModule module, Dictionary<Guid, SparkleUserParameter> parameters)
    {
        ImmutableDictionary<string, string> values = module.Values;
        foreach ((string name, string value) in module.Values)
        {
            if (BoundId(value) is not { } id)
            {
                continue;
            }

            SparkleParameterInfo? info = module.Info.FindParameter(name);
            values = parameters.TryGetValue(id, out SparkleUserParameter? parameter) && info is not null && parameter.Accepts(info.Type)
                ? values.SetItem(name, parameter.Value)
                : values.Remove(name);
        }

        return module with { Values = values };
    }

    public override string ToString() => Name;
}
