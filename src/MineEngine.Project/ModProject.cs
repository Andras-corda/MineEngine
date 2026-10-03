using MineEngine.Assets;
using MineEngine.Core.Commands;

namespace MineEngine.Project;

/// <summary>Un projet de mod ouvert dans Mine Engine.</summary>
public sealed class ModProject
{
    private bool _isDirty;

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

    public ProjectLayout Layout { get; }

    public ProjectSettings Settings { get; }

    public AssetRegistry Assets { get; }

    /// <summary>Historique annuler/rétablir des modifications faites depuis l'ouverture.</summary>
    public UndoHistory History { get; } = new();

    public string Name => Settings.ModName;

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
