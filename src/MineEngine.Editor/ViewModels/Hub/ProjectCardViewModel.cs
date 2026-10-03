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
        ? "INTROUVABLE"
        : $"{LoaderName(_summary.Settings.LoaderId)} {_summary.Settings.MinecraftVersion}".Trim();

    public string Description => _summary switch
    {
        null => "Le dossier a été déplacé ou supprimé.",
        { Settings.Description: { Length: > 0 } text } => text.ReplaceLineEndings(" "),
        _ => "Aucune description",
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
                         (AssetType.Item, "item", "items"), (AssetType.Block, "bloc", "blocs"),
                         (AssetType.Mob, "mob", "mobs"), (AssetType.Recipe, "recette", "recettes"),
                     })
            {
                int count = _summary.Count(type);
                if (count > 0)
                {
                    parts.Add($"{count} {(count > 1 ? plural : singular)}");
                }
            }

            if (parts.Count == 1)
            {
                parts.Add("vide");
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
                return "Jamais ouvert";
            }

            int days = (DateTime.Today - date.LocalDateTime.Date).Days;
            return days switch
            {
                <= 0 => "Ouvert aujourd'hui à " + date.LocalDateTime.ToString("HH:mm", CultureInfo.CurrentCulture),
                1 => "Ouvert hier",
                < 30 => $"Ouvert il y a {days} jours",
                _ => "Ouvert le " + date.LocalDateTime.ToString("d MMMM yyyy", CultureInfo.CurrentCulture),
            };
        }
    }

    public string MdkLabel => _summary?.Settings.MdkId is { Length: > 0 } mdk ? mdk : "Aucun MDK choisi";

    /// <summary>Première texture du projet pour l'icône, ou null.</summary>
    public string? IconTexture => _summary?.PreviewTextures.FirstOrDefault();

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

    private static string LoaderName(string loaderId) => loaderId.ToLowerInvariant() switch
    {
        "neoforge" => "NeoForge",
        "forge" => "Forge",
        "fabric" => "Fabric",
        _ => loaderId,
    };
}
