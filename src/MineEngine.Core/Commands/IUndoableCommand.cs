namespace MineEngine.Core.Commands;

/// <summary>
/// Une modification des données qui sait s'annuler. Toute modification faite
/// depuis l'éditeur passe par une commande enregistrée dans <see cref="UndoHistory"/>.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Texte affiché dans les menus ("Modifier le nom affiché").</summary>
    string Description { get; }

    void Execute();

    void Undo();

    /// <summary>
    /// Tente d'absorber la commande suivante (par exemple plusieurs frappes dans le
    /// même champ) pour qu'un seul Annuler les défasse toutes. Vrai si absorbée.
    /// </summary>
    bool TryMerge(IUndoableCommand next);
}
