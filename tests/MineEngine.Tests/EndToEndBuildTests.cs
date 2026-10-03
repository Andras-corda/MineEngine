using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Build;
using MineEngine.Build.Gradle;
using MineEngine.Build.Java;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Project;
using MineEngine.Project.Serialization;
using Xunit.Abstractions;

namespace MineEngine.Tests;

/// <summary>
/// Build complet avec les vrais MDK et Gradle : télécharge plusieurs centaines
/// de Mo et prend plusieurs minutes par MDK la première fois. Exécuté seulement
/// si la variable d'environnement MINEENGINE_E2E vaut 1.
/// </summary>
public sealed class EndToEndBuildTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "mineengine-e2e");

    private readonly ITestOutputHelper _output;

    public EndToEndBuildTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("forge-1.20.1-47.4.10")]
    [InlineData("forge-1.21.1-52.1.0")]
    [InlineData("neoforge-1.21.1-21.1.252")]
    public async Task Sample_mod_builds_into_a_jar(string mdkId)
    {
        if (Environment.GetEnvironmentVariable("MINEENGINE_E2E") != "1")
        {
            return;
        }

        var log = new TestLog(_output);
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var library = new MdkLibrary(Path.Combine(Root, "mdks"), new MdkInspector(), new MdkManifestSerializer());
        library.Refresh(log);

        MdkDescriptor? mdk = library.Find(mdkId);
        if (mdk is null)
        {
            MdkCatalog catalog = MdkCatalog.CreateDefault(http);
            mdk = await catalog.DownloadAsync(catalog.Downloads.Single(d => d.Id == mdkId), library, log, CancellationToken.None);
        }

        string projectDirectory = Path.Combine(Root, "projects");
        var settings = new ProjectSettings(ModId.Parse("e2emod"), "E2E Mod " + mdkId)
        {
            MdkId = mdk.Id,
            LoaderId = mdk.Loader.ToId(),
            MinecraftVersion = mdk.MinecraftVersion.Id,
            Authors = "Mine Engine",
            Website = "https://example.com",
        };
        string existing = ProjectRepository.GetProjectDirectory(projectDirectory, settings);
        if (Directory.Exists(existing))
        {
            Directory.Delete(existing, recursive: true);
        }

        var repository = new ProjectRepository(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());
        ModProject project = repository.Create(projectDirectory, settings);
        SampleContent.AddTo(project);
        repository.Save(project);

        var pipeline = new BuildPipeline(
            ModIRBuilder.CreateDefault(), library, ModLoaderBackendRegistry.CreateDefault(), ModGenerator.CreateDefault,
            new JdkLocator(), new GradleJavaCompatibility(), new GradleRunner());

        BuildResult result = await pipeline.BuildAsync(project, log, CancellationToken.None);

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        Assert.True(File.Exists(result.JarPath));
    }

    private sealed class TestLog : ILog
    {
        private readonly ITestOutputHelper _output;

        public TestLog(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Write(LogLevel level, string message) => _output.WriteLine($"[{level}] {message}");
    }
}
