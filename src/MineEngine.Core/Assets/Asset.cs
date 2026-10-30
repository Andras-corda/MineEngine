using System.Runtime.CompilerServices;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Core.Assets;

public abstract class Asset
{
    private ResourceId _resourceId; // id
    private string _displayName; // nom d'affichage
    private string _folder = string.Empty; // chemin d'acces du dossier

    protected Asset(Guid id, ResourceId resourceId, string displayName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(Tr.T("The internal identifier of an asset cannot be empty."), nameof(id));
        }

        Id = id;
        _resourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        _displayName = displayName ?? string.Empty;
    }

    public event EventHandler<AssetChangedEventArgs>? Changed; // Declenche apres chaque modification d'une propriete

    public Guid Id { get; }

    public abstract AssetType Type { get; }

    public ResourceId ResourceId
    {
        get => _resourceId;
        set => SetField(ref _resourceId, value ?? throw new ArgumentNullException(nameof(value))); // si value !null on garde sinon exception
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value ?? string.Empty); // si value !null on garde sinon chaine vide
    }

    public string Folder
    {
        get => _folder;
        set => SetField(
            ref _folder,
            AssetFolders.Normalize(value)
                ?? throw new ArgumentException(
                    Tr.F("Invalid folder path: '{0}'.", value),
                    nameof(value)
                )
        );
    }

    // Vérifie les asset et ajoute les problemes trouve
    public virtual void Validate(DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            diagnostics.Warning(Tr.T("The display name is empty. The identifier will be used in game."), ToString(), Id);
        }
    }

    public virtual IEnumerable<AssetReference> GetReferences() => []; // Liens de cet asset vers d'autres assets du projet (textures, items, sons)

    public override string ToString() => $"{Type} '{ResourceId}'";

    // Signale la modification d'une propriete qui n'est pas stockee dans un simple champ
    protected void RaiseChanged(string propertyName) => Changed?.Invoke(this, new AssetChangedEventArgs(propertyName));

    /// <summary>
    /// Updates a field if its value has changed and raises the Changed event
    /// </summary>
    /// <typeparam name="T">The type of the field.</typeparam>
    /// <param name="field">A reference to the field to update.</param>
    /// <param name="value">The new value to assign to the field.</param>
    /// <param name="propertyName">The name of the calling property, automatically provided by the compile.</param>
    /// <returns>
    /// True if the field was updated; otherwise, false.
    /// </returns>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Changed?.Invoke(this, new AssetChangedEventArgs(propertyName));
        return true;
    }
}
