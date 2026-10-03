namespace MineEngine.Core.Assets;

/// <summary>Ce qu'une référence attend de l'asset visé.</summary>
public enum AssetReferenceKind
{
    /// <summary>Un asset Texture.</summary>
    Texture,

    /// <summary>Un item, ou un bloc qui a une forme item.</summary>
    Item,

    /// <summary>Un asset Sound.</summary>
    Sound,
}

/// <summary>
/// Lien d'un asset vers un autre, par identifiant interne : renommer l'asset visé
/// ne casse pas le lien. Sert à détecter les références cassées et à afficher
/// "Utilisé par".
/// </summary>
/// <param name="TargetId">Asset visé.</param>
/// <param name="Kind">Type d'asset attendu.</param>
/// <param name="Role">Rôle lisible du lien ("texture", "item laissé"...).</param>
public sealed record AssetReference(Guid TargetId, AssetReferenceKind Kind, string Role);
