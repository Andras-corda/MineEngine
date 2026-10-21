using System.Globalization;
using MineEngine.Core.Assets;
using MineEngine.Editor.Mvvm;
using MineEngine.Project.Workspace;

namespace MineEngine.Editor.ViewModels.Hub;

/// <summary>Un projet dans la liste de l'accueil : aperçu lu sur le disque, ou projet introuvable.</summary>
public sealed class ProjectCardViewModel : ObservableObject
{
    private KnownProject _known;
    private ProjectSummary? _summary;

    public ProjectCardViewModel(KnownProject known, ProjectSummary? summary)
    {
        _known = known;
        _summary = summary;
    }

    public KnownProject Known => _known;

    public string RootDirectory => _known.RootDirectory;

    /// <summary>Le dossier n'existe plus ou ne contient plus de projet lisible.</summary>
    public bool IsMissing => _summary is null;

    public string Name => _summary?.Settings.ModName ?? Path.GetFileName(RootDirectory);

    /// <summary>Loader et version ciblés ("NeoForge 1.21.1").</summary>
    public string Badge => _summary is null
        ? Tr.T("NOT FOUND")
        : $"{LoaderName(_summary.Settings.LoaderId)} {_summary.Settings.MinecraftVersion}".Trim();

    public string Description => _summary switch
    {
        null => Tr.T("The folder was moved or deleted."),
        { Settings.Description: { Length: > 0 } text } => text.ReplaceLineEndings(" "),
        _ => Tr.T("No description"),
    };

    /// <summary>Identifiant et contenu : "e2emod · 3 items · 5 blocs · 3 mobs".</summary>
    public string Details
    {
        get
        {
            if (_summary is null)
            {
                return string.Empty;
            }

            var parts = new List<string> { _summary.Settings.ModId.Value };
            foreach ((AssetType type, string singular, string plural) in new[]
                     {
                         (AssetType.Item, Tr.T("{0} item"), Tr.T("{0} items")), (AssetType.Block, Tr.T("{0} block"), Tr.T("{0} blocks")),
                         (AssetType.Mob, Tr.T("{0} mob"), Tr.T("{0} mobs")), (AssetType.Recipe, Tr.T("{0} recipe"), Tr.T("{0} recipes")),
                         (AssetType.Graph, Tr.T("{0} graph"), Tr.T("{0} graphs")),
                         (AssetType.BehaviorTree, Tr.T("{0} AI tree"), Tr.T("{0} AI trees")),
                     })
            {
                int count = _summary.Count(type);
                if (count > 0)
                {
                    parts.Add(string.Format(CultureInfo.CurrentCulture, count > 1 ? plural : singular, count));
                }
            }

            if (parts.Count == 1)
            {
                parts.Add(Tr.T("empty"));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>Chemin abrégé : le dossier de l'utilisateur devient "~".</summary>
    public string PathLabel
    {
        get
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return RootDirectory.StartsWith(home, StringComparison.OrdinalIgnoreCase)
                ? "~" + RootDirectory[home.Length..].Replace('\\', '/')
                : RootDirectory.Replace('\\', '/');
        }
    }

    public DateTimeOffset? LastOpened => _known.LastOpened;

    public DateTimeOffset SortDate => _known.LastOpened ?? DateTimeOffset.MinValue;

    /// <summary>Version de Minecraft comparable ("1.21.1" avant "1.20.1" une fois inversé).</summary>
    public Version SortVersion =>
        System.Version.TryParse(_summary?.Settings.MinecraftVersion, out Version? version) ? version : new Version(0, 0);

    public string LastOpenedLabel
    {
        get
        {
            if (_known.LastOpened is not { } date)
            {
                return Tr.T("Never opened");
            }

            int days = (DateTime.Today - date.LocalDateTime.Date).Days;
            return days switch
            {
                <= 0 => Tr.T("Opened today at ") + date.LocalDateTime.ToString("HH:mm", CultureInfo.CurrentCulture),
                1 => Tr.T("Opened yesterday"),
                < 30 => Tr.F("Opened {0} days ago", days),
                _ => Tr.T("Opened on ") + date.LocalDateTime.ToString("D", CultureInfo.CurrentCulture),
            };
        }
    }

    public string MdkLabel => _summary?.Settings.MdkId is { Length: > 0 } mdk ? mdk : Tr.T("No MDK chosen");

    /// <summary>Icon of the card: the icon of the mod (project settings), otherwise the first texture of the project, or null.</summary>
    public string? IconTexture =>
        _summary is not null && MineEngine.Project.ProjectIcon.Resolve(RootDirectory, _summary.Settings.IconFile) is { } icon && File.Exists(icon)
            ? icon
            : _summary?.PreviewTextures.FirstOrDefault();

    public bool HasIconTexture => IconTexture is not null;

    public IReadOnlyList<string> PreviewTextures => _summary?.PreviewTextures ?? [];

    public int ItemCount => _summary?.Count(AssetType.Item) ?? 0;

    public int BlockCount => _summary?.Count(AssetType.Block) ?? 0;

    public int MobCount => _summary?.Count(AssetType.Mob) ?? 0;

    public int RecipeCount => _summary?.Count(AssetType.Recipe) ?? 0;

    /// <summary>Met à jour la carte (nouvelle ouverture, projet modifié sur le disque).</summary>
    public void Update(KnownProject known, ProjectSummary? summary)
    {
        _known = known;
        _summary = summary;
        OnPropertyChanged(string.Empty);
    }

    public override string ToString() => Name;

    /// <summary>Icône du loader (clé de Themes/Icons.xaml), ou null s'il est inconnu.</summary>
    public string? LoaderIcon => _summary?.Settings.LoaderId.ToLowerInvariant() switch
    {
        "neoforge" => "NeoForgeGeometry",
        "forge" => "ForgeGeometry",
        "fabric" => "FabricGeometry",
        "quilt" => "QuiltGeometry",
        _ => null,
    };

    private static string LoaderName(string loaderId) => loaderId.ToLowerInvariant() switch
    {
        "neoforge" => "NeoForge",
        "forge" => "Forge",
        "fabric" => "Fabric",
        _ => loaderId,
    };
}
