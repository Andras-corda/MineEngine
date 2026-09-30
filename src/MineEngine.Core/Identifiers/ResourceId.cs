namespace MineEngine.Core.Identifiers;

/// <summary>
/// Identifiant Minecraft d'un élément dans l'espace de noms du mod
/// (partie "magic_sword" de "mymod:magic_sword"). Objet valeur immuable.
/// </summary>
public sealed class ResourceId : IEquatable<ResourceId>, IComparable<ResourceId>
{
    public const int MinLength = 1;
    public const int MaxLength = 64;
    public const string RuleDescription = IdentifierRules.Description;

    private ResourceId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool IsValid(string? candidate) => IdentifierRules.IsValid(candidate, MinLength, MaxLength);

    public static ResourceId Parse(string candidate)
    {
        if (!IsValid(candidate))
        {
            throw new ArgumentException(
                $"Identifiant invalide : '{candidate}'. Attendu : {RuleDescription}.", nameof(candidate));
        }

        return new ResourceId(candidate);
    }

    public static bool TryParse(string? candidate, out ResourceId? result)
    {
        result = IsValid(candidate) ? new ResourceId(candidate!) : null;
        return result is not null;
    }

    /// <summary>Construit un identifiant valide à partir d'un texte libre.</summary>
    public static ResourceId FromText(string? text) =>
        new(IdentifierRules.Sanitize(text, MinLength, MaxLength, "unnamed"));

    public bool Equals(ResourceId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ResourceId);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(ResourceId? other) => string.CompareOrdinal(Value, other?.Value);

    public override string ToString() => Value;

    public static bool operator ==(ResourceId? left, ResourceId? right) => Equals(left, right);

    public static bool operator !=(ResourceId? left, ResourceId? right) => !Equals(left, right);
}
