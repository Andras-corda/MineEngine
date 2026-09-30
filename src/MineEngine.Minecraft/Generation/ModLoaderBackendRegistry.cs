using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;

namespace MineEngine.Minecraft.Generation;

/// <summary>Ensemble des backends disponibles ; choisit celui qui sait utiliser un MDK donné.</summary>
public sealed class ModLoaderBackendRegistry
{
    private readonly IReadOnlyList<IModLoaderBackendProvider> _providers;

    public ModLoaderBackendRegistry(IEnumerable<IModLoaderBackendProvider> providers)
    {
        _providers = [.. providers];
    }

    public static ModLoaderBackendRegistry CreateDefault() =>
        new([new ForgeBackendProvider(), new NeoForgeBackendProvider()]);

    /// <summary>Liste lisible de ce que le générateur sait produire.</summary>
    public string SupportDescription => string.Join(", ", _providers.Select(p => p.SupportDescription));

    public bool Supports(MdkDescriptor mdk) => _providers.Any(p => p.Supports(mdk));

    public IModLoaderBackend Create(MdkDescriptor mdk)
    {
        ArgumentNullException.ThrowIfNull(mdk);
        IModLoaderBackendProvider provider = _providers.FirstOrDefault(p => p.Supports(mdk))
            ?? throw new NotSupportedException(
                $"Le générateur ne prend pas encore en charge {mdk.DisplayName}. Versions prises en charge : {SupportDescription}.");
        return provider.Create(mdk);
    }
}
