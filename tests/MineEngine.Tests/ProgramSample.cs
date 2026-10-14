using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Programme d'exemple (V0.4.3) : énumération, structure, interface, classe (champs,
/// constructeur, méthodes, méthode statique) et classe héritière (constructeur parent,
/// redéfinition, énumération privée imbriquée), fonction pure à deux valeurs renvoyées,
/// procédure privée avec variable globale, et un graphe d'événements qui utilise le tout.
/// </summary>
internal sealed class ProgramSample
{
    private ProgramSample(ModProject project)
    {
        Project = project;
    }

    public ModProject Project { get; }

    public GraphAsset Rarity { get; private set; } = null!;

    public GraphAsset Loot { get; private set; } = null!;

    public GraphAsset Reward { get; private set; } = null!;

    public GraphAsset Chest { get; private set; } = null!;

    public GraphAsset CursedChest { get; private set; } = null!;

    public GraphAsset Curse { get; private set; } = null!;

    public GraphAsset Give { get; private set; } = null!;

    public GraphAsset Value { get; private set; } = null!;

    public GraphAsset CreateCommon { get; private set; } = null!;

    public GraphAsset CursedGive { get; private set; } = null!;

    public GraphAsset Bonus { get; private set; } = null!;

    public GraphAsset Announce { get; private set; } = null!;

    public GraphAsset Events { get; private set; } = null!;

    public const string Folder = "Programme";

    public static ProgramSample AddTo(ModProject project)
    {
        var sample = new ProgramSample(project);
        sample.Declare();
        sample.BuildBodies();
        return sample;
    }

    public ProgramModel Program => ProgramModel.From(Project.Assets);

    public static string Pin(VariableDefinition variable) => variable.Id.ToString("D");

    public static string Constant(GraphAsset enumeration, int index) => enumeration.Constants[index].Id.ToString("D");

    private static GraphAsset Graph(string id, string name, GraphKind kind, AccessLevel access = AccessLevel.Public) =>
        new(Guid.NewGuid(), ResourceId.Parse(id), name) { Kind = kind, Access = access };

    /// <summary>Signatures d'abord : les nœuds qui utilisent une définition ont besoin de la connaître.</summary>
    private void Declare()
    {
        Rarity = Graph("rarete", "Rareté", GraphKind.Enum);
        Rarity.Constants = [EnumConstant.Create("Commun"), EnumConstant.Create("Rare"), EnumConstant.Create("Légendaire")];
        DataType rarity = ProgramModel.TypeOf(Rarity);

        Loot = Graph("butin", "Butin", GraphKind.Struct);
        Loot.Variables =
        [
            VariableDefinition.Create("Item", DataType.Item) with { DefaultValue = "minecraft:diamond" },
            VariableDefinition.Create("Quantité", DataType.Number) with { DefaultValue = "1" },
            VariableDefinition.Create("Rareté", rarity),
        ];

        Reward = Graph("recompense", "Récompense", GraphKind.Interface);
        Reward.Methods =
        [
            MethodSignature.Create("donner") with { Parameters = [VariableDefinition.Create("joueur", DataType.Player)] },
            MethodSignature.Create("valeur") with { Returns = [VariableDefinition.Create("valeur", DataType.Number)] },
        ];

        Chest = Graph("coffre", "Coffre", GraphKind.Class);
        Chest.InterfaceIds = [Reward.Id];
        Chest.Parameters = [VariableDefinition.Create("rareté initiale", rarity)];
        Chest.Variables =
        [
            VariableDefinition.Create("ouvertures", DataType.Number) with { DefaultValue = "0", Access = AccessLevel.Private },
            VariableDefinition.Create("rareté", rarity) with { Access = AccessLevel.Protected },
        ];

        Give = Graph("coffre_donner", "donner", GraphKind.Procedure);
        Give.OwnerId = Chest.Id;
        Give.Parameters = [VariableDefinition.Create("joueur", DataType.Player)];

        Value = Graph("coffre_valeur", "valeur", GraphKind.Function);
        Value.OwnerId = Chest.Id;
        Value.Returns = [VariableDefinition.Create("valeur", DataType.Number)];

        CreateCommon = Graph("coffre_creer_commun", "créer commun", GraphKind.Function);
        CreateCommon.OwnerId = Chest.Id;
        CreateCommon.IsStatic = true;
        CreateCommon.Returns = [VariableDefinition.Create("coffre", ProgramModel.TypeOf(Chest))];

        Curse = Graph("coffre_maudit_malediction", "Malédiction", GraphKind.Enum, AccessLevel.Private);
        Curse.Constants = [EnumConstant.Create("Feu"), EnumConstant.Create("Poison")];

        CursedChest = Graph("coffre_maudit", "Coffre maudit", GraphKind.Class);
        CursedChest.BaseClassId = Chest.Id;
        Curse.OwnerId = CursedChest.Id;
        CursedChest.Variables = [VariableDefinition.Create("malédiction", ProgramModel.TypeOf(Curse)) with { Access = AccessLevel.Private }];

        CursedGive = Graph("coffre_maudit_donner", "donner", GraphKind.Procedure);
        CursedGive.OwnerId = CursedChest.Id;
        CursedGive.Parameters = [VariableDefinition.Create("joueur", DataType.Player)];

        Bonus = Graph("calculer_bonus", "calculer bonus", GraphKind.Function);
        Bonus.IsPure = true;
        Bonus.Parameters = [VariableDefinition.Create("base", DataType.Number), VariableDefinition.Create("multiplicateur", DataType.Number) with { DefaultValue = "2" }];
        Bonus.Returns = [VariableDefinition.Create("total", DataType.Number), VariableDefinition.Create("critique", DataType.Boolean)];

        Announce = Graph("annoncer", "annoncer", GraphKind.Procedure, AccessLevel.Private);
        Announce.Folder = Folder;
        Announce.Parameters = [VariableDefinition.Create("joueur", DataType.Player), VariableDefinition.Create("texte", DataType.Text)];
        Announce.Variables = [VariableDefinition.Create("compteur", DataType.Number) with { DefaultValue = "0", IsGlobal = true }];

        Events = Graph("programme", "Programme", GraphKind.Events);
        Events.Folder = Folder;
        Events.Variables = [VariableDefinition.Create("nombre de connexions", DataType.Number) with { DefaultValue = "0" }];

        foreach (GraphAsset graph in new[] { Rarity, Loot, Reward, Chest, Give, Value, CreateCommon, Curse, CursedChest, CursedGive, Bonus, Announce, Events })
        {
            Project.Assets.Add(graph);
        }
    }

