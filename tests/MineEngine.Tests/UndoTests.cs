using MineEngine.Assets;
using MineEngine.Assets.Commands;
using MineEngine.Core.Commands;
using MineEngine.Core.Identifiers;
using MineEngine.Project;
using MineEngine.Project.Commands;

namespace MineEngine.Tests;

public sealed class UndoTests
{
    [Fact]
    public void Property_change_is_undone_and_redone()
    {
        var history = new UndoHistory();
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis");

        history.Execute(Rename(item, "Gros rubis"));
        Assert.Equal("Gros rubis", item.DisplayName);
        Assert.Equal("Modifier le nom", history.UndoDescription);

        history.Undo();
        Assert.Equal("Rubis", item.DisplayName);
        Assert.True(history.CanRedo);

        history.Redo();
        Assert.Equal("Gros rubis", item.DisplayName);
    }

    [Fact]
    public void Successive_keystrokes_are_merged_into_one_step()
    {
        var clock = new ManualClock();
        var history = new UndoHistory();
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "R");

        history.Execute(Rename(item, "Ru", clock));
        clock.Advance(TimeSpan.FromMilliseconds(300));
        history.Execute(Rename(item, "Rub", clock));

        history.Undo();
        Assert.Equal("R", item.DisplayName);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Changes_separated_by_a_pause_or_a_seal_are_separate_steps()
    {
        var clock = new ManualClock();
        var history = new UndoHistory();
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "A");

        history.Execute(Rename(item, "B", clock));
        clock.Advance(TimeSpan.FromSeconds(5));
        history.Execute(Rename(item, "C", clock));
        history.Seal();
        history.Execute(Rename(item, "D", clock));

        history.Undo();
        Assert.Equal("C", item.DisplayName);
        history.Undo();
        Assert.Equal("B", item.DisplayName);
        history.Undo();
        Assert.Equal("A", item.DisplayName);
    }

    [Fact]
    public void A_new_change_clears_redo()
    {
        var history = new UndoHistory();
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "A");
        history.Execute(Rename(item, "B"));
        history.Undo();

        history.Execute(new PropertyChangeCommand<int>("Taille", item, nameof(ItemAsset.MaxStackSize), () => item.MaxStackSize, v => item.MaxStackSize = v, 16));

        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Removing_an_asset_is_undone_at_the_same_position()
    {
        var registry = new AssetRegistry();
        var a = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("a"), "A");
        var b = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("b"), "B");
        var c = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("c"), "C");
        var history = new UndoHistory();
        history.Execute(new AddAssetCommand(registry, a, "Item"));
        history.Execute(new AddAssetCommand(registry, b, "Item"));
        history.Execute(new AddAssetCommand(registry, c, "Item"));

        history.Execute(new RemoveAssetCommand(registry, b, "Item"));
        Assert.Equal(["a", "c"], registry.Select(x => x.ResourceId.Value));

        history.Undo();
        Assert.Equal(["a", "b", "c"], registry.Select(x => x.ResourceId.Value));
    }

    [Fact]
    public void Project_settings_change_is_undoable()
    {
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Avant") { ModVersion = "1.0.0" };
        var draft = settings.Clone();
        draft.ModName = "Après";
        draft.ModVersion = "2.0.0";
        var history = new UndoHistory();

        history.Execute(new ChangeProjectSettingsCommand(settings, draft));
        Assert.Equal("Après", settings.ModName);

        history.Undo();
        Assert.Equal("Avant", settings.ModName);
        Assert.Equal("1.0.0", settings.ModVersion);
    }

    private static PropertyChangeCommand<string> Rename(ItemAsset item, string name, TimeProvider? clock = null) =>
        new("Modifier le nom", item, nameof(ItemAsset.DisplayName), () => item.DisplayName, v => item.DisplayName = v, name, clock);
}
