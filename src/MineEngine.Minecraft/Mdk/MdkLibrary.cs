using System.IO.Compression;
using MineEngine.Core.IO;
using MineEngine.Core.Logging;

namespace MineEngine.Minecraft.Mdk;

/// <summary>
/// Dossier où sont rangés les MDK installés, un sous-dossier par MDK.
/// Un MDK peut y arriver de trois façons : téléchargé depuis le catalogue,
/// importé depuis une archive ou un dossier, ou déposé à la main dans le
/// dossier (une archive .zip ou un dossier extrait) puis détecté par <see cref="Refresh"/>.
/// </summary>
public sealed class MdkLibrary
{
    /// <summary>Dossiers de travail ignorés lors de l'import d'un dossier déjà utilisé.</summary>
    private static readonly string[] IgnoredEntries = [".gradle", "build", "run", ".idea", "out", "bin", ".vscode", ".git"];

    private readonly MdkInspector _inspector;
    private readonly MdkManifestSerializer _manifests;
    private readonly List<MdkDescriptor> _installed = [];

    public MdkLibrary(string rootDirectory, MdkInspector inspector, MdkManifestSerializer manifests)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _manifests = manifests ?? throw new ArgumentNullException(nameof(manifests));
    }

    /// <summary>Déclenché après chaque modification de la liste des MDK installés.</summary>
    public event EventHandler? Changed;

    public string RootDirectory { get; }

    public IReadOnlyList<MdkDescriptor> Installed => _installed;

    public MdkDescriptor? Find(string? id) =>
        id is null ? null : _installed.Find(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Relit le dossier : charge les MDK installés, enregistre les dossiers de MDK
    /// déposés à la main et importe les archives .zip déposées à la racine.
    /// </summary>
    public void Refresh(ILog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        Directory.CreateDirectory(RootDirectory);
        _installed.Clear();

        foreach (string directory in Directory.EnumerateDirectories(RootDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Path.GetFileName(directory).StartsWith('.'))
            {
                continue;
            }

            try
            {
                MdkDescriptor? mdk = LoadOrRegister(directory);
                if (mdk is not null)
                {
                    _installed.Add(mdk);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                log.Warning($"Dossier de MDK ignoré ({Path.GetFileName(directory)}) : {exception.Message}");
            }
        }

        foreach (string archive in Directory.EnumerateFiles(RootDirectory, "*.zip").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (_installed.Exists(m => m.Source.EndsWith(Path.GetFileName(archive), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                MdkDescriptor mdk = Install(ExtractArchive(archive), "Archive déposée : " + Path.GetFileName(archive), deleteSource: true);
                log.Info("MDK détecté et installé : " + mdk.DisplayName);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                log.Warning($"Archive ignorée ({Path.GetFileName(archive)}) : {exception.Message}");
            }
        }

        _installed.Sort((a, b) => b.MinecraftVersion.CompareTo(a.MinecraftVersion) is var byVersion && byVersion != 0
            ? byVersion
            : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Installe un MDK à partir d'une archive .zip (celle proposée par Forge ou NeoForge).</summary>
    public MdkDescriptor ImportArchive(string archiveFile, string source)
    {
        MdkDescriptor mdk = Install(ExtractArchive(archiveFile), source, deleteSource: true);
        Changed?.Invoke(this, EventArgs.Empty);
        return mdk;
    }

    /// <summary>Installe un MDK à partir d'un dossier déjà extrait (copié, l'original n'est pas modifié).</summary>
    public MdkDescriptor ImportDirectory(string directory)
    {
        string root = _inspector.FindRoot(directory)
            ?? throw new InvalidDataException($"Aucun MDK trouvé dans '{directory}'.");
        MdkDescriptor mdk = Install(root, "Dossier importé : " + directory, deleteSource: false);
        Changed?.Invoke(this, EventArgs.Empty);
        return mdk;
    }

    /// <summary>Supprime un MDK de la bibliothèque (les projets qui l'utilisent devront en choisir un autre).</summary>
    public void Remove(MdkDescriptor mdk)
    {
        ArgumentNullException.ThrowIfNull(mdk);
        if (!Path.GetFullPath(mdk.Directory).StartsWith(RootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ce MDK n'est pas rangé dans la bibliothèque.");
        }

        if (Directory.Exists(mdk.Directory))
        {
            Directory.Delete(mdk.Directory, recursive: true);
        }

        _installed.Remove(mdk);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private MdkDescriptor? LoadOrRegister(string directory)
    {
        string manifest = Path.Combine(directory, MdkManifestSerializer.FileName);
        if (File.Exists(manifest))
        {
            return _manifests.Deserialize(File.ReadAllText(manifest), directory);
        }

        string? root = _inspector.FindRoot(directory);
        if (root is null)
        {
            return null;
        }

        // Dossier de MDK déposé à la main : on l'enregistre sur place.
        MdkInspection inspection = _inspector.Inspect(root);
        var mdk = new MdkDescriptor(
            MdkDescriptor.CreateId(inspection.Loader, inspection.MinecraftVersion, inspection.LoaderVersion),
            inspection.Loader, inspection.MinecraftVersion, inspection.LoaderVersion, inspection.JavaVersion,
            inspection.GradleVersion, root, "Dossier déposé : " + Path.GetFileName(directory), DateTime.Now);
        AtomicFile.WriteAllText(Path.Combine(root, MdkManifestSerializer.FileName), _manifests.Serialize(mdk));
        return mdk;
    }

    private MdkDescriptor Install(string sourceRoot, string source, bool deleteSource)
    {
        try
        {
            MdkInspection inspection = _inspector.Inspect(sourceRoot);
            string id = MdkDescriptor.CreateId(inspection.Loader, inspection.MinecraftVersion, inspection.LoaderVersion);
            if (Find(id) is not null || Directory.Exists(Path.Combine(RootDirectory, id)))
            {
                throw new IOException($"Le MDK {id} est déjà installé.");
            }

            string target = Path.Combine(RootDirectory, id);
            new DirectoryCopier(path => !IgnoredEntries.Contains(path, StringComparer.OrdinalIgnoreCase))
                .Copy(sourceRoot, target, overwrite: true);

            var mdk = new MdkDescriptor(
                id, inspection.Loader, inspection.MinecraftVersion, inspection.LoaderVersion, inspection.JavaVersion,
                inspection.GradleVersion, target, source, DateTime.Now);
            AtomicFile.WriteAllText(Path.Combine(target, MdkManifestSerializer.FileName), _manifests.Serialize(mdk));
            _installed.Add(mdk);
            return mdk;
        }
        finally
        {
            if (deleteSource)
            {
                TryDeleteTemporary(sourceRoot);
            }
        }
    }

    /// <summary>Extrait une archive dans un dossier temporaire et retourne la racine du MDK.</summary>
    private string ExtractArchive(string archiveFile)
    {
        if (!File.Exists(archiveFile))
        {
            throw new FileNotFoundException("Archive introuvable.", archiveFile);
        }

        string temporary = Path.Combine(Path.GetTempPath(), "mineengine-mdk-" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(archiveFile, temporary);
        }
        catch (InvalidDataException)
        {
            TryDeleteTemporary(temporary);
            throw new InvalidDataException($"'{Path.GetFileName(archiveFile)}' n'est pas une archive .zip valide.");
        }

        string? root = _inspector.FindRoot(temporary);
        if (root is null)
        {
            TryDeleteTemporary(temporary);
            throw new InvalidDataException($"L'archive '{Path.GetFileName(archiveFile)}' ne contient pas de MDK (gradlew.bat introuvable).");
        }

        return root;
    }

    private static void TryDeleteTemporary(string path)
    {
        string temp = Path.GetFullPath(Path.GetTempPath());
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Supprime le dossier d'extraction entier, même si le MDK était dans un sous-dossier.
        string relative = Path.GetRelativePath(temp, full);
        string topLevel = Path.Combine(temp, relative.Split(Path.DirectorySeparatorChar)[0]);
        try
        {
            if (Directory.Exists(topLevel))
            {
                Directory.Delete(topLevel, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
