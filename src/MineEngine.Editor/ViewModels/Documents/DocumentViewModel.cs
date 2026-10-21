using System.Windows.Input;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Documents;

/// <summary>
/// Un onglet de la zone centrale (hors Dashboard) : fichier de code ou graphe nodal.
/// Fournit ce qu'attendent les onglets, le panneau Détails et la section Structure de
/// l'explorateur ; les sous-classes disent comment enregistrer et où mène la structure.
/// </summary>
public abstract class DocumentViewModel : ObservableObject, IDetailsTarget
{
    private bool _isDirty;
    private IReadOnlyList<OutlineItem> _outline = [];

    protected DocumentViewModel(string id, string displayPath)
    {
        Id = id;
        DisplayPath = displayPath.Replace('\\', '/');
        NavigateCommand = new RelayCommand<OutlineItem>(Navigate);
    }

    /// <summary>Identifiant de l'onglet (unique parmi les onglets ouverts).</summary>
    public string Id { get; }

    /// <summary>Chemin relatif au projet, avec des '/'.</summary>
    public string DisplayPath { get; }

    public virtual string FileName => Path.GetFileName(DisplayPath);

    public abstract bool IsReadOnly { get; }

    public bool IsDirty
    {
        get => _isDirty;
        protected set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    /// <summary>Titre de l'onglet ; un point signale des modifications non enregistrées.</summary>
    public string Title => _isDirty ? FileName + " ●" : FileName;

    /// <summary>Entrées de la section Structure (méthodes d'un fichier, événements d'un graphe).</summary>
    public IReadOnlyList<OutlineItem> Outline
    {
        get => _outline;
        protected set => SetProperty(ref _outline, value);
    }

    /// <summary>Va à une entrée de la structure (paramètre : l'entrée).</summary>
    public ICommand NavigateCommand { get; }

    public virtual string DetailsTitle => FileName;

    public virtual string DetailsBadge => IsReadOnly ? Tr.T("READ-ONLY") : Tr.T("FILE");

    public string DetailsSubtitle => DisplayPath;

    /// <summary>Enregistre l'onglet ; faux si l'enregistrement a échoué ou est impossible.</summary>
    public abstract bool Save();

    /// <summary>Affiche l'élément désigné par une entrée de la structure.</summary>
    public abstract void Navigate(OutlineItem item);
}
