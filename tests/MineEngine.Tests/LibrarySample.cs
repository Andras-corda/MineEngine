using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Function library: a library "Outils" with a pure function and a procedure, used by an
/// event graph that also holds a comment and a tooltip (they must change nothing in the Java).
/// </summary>
internal sealed class LibrarySample
{
    private LibrarySample(ModProject project)
    {
        Project = project;
    }

    public ModProject Project { get; }

    public GraphAsset Library { get; private set; } = null!;

    public GraphAsset Double { get; private set; } = null!;

    public GraphAsset Greet { get; private set; } = null!;

    public GraphAsset Events { get; private set; } = null!;

    public GraphNode Comment { get; private set; } = null!;

    public GraphNode Tooltip { get; private set; } = null!;

    public ProgramModel Program => ProgramModel.From(Project.Assets);

    public static LibrarySample AddTo(ModProject project)
    {
        var sample = new LibrarySample(project);
        sample.Build();
        return sample;
    }

    private void Build()
    {
        Library = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("outils"), "Outils") { Kind = GraphKind.Library };

        Double = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("outils_doubler"), "doubler")
        {
            Kind = GraphKind.Function, OwnerId = Library.Id, IsStatic = true, IsPure = true,
        };
        Double.Parameters = [VariableDefinition.Create("nombre", DataType.Number)];
        Double.Returns = [VariableDefinition.Create("double", DataType.Number)];

        Greet = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("outils_saluer"), "saluer") { Kind = GraphKind.Procedure, OwnerId = Library.Id, IsStatic = true };
        Greet.Parameters = [VariableDefinition.Create("joueur", DataType.Player)];

        Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("usage_outils"), "Usage des outils") { Kind = GraphKind.Events };
        foreach (GraphAsset graph in new[] { Library, Double, Greet, Events })
        {
            Project.Assets.Add(graph);
        }

        // doubler(nombre) = nombre + nombre.
        var doubler = new ProgramGraphBuilder(Double, () => Program);
        GraphNode entry = doubler.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode sum = doubler.Add(GraphNodeKind.Add, 300, 200);
        GraphNode result = doubler.Add(GraphNodeKind.FunctionReturn, 600, 0);
        doubler.Exec(entry, result)
            .Data(entry, ProgramSample.Pin(Double.Parameters[0]), sum, "a").Data(entry, ProgramSample.Pin(Double.Parameters[0]), sum, "b")
            .Data(sum, "result", result, ProgramSample.Pin(Double.Returns[0])).Commit();

        // saluer(joueur) : un message.
        var greet = new ProgramGraphBuilder(Greet, () => Program);
        entry = greet.Add(GraphNodeKind.FunctionEntry, 0, 0);
        GraphNode message = greet.Add(GraphNodeKind.SendMessage, 300, 0, values: ("message", "Bonjour !"));
        greet.Exec(entry, message).Data(entry, ProgramSample.Pin(Greet.Parameters[0]), message, "player").Commit();

        // Connexion d'un joueur : saluer(joueur), puis un message avec doubler(21).
        var events = new ProgramGraphBuilder(Events, () => Program);
        GraphNode join = events.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode callGreet = events.Add(GraphNodeKind.CallFunction, 300, 0, Greet.Id);
        GraphNode callDouble = events.Add(GraphNodeKind.CallFunction, 300, 200, Double.Id, values: (ProgramSample.Pin(Double.Parameters[0]), "21"));
        GraphNode say = events.Add(GraphNodeKind.SendMessage, 600, 0);
        events.Exec(join, callGreet).Exec(callGreet, say)
            .Data(join, "player", callGreet, ProgramSample.Pin(Greet.Parameters[0])).Data(join, "player", say, "player")
            .Data(callDouble, ProgramSample.Pin(Double.Returns[0]), say, "message").Commit();

        Comment = GraphAnnotations.CreateComment(0, 0, 900, 300, "Accueil");
        Tooltip = GraphAnnotations.CreateTooltip([callGreet.Id, callDouble.Id], 300, -60, "Utilise la bibliothèque Outils.");
        Events.Graph = Events.Graph.AddNode(Comment).AddNode(Tooltip);
    }
}
