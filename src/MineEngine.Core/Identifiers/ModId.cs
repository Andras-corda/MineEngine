namespace MineEngine.Core.Identifiers;

/// <summary>
/// Identifiant unique d'un mod (espace de noms Minecraft). NeoForge impose
/// 2 à 64 caractères : [a-z][a-z0-9_]{1,63}. Objet valeur immuable.
/// </summary>
public sealed class ModId : IEquatable<ModId>
{
    public const int MinLength = 2;
    public const int MaxLength = 64;
    public const string RuleDescription = IdentifierRules.Description + ", 2 caractères minimum";

    private ModId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool IsValid(string? candidate) => IdentifierRules.IsValid(candidate, MinLength, MaxLength);

    public static ModId Parse(string candidate)
    {
        if (!IsValid(candidate))
        {
            throw new ArgumentException(
                $"Identifiant de mod invalide : '{candidate}'. Attendu : {RuleDescription}.", nameof(candidate));
        }

        return new ModId(candidate);
    }

    public static bool TryParse(string? candidate, out ModId? result)
    {
        result = IsValid(candidate) ? new ModId(candidate!) : null;
        return result is not null;
    }

    /// <summary>Construit un identifiant de mod valide à partir d'un nom libre.</summary>
    public static ModId FromText(string? text) =>
        new(IdentifierRules.Sanitize(text, MinLength, MaxLength, "mymod"));

    public bool Equals(ModId? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ModId);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(ModId? left, ModId? right) => Equals(left, right);

    public static bool operator !=(ModId? left, ModId? right) => !Equals(left, right);
}
