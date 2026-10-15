using MineEngine.Core.Assets;

namespace MineEngine.Assets.Definitions;

/// <summary>Liste des types d'assets connus de l'application.</summary>
public sealed class AssetCatalog
{
    private readonly Dictionary<AssetType, IAssetDefinition> _definitions = [];

    public AssetCatalog(IEnumerable<IAssetDefinition> definitions)
    {
        foreach (IAssetDefinition definition in definitions)
        {
            Register(definition);
        }
    }

    public IReadOnlyCollection<IAssetDefinition> Definitions => _definitions.Values;

    /// <summary>Catalogue standard : items, blocs, mobs, recettes, textures, sons et graphes.</summary>
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

    public void Register(IAssetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!_definitions.TryAdd(definition.Type, definition))
        {
            throw new InvalidOperationException(Tr.F("Le type d'asset {0} est déjà enregistré.", definition.Type));
        }
    }

    public IAssetDefinition Get(AssetType type) =>
        _definitions.TryGetValue(type, out IAssetDefinition? definition)
            ? definition
            : throw new KeyNotFoundException(Tr.F("Aucune définition pour le type d'asset {0}.", type));

    public bool TryGet(string typeName, out IAssetDefinition? definition)
    {
        definition = Enum.TryParse(typeName, ignoreCase: false, out AssetType type)
            ? _definitions.GetValueOrDefault(type)
            : null;
        return definition is not null;
    }
}
