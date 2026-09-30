using System.Text.Json.Nodes;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Décrit un type d'asset : comment en créer un nouveau et comment lire ou
/// écrire ses propriétés spécifiques. Ajouter un type d'asset revient à
/// fournir une nouvelle définition (point d'extension des futurs plugins).
/// </summary>
public interface IAssetDefinition
{
    AssetType Type { get; }

    /// <summary>Nom lisible du type, affiché dans l'éditeur.</summary>
    string Label { get; }

    /// <summary>Base proposée pour l'identifiant d'un nouvel asset ("new_item").</summary>
    string DefaultResourceIdBase { get; }

    /// <summary>Nom affiché proposé pour un nouvel asset.</summary>
    string DefaultDisplayName { get; }

    Asset CreateNew(ResourceId resourceId, string displayName);

    Asset Read(Guid id, ResourceId resourceId, string displayName, JsonObject properties);

    JsonObject WriteProperties(Asset asset);
}
