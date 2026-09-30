namespace MineEngine.IR.Model;

/// <summary>
/// Représentation intermédiaire d'un mod : seule entrée du générateur.
/// En V0.1 elle ne contient que le contenu déclaratif (items et blocs) ;
/// la partie comportement (événements, fonctions) arrive en V0.4.
/// </summary>
public sealed class ModIR
{
    public ModIR(IRModInfo mod, IRContent content)
    {
        Mod = mod ?? throw new ArgumentNullException(nameof(mod));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public IRModInfo Mod { get; }

    public IRContent Content { get; }
}
