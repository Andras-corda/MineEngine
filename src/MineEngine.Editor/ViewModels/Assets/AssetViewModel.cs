using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Propriétés communes à tous les assets, affichées dans l'Inspector.</summary>
public abstract class AssetViewModel : ValidatingObservableObject
{
    private string _resourceIdText;

    protected AssetViewModel(Asset model, AssetEditingContext context, string typeLabel)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        TypeLabel = typeLabel;
        _resourceIdText = model.ResourceId.Value;
    }

    public Asset Model { get; }

    public string TypeLabel { get; }

    /// <summary>Texte affiché dans la liste du contenu.</summary>
    public string Header => string.IsNullOrWhiteSpace(Model.DisplayName) ? Model.ResourceId.Value : Model.DisplayName;

    public string DisplayName
    {
        get => Model.DisplayName;
        set
        {
            if (Model.DisplayName != value)
            {
                Model.DisplayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Header));
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

    public string ResourceIdRule => "Minuscules, chiffres et '_' ; commence par une lettre.";

    protected AssetEditingContext Context { get; }

    /// <summary>Utilisé par WPF pour le nom accessible et la recherche au clavier dans la liste.</summary>
    public override string ToString() => Header;

    private void ApplyResourceId(string text)
    {
        if (!ResourceId.TryParse(text, out ResourceId? id))
        {
            SetError(nameof(ResourceIdText), $"Identifiant invalide : {ResourceId.RuleDescription}.");
            return;
        }

        if (!Context.Project.Assets.IsResourceIdAvailable(id!, Model))
        {
            SetError(nameof(ResourceIdText), $"L'identifiant '{id}' est déjà utilisé par un autre asset.");
            return;
        }

        ClearErrors(nameof(ResourceIdText));
        Model.ResourceId = id!;
        OnPropertyChanged(nameof(FullResourceId));
        OnPropertyChanged(nameof(Header));
    }
}
