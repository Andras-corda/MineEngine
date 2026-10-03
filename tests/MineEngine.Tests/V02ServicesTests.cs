using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Build.Gradle;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Minecraft.Generation;
using MineEngine.Project;
using MineEngine.Project.Backups;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

public sealed class V02ServicesTests
{
    [Theory]
    [InlineData("error: cannot find symbol")]
    [InlineData("erreur : symbole introuvable")]
    public void Javac_errors_are_mapped_to_the_asset_that_produced_the_line(string javacMessage)
    {
        var map = new SourceMap();
        Guid assetId = Guid.NewGuid();
        map.Add("src/main/java/com/mineengine/mods/test/registry/ModItems.java", 10, 11, assetId, "Item 'ruby'");
        string workspace = Path.Combine(Path.GetTempPath(), "ws");
        string file = Path.Combine(workspace, "src", "main", "java", "com", "mineengine", "mods", "test", "registry", "ModItems.java");

        IReadOnlyList<Diagnostic> diagnostics = new CompilerErrorMapper().Map(
            [$"{file}:11: {javacMessage}", "> Task :compileJava FAILED", $"{file}:3: warning: [deprecation] x"],
            workspace,
            map);

        Assert.Equal(2, diagnostics.Count);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
        Assert.Equal(assetId, diagnostics[0].AssetId);
        Assert.Equal("Item 'ruby'", diagnostics[0].Source);
        Assert.Null(diagnostics[1].AssetId);
        Assert.Equal("src/main/java/com/mineengine/mods/test/registry/ModItems.java:3", diagnostics[1].Source);
    }

    [Fact]
    public void Backups_respect_the_interval_and_keep_only_the_most_recent()
    {
        using var temp = new TemporaryDirectory();
        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test"));
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis"));
        repository.Save(project);

        var clock = new ManualClock();
        var backups = new ProjectBackupService(clock);
        TimeSpan interval = TimeSpan.FromMinutes(10);

        Assert.NotNull(backups.CreateIfDue(project, interval, keep: 2));
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Null(backups.CreateIfDue(project, interval, keep: 2));

        for (int i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(11));
            Assert.NotNull(backups.CreateIfDue(project, interval, keep: 2));
        }

        IReadOnlyList<ProjectBackup> kept = backups.List(project);
        Assert.Equal(2, kept.Count);
        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(kept[0].File);
        Assert.Contains(archive.Entries, e => e.FullName == "Content/Items/ruby.asset.json");
        Assert.Contains(archive.Entries, e => e.FullName == ProjectLayout.ProjectFileName);
    }

    [Fact]
    public void File_log_and_composite_log_write_every_message()
    {
        using var temp = new TemporaryDirectory();
        var memory = new MemoryLog();
        string file;
        using (var fileLog = new FileLog(temp.Path))
        {
            var log = new CompositeLog(memory, fileLog);
            log.Info("Bonjour");
            log.Error("Problème");
            file = fileLog.CurrentFile;
        }

        Assert.Equal(["Bonjour", "Problème"], memory.Messages);
        string content = File.ReadAllText(file);
        Assert.Contains("[INFO   ] Bonjour", content);
        Assert.Contains("[ERROR  ] Problème", content);
    }

    private sealed class MemoryLog : ILog
    {
        public List<string> Messages { get; } = [];

        public void Write(LogLevel level, string message) => Messages.Add(message);
    }
}
