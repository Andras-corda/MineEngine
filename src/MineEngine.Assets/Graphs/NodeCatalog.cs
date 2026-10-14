using MineEngine.Core.GameData;

namespace MineEngine.Assets.Graphs;

/// <summary>
/// Les nœuds disponibles dans l'éditeur nodal (V0.4, volontairement réduit) : quatre
/// événements, des conditions, des actions et des valeurs. Le générateur Java traduit
/// chacun d'eux ; ajouter un nœud revient à le décrire ici puis à le traduire.
/// </summary>
public static class NodeCatalog
{
    private static readonly PinInfo Player = new("player", "Joueur", PinType.Player);
    private static readonly PinInfo Position = new("position", "Position", PinType.Position);

    private static readonly Dictionary<GraphNodeKind, NodeInfo> Nodes = new NodeInfo[]
    {
        // ----- Événements -----
        new(GraphNodeKind.OnRightClickBlock, NodeCategory.Event, "Clic droit sur un bloc",
            "Un joueur fait un clic droit sur un bloc (main principale).",
            outputs: [Player, new PinInfo("position", "Position du bloc", PinType.Position)]) { IsCancellable = true },
        new(GraphNodeKind.OnUseItem, NodeCategory.Event, "Utilisation d'un item",
            "Un joueur fait un clic droit dans le vide avec un item en main.",
            outputs: [Player, new PinInfo("position", "Position du joueur", PinType.Position)]) { IsCancellable = true },
        new(GraphNodeKind.OnPlayerJoin, NodeCategory.Event, "Connexion d'un joueur",
            "Un joueur rejoint le monde ou le serveur.",
            outputs: [Player, new PinInfo("position", "Position du joueur", PinType.Position)]),
        new(GraphNodeKind.OnBlockBroken, NodeCategory.Event, "Bloc cassé",
            "Un joueur casse un bloc.",
            outputs: [Player, new PinInfo("position", "Position du bloc", PinType.Position)]) { IsCancellable = true },

        // ----- Événements d'un item du mod -----
        ItemEvent(GraphNodeKind.OnItemRightClick, ItemEventKind.RightClick, "Item : clic droit",
            "Le joueur fait un clic droit dans le vide avec l'item.", "Position du joueur"),
        ItemEvent(GraphNodeKind.OnItemUseOnBlock, ItemEventKind.UseOnBlock, "Item : utilisé sur un bloc",
            "Le joueur fait un clic droit sur un bloc avec l'item.", "Position du bloc"),
        ItemEvent(GraphNodeKind.OnItemHitEntity, ItemEventKind.HitEntity, "Item : frappe une créature",
            "Le joueur frappe une créature avec l'item.", "Position de la créature"),
        ItemEvent(GraphNodeKind.OnItemBlockMined, ItemEventKind.BlockMined, "Item : bloc cassé avec",
            "Le joueur casse un bloc en tenant l'item.", "Position du bloc"),
        ItemEvent(GraphNodeKind.OnItemConsumed, ItemEventKind.Consumed, "Item : mangé ou bu",
            "Le joueur finit de manger ou de boire l'item (item comestible).", "Position du joueur"),
        ItemEvent(GraphNodeKind.OnItemCrafted, ItemEventKind.Crafted, "Item : fabriqué",
            "Le joueur fabrique l'item.", "Position du joueur"),
        ItemEvent(GraphNodeKind.OnItemHeldTick, ItemEventKind.HeldTick, "Item : tenu en main",
            "À chaque tick (20 fois par seconde) tant que le joueur tient l'item.", "Position du joueur"),

        // ----- Conditions -----
        new(GraphNodeKind.Branch, NodeCategory.Condition, "Si",
            "Continue par Vrai ou par Faux selon une condition.",
            inputs: [new PinInfo("condition", "Condition", PinType.Boolean, "true")]),
        new(GraphNodeKind.HasItem, NodeCategory.Condition, "Possède l'item",
            "Vrai si l'inventaire du joueur contient au moins la quantité indiquée.",
            inputs: [Player, new PinInfo("item", "Item", PinType.Item, "minecraft:diamond"), new PinInfo("count", "Quantité", PinType.Number, "1")]),
        new(GraphNodeKind.HoldsItem, NodeCategory.Condition, "Tient l'item",
            "Vrai si le joueur tient cet item dans sa main principale.",
            inputs: [Player, new PinInfo("item", "Item", PinType.Item, "minecraft:stick")]),
        new(GraphNodeKind.IsSneaking, NodeCategory.Condition, "Est accroupi",
            "Vrai si le joueur est accroupi (touche Maj).",
            inputs: [Player]),
        new(GraphNodeKind.Chance, NodeCategory.Condition, "Chance",
            "Vrai au hasard, avec la probabilité indiquée en pourcentage.",
            inputs: [new PinInfo("percent", "Pourcentage", PinType.Number, "50")]),

        // ----- Actions -----
        new(GraphNodeKind.SpawnItem, NodeCategory.Action, "Faire apparaître un item",
            "Fait apparaître un item au sol, juste au-dessus de la position.",
            inputs: [Position, new PinInfo("item", "Item", PinType.Item, "minecraft:diamond"), new PinInfo("count", "Quantité", PinType.Number, "1")]),
        new(GraphNodeKind.GiveItem, NodeCategory.Action, "Donner un item",
            "Ajoute un item à l'inventaire du joueur (il tombe au sol si l'inventaire est plein).",
            inputs: [Player, new PinInfo("item", "Item", PinType.Item, "minecraft:diamond"), new PinInfo("count", "Quantité", PinType.Number, "1")]),
        new(GraphNodeKind.RemoveItem, NodeCategory.Action, "Retirer un item",
            "Retire au plus la quantité indiquée de l'inventaire du joueur.",
            inputs: [Player, new PinInfo("item", "Item", PinType.Item, "minecraft:diamond"), new PinInfo("count", "Quantité", PinType.Number, "1")]),
        new(GraphNodeKind.SendMessage, NodeCategory.Action, "Envoyer un message",
            "Affiche un message dans le chat du joueur.",
            inputs: [Player, new PinInfo("message", "Message", PinType.Text, "Bonjour !")]),
        new(GraphNodeKind.PlaySound, NodeCategory.Action, "Jouer un son",
            "Joue un son à la position, entendu par les joueurs proches.",
            inputs: [Position, new PinInfo("sound", "Son", PinType.Sound, "minecraft:entity.experience_orb.pickup")]),
        new(GraphNodeKind.SetBlock, NodeCategory.Action, "Placer un bloc",
            "Remplace le bloc à la position.",
            inputs: [Position, new PinInfo("block", "Bloc", PinType.Block, "minecraft:stone")]),
        new(GraphNodeKind.Explosion, NodeCategory.Action, "Explosion",
            "Provoque une explosion à la position (TNT : puissance 4).",
            inputs: [Position, new PinInfo("power", "Puissance", PinType.Number, "2")]),
        new(GraphNodeKind.CancelEvent, NodeCategory.Action, "Annuler l'événement",
            "Empêche l'action du joueur (le bloc n'est pas cassé, le clic n'a pas d'effet)."),

        // ----- Valeurs -----
        new(GraphNodeKind.NumberValue, NodeCategory.Variable, "Nombre", "Un nombre entier.",
            inputs: [new PinInfo("value", "Valeur", PinType.Number, "0", Connectable: false)],
            outputs: [new PinInfo("value", "Nombre", PinType.Number)]),
        new(GraphNodeKind.TextValue, NodeCategory.Variable, "Texte", "Un texte.",
            inputs: [new PinInfo("value", "Valeur", PinType.Text, string.Empty, Connectable: false)],
            outputs: [new PinInfo("value", "Texte", PinType.Text)]),
        new(GraphNodeKind.BooleanValue, NodeCategory.Variable, "Booléen", "Vrai ou faux.",
            inputs: [new PinInfo("value", "Valeur", PinType.Boolean, "true", Connectable: false)],
            outputs: [new PinInfo("value", "Booléen", PinType.Boolean)]),
        new(GraphNodeKind.PlayerPosition, NodeCategory.Variable, "Position du joueur",
            "Le bloc où se tiennent les pieds du joueur.",
            inputs: [Player],
            outputs: [new PinInfo("position", "Position", PinType.Position)]),
        new(GraphNodeKind.PlayerName, NodeCategory.Variable, "Nom du joueur", "Le pseudo du joueur.",
            inputs: [Player],
            outputs: [new PinInfo("name", "Nom", PinType.Text)]),
        new(GraphNodeKind.OffsetPosition, NodeCategory.Variable, "Décaler une position",
            "Ajoute un décalage en blocs (Y vers le haut).",
            inputs: [Position, new PinInfo("dx", "X", PinType.Number, "0"), new PinInfo("dy", "Y", PinType.Number, "1"), new PinInfo("dz", "Z", PinType.Number, "0")],
            outputs: [new PinInfo("position", "Position", PinType.Position)]),
        new(GraphNodeKind.RandomNumber, NodeCategory.Variable, "Nombre aléatoire",
            "Un nombre entier au hasard entre le minimum et le maximum inclus.",
            inputs: [new PinInfo("min", "Minimum", PinType.Number, "1"), new PinInfo("max", "Maximum", PinType.Number, "6")],
            outputs: [new PinInfo("value", "Nombre", PinType.Number)]),
        new(GraphNodeKind.JoinText, NodeCategory.Variable, "Joindre des textes",
            "Met deux textes bout à bout (un nombre est converti en texte).",
            inputs: [new PinInfo("first", "Début", PinType.Text, string.Empty), new PinInfo("second", "Fin", PinType.Text, string.Empty)],
            outputs: [new PinInfo("text", "Texte", PinType.Text)]),
        new(GraphNodeKind.GreaterThan, NodeCategory.Variable, "Plus grand que (>)",
            "Vrai si A est strictement plus grand que B.",
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", "Résultat", PinType.Boolean)]),
        new(GraphNodeKind.NumbersEqual, NodeCategory.Variable, "Égal à (==)",
            "Vrai si les deux nombres sont égaux.",
            inputs: [new PinInfo("a", "A", PinType.Number, "0"), new PinInfo("b", "B", PinType.Number, "0")],
            outputs: [new PinInfo("result", "Résultat", PinType.Boolean)]),
        new(GraphNodeKind.Not, NodeCategory.Variable, "Non (!)",
            "Inverse une condition.",
            inputs: [new PinInfo("value", "Condition", PinType.Boolean, "true")],
            outputs: [new PinInfo("result", "Résultat", PinType.Boolean)]),
    }.ToDictionary(n => n.Kind);

    /// <summary>Plus grande valeur acceptée pour un nombre saisi sur un nœud.</summary>
    public const int MaxNumber = 1_000_000;

    public static IReadOnlyCollection<NodeInfo> All => Nodes.Values;

    public static NodeInfo Get(GraphNodeKind kind) =>
        Nodes.TryGetValue(kind, out NodeInfo? info) ? info : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nœud inconnu.");

    /// <summary>Nœud de l'événement d'un item, par sorte d'événement.</summary>
    public static NodeInfo ForItemEvent(ItemEventKind kind) => Nodes.Values.First(n => n.ItemEvent == kind);

    private static NodeInfo ItemEvent(GraphNodeKind kind, ItemEventKind itemEvent, string title, string description, string positionLabel) =>
        new(kind, NodeCategory.Event, title, description,
            inputs: [new PinInfo("item", "Item du mod", PinType.Item, string.Empty, Connectable: false)],
            outputs: [Player, new PinInfo("position", positionLabel, PinType.Position)]) { ItemEvent = itemEvent };

    public static string CategoryLabel(NodeCategory category) => category switch
    {
        NodeCategory.Event => "Événements",
        NodeCategory.Condition => "Conditions",
        NodeCategory.Action => "Actions",
        NodeCategory.Variable => "Valeurs",
        _ => category.ToString(),
    };
}