    private void BuildBodies()
    {
        // Constructeur de Coffre : rareté = rareté initiale.
        var chest = new ProgramGraphBuilder(Chest, () => Program);
        GraphNode entry = chest.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode setRarity = chest.Add(GraphNodeKind.SetField, 300, 0, Chest.Id, Chest.Variables[1].Id);
        chest.Exec(entry, setRarity).Data(entry, Pin(Chest.Parameters[0]), setRarity, "value").Commit();

        // Coffre.donner(joueur) : un diamant, puis ouvertures = ouvertures + 1.
        var give = new ProgramGraphBuilder(Give, () => Program);
        entry = give.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode giveItem = give.Add(GraphNodeKind.GiveItem, 300, 0, values: ("item", "minecraft:diamond"));
        GraphNode opened = give.Add(GraphNodeKind.GetField, 300, 200, Chest.Id, Chest.Variables[0].Id);
        GraphNode plusOne = give.Add(GraphNodeKind.Add, 500, 200, values: ("b", "1"));
        GraphNode setOpened = give.Add(GraphNodeKind.SetField, 700, 0, Chest.Id, Chest.Variables[0].Id);
        give.Exec(entry, giveItem).Exec(giveItem, setOpened)
            .Data(entry, Pin(Give.Parameters[0]), giveItem, "player")
            .Data(opened, "value", plusOne, "a").Data(plusOne, "result", setOpened, "value").Commit();

        // Coffre.valeur() : 100 si légendaire, sinon 10.
        var value = new ProgramGraphBuilder(Value, () => Program);
        entry = value.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode isLegendary = value.Add(GraphNodeKind.EnumEquals, 0, 200, Rarity.Id, values: ("b", Constant(Rarity, 2)));
        GraphNode rarityField = value.Add(GraphNodeKind.GetField, -300, 200, Chest.Id, Chest.Variables[1].Id);
        GraphNode branch = value.Add(GraphNodeKind.Branch, 300, 0);
        GraphNode high = value.Add(GraphNodeKind.FunctionReturn, 600, 0, values: (Pin(Value.Returns[0]), "100"));
        GraphNode low = value.Add(GraphNodeKind.FunctionReturn, 600, 200, values: (Pin(Value.Returns[0]), "10"));
        value.Exec(entry, branch).Exec(branch, high, NodeInfo.ExecTrue).Exec(branch, low, NodeInfo.ExecFalse)
            .Data(rarityField, "value", isLegendary, "a").Data(isLegendary, "result", branch, "condition").Commit();

        // Coffre.créer commun() (statique) : new Coffre(COMMUN).
        var create = new ProgramGraphBuilder(CreateCommon, () => Program);
        entry = create.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode newChest = create.Add(GraphNodeKind.NewObject, 300, 0, Chest.Id, values: (Pin(Chest.Parameters[0]), Constant(Rarity, 0)));
        GraphNode returnChest = create.Add(GraphNodeKind.FunctionReturn, 600, 0);
        create.Exec(entry, newChest).Exec(newChest, returnChest).Data(newChest, "object", returnChest, Pin(CreateCommon.Returns[0])).Commit();

        // Constructeur de Coffre maudit : constructeur parent (RARE).
        var cursed = new ProgramGraphBuilder(CursedChest, () => Program);
        entry = cursed.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode super = cursed.Add(GraphNodeKind.SuperConstructor, 300, 0, Chest.Id, values: (Pin(Chest.Parameters[0]), Constant(Rarity, 1)));
        cursed.Exec(entry, super).Commit();

        // Coffre maudit.donner(joueur) redéfinit Coffre.donner : un message.
        var cursedGive = new ProgramGraphBuilder(CursedGive, () => Program);
        entry = cursedGive.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode message = cursedGive.Add(GraphNodeKind.SendMessage, 300, 0, values: ("message", "Maudit !"));
        cursedGive.Exec(entry, message).Data(entry, Pin(CursedGive.Parameters[0]), message, "player").Commit();

        // calculer bonus(base, multiplicateur) -> total = base × multiplicateur, critique = total > 50.
        var bonus = new ProgramGraphBuilder(Bonus, () => Program);
        entry = bonus.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode product = bonus.Add(GraphNodeKind.Multiply, 300, 200);
        GraphNode critical = bonus.Add(GraphNodeKind.GreaterThan, 500, 200, values: ("b", "50"));
        GraphNode result = bonus.Add(GraphNodeKind.FunctionReturn, 700, 0);
        bonus.Exec(entry, result)
            .Data(entry, Pin(Bonus.Parameters[0]), product, "a").Data(entry, Pin(Bonus.Parameters[1]), product, "b")
            .Data(product, "result", critical, "a")
            .Data(product, "result", result, Pin(Bonus.Returns[0])).Data(critical, "result", result, Pin(Bonus.Returns[1])).Commit();

        // annoncer(joueur, texte) : compteur global + 1, puis message "texte compteur".
        var announce = new ProgramGraphBuilder(Announce, () => Program);
        entry = announce.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode counter = announce.Add(GraphNodeKind.GetVariable, 0, 200, member: Announce.Variables[0].Id);
        GraphNode next = announce.Add(GraphNodeKind.Add, 200, 200, values: ("b", "1"));
        GraphNode setCounter = announce.Add(GraphNodeKind.SetVariable, 300, 0, member: Announce.Variables[0].Id);
        GraphNode text = announce.Add(GraphNodeKind.JoinText, 400, 200);
        GraphNode say = announce.Add(GraphNodeKind.SendMessage, 600, 0);
        announce.Exec(entry, setCounter).Exec(setCounter, say)
            .Data(counter, "value", next, "a").Data(next, "result", setCounter, "value")
            .Data(entry, Pin(Announce.Parameters[1]), text, "first").Data(counter, "value", text, "second")
            .Data(entry, Pin(Announce.Parameters[0]), say, "player").Data(text, "text", say, "message").Commit();

        BuildEvents();
    }

