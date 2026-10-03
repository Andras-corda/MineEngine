using System.Runtime.CompilerServices;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Core.Assets;

/// <summary>
/// Élément de contenu d'un projet (item, bloc...). Chaque asset possède un
/// identifiant interne stable (<see cref="Id"/>) et un identifiant Minecraft
/// renommable (<see cref="ResourceId"/>).
/// </summary>
public abstract class Asset
{
    private ResourceId _resourceId;
    private string _displayName;

    protected Asset(Guid id, ResourceId resourceId, string displayName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("L'identifiant interne d'un asset ne peut pas être vide.", nameof(id));
        }

        Id = id;
        _resourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        _displayName = displayName ?? string.Empty;
    }

    /// <summary>Déclenché après chaque modification d'une propriété.</summary>
    public event EventHandler<AssetChangedEventArgs>? Changed;

    public Guid Id { get; }

    public abstract AssetType Type { get; }

    public ResourceId ResourceId
    {
        get => _resourceId;
        set => SetField(ref _resourceId, value ?? throw new ArgumentNullException(nameof(value)));
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value ?? string.Empty);
    }

    /// <summary>Vérifie la cohérence de l'asset et ajoute les problèmes trouvés.</summary>
    public virtual void Validate(DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            diagnostics.Warning("Le nom affiché est vide ; l'identifiant sera utilisé en jeu.", ToString(), Id);
        }
    }

    /// <summary>Liens de cet asset vers d'autres assets du projet (textures, items, sons).</summary>
    public virtual IEnumerable<AssetReference> GetReferences() => [];

    public override string ToString() => $"{Type} '{ResourceId}'";

    /// <summary>Signale la modification d'une propriété qui n'est pas stockée dans un simple champ.</summary>
    protected void RaiseChanged(string propertyName) => Changed?.Invoke(this, new AssetChangedEventArgs(propertyName));

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
