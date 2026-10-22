using System.Windows.Input;
using MineEngine.Core.Assets;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;
using MineEngine.Editor.ViewModels.Documents;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Lien vers un autre asset ("Utilisé par").</summary>
/// <param name="TargetId">Asset à ouvrir.</param>
/// <param name="Label">Nom de l'asset et rôle du lien.</param>
public sealed record AssetLinkViewModel(Guid TargetId, string Label);

/// <summary>A fact of the summary of an element in Properties ("Typed in the chat: /boost").</summary>
public sealed record SummaryLine(string Label, string Value);

/// <summary>
/// Propriétés communes à tous les assets, affichées dans l'Inspector. Chaque
/// modification passe par l'historique du projet (annuler/rétablir) ; la vue est
/// mise à jour à partir des changements de l'asset, y compris lors d'une annulation.
/// </summary>
public abstract class AssetViewModel : ValidatingObservableObject, IDisposable, IDetailsTarget
{
    private string _resourceIdText;
    private int _errorCount;
    private int _warningCount;

    protected AssetViewModel(Asset model, AssetEditingContext context, string typeLabel)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _typeLabel = typeLabel;
        _resourceIdText = model.ResourceId.Value;
        OpenAssetCommand = new RelayCommand<Guid>(id => Context.SelectAsset(id));
        Model.Changed += OnModelChanged;
    }

    public Asset Model { get; }

    private readonly string _typeLabel;

    /// <summary>Type affiché ("Item", "Fonction"...).</summary>
    public virtual string TypeLabel => _typeLabel;

    /// <summary>Texte affiché dans la liste du contenu.</summary>
    public string Header => string.IsNullOrWhiteSpace(Model.DisplayName) ? Model.ResourceId.Value : Model.DisplayName;

    public string DisplayName
    {
        get => Model.DisplayName;
        set
        {
            string newValue = value ?? string.Empty;
            if (Model.DisplayName != newValue)
            {
                Change(Tr.T("Change display name"), nameof(Asset.DisplayName), () => Model.DisplayName, v => Model.DisplayName = v, newValue);
            }
        }
    }

    /// <summary>Identifiant saisi ; il n'est appliqué à l'asset que s'il est valide et libre.</summary>
    public string ResourceIdText
    {
        get => _resourceIdText;
        set
        {
            if (SetProperty(ref _resourceIdText, value ?? string.Empty))
            {
                ApplyResourceId(_resourceIdText);
            }
        }
    }

    /// <summary>Identifiant complet utilisé en jeu, par exemple "mymod:magic_sword".</summary>
    public string FullResourceId => $"{Context.Project.Settings.ModId}:{Model.ResourceId}";

    public string DetailsTitle => Header;

    public string DetailsBadge => TypeLabel.ToUpperInvariant();

    /// <summary>Dossier du Dashboard qui contient l'élément ("Armes/Épées"), ou "Racine".</summary>
    public string FolderLabel => Model.Folder.Length == 0 ? Tr.T("Root") : Model.Folder;

    public string DetailsSubtitle => HasGameId ? FullResourceId : Model.ResourceId.Value;

    /// <summary>Icône du type (clé de Themes/Icons.xaml), affichée quand l'asset n'a pas d'image.</summary>
    public virtual string TypeIcon => Model.Type switch
    {
        AssetType.Item => "ItemGeometry",
        AssetType.Block => "BlockGeometry",
        AssetType.Mob => "MobGeometry",
        AssetType.Recipe => "RecipeGeometry",
        AssetType.Texture => "Glyph.Texture",
        AssetType.Sound => "Glyph.Sound",
        AssetType.Graph => "NodeGraphGeometry",
        AssetType.BehaviorTree => "BehaviorTreeGeometry",
        AssetType.Model => "ModelGeometry",
        AssetType.Command => "CommandGeometry",
        AssetType.CreativeTab => "Glyph.Tab",
        AssetType.Keybind => "Glyph.Keyboard",
        AssetType.Enchantment => "Glyph.Star",
        AssetType.Fluid => "FluidGeometry",
        AssetType.GameRule => "Glyph.Settings",
        AssetType.Effect => "PotionGeometry",
        AssetType.GlobalVariable => "Glyph.Field",
        AssetType.Tag => "Glyph.Link",
        AssetType.Plant => "Glyph.Star",
        AssetType.Biome => "Glyph.Explorer",
        _ => "Glyph.File",
    };

    /// <summary>Image représentant l'asset dans le Dashboard (sa texture), ou null.</summary>
    public virtual string? IconPath => null;

    public bool HasIcon => IconPath is not null;

    /// <summary>Problèmes de la dernière validation ("OK", "2 avertissements"...).</summary>
    public string IssueLabel => (_errorCount, _warningCount) switch
    {
        (0, 0) => "OK",
        (> 0, _) => _errorCount == 1 ? Tr.T("1 error") : Tr.F("{0} errors", _errorCount),
        _ => _warningCount == 1 ? Tr.T("1 warning") : Tr.F("{0} warnings", _warningCount),
    };

    public bool HasIssueErrors => _errorCount > 0;

    /// <summary>Gravité pour le tri : 2 erreurs, 1 avertissements, 0 aucun problème.</summary>
    public int IssueRank => _errorCount > 0 ? 2 : _warningCount > 0 ? 1 : 0;

    public bool HasIssueWarnings => _errorCount == 0 && _warningCount > 0;

    public int UsageCount => Context.Project.Assets.FindReferencesTo(Model.Id).Select(r => r.Source).Distinct().Count();

    /// <summary>Résultat de la validation pour cet asset (appelé après chaque validation).</summary>
    public void SetIssues(int errors, int warnings)
    {
        if (_errorCount == errors && _warningCount == warnings)
        {
            return;
        }

        _errorCount = errors;
        _warningCount = warnings;
        OnPropertyChanged(nameof(IssueLabel));
        OnPropertyChanged(nameof(HasIssueErrors));
        OnPropertyChanged(nameof(HasIssueWarnings));
    }

    /// <summary>Faux pour les assets qui n'existent pas en jeu sous leur identifiant (textures).</summary>
    public virtual bool HasGameId => true;

    public string ResourceIdRule => Tr.T("Lowercase letters, digits and '_'; starts with a letter.");

    /// <summary>Assets qui utilisent celui-ci : renommer cet asset ne casse pas ces liens.</summary>
    public IReadOnlyList<AssetLinkViewModel> UsedBy =>
        Context.Project.Assets.FindReferencesTo(Model.Id)
            .GroupBy(r => r.Source)
            .Select(g => new AssetLinkViewModel(g.Key.Id, Tr.F("{0}: {1}", DisplayLabel(g.Key), string.Join(", ", g.Select(r => r.Reference.Role).Distinct()))))
            .ToList();

    public bool IsUsed => UsedBy.Count > 0;

    /// <summary>Elements this one uses (texture, item, called function...), for the summary of Properties.</summary>
    public IReadOnlyList<AssetLinkViewModel> Uses =>
        Model.GetReferences()
            .GroupBy(r => r.TargetId)
            .Select(g => (Target: Context.Project.Assets.Find(g.Key), Roles: g.Select(r => r.Role).Distinct()))
            .Where(t => t.Target is not null)
            .Select(t => new AssetLinkViewModel(t.Target!.Id, Tr.F("{0}: {1}", DisplayLabel(t.Target!), string.Join(", ", t.Roles))))
            .ToList();

    public bool HasUses => Uses.Count > 0;

    /// <summary>Main facts of the element shown in Properties when it is selected in the Dashboard.</summary>
    public virtual IReadOnlyList<SummaryLine> SummaryLines => [];

    /// <summary>Ouvre un autre asset (paramètre : son identifiant interne).</summary>
    public ICommand OpenAssetCommand { get; }

    protected AssetEditingContext Context { get; }

    /// <summary>Utilisé par WPF pour le nom accessible et la recherche au clavier dans la liste.</summary>
    public override string ToString() => Header;

    public void Dispose() => Model.Changed -= OnModelChanged;

    /// <summary>
    /// Appelé quand un autre asset du projet change (ajout, suppression, renommage) :
    /// les listes de choix et les liens affichés sont recalculés.
    /// </summary>
    public virtual void OnProjectContentChanged()
    {
        OnPropertyChanged(nameof(UsedBy));
        OnPropertyChanged(nameof(IsUsed));
        OnPropertyChanged(nameof(UsageCount));
        OnPropertyChanged(nameof(IconPath));
        OnPropertyChanged(nameof(HasIcon));
    }

    /// <summary>Nom affiché d'un asset dans les listes : nom en jeu, sinon identifiant.</summary>
    protected static string DisplayLabel(Asset asset) =>
        string.IsNullOrWhiteSpace(asset.DisplayName) ? asset.ResourceId.Value : $"{asset.DisplayName} ({asset.ResourceId})";

    /// <summary>Applique une modification à l'asset au travers de l'historique.</summary>
    protected void Change<T>(string description, string propertyName, Func<T> getter, Action<T> setter, T newValue) =>
        Context.History.Execute(new PropertyChangeCommand<T>(description, Model, propertyName, getter, setter, newValue));

    /// <summary>Applique la modification seulement si la valeur change (cases à cocher, listes).</summary>
    protected void ChangeIfDifferent<T>(string description, string propertyName, Func<T> getter, Action<T> setter, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(getter(), value))
        {
            Change(description, propertyName, getter, setter, value);
            Context.History.Seal();
        }
    }

    /// <summary>Modification d'un nombre saisi dans un champ numérique (NaN = champ vidé).</summary>
    protected void ChangeNumber<T>(string description, string propertyName, double value, Func<T> getter, Action<T> setter, Func<double, T> convert)
    {
        if (double.IsNaN(value))
        {
            OnPropertyChanged(propertyName);
            return;
        }

        T converted = convert(value);
        if (!EqualityComparer<T>.Default.Equals(getter(), converted))
        {
            Change(description, propertyName, getter, setter, converted);
        }
    }

    /// <summary>Réagit à un changement d'une propriété de l'asset (saisie, annulation, rétablissement).</summary>
    protected virtual void OnModelPropertyChanged(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(Asset.Folder):
                OnPropertyChanged(nameof(FolderLabel));
                break;

            case nameof(Asset.DisplayName):
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(Header));
                OnPropertyChanged(nameof(DetailsTitle));
                break;

            case nameof(Asset.ResourceId):
                if (_resourceIdText != Model.ResourceId.Value)
                {
                    _resourceIdText = Model.ResourceId.Value;
                    ClearErrors(nameof(ResourceIdText));
                    OnPropertyChanged(nameof(ResourceIdText));
                }

                OnPropertyChanged(nameof(FullResourceId));
                OnPropertyChanged(nameof(Header));
                OnPropertyChanged(nameof(DetailsTitle));
                OnPropertyChanged(nameof(DetailsSubtitle));
                break;
        }
    }

    private void OnModelChanged(object? sender, AssetChangedEventArgs e) => OnModelPropertyChanged(e.PropertyName);

    private void ApplyResourceId(string text)
    {
        if (!ResourceId.TryParse(text, out ResourceId? id))
        {
            SetError(nameof(ResourceIdText), Tr.F("Invalid identifier: {0}.", ResourceId.RuleDescription));
            return;
        }

        if (!Context.Project.Assets.IsResourceIdAvailable(id!, Model.Type, Model))
        {
            SetError(nameof(ResourceIdText), Tr.F("The identifier '{0}' is already used by another asset.", id));
            return;
        }

        ClearErrors(nameof(ResourceIdText));
        if (Model.ResourceId != id)
        {
            Change(Tr.T("Change identifier"), nameof(Asset.ResourceId), () => Model.ResourceId, v => Model.ResourceId = v, id!);
        }
    }
}