    private void BuildEvents()
    {
        var b = new ProgramGraphBuilder(Events, () => Program);
        GraphNode join = b.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode newChest = b.Add(GraphNodeKind.NewObject, 300, 0, Chest.Id, values: (Pin(Chest.Parameters[0]), Constant(Rarity, 2)));
        GraphNode callGive = b.Add(GraphNodeKind.CallFunction, 600, 0, Give.Id);
        GraphNode callValue = b.Add(GraphNodeKind.CallFunction, 900, 0, Value.Id);
        GraphNode bonus = b.Add(GraphNodeKind.CallFunction, 900, 300, Bonus.Id);
        GraphNode connections = b.Add(GraphNodeKind.GetVariable, 1100, 300, member: Events.Variables[0].Id);
        GraphNode plusOne = b.Add(GraphNodeKind.Add, 1300, 300, values: ("b", "1"));
        GraphNode setConnections = b.Add(GraphNodeKind.SetVariable, 1200, 0, member: Events.Variables[0].Id);
        GraphNode cursed = b.Add(GraphNodeKind.NewObject, 1500, 0, CursedChest.Id);
        GraphNode reward = b.Add(GraphNodeKind.CallFunction, 1800, 0, Reward.Id, Reward.Methods[0].Id);
        GraphNode loot = b.Add(GraphNodeKind.NewObject, 2100, 0, Loot.Id, null, (Pin(Loot.Variables[1]), "2"), (Pin(Loot.Variables[2]), Constant(Rarity, 1)));
        GraphNode amount = b.Add(GraphNodeKind.GetField, 2300, 300, Loot.Id, Loot.Variables[1].Id);
        GraphNode giveLoot = b.Add(GraphNodeKind.GiveItem, 2400, 0, values: ("item", "minecraft:emerald"));
        GraphNode common = b.Add(GraphNodeKind.CallFunction, 2700, 0, CreateCommon.Id);
        GraphNode branch = b.Add(GraphNodeKind.Branch, 3000, 0);
        GraphNode text = b.Add(GraphNodeKind.JoinText, 3000, 300, values: ("first", "Bonus : "));
        GraphNode announce = b.Add(GraphNodeKind.CallFunction, 3300, 0, Announce.Id);

        b.Exec(join, newChest).Exec(newChest, callGive).Exec(callGive, callValue).Exec(callValue, setConnections)
            .Exec(setConnections, cursed).Exec(cursed, reward).Exec(reward, loot).Exec(loot, giveLoot).Exec(giveLoot, common)
            .Exec(common, branch).Exec(branch, announce, NodeInfo.ExecTrue)
            .Data(newChest, "object", callGive, NodeInfo.TargetInput).Data(join, "player", callGive, Pin(Give.Parameters[0]))
            .Data(newChest, "object", callValue, NodeInfo.TargetInput)
            .Data(callValue, Pin(Value.Returns[0]), bonus, Pin(Bonus.Parameters[0]))
            .Data(connections, "value", plusOne, "a").Data(plusOne, "result", setConnections, "value")
            .Data(cursed, "object", reward, NodeInfo.TargetInput).Data(join, "player", reward, Pin(Reward.Methods[0].Parameters[0]))
            .Data(join, "player", giveLoot, "player").Data(loot, "object", amount, NodeInfo.TargetInput).Data(amount, "value", giveLoot, "count")
            .Data(bonus, Pin(Bonus.Returns[1]), branch, "condition")
            .Data(bonus, Pin(Bonus.Returns[0]), text, "second")
            .Data(join, "player", announce, Pin(Announce.Parameters[0])).Data(text, "text", announce, Pin(Announce.Parameters[1]))
            .Commit();
    }
}

