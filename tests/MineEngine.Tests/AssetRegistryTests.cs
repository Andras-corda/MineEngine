using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Tests;

public sealed class AssetRegistryTests
{
    [Fact]
    public void Unique_identifiers_get_a_numeric_suffix()
    {
        var registry = new AssetRegistry();
        registry.Add(new ItemAsset(Guid.NewGuid(), registry.CreateUniqueResourceId("new_item", AssetType.Item), "A"));
        registry.Add(new ItemAsset(Guid.NewGuid(), registry.CreateUniqueResourceId("new_item", AssetType.Item), "B"));

        Assert.Equal("new_item_3", registry.CreateUniqueResourceId("new_item", AssetType.Item).Value);
    }

    [Fact]
    public void Duplicate_identifier_is_rejected()
    {
        var registry = new AssetRegistry();
        registry.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby block")));
    }

    [Fact]
    public void Modifying_an_asset_raises_a_registry_change()
    {
        var registry = new AssetRegistry();
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby");
        registry.Add(item);

        AssetRegistryChange? change = null;
        registry.Changed += (_, e) => change = e.Change;
        item.MaxStackSize = 16;

        Assert.Equal(AssetRegistryChange.Modified, change);
    }

    [Fact]
    public void Stack_size_outside_limits_is_refused()
    {
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby");
        Assert.Throws<ArgumentOutOfRangeException>(() => item.MaxStackSize = 100);
    }

    [Fact]
    public void Reference_index_follows_the_changes_of_the_assets()
    {
        var registry = new AssetRegistry();
        var ruby = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby");
        registry.Add(ruby);
        GraphAsset altar = SampleContent.CreateAltarGraph(ContentReference.ToAsset(ruby.Id), ContentReference.ToId(NamespacedId.TryParse("minecraft:diamond", out NamespacedId? diamond) ? diamond! : throw new InvalidOperationException()));
        registry.Add(altar);

        Assert.Equal(altar, Assert.Single(registry.FindReferencesTo(ruby.Id)).Source);

        altar.Graph = NodeGraph.Empty;
        Assert.Empty(registry.FindReferencesTo(ruby.Id));

        registry.Remove(altar);
        Assert.Empty(registry.FindReferencesTo(ruby.Id));
    }

    [Fact]
    public void Validation_is_reused_until_the_asset_changes()
    {
        var registry = new AssetRegistry();
        var builder = new GraphBuilder();
        GraphNode join = builder.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode message = builder.Add(GraphNodeKind.SendMessage, 300, 0);
        builder.Exec(join, message);
        var graph = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("greeting"), "Greeting") { Graph = builder.Build() };
        registry.Add(graph);

        IReadOnlyList<Diagnostic> first = Validate(registry);
        Assert.Contains(first, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(first, Validate(registry));

        // The player is connected: the graph changed, so it is checked again.
        graph.Graph = builder.Data(join, "player", message, "player").Build();
        Assert.DoesNotContain(Validate(registry), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Program_model_is_shared_until_a_graph_is_added_or_removed()
    {
        var registry = new AssetRegistry();
        ProgramModel program = registry.Program;
        Assert.Same(program, registry.Program);

        registry.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Ruby"));
        Assert.Same(program, registry.Program);

        var graph = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("logic"), "Logic");
        registry.Add(graph);
        Assert.NotSame(program, registry.Program);
    }

    private static IReadOnlyList<Diagnostic> Validate(AssetRegistry registry)
    {
        var bag = new DiagnosticBag();
        registry.Validate(bag);
        return [.. bag];
    }
}
