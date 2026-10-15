using MineEngine.Core.GameData;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Les nœuds disponibles dans l'éditeur nodal (V0.4, volontairement réduit) : quatre
/// événements, des conditions, des actions et des valeurs. Le générateur Java traduit
/// chacun d'eux ; ajouter un nœud revient à le décrire ici puis à le traduire.
/// </summary>
public static class NodeCatalog
{
    private static readonly PinInfo Player = new("player", Tr.T("Joueur"), PinType.Player);
    private static readonly PinInfo Position = new("position", Tr.T("Position"), PinType.Position);

    private static readonly Dictionary<GraphNodeKind, NodeInfo> Nodes = new NodeInfo[]
    {
        // ----- Événements -----
        new(GraphNodeKind.OnRightClickBlock, NodeCategory.Event, Tr.T("Clic droit sur un bloc"),
            Tr.T("Un joueur fait un clic droit sur un bloc (main principale)."),
            outputs: [Player, new PinInfo("position", Tr.T("Position du bloc"), PinType.Position)]) { IsCancellable = true },
        new(GraphNodeKind.OnUseItem, NodeCategory.Event, Tr.T("Utilisation d'un item"),
            Tr.T("Un joueur fait un clic droit dans le vide avec un item en main."),
            outputs: [Player, new PinInfo("position", Tr.T("Position du joueur"), PinType.Position)]) { IsCancellable = true },
        new(GraphNodeKind.OnPlayerJoin, NodeCategory.Event, Tr.T("Connexion d'un joueur"),
            Tr.T("Un joueur rejoint le monde ou le serveur."),
            outputs: [Player, new PinInfo("position", Tr.T("Position du joueur"), PinType.Position)]),
        new(GraphNodeKind.OnBlockBroken, NodeCategory.Event, Tr.T("Bloc cassé"),
            Tr.T("Un joueur casse un bloc."),
            outputs: [Player, new PinInfo("position", Tr.T("Position du bloc"), PinType.Position)]) { IsCancellable = true },

        // ----- Événements d'un item du mod -----
        ItemEvent(GraphNodeKind.OnItemRightClick, ItemEventKind.RightClick, Tr.T("Item : clic droit"),
            Tr.T("Le joueur fait un clic droit dans le vide avec l'item."), Tr.T("Position du joueur")),
        ItemEvent(GraphNodeKind.OnItemUseOnBlock, ItemEventKind.UseOnBlock, Tr.T("Item : utilisé sur un bloc"),
            Tr.T("Le joueur fait un clic droit sur un bloc avec l'item."), Tr.T("Position du bloc")),
        ItemEvent(GraphNodeKind.OnItemHitEntity, ItemEventKind.HitEntity, Tr.T("Item : frappe une créature"),
            Tr.T("Le joueur frappe une créature avec l'item."), Tr.T("Position de la créature")),
        ItemEvent(GraphNodeKind.OnItemBlockMined, ItemEventKind.BlockMined, Tr.T("Item : bloc cassé avec"),
            Tr.T("Le joueur casse un bloc en tenant l'item."), Tr.T("Position du bloc")),
        ItemEvent(GraphNodeKind.OnItemConsumed, ItemEventKind.Consumed, Tr.T("Item : mangé ou bu"),
            Tr.T("Le joueur finit de manger ou de boire l'item (item comestible)."), Tr.T("Position du joueur")),
        ItemEvent(GraphNodeKind.OnItemCrafted, ItemEventKind.Crafted, Tr.T("Item : fabriqué"),
            Tr.T("Le joueur fabrique l'item."), Tr.T("Position du joueur")),
        ItemEvent(GraphNodeKind.OnItemHeldTick, ItemEventKind.HeldTick, Tr.T("Item : tenu en main"),
            Tr.T("À chaque tick (20 fois par seconde) tant que le joueur tient l'item."), Tr.T("Position du joueur")),

        // ----- Conditions -----
        new(GraphNodeKind.Branch, NodeCategory.Condition, Tr.T("Si"),
            Tr.T("Continue par Vrai ou par Faux selon une condition."),
            inputs: [new PinInfo("condition", Tr.T("Condition"), PinType.Boolean, "true")]),
        new(GraphNodeKind.HasItem, NodeCategory.Condition, Tr.T("Possède l'item"),
            Tr.T("Vrai si l'inventaire du joueur contient au moins la quantité indiquée."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantité"), PinType.Number, "1")]),
        new(GraphNodeKind.HoldsItem, NodeCategory.Condition, Tr.T("Tient l'item"),
            Tr.T("Vrai si le joueur tient cet item dans sa main principale."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:stick")]),
        new(GraphNodeKind.IsSneaking, NodeCategory.Condition, Tr.T("Est accroupi"),
            Tr.T("Vrai si le joueur est accroupi (touche Maj)."),
            inputs: [Player]),
        new(GraphNodeKind.Chance, NodeCategory.Condition, Tr.T("Chance"),
            Tr.T("Vrai au hasard, avec la probabilité indiquée en pourcentage."),
            inputs: [new PinInfo("percent", Tr.T("Pourcentage"), PinType.Number, "50")]),

        // ----- Actions -----
        new(GraphNodeKind.SpawnItem, NodeCategory.Action, Tr.T("Faire apparaître un item"),
            Tr.T("Fait apparaître un item au sol, juste au-dessus de la position."),
            inputs: [Position, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantité"), PinType.Number, "1")]),
        new(GraphNodeKind.GiveItem, NodeCategory.Action, Tr.T("Donner un item"),
            Tr.T("Ajoute un item à l'inventaire du joueur (il tombe au sol si l'inventaire est plein)."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantité"), PinType.Number, "1")]),
        new(GraphNodeKind.RemoveItem, NodeCategory.Action, Tr.T("Retirer un item"),
            Tr.T("Retire au plus la quantité indiquée de l'inventaire du joueur."),
            inputs: [Player, new PinInfo("item", Tr.T("Item"), PinType.Item, "minecraft:diamond"), new PinInfo("count", Tr.T("Quantité"), PinType.Number, "1")]),
        new(GraphNodeKind.SendMessage, NodeCategory.Action, Tr.T("Envoyer un message"),
            Tr.T("Affiche un message dans le chat du joueur."),
            inputs: [Player, new PinInfo("message", Tr.T("Message"), PinType.Text, Tr.T("Bonjour !"))]),
        new(GraphNodeKind.PlaySound, NodeCategory.Action, Tr.T("Jouer un son"),
            Tr.T("Joue un son à la position, entendu par les joueurs proches."),
            inputs: [Position, new PinInfo("sound", Tr.T("Son"), PinType.Sound, "minecraft:entity.experience_orb.pickup")]),
        new(GraphNodeKind.SetBlock, NodeCategory.Action, Tr.T("Placer un bloc"),
            Tr.T("Remplace le bloc à la position."),
            inputs: [Position, new PinInfo("block", Tr.T("Bloc"), PinType.Block, "minecraft:stone")]),
        new(GraphNodeKind.Explosion, NodeCategory.Action, Tr.T("Explosion"),
            Tr.T("Provoque une explosion à la position (TNT : puissance 4)."),
            inputs: [Position, new PinInfo("power", Tr.T("Puissance"), PinType.Number, "2")]),
        new(GraphNodeKind.CancelEvent, NodeCategory.Action, Tr.T("Annuler l'événement"),
            Tr.T("Empêche l'action du joueur (le bloc n'est pas cassé, le clic n'a pas d'effet).")),

        // ----- Valeurs -----
        new(GraphNodeKind.NumberValue, NodeCategory.Variable, Tr.T("Nombre"), Tr.T("Un nombre entier."),
            inputs: [new PinInfo("value", Tr.T("Valeur"), PinType.Number, "0", Connectable: false)],
            outputs: [new PinInfo("value", Tr.T("Nombre"), PinType.Number)]),
        new(GraphNodeKind.TextValue, NodeCategory.Variable, Tr.T("Texte"), Tr.T("Un texte."),
            inputs: [new PinInfo("value", Tr.T("Valeur"), PinType.Text, string.Empty, Connectable: false)],
            outputs: [new PinInfo("value", Tr.T("Texte"), PinType.Text)]),
        new(GraphNodeKind.BooleanValue, NodeCategory.Variable, Tr.T("Booléen"), Tr.T("Vrai ou faux."),
            inputs: [new PinInfo("value", Tr.T("Valeur"), PinType.Boolean, "true", Connectable: false)],
            outputs: [new PinInfo("value", Tr.T("Booléen"), PinType.Boolean)]),
        new(GraphNodeKind.PlayerPosition, NodeCategory.Variable, Tr.T("Position du joueur"),
            Tr.T("Le bloc où se tiennent les pieds du joueur."),
            inputs: [Player],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.PlayerName, NodeCategory.Variable, Tr.T("Nom du joueur"), Tr.T("Le pseudo du joueur."),
            inputs: [Player],
            outputs: [new PinInfo("name", Tr.T("Nom"), PinType.Text)]),
        new(GraphNodeKind.OffsetPosition, NodeCategory.Variable, Tr.T("Décaler une position"),
            Tr.T("Ajoute un décalage en blocs (Y vers le haut)."),
            inputs: [Position, new PinInfo("dx", "X", PinType.Number, "0"), new PinInfo("dy", "Y", PinType.Number, "1"), new PinInfo("dz", "Z", PinType.Number, "0")],
            outputs: [new PinInfo("position", Tr.T("Position"), PinType.Position)]),
        new(GraphNodeKind.RandomNumber, NodeCategory.Variable, Tr.T("Nombre aléatoire"),
            Tr.T("Un nombre entier au hasard entre le minimum et le maximum inclus."),
            inputs: [new PinInfo("min", Tr.T("Minimum"), PinType.Number, "1"), new PinInfo("max", Tr.T("Maximum"), PinType.Number, "6")],
            outputs: [new PinInfo("value", Tr.T("Nombre"), PinType.Number)]),
        new(GraphNodeKind.JoinText, NodeCategory.Variable, Tr.T("Joindre des textes"),
            Tr.T("Met deux textes bout à bout (un nombre est converti en texte)."),
            inputs: [new PinInfo("first", Tr.T("Début"), PinType.Text, string.Empty), new PinInfo("second", Tr.T("Fin"), PinType.Text, string.Empty)],
            outputs: [new PinInfo("text", Tr.T("Texte"), PinType.Text)]),
        new(GraphNodeKind.GreaterThan, NodeCategory.Variable, Tr.T("Plus grand que (>)"),
            Tr.T("Vrai si A est strictement plus grand que B."),
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Résultat"), PinType.Boolean)]),
        new(GraphNodeKind.NumbersEqual, NodeCategory.Variable, Tr.T("Égal à (==)"),
            Tr.T("Vrai si les deux nombres sont égaux."),
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Résultat"), PinType.Boolean)]),
        Arithmetic(GraphNodeKind.Add, Tr.T("Addition (+)"), Tr.T("A plus B.")),
        Arithmetic(GraphNodeKind.Subtract, Tr.T("Soustraction (-)"), Tr.T("A moins B.")),
        Arithmetic(GraphNodeKind.Multiply, Tr.T("Multiplication (×)"), Tr.T("A fois B.")),
        new(GraphNodeKind.LessThan, NodeCategory.Variable, Tr.T("Plus petit que (<)"),
            Tr.T("Vrai si A est strictement plus petit que B."),
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Résultat"), PinType.Boolean)]),
        new(GraphNodeKind.Not, NodeCategory.Variable, Tr.T("Non (!)"),
            Tr.T("Inverse une condition."),
            inputs: [new PinInfo("value", Tr.T("Condition"), PinType.Boolean, "true")],
            outputs: [new PinInfo("result", Tr.T("Résultat"), PinType.Boolean)]),
    }.ToDictionary(n => n.Kind);

    /// <summary>Plus grande valeur acceptée pour un nombre saisi sur un nœud.</summary>
    public const int MaxNumber = 1_000_000;

    /// <summary>Nœuds du catalogue ; les nœuds de programmation (appels, objets...) sont décrits par ProgramModel.</summary>
    public static IReadOnlyCollection<NodeInfo> All => Nodes.Values;

    public static NodeInfo Get(GraphNodeKind kind) =>
        Nodes.TryGetValue(kind, out NodeInfo? info) ? info : throw new ArgumentOutOfRangeException(nameof(kind), kind, Tr.T("Nœud inconnu."));

    /// <summary>Description d'un nœud du catalogue, ou null pour un nœud de programmation.</summary>
    public static NodeInfo? Find(GraphNodeKind kind) => Nodes.GetValueOrDefault(kind);

    /// <summary>Nœud dont la définition n'existe plus : sans broches, il est signalé en erreur.</summary>
    public static NodeInfo Missing(GraphNodeKind kind, string title) =>
        new(kind, NodeCategory.Object, title, Tr.T("La définition utilisée par ce nœud a été supprimée ou n'est pas accessible ici."),
            flow: NodeFlow.Pure) { IsMissing = true };

    /// <summary>Nœud de l'événement d'un item, par sorte d'événement.</summary>
    public static NodeInfo ForItemEvent(ItemEventKind kind) => Nodes.Values.First(n => n.ItemEvent == kind);

    private static NodeInfo Arithmetic(GraphNodeKind kind, string title, string description) =>
        new(kind, NodeCategory.Variable, title, description,
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", Tr.T("Résultat"), PinType.Number)]);

    private static NodeInfo ItemEvent(GraphNodeKind kind, ItemEventKind itemEvent, string title, string description, string positionLabel) =>
        new(kind, NodeCategory.Event, title, description,
            inputs: [new PinInfo("item", Tr.T("Item du mod"), PinType.Item, string.Empty, Connectable: false)],
            outputs: [Player, new PinInfo("position", positionLabel, PinType.Position)]) { ItemEvent = itemEvent };

    public static string CategoryLabel(NodeCategory category) => category switch
    {
        NodeCategory.Event => Tr.T("Événements"),
        NodeCategory.Condition => Tr.T("Conditions"),
        NodeCategory.Action => Tr.T("Actions"),
        NodeCategory.Variable => Tr.T("Valeurs"),
        NodeCategory.Function => Tr.T("Fonctions"),
        NodeCategory.Object => Tr.T("Objets et variables"),
        _ => category.ToString(),
    };
}
