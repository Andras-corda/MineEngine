using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Project;
using MineEngine.Project.Serialization;
using MineEngine.Project.Workspace;

namespace MineEngine.Tests;

/// <summary>Accueil : liste des projets connus et aperçu d'un projet.</summary>
public sealed class WorkspaceTests
{
    [Fact]
    public void Known_projects_are_saved_once_per_folder_with_the_latest_opening()
    {
        using var temp = new TemporaryDirectory();
        var store = new KnownProjectsStore(Path.Combine(temp.Path, "projects.json"));
        Assert.False(store.Exists);
        Assert.Empty(store.Load());

        DateTimeOffset yesterday = DateTimeOffset.Now.AddDays(-1);
        DateTimeOffset today = DateTimeOffset.Now;
        string folder = Path.Combine(temp.Path, "Mon Mod");
        store.Save(
        [
            new KnownProject(folder, yesterday),
            new KnownProject(folder + Path.DirectorySeparatorChar, today),
            new KnownProject(Path.Combine(temp.Path, "Autre"), null),
        ]);

        IReadOnlyList<KnownProject> loaded = store.Load();
        Assert.True(store.Exists);
        Assert.Equal(2, loaded.Count);
        KnownProject mine = Assert.Single(loaded, p => p.RootDirectory.EndsWith("Mon Mod", StringComparison.Ordinal));
        Assert.Equal(today.ToUnixTimeSeconds(), mine.LastOpened!.Value.ToUnixTimeSeconds());
        Assert.Null(Assert.Single(loaded, p => p.RootDirectory.EndsWith("Autre", StringComparison.Ordinal)).LastOpened);
    }

    [Fact]
    public void An_unreadable_list_gives_an_empty_list()
    {
        using var temp = new TemporaryDirectory();
        string file = Path.Combine(temp.Path, "projects.json");
        File.WriteAllText(file, "{ pas du json");

        Assert.Empty(new KnownProjectsStore(file).Load());
    }

    [Fact]
    public void A_project_summary_counts_assets_by_type_without_opening_the_project()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        var settings = new ProjectSettings(ModId.Parse("testmod"), "Test Mod")
        {
            LoaderId = "neoforge", MinecraftVersion = "1.21.1", Description = "Un mod de test",
        };
        ModProject project = repository.Create(temp.Path, settings);
        SampleContent.AddTo(project);
        repository.Save(project);

        ProjectSummary summary = ProjectSummary.TryRead(project.Layout.RootDirectory, new ProjectSettingsSerializer())!;

        Assert.Equal("Test Mod", summary.Settings.ModName);
        Assert.Equal("Un mod de test", summary.Settings.Description);
        Assert.Equal(3, summary.Count(AssetType.Item));
        Assert.Equal(5, summary.Count(AssetType.Block));
        Assert.Equal(3, summary.Count(AssetType.Mob));
        Assert.Equal(3, summary.Count(AssetType.Recipe));
        Assert.Equal(2, summary.PreviewTextures.Count);

        Assert.Null(ProjectSummary.TryRead(Path.Combine(temp.Path, "absent"), new ProjectSettingsSerializer()));
    }
}
