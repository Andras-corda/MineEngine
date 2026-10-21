using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>3D models imported in the Resources tab.</summary>
public sealed class ModelAssetTests
{
    private const string JavaModel = """{ "textures": { "0": "minecraft:block/stone" }, "elements": [ { "from": [0, 0, 0], "to": [16, 8, 16] } ] }""";

    private static readonly ProjectRepository Repository = new(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());

    [Theory]
    [InlineData("altar.json", ModelFormat.JavaJson)]
    [InlineData("golem.geo.json", ModelFormat.BedrockGeometry)]
    [InlineData("golem.bbmodel", ModelFormat.Blockbench)]
    [InlineData("rock.obj", ModelFormat.Obj)]
    [InlineData("ship.glb", ModelFormat.Gltf)]
    public void Format_comes_from_the_file_name(string file, ModelFormat format)
    {
        Assert.Equal(format, ModelFormats.FromPath(file));
        Assert.Null(ModelFormats.FromPath("picture.png"));
    }

    [Fact]
    public void Files_that_are_not_models_are_refused()
    {
        using var temp = new TemporaryDirectory();
        string notModel = Write(temp.Path, "settings.json", """{ "volume": 3 }""");
        string broken = Write(temp.Path, "broken.json", "{ not json");
        string emptyObj = Write(temp.Path, "empty.obj", "# nothing");

        Assert.NotNull(ModelFormats.DescribeProblem(notModel));
        Assert.NotNull(ModelFormats.DescribeProblem(broken));
        Assert.NotNull(ModelFormats.DescribeProblem(emptyObj));
        Assert.Null(ModelFormats.DescribeProblem(Write(temp.Path, "altar.json", JavaModel)));
        Assert.Null(ModelFormats.DescribeProblem(Write(temp.Path, "rock.obj", "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3")));
        Assert.Null(ModelFormats.DescribeProblem(Write(temp.Path, "golem.bbmodel", """{ "meta": { "format_version": "4.5" } }""")));
    }

    [Fact]
    public async Task Java_models_are_copied_into_the_mod_and_others_stay_in_the_project()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = Repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = "neoforge", MinecraftVersion = "1.21.1" });
        var importer = new AssetFileImporter();
        ModelAsset altar = importer.ImportModel(project, Write(temp.Path, "Magic Altar.json", JavaModel));
        ModelAsset golem = importer.ImportModel(project, Write(temp.Path, "golem.geo.json", """{ "format_version": "1.12.0", "minecraft:geometry": [] }"""));
        project.Assets.Add(altar);
        project.Assets.Add(golem);

        Assert.Equal("magic_altar", altar.ResourceId.Value);
        Assert.StartsWith("Models/", altar.FilePath);
        Assert.EndsWith(".geo.json", golem.FilePath);
        Assert.Throws<InvalidDataException>(() => importer.ImportModel(project, Write(temp.Path, "bad.json", "{}")));

        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.Equal("magic_altar", Assert.Single(ir.Content.Models).Id.Value);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Info && d.AssetId == golem.Id);

        string workspace = Path.Combine(temp.Path, "workspace");
        GenerationResult result = await ModGenerator.CreateDefault(new Backend()).GenerateAsync(ir, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        Assert.Equal(JavaModel, File.ReadAllText(Path.Combine(workspace, "src/main/resources/assets/testmod/models/custom/magic_altar.json")));

        Repository.Save(project);
        ModProject reopened = Repository.Open(project.Layout.RootDirectory);
        Assert.Equal(altar.FilePath, ((ModelAsset)reopened.Assets.Find(altar.Id)!).FilePath);
        Assert.Equal(AssetType.Model, reopened.Assets.Find(golem.Id)!.Type);
    }

    private static string Write(string directory, string name, string content)
    {
        string file = Path.Combine(directory, name);
        File.WriteAllText(file, content);
        return file;
    }

    private sealed class Backend : IModLoaderBackend
    {
        public Backend()
        {
            Mdk = new MdkDescriptor("test", ModLoaderKind.NeoForge, MinecraftVersion.Parse("1.21.1"), "0", 21, null, Path.GetTempPath(), "test", DateTime.Now);
        }

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MdkDescriptor Mdk { get; }

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => [new NeoForgeJavaEmitter()];

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
