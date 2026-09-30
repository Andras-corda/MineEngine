using MineEngine.Core.Logging;

namespace MineEngine.Minecraft.Mdk;

/// <summary>Un MDK officiel téléchargeable depuis Mine Engine.</summary>
public sealed class MdkDownload
{
    public MdkDownload(ModLoaderKind loader, string minecraftVersion, string loaderVersion, Uri source)
    {
        Loader = loader;
        MinecraftVersion = MinecraftVersion.Parse(minecraftVersion);
        LoaderVersion = loaderVersion;
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public ModLoaderKind Loader { get; }

    public MinecraftVersion MinecraftVersion { get; }

    public string LoaderVersion { get; }

    public Uri Source { get; }

    public string Id => MdkDescriptor.CreateId(Loader, MinecraftVersion, LoaderVersion);

    public string DisplayName => $"{Loader.ToDisplayName()} {MinecraftVersion} ({LoaderVersion})";
}

/// <summary>
/// MDK officiels proposés au téléchargement. La liste se limite aux versions
/// que le générateur sait produire ; tout autre MDK peut être importé à la main.
/// </summary>
public sealed class MdkCatalog
{
    private const string NeoForgeMdkCommit = "7819b902a351b03fe71db00754d103b5a31c4ebf";

    private readonly HttpClient _httpClient;

    public MdkCatalog(HttpClient httpClient, IEnumerable<MdkDownload> downloads)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Downloads = [.. downloads];
    }

    public IReadOnlyList<MdkDownload> Downloads { get; }

    public static MdkCatalog CreateDefault(HttpClient httpClient) => new(httpClient,
    [
        ForgeMdk("1.20.1", "47.4.10"),
        ForgeMdk("1.21.1", "52.1.0"),
        new MdkDownload(ModLoaderKind.NeoForge, "1.21.1", "21.1.252",
            new Uri($"https://github.com/NeoForgeMDKs/MDK-1.21.1-ModDevGradle/archive/{NeoForgeMdkCommit}.zip")),
    ]);

    /// <summary>Télécharge l'archive du MDK puis l'installe dans la bibliothèque.</summary>
    public async Task<MdkDescriptor> DownloadAsync(MdkDownload download, MdkLibrary library, ILog log, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(download);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(log);

        string archive = Path.Combine(Path.GetTempPath(), "mineengine-download-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            log.Info($"Téléchargement de {download.DisplayName} depuis {download.Source} ...");
            using (HttpResponseMessage response = await _httpClient
                       .GetAsync(download.Source, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                       .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using FileStream file = File.Create(archive);
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            MdkDescriptor mdk = library.ImportArchive(archive, "Téléchargé depuis " + download.Source);
            log.Success("MDK installé : " + mdk.DisplayName);
            return mdk;
        }
        finally
        {
            try
            {
                File.Delete(archive);
            }
            catch (IOException)
            {
            }
        }
    }

    private static MdkDownload ForgeMdk(string minecraftVersion, string forgeVersion)
    {
        string version = $"{minecraftVersion}-{forgeVersion}";
        return new MdkDownload(ModLoaderKind.Forge, minecraftVersion, forgeVersion,
            new Uri($"https://maven.minecraftforge.net/net/minecraftforge/forge/{version}/forge-{version}-mdk.zip"));
    }
}
