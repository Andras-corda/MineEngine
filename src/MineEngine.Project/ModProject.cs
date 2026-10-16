using MineEngine.Assets;
using MineEngine.Core.Commands;
using MineEngine.Project.Localization;

namespace MineEngine.Project;

/// <summary>Un projet de mod ouvert dans Mine Engine.</summary>
public sealed class ModProject
{
    private bool _isDirty;
    private ModLocalization _localization = ModLocalization.Default;

    public ModProject(ProjectLayout layout, ProjectSettings settings, AssetRegistry assets)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        Assets.Changed += (_, _) => MarkDirty();
        History.Changed += (_, _) => MarkDirty();
    }

    /// <summary>Déclenché quand le projet passe de "enregistré" à "modifié" ou inversement.</summary>
    public event EventHandler? DirtyStateChanged;

    /// <summary>Déclenché quand les langues ou les traductions du mod changent (modification, annulation).</summary>
    public event EventHandler? LocalizationChanged;

    public ProjectLayout Layout { get; }

    public ProjectSettings Settings { get; }

    public AssetRegistry Assets { get; }

    /// <summary>Historique annuler/rétablir des modifications faites depuis l'ouverture.</summary>
    public UndoHistory History { get; } = new();

    public string Name => Settings.ModName;

    /// <summary>Langues du mod et traductions saisies (onglet Langues). Se modifie par l'historique.</summary>
    public ModLocalization Localization
    {
        get => _localization;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!ReferenceEquals(_localization, value))
            {
                _localization = value;
                LocalizationChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Remarques produites à l'ouverture (conversion d'un ancien format...).</summary>
    public IReadOnlyList<string> LoadNotes { get; init; } = [];

    /// <summary>Vrai si le projet contient des modifications non enregistrées.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                DirtyStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void MarkDirty() => IsDirty = true;

    public void MarkSaved() => IsDirty = false;
}
