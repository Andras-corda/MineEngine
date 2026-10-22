using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Puppeteer;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.6: Puppeteer behavior trees (model, files, checks, Java).</summary>
public sealed class PuppeteerTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    [Fact]
    public void Every_node_kind_is_described_by_the_catalog()
    {
        foreach (BtNodeKind kind in Enum.GetValues<BtNodeKind>())
        {
            BtNodeInfo info = BtCatalog.Get(kind);
            Assert.False(string.IsNullOrWhiteSpace(info.Title));
            Assert.All(info.Parameters.Where(p => p.Type == BtParameterType.Choice), p => Assert.Contains(p.Choices, c => c.Value == p.DefaultValue));
        }

        Assert.All(BtCatalog.InCategory(BtCategory.Service), s => Assert.NotNull(s.FindParameter(BtCatalog.Interval)));
        Assert.All(BtCatalog.All.Where(i => i.IsCondition && i.Kind != BtNodeKind.RandomChance), c => Assert.NotNull(c.FindParameter(BtCatalog.Abort)));
    }

    [Fact]
    public void Tree_keeps_one_root_one_parent_per_node_and_no_cycle()
    {
        BehaviorTree tree = BehaviorTree.Empty;
        BtNode selector = BtNode.Create(BtNodeKind.Selector, 0, 100);
        BtNode sequence = BtNode.Create(BtNodeKind.Sequence, 200, 100);
        BtNode right = BtNode.Create(BtNodeKind.Wait, 300, 250);
        BtNode left = BtNode.Create(BtNodeKind.Wander, -100, 250);
        tree = tree.AddNode(selector).AddNode(sequence).AddNode(right).AddNode(left);

        tree = tree.Connect(tree.Root.Id, selector.Id).Connect(selector.Id, right.Id).Connect(selector.Id, left.Id);
        Assert.Equal([left.Id, right.Id], tree.ChildrenOf(selector.Id).Select(n => n.Id));

        // The root has a single child; a node has a single parent.
        tree = tree.Connect(tree.Root.Id, sequence.Id);
        Assert.Equal(sequence.Id, Assert.Single(tree.ChildrenOf(tree.Root.Id)).Id);
        tree = tree.Connect(sequence.Id, right.Id);
        Assert.Equal(sequence.Id, tree.ParentOf(right.Id)!.Id);

        // No loop, no child under a task, no parent for the root.
        tree = tree.Connect(sequence.Id, selector.Id);
        Assert.NotNull(tree.DescribeLinkProblem(selector.Id, sequence.Id));
        Assert.NotNull(tree.DescribeLinkProblem(right.Id, left.Id));
        Assert.NotNull(tree.DescribeLinkProblem(selector.Id, tree.Root.Id));

        // Moving a node changes its order; the root cannot be deleted.
        tree = tree.MoveNodes(new Dictionary<Guid, (double X, double Y)> { [right.Id] = (-500, 250) });
        Assert.Equal(right.Id, tree.ChildrenOf(sequence.Id)[0].Id);
        Assert.Contains(tree.Nodes, n => n.Id == tree.Root.Id);
        Assert.Same(tree.Root, tree.RemoveNodes([tree.Root.Id]).Root);
        Assert.Equal(
            [BtNodeKind.Root, BtNodeKind.Sequence, BtNodeKind.Wait, BtNodeKind.Selector, BtNodeKind.Wander],
            tree.ExecutionOrder().Select(n => n.Kind));
    }

    [Fact]
    public void Arranging_keeps_the_execution_order_and_separates_the_nodes()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, "neoforge", "1.21.1");
        BehaviorTree tree = PuppeteerSample.AddTo(project, AddSound(project)).Tree;
        BehaviorTree messy = tree.MoveNodes(tree.Nodes.ToDictionary(n => n.Id, n => (n.Y, n.X)));

        BehaviorTree arranged = messy.Arranged();

        Assert.Equal(messy.ExecutionOrder().Select(n => n.Id), arranged.ExecutionOrder().Select(n => n.Id));
        foreach (BtNode parent in arranged.Nodes)
        {
            IReadOnlyList<BtNode> children = arranged.ChildrenOf(parent.Id);
            Assert.All(children, c => Assert.True(c.Y > parent.Y));
            Assert.All(children.Zip(children.Skip(1)), p => Assert.True(p.Second.X - p.First.X >= BehaviorTree.LayoutColumn));
        }
    }

    [Fact]
    public void Attachments_are_added_reordered_and_removed()
    {
        BtNode task = BtNode.Create(BtNodeKind.Wait, 0, 100);
        BehaviorTree tree = BehaviorTree.Empty.AddNode(task);
        BtAttachment first = BtAttachment.Create(BtNodeKind.Cooldown);
        BtAttachment second = BtAttachment.Create(BtNodeKind.IsSet);
        BtAttachment service = BtAttachment.Create(BtNodeKind.RememberAttacker);
        tree = tree.AddAttachment(task.Id, first).AddAttachment(task.Id, second).AddAttachment(task.Id, service);

        BtNode updated = tree.FindNode(task.Id)!;
        Assert.Equal([first.Id, second.Id], updated.Decorators.Select(a => a.Id));
        Assert.Equal(service.Id, Assert.Single(updated.Services).Id);

        tree = tree.MoveAttachment(second.Id, -1).ReplaceAttachment(first.WithValue("seconds", "9"));
        Assert.Equal([second.Id, first.Id], tree.FindNode(task.Id)!.Decorators.Select(a => a.Id));
        Assert.Equal("9", tree.FindAttachment(first.Id)!.Value.Attachment.GetValue("seconds"));

        tree = tree.RemoveAttachment(second.Id).RemoveAttachment(service.Id);
        Assert.Equal(first.Id, Assert.Single(tree.FindNode(task.Id)!.Decorators).Id);
        Assert.Empty(tree.FindNode(task.Id)!.Services);
    }

    [Fact]
    public void New_tree_starts_from_a_valid_example()
    {
        var asset = (BehaviorTreeAsset)new BehaviorTreeAssetDefinition().CreateNew(ResourceId.Parse("brain"), "Cerveau");

        Assert.Single(asset.Keys);
        Assert.Equal(BtNodeKind.Selector, asset.Tree.ChildrenOf(asset.Tree.Root.Id).Single().Kind);
        Assert.Empty(BehaviorTreeValidator.Validate(asset));
    }

    [Fact]
    public void Problems_point_to_the_node_or_attachment()
    {
        var number = new BlackboardKey(Guid.NewGuid(), "count", BlackboardKeyType.Number);
        BtNode sequence = BtNode.Create(BtNodeKind.Sequence, 0, 100);
        BtNode empty = BtNode.Create(BtNodeKind.Selector, 0, 250);
        BtAttachment lower = BtAttachment.Create(BtNodeKind.IsInWater).WithValue(BtCatalog.Abort, nameof(BtAbortMode.LowerPriority));
        BtNode attack = BtNode.Create(BtNodeKind.MeleeAttack, 200, 250).WithValue(BtCatalog.Target, number.Id.ToString("D")) with { Decorators = [lower] };
        BtNode orphan = BtNode.Create(BtNodeKind.Wait, 600, 600);
        BtNode wrongNumber = BtNode.Create(BtNodeKind.Wait, 300, 250).WithValue("seconds", "abc");
        var asset = new BehaviorTreeAsset(Guid.NewGuid(), ResourceId.Parse("bad"), "Mauvais")
        {
            Keys = [number, new BlackboardKey(Guid.NewGuid(), "Count", BlackboardKeyType.Boolean)],
        };
        BehaviorTree tree = BehaviorTree.Empty.AddNode(sequence).AddNode(empty).AddNode(attack).AddNode(orphan).AddNode(wrongNumber);
        asset.Tree = tree.Connect(tree.Root.Id, sequence.Id).Connect(sequence.Id, empty.Id).Connect(sequence.Id, attack.Id).Connect(sequence.Id, wrongNumber.Id);

        IReadOnlyList<BtIssue> issues = BehaviorTreeValidator.Validate(asset);

        Assert.Contains(issues, i => i.ItemId == empty.Id && i.Severity == DiagnosticSeverity.Error);
        Assert.Contains(issues, i => i.ItemId == attack.Id && i.Severity == DiagnosticSeverity.Error && i.Message.Contains("count"));
        Assert.Contains(issues, i => i.ItemId == lower.Id && i.Severity == DiagnosticSeverity.Warning);
        Assert.Contains(issues, i => i.ItemId == orphan.Id && i.Severity == DiagnosticSeverity.Warning);
        Assert.Contains(issues, i => i.ItemId == wrongNumber.Id && i.Severity == DiagnosticSeverity.Error);
        Assert.Contains(issues, i => i.ItemId is null && i.Message.Contains("Count", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tree_and_mob_link_survive_save_and_reopen()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, "neoforge", "1.21.1");
        SoundAsset sound = AddSound(project);
        BehaviorTreeAsset original = PuppeteerSample.AddTo(project, sound);
        Repository.Save(project);

        ModProject reopened = Repository.Open(project.Layout.RootDirectory);
        var loaded = (BehaviorTreeAsset)reopened.Assets.Find(original.Id)!;

        Assert.Equal(original.Keys, loaded.Keys);
        Assert.Equal(original.Tree.Nodes.Length, loaded.Tree.Nodes.Length);
        Assert.Equal(original.Tree.Links.OrderBy(l => l.Child), loaded.Tree.Links.OrderBy(l => l.Child));
        foreach (BtNode node in original.Tree.Nodes)
        {
            BtNode copy = loaded.Tree.FindNode(node.Id)!;
            Assert.Equal(node.Kind, copy.Kind);
            Assert.Equal((node.X, node.Y), (copy.X, copy.Y));
            Assert.Equal(node.Values.OrderBy(v => v.Key), copy.Values.OrderBy(v => v.Key));
            Assert.Equal(node.Decorators.Select(a => (a.Id, a.Kind)), copy.Decorators.Select(a => (a.Id, a.Kind)));
            Assert.Equal(node.Services.Select(a => (a.Id, a.Kind)), copy.Services.Select(a => (a.Id, a.Kind)));
        }

        MobAsset mob = reopened.Assets.OfType<MobAsset>().Single(m => m.ResourceId.Value == "shadow_puppet");
        Assert.Equal(original.Id, mob.BehaviorTreeId);
        Assert.Contains(reopened.Assets.FindReferencesTo(original.Id), r => r.Source == mob);
        Assert.Contains(reopened.Assets.FindReferencesTo(sound.Id), r => r.Source.Id == original.Id);
    }

    [Fact]
    public void Deleting_the_tree_of_a_mob_is_an_error()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, "neoforge", "1.21.1");
        BehaviorTreeAsset tree = PuppeteerSample.AddTo(project, AddSound(project));
        project.Assets.Remove(tree);

        var diagnostics = new DiagnosticBag();
        Assert.Null(ModIRBuilder.CreateDefault().Build(project, diagnostics));
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("behavior tree", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task Tree_becomes_java_code_used_by_its_mob(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, loader.ToId(), version);
        PuppeteerSample.AddTo(project, AddSound(project));
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.NotNull(ir);

        IRBehaviorTree lowered = ir.Content.BehaviorTrees.Single(t => t.Id.Value == "shadow_brain");
        Assert.Equal(BtNodeKind.Selector, lowered.Root!.Kind);
        Assert.Equal(ResourceId.Parse("shadow_brain"), ir.Content.Mobs.Single(m => m.Id.Value == "shadow_puppet").BehaviorTree);
        Assert.Empty(ir.Content.Mobs.Single(m => m.Id.Value == "shadow_puppet").Goals);

        IFileEmitter emitter = loader == ModLoaderKind.Forge ? new ForgeJavaEmitter() : new NeoForgeJavaEmitter();
        string workspace = Path.Combine(temp.Path, "workspace");
        GenerationResult result = await ModGenerator.CreateDefault(new Backend(loader, version, emitter))
            .GenerateAsync(ir, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));

        string ai = Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod/ai");
        string runtime = File.ReadAllText(Path.Combine(ai, "Puppeteer.java"));
        string tree = File.ReadAllText(Path.Combine(ai, "ShadowBrainTree.java"));
        string mob = File.ReadAllText(Path.Combine(workspace, "src/main/java/com/mineengine/mods/testmod/entity/ShadowPuppetEntity.java"));

        // Every kind of node is created in the tree and exists in the engine.
        foreach (BtNodeKind kind in Enum.GetValues<BtNodeKind>().Where(k => k != BtNodeKind.Root && !BtCatalog.Get(k).IsAnnotation))
        {
            if (kind == BtNodeKind.RunGoal)
            {
                Assert.Contains("Puppeteer.goal(PatrolGoalTree.build())", tree);
                continue;
            }

            string javaClass = kind == BtNodeKind.RandomSelector ? "Selector" : kind.ToString();
            Assert.Contains($"new Puppeteer.{javaClass}(", tree);
            Assert.Contains($"class {javaClass} ", runtime);
        }

        Assert.Contains("\"target\"", tree);
        Assert.Contains("\"testmod:goblin_hurt\"", tree);
        Assert.Contains(".observe(false, 3)", tree);
        Assert.Contains("AbstractVillager.class", tree);
        Assert.Contains("new Puppeteer.TreeGoal(this, ShadowBrainTree.build())", mob);
        Assert.DoesNotContain("MeleeAttackGoal", mob);
        Assert.Contains(version == "1.20.1" ? "new ResourceLocation(id)" : "ResourceLocation.parse(id)", runtime);
        Assert.Contains(version == "1.20.1" ? "new Arrow(level, mob)" : "new Arrow(level, mob, new ItemStack(Items.ARROW), null)", runtime);
    }

    [Fact]
    public void Goals_must_be_public_and_must_not_loop()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, "neoforge", "1.21.1");
        BehaviorTreeAsset first = AddGoalTree(project, "first");
        BehaviorTreeAsset second = AddGoalTree(project, "second");
        Run(first, second);
        Func<Guid, BehaviorTreeAsset?> resolve = id => project.Assets.Find(id) as BehaviorTreeAsset;

        Assert.Contains(BehaviorTreeValidator.Validate(first, resolve), i => i.Severity == DiagnosticSeverity.Error && i.Message.Contains("not a public goal"));

        second.IsPublicGoal = true;
        Assert.DoesNotContain(BehaviorTreeValidator.Validate(first, resolve), i => i.Severity == DiagnosticSeverity.Error);

        first.IsPublicGoal = true;
        Run(second, first);
        Assert.Contains(BehaviorTreeValidator.Validate(first, resolve), i => i.Message.Contains("Goal loop"));

        Run(first, first);
        Assert.Contains(BehaviorTreeValidator.Validate(first, resolve), i => i.Message.Contains("cannot run itself"));
        Assert.Contains(first.GetReferences(), r => r.TargetId == first.Id && r.Kind == AssetReferenceKind.BehaviorTree);
    }

    private static BehaviorTreeAsset AddGoalTree(ModProject project, string id)
    {
        var tree = new BehaviorTreeAsset(Guid.NewGuid(), ResourceId.Parse(id), id);
        project.Assets.Add(tree);
        return tree;
    }

    /// <summary>The tree's only branch runs the goal.</summary>
    private static void Run(BehaviorTreeAsset tree, BehaviorTreeAsset goal)
    {
        BehaviorTree empty = BehaviorTree.Empty;
        BtNode run = BtNode.Create(BtNodeKind.RunGoal, 0, 200).WithValue(BtCatalog.Goal, goal.Id.ToString("D"));
        tree.Tree = empty.AddNode(run).Connect(empty.Root.Id, run.Id);
    }

    [Fact]
    public void Mob_without_tree_keeps_its_goals()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path, "neoforge", "1.21.1");
        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("plain"), "Simple"));
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;

        Assert.Null(ir.Content.Mobs.Single().BehaviorTree);
        Assert.NotEmpty(ir.Content.Mobs.Single().Goals);
        Assert.Empty(ir.Content.BehaviorTrees);
    }

    [Fact]
    public void Tree_class_names_avoid_repeating_tree()
    {
        Assert.Equal("GoblinBrainTree", JavaModNaming.BehaviorTreeClassName(ResourceId.Parse("goblin_brain")));
        Assert.Equal("GoblinTree", JavaModNaming.BehaviorTreeClassName(ResourceId.Parse("goblin_tree")));
    }

    private static ProjectRepository Repository => new(Serializer, new ProjectSettingsSerializer());

    private static ModProject CreateProject(string directory, string loaderId, string minecraftVersion)
    {
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loaderId, MinecraftVersion = minecraftVersion };
        return Repository.Create(directory, settings);
    }

    private static SoundAsset AddSound(ModProject project)
    {
        Directory.CreateDirectory(project.Layout.SoundsDirectory);
        string file = Path.Combine(project.Layout.SoundsDirectory, "goblin_hurt.ogg");
        File.WriteAllBytes(file, [.. "OggS"u8.ToArray(), 0, 2, 0, 0]);
        var sound = new SoundAsset(Guid.NewGuid(), ResourceId.Parse("goblin_hurt"), "Cri", project.Layout.ToRelativePath(file));
        project.Assets.Add(sound);
        return sound;
    }

    private sealed class Backend : IModLoaderBackend
    {
        private readonly IFileEmitter _javaEmitter;

        public Backend(ModLoaderKind loader, string version, IFileEmitter javaEmitter)
        {
            _javaEmitter = javaEmitter;
            Mdk = new MdkDescriptor("test", loader, MinecraftVersion.Parse(version), "0", 21, null, Path.GetTempPath(), "test", DateTime.Now);
        }

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MdkDescriptor Mdk { get; }

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => [_javaEmitter];

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
