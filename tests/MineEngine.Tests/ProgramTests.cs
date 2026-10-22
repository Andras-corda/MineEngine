using System.Collections.Immutable;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.4.3 : fonctions, procédures, classes, structures, interfaces et énumérations en graphes.</summary>
public sealed class ProgramTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static (ModProject Project, ProgramSample Sample) CreateProject(TemporaryDirectory temp, string loader = "neoforge", string version = "1.21.1")
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader, MinecraftVersion = version });
        return (project, ProgramSample.AddTo(project));
    }

    [Fact]
    public void The_sample_program_is_valid_and_lowered_into_definitions()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, ProgramSample sample) = CreateProject(temp);
        var diagnostics = new DiagnosticBag();

        ModIR? ir = ModIRBuilder.CreateDefault().Build(project, diagnostics);

        Assert.True(ir is not null, string.Join('\n', diagnostics));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(12, ir.Content.Definitions.Count);
        IRClass chest = Assert.Single(ir.Content.Definitions.OfType<IRClass>(), c => c.Id == sample.Chest.Id);
        Assert.Equal([sample.Reward.Id], chest.InterfaceIds);
        IRClass cursed = Assert.Single(ir.Content.Definitions.OfType<IRClass>(), c => c.Id == sample.CursedChest.Id);
        Assert.IsType<IRSuperStatement>(cursed.Constructor.Statements[0]);
        IRFunction bonus = Assert.Single(ir.Content.Definitions.OfType<IRFunction>(), f => f.Id == sample.Bonus.Id);
        Assert.Equal(2, bonus.Returns.Count);
        IRBehavior events = Assert.Single(ir.Content.Behaviors, b => b.SourceAssetId == sample.Events.Id);
        Assert.Single(events.Variables);
        Assert.Contains(events.Handlers[0].Body, s => s is IRCallStatement { Callee.InterfaceMethod: not null });
    }

    [Fact]
    public void Subclasses_feed_inputs_of_their_parent_and_of_their_interfaces()
    {
        using var temp = new TemporaryDirectory();
        (_, ProgramSample sample) = CreateProject(temp);
        ProgramModel program = sample.Program;

        Assert.True(program.CanFeed(ProgramModel.TypeOf(sample.CursedChest), ProgramModel.TypeOf(sample.Chest)));
        Assert.True(program.CanFeed(ProgramModel.TypeOf(sample.CursedChest), ProgramModel.TypeOf(sample.Reward)));
        Assert.False(program.CanFeed(ProgramModel.TypeOf(sample.Chest), ProgramModel.TypeOf(sample.CursedChest)));
        Assert.False(program.CanFeed(ProgramModel.TypeOf(sample.Loot), ProgramModel.TypeOf(sample.Chest)));
        Assert.Equal(["creerCommun", "donner", "valeur"], program.Methods(sample.CursedChest).Select(ProgramModel.MemberName).Order());
        Assert.Equal(sample.CursedGive.Id, program.Methods(sample.CursedChest).First(m => ProgramModel.MemberName(m) == "donner").Id);
    }

    [Fact]
    public void Private_definitions_are_only_visible_where_they_are_integrated()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, ProgramSample sample) = CreateProject(temp);
        ProgramModel program = sample.Program;

        // Procédure privée hors classe : visible depuis son dossier seulement.
        Assert.True(program.IsVisible(sample.Announce, sample.Events));
        var elsewhere = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("ailleurs"), "Ailleurs") { Folder = "Autre" };
        Assert.False(program.IsVisible(sample.Announce, elsewhere));

        // Énumération privée intégrée à Coffre maudit : visible dans la classe et ses membres seulement.
        Assert.True(program.IsVisible(sample.Curse, sample.CursedChest));
        Assert.True(program.IsVisible(sample.Curse, sample.CursedGive));
        Assert.False(program.IsVisible(sample.Curse, sample.Chest));
        Assert.False(program.IsVisible(sample.Curse, sample.Events));
        Assert.DoesNotContain(sample.Curse, program.VisibleTypes(sample.Events));
        Assert.Contains(sample.Curse, program.VisibleTypes(sample.CursedGive));

        // Champ privé "ouvertures" : pas depuis la classe héritière ; champ protégé "rareté" : oui.
        Assert.False(program.CanAccess(sample.Chest, sample.Chest.Variables[0].Access, sample.CursedGive));
        Assert.True(program.CanAccess(sample.Chest, sample.Chest.Variables[1].Access, sample.CursedGive));

        // Un appel depuis un autre dossier devient un nœud introuvable, signalé en erreur.
        project.Assets.Add(elsewhere);
        var call = new GraphNode(Guid.NewGuid(), GraphNodeKind.CallFunction, 0, 0, ImmutableDictionary<string, string>.Empty) { Target = sample.Announce.Id };
        Assert.True(program.Describe(call, elsewhere).IsMissing);
        elsewhere.Graph = NodeGraph.Empty.AddNode(call);
        var diagnostics = new DiagnosticBag();
        project.Assets.Validate(diagnostics);
        Assert.Contains(diagnostics, d => d.AssetId == elsewhere.Id && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Inheritance_and_interfaces_are_checked()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, ProgramSample sample) = CreateProject(temp);
        string Errors(GraphAsset graph) => string.Join('\n', ProgramValidator.Validate(graph, ProgramModel.From(project.Assets))
            .Where(i => i.Severity == DiagnosticSeverity.Error).Select(i => i.Message));

        Assert.Equal(string.Empty, Errors(sample.Chest));
        Assert.Equal(string.Empty, Errors(sample.CursedChest));

        // Héritage circulaire.
        sample.Chest.BaseClassId = sample.CursedChest.Id;
        Assert.Contains("Circular", Errors(sample.Chest));
        sample.Chest.BaseClassId = null;

        // Méthode d'interface manquante.
        sample.Reward.Methods = [.. sample.Reward.Methods, MethodSignature.Create("ouvrir")];
        Assert.Contains("\"ouvrir\"", Errors(sample.Chest));
        sample.Reward.Methods = [.. sample.Reward.Methods.Take(2)];

        // Redéfinition avec une autre signature.
        sample.CursedGive.Parameters = [];
        Assert.Contains("same parameters", Errors(sample.CursedGive));
        sample.CursedGive.Parameters = [VariableDefinition.Create("joueur", DataType.Player)];

        // Constructeur parent oublié alors qu'il a des paramètres.
        sample.CursedChest.Graph = sample.CursedChest.Graph.RemoveNodes(sample.CursedChest.Graph.Nodes.Where(n => n.Kind == GraphNodeKind.SuperConstructor).Select(n => n.Id));
        Assert.Contains("Parent constructor", Errors(sample.CursedChest));

        // Nom de type réservé.
        sample.Loot.DisplayName = "Player";
        Assert.Contains("already the name", Errors(sample.Loot));
    }

    [Fact]
    public void Definitions_survive_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, ProgramSample sample) = CreateProject(temp);
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        repository.Save(project);

        ModProject reopened = repository.Open(project.Layout.RootDirectory);
        var chest = (GraphAsset)reopened.Assets.Find(sample.Chest.Id)!;
        var cursed = (GraphAsset)reopened.Assets.Find(sample.CursedChest.Id)!;
        var bonus = (GraphAsset)reopened.Assets.Find(sample.Bonus.Id)!;
        var events = (GraphAsset)reopened.Assets.Find(sample.Events.Id)!;

        Assert.Equal(GraphKind.Class, chest.Kind);
        Assert.Equal(sample.Chest.Parameters, chest.Parameters);
        Assert.Equal(sample.Chest.Variables, chest.Variables);
        Assert.Equal(sample.Chest.InterfaceIds, chest.InterfaceIds);
        Assert.Equal(sample.Chest.Id, cursed.BaseClassId);
        Assert.True(bonus.IsPure);
        Assert.Equal(sample.Bonus.Returns, bonus.Returns);
        Assert.Equal(sample.Rarity.Constants, ((GraphAsset)reopened.Assets.Find(sample.Rarity.Id)!).Constants);
        Assert.Equal(sample.Reward.Methods.Select(m => m.Name), ((GraphAsset)reopened.Assets.Find(sample.Reward.Id)!).Methods.Select(m => m.Name));
        Assert.Equal(sample.Curse.OwnerId, ((GraphAsset)reopened.Assets.Find(sample.Curse.Id)!).OwnerId);
        Assert.True(((GraphAsset)reopened.Assets.Find(sample.Announce.Id)!).Variables[0].IsGlobal);
        Assert.Equal(sample.Events.Graph.Nodes.Select(n => (n.Kind, n.Target, n.Member)), events.Graph.Nodes.Select(n => (n.Kind, n.Target, n.Member)));
        Assert.Equal(sample.Events.Graph.Connections.Length, events.Graph.Connections.Length);
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Definitions_become_java_classes_in_the_code_package(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        (ModProject project, _) = CreateProject(temp, loader.ToId(), version);
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string Read(string file) => File.ReadAllText(Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod", file));

        string rarity = Read("code/Rarete.java");
        Assert.Contains("public enum Rarete {", rarity);
        Assert.Contains("COMMUN, RARE, LEGENDAIRE;", rarity);

        string chest = Read("code/Coffre.java");
        Assert.Contains("public class Coffre implements Recompense {", chest);
        Assert.Contains("private int ouvertures = 0;", chest);
        Assert.Contains("protected Rarete rarete = Rarete.COMMUN;", chest);
        Assert.Contains("public Coffre(Level level, Rarete rareteInitiale) {", chest);
        Assert.Contains("this.rarete = rareteInitiale;", chest);
        Assert.Contains("public void donner(Level level, Player joueur) {", chest);
        Assert.Contains("this.ouvertures = (this.ouvertures + 1);", chest);
        Assert.Contains("public static Coffre creerCommun(Level level) {", chest);
        Assert.Contains("if (this.rarete == Rarete.LEGENDAIRE) {", chest);

        string cursed = Read("code/CoffreMaudit.java");
        Assert.Contains("public class CoffreMaudit extends Coffre {", cursed);
        Assert.Contains("super(level, Rarete.RARE);", cursed);
        Assert.Contains("private CoffreMaudit.Malediction malediction = CoffreMaudit.Malediction.FEU;", cursed);
        Assert.Contains("private enum Malediction {", cursed);
        Assert.Contains("    @Override\n    public void donner(Level level, Player joueur) {", cursed);

        string bonus = Read("code/CalculerBonus.java");
        Assert.Contains("public record CalculerBonusResult(int total, boolean critique) {", bonus);
        Assert.Contains("public static CalculerBonus.CalculerBonusResult calculerBonus(Level level, int base, int multiplicateur) {", bonus);
        Assert.Contains("return new CalculerBonus.CalculerBonusResult((base * multiplicateur), ((base * multiplicateur) > 50));", bonus);

        string announce = Read("code/Annoncer.java");
        Assert.Contains("private static int compteur = 0;", announce);
        Assert.Contains("compteur = (compteur + 1);", announce);

        Assert.Contains("void donner(Level level, Player joueur);", Read("code/Recompense.java"));
        Assert.Contains("public Butin(Item item, int quantite, Rarete rarete) {", Read("code/Butin.java"));

        string events = Read("behavior/ProgrammeBehavior.java");
        Assert.Contains("import com.mineengine.mods.testmod.code.*;", events);
        Assert.Contains("private static int nombreDeConnexions = 0;", events);
        Assert.Contains("_v1 = new Coffre(level, Rarete.LEGENDAIRE);", events);
        Assert.Contains("_v1.donner(level, player);", events);
        Assert.Contains("CalculerBonus.calculerBonus(level, _v2, 2).critique()", events);
        Assert.Contains("Annoncer.annoncer(level, player, (\"Bonus : \" + String.valueOf(CalculerBonus.calculerBonus(level, _v2, 2).total())));", events);
        Assert.Contains("Coffre.creerCommun(level);", events);
    }

    [Theory]
    [InlineData(GraphKind.Function)]
    [InlineData(GraphKind.Procedure)]
    [InlineData(GraphKind.Class)]
    [InlineData(GraphKind.Struct)]
    [InlineData(GraphKind.Interface)]
    [InlineData(GraphKind.Enum)]
    public void New_graphs_start_without_errors(GraphKind kind)
    {
        GraphAsset graph = ProgramPalette.CreateGraph(kind, ResourceId.Parse("nouveau"), ProgramPalette.DefaultName(kind));

        Assert.Empty(ProgramValidator.Validate(graph, new ProgramModel([graph])).Where(i => i.Severity == DiagnosticSeverity.Error));
        Assert.Equal(kind.HasBody(), graph.Graph.Nodes.Any(n => n.Kind == GraphNodeKind.FunctionEntry));
    }

    [Fact]
    public void The_add_menu_offers_only_what_the_graph_can_use()
    {
        using var temp = new TemporaryDirectory();
        (_, ProgramSample sample) = CreateProject(temp);
        ProgramModel program = sample.Program;
        List<NodeTemplate> inEvents = [.. ProgramPalette.AvailableNodes(program, sample.Events)];
        List<NodeTemplate> inMethod = [.. ProgramPalette.AvailableNodes(program, sample.CursedGive)];

        // Graphe d'événements : événements, appels, création d'objets ; pas d'entrée ni de "Moi".
        Assert.Contains(inEvents, t => t.Info.Kind == GraphNodeKind.OnPlayerJoin);
        Assert.Contains(inEvents, t => t.Info.Kind == GraphNodeKind.CallFunction && t.Target == sample.Bonus.Id);
        Assert.Contains(inEvents, t => t.Info.Kind == GraphNodeKind.NewObject && t.Target == sample.Chest.Id);
        Assert.DoesNotContain(inEvents, t => t.Info.Kind is GraphNodeKind.Self or GraphNodeKind.FunctionReturn);
        Assert.DoesNotContain(inEvents, t => t.Target == sample.Curse.Id);
        Assert.DoesNotContain(inEvents, t => t.Info.Kind == GraphNodeKind.GetField && t.Member == sample.Chest.Variables[0].Id);

        // Méthode de Coffre maudit : pas d'événement ; "Moi", l'énumération privée et le champ protégé hérité.
        Assert.DoesNotContain(inMethod, t => t.Info.Category == NodeCategory.Event);
        Assert.Contains(inMethod, t => t.Info.Kind == GraphNodeKind.Self);
        Assert.Contains(inMethod, t => t.Info.Kind == GraphNodeKind.EnumLiteral && t.Target == sample.Curse.Id);
        Assert.Contains(inMethod, t => t.Info.Kind == GraphNodeKind.GetField && t.Target == sample.CursedChest.Id && t.Member == sample.Chest.Variables[1].Id);
        Assert.DoesNotContain(inMethod, t => t.Info.Kind == GraphNodeKind.GetField && t.Member == sample.Chest.Variables[0].Id);
    }

    /// <summary>Backend sans MDK : seules les sources sont générées.</summary>
    internal sealed class SourceBackend : IModLoaderBackend
    {
        private readonly IFileEmitter[] _emitters;

        public SourceBackend(ModLoaderKind loader, string version, params IFileEmitter[] emitters)
        {
            _emitters = emitters;
            Mdk = new MdkDescriptor("test", loader, MinecraftVersion.Parse(version), "0", 21, "8.8", Path.GetTempPath(), "test", DateTime.Now);
        }

        public MdkDescriptor Mdk { get; }

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => _emitters;

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