/// <summary>Construction d'un graphe de programme dans les tests : les nœuds sont décrits par les définitions du projet.</summary>
internal sealed class ProgramGraphBuilder
{
    private readonly GraphAsset _asset;
    private readonly Func<ProgramModel> _program;
    private NodeGraph _graph = NodeGraph.Empty;

    public ProgramGraphBuilder(GraphAsset asset, Func<ProgramModel> program)
    {
        _asset = asset;
        _program = program;
    }

    public GraphNode Add(GraphNodeKind kind, double x, double y, Guid? target = null, Guid? member = null, params (string Pin, string Value)[] values)
    {
        var probe = new GraphNode(Guid.Empty, kind, x, y, System.Collections.Immutable.ImmutableDictionary<string, string>.Empty) { Target = target, Member = member };
        GraphNode node = GraphNode.Create(_program().Describe(probe, _asset), x, y, target, member);
        foreach ((string pin, string value) in values)
        {
            node = node.WithValue(pin, value);
        }

        _graph = _graph.AddNode(node);
        return node;
    }

    public ProgramGraphBuilder Exec(GraphNode from, GraphNode to, string pin = NodeInfo.ExecThen) =>
        Connect(new GraphConnection(from.Id, pin, to.Id, NodeInfo.ExecInput));

    public ProgramGraphBuilder Data(GraphNode from, string fromPin, GraphNode to, string toPin) =>
        Connect(new GraphConnection(from.Id, fromPin, to.Id, toPin));

    public void Commit() => _asset.Graph = _graph;

    private ProgramGraphBuilder Connect(GraphConnection connection)
    {
        IGraphContext context = _program().ContextFor(_asset);
        if (_graph.DescribeConnectionProblem(connection, context) is { } problem)
        {
            NodeInfo from = context.Describe(_graph.FindNode(connection.FromNode)!);
            NodeInfo to = context.Describe(_graph.FindNode(connection.ToNode)!);
            throw new InvalidOperationException($"{_asset.ResourceId} : {from.Title}.{connection.FromPin} -> {to.Title}.{connection.ToPin} : {problem}");
        }

        _graph = _graph.Connect(connection, context);
        return this;
    }
}
