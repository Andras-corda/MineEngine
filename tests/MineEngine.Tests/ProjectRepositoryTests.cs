using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Identifiers;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

public sealed class ProjectRepositoryTests
{
    private readonly ProjectRepository _repository =
        new(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());

    [Fact]
    public void Saved_project_is_reopened_identically()
    {
        using var temp = new TemporaryDirectory();
        var settings = new ProjectSettings(ModId.Parse("monmod"), "Mon Mod")
        {
            MdkId = "forge-1.20.1-47.4.10",
            LoaderId = "forge",
            MinecraftVersion = "1.20.1",
            Website = "https://example.com/monmod",
        };
        ModProject project = _repository.Create(temp.Path, settings);

        var sword = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("magic_sword"), "Épée magique") { MaxStackSize = 1 };
        var block = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("ruby_block"), "Bloc de rubis") { Hardness = 3f, Resistance = 9.5f };
        project.Assets.Add(sword);
        project.Assets.Add(block);
        Assert.True(project.IsDirty);

        _repository.Save(project);
        Assert.False(project.IsDirty);

        ModProject reopened = _repository.Open(project.Layout.ProjectFile);
        Assert.Equal("Mon Mod", reopened.Name);
        Assert.Equal("monmod", reopened.Settings.ModId.Value);
        Assert.Equal("forge-1.20.1-47.4.10", reopened.Settings.MdkId);
        Assert.Equal("1.20.1", reopened.Settings.MinecraftVersion);
        Assert.Equal("https://example.com/monmod", reopened.Settings.Website);

        var reopenedSword = Assert.IsType<ItemAsset>(reopened.Assets.Find(sword.Id));
        Assert.Equal("Épée magique", reopenedSword.DisplayName);
        Assert.Equal(1, reopenedSword.MaxStackSize);

        var reopenedBlock = Assert.IsType<BlockAsset>(reopened.Assets.Find(block.Id));
        Assert.Equal(3f, reopenedBlock.Hardness);
        Assert.Equal(9.5f, reopenedBlock.Resistance);
    }

    [Fact]
    public void Renamed_asset_does_not_leave_an_old_file()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = _repository.Create(temp.Path, new ProjectSettings(ModId.Parse("test"), "Test"));
        var item = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("old_name"), "Item");
        project.Assets.Add(item);
        _repository.Save(project);

        item.ResourceId = ResourceId.Parse("new_name");
        _repository.Save(project);

        string[] files = Directory.GetFiles(project.Layout.ContentDirectory).Select(Path.GetFileName).ToArray()!;
        Assert.Equal(["new_name.asset.json"], files);
    }

    [Fact]
    public void Saving_twice_produces_identical_files()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = _repository.Create(temp.Path, new ProjectSettings(ModId.Parse("test"), "Test"));
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("ruby_block"), "Ruby"));

        _repository.Save(project);
        string first = File.ReadAllText(Path.Combine(project.Layout.ContentDirectory, "ruby_block.asset.json"));
        _repository.Save(project);
        string second = File.ReadAllText(Path.Combine(project.Layout.ContentDirectory, "ruby_block.asset.json"));

        Assert.Equal(first, second);
        Assert.DoesNotContain("\r\n", first);
    }

    [Fact]
    public void Creating_a_project_in_a_non_empty_folder_fails()
    {
        using var temp = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Test"));
        File.WriteAllText(Path.Combine(temp.Path, "Test", "existing.txt"), "x");

        Assert.Throws<IOException>(() => _repository.Create(temp.Path, new ProjectSettings(ModId.Parse("test"), "Test")));
    }
}
