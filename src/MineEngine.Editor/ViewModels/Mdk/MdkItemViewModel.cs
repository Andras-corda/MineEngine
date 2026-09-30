using MineEngine.Minecraft.Mdk;

namespace MineEngine.Editor.ViewModels.Mdk;

/// <summary>Une ligne de la liste des MDK installés.</summary>
public sealed class MdkItemViewModel
{
    public MdkItemViewModel(MdkDescriptor descriptor, bool isSupported)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        IsSupported = isSupported;
    }

    public MdkDescriptor Descriptor { get; }

    public string Loader => Descriptor.Loader.ToDisplayName();

    public string MinecraftVersion => Descriptor.MinecraftVersion.Id;

    public string LoaderVersion => Descriptor.LoaderVersion;

    public string Java => Descriptor.JavaVersion.ToString();

    public string Gradle => Descriptor.GradleVersion ?? "?";

    public bool IsSupported { get; }

    public string Status => IsSupported ? "Utilisable" : "Non pris en charge par le générateur";

    public string Source => Descriptor.Source;

    public string Directory => Descriptor.Directory;
}

/// <summary>Un MDK officiel proposé au téléchargement.</summary>
public sealed class MdkDownloadViewModel
{
    public MdkDownloadViewModel(MdkDownload download, bool isInstalled)
    {
        Download = download ?? throw new ArgumentNullException(nameof(download));
        IsInstalled = isInstalled;
    }

    public MdkDownload Download { get; }

    public string DisplayName => Download.DisplayName;

    public bool IsInstalled { get; }

    public string Status => IsInstalled ? "Installé" : "Disponible";

    public string Url => Download.Source.ToString();
}
