using MineEngine.Core.Assets;

namespace MineEngine.Assets.Definitions;

/// <summary>
/// Catalogue des définitions d'assets, permettant de gérer et d'accéder aux différentes définitions d'assets disponibles dans le projet.
/// </summary>
public sealed class AssetCatalog
{
    private readonly Dictionary<AssetType, IAssetDefinition> _definitions = []; // dictionnaire stockant les définitions d'assets, où la clé est le type d'asset et la valeur est l'objet représentant la définition de l'asset

    /// <summary>
    /// Initializes a new instance of the AssetCatalog class with the provided definitions, allowing for the management and access of asset definitions within the project.
    /// </summary>
    /// <param name="definitions"></param>
    public AssetCatalog(IEnumerable<IAssetDefinition> definitions)
    {
        foreach (IAssetDefinition definition in definitions)
        {
            Register(definition);
        }
    }

    public IReadOnlyCollection<IAssetDefinition> Definitions => _definitions.Values; // Interface pour accéder aux définitions d'assets

    /// <summary>
    /// Creates a default AssetCatalog with predefined asset definitions, providing a convenient way to initialize the catalog with commonly used asset types.
    /// </summary>
    /// <returns></returns>
    public static AssetCatalog CreateDefault() =>
        new(
        [
            new ItemAssetDefinition(),
            new BlockAssetDefinition(),
            new MobAssetDefinition(),
            new RecipeAssetDefinition(),
            new TextureAssetDefinition(),
            new SoundAssetDefinition(),
            new GraphAssetDefinition(),
        ]);

    /// <summary>
    /// Registers a new asset definition in the catalog, allowing for the management and access of the specified asset type within the project.
    /// </summary>
    /// <param name="definition"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Register(IAssetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!_definitions.TryAdd(definition.Type, definition))
        {
            throw new InvalidOperationException(Tr.F("Le type d'asset {0} est déjà enregistré.", definition.Type));
        }
    }

    /// <summary>
    /// Retrieves the asset definition for the specified asset type from the catalog, allowing for access to the properties and methods associated with that asset type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public IAssetDefinition Get(AssetType type)
    {
        if (_definitions.TryGetValue(type, out IAssetDefinition? definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            Tr.F("Aucune définition pour le type d'asset {0}.", type));
    }

    /// <summary>
    /// Attempts to retrieve the asset definition for the specified type name from the catalog, returning a boolean indicating whether the definition was found and outputting the definition if successful.
    /// </summary>
    /// <param name="typeName"></param>
    /// <param name="definition"></param>
    /// <returns></returns>
    public bool TryGet(string typeName, out IAssetDefinition? definition)
    {
        definition = null;

        if (Enum.TryParse(typeName, false, out AssetType type))
        {
            _definitions.TryGetValue(type, out definition);
        }

        return definition != null;
    }
}
