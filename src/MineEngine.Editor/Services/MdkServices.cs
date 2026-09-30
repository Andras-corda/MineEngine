using MineEngine.Core.Logging;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Editor.Services;

/// <summary>Accès aux MDK pour les écrans de l'éditeur : bibliothèque, téléchargements et prise en charge.</summary>
public sealed class MdkServices
{
    public MdkServices(MdkLibrary library, MdkCatalog catalog, ModLoaderBackendRegistry backends, ILog log)
    {
        Library = library ?? throw new ArgumentNullException(nameof(library));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Backends = backends ?? throw new ArgumentNullException(nameof(backends));
        Log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public MdkLibrary Library { get; }

    public MdkCatalog Catalog { get; }

    public ModLoaderBackendRegistry Backends { get; }

    public ILog Log { get; }

    /// <summary>MDK installés que le générateur sait utiliser, du plus récent au plus ancien.</summary>
    public IReadOnlyList<MdkDescriptor> UsableMdks => Library.Installed.Where(Backends.Supports).ToList();
}
