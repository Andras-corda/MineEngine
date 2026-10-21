using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Backends;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Minecraft.Resources;
using MineEngine.Minecraft.Resources.Imaging;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>V0.6.1: icon of the mod chosen in the project settings.</summary>
public sealed class ModIconTests
{
    private static readonly ProjectRepository Repository = new(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());

    [Fact]
    public void Icon_setting_survives_save_and_reopen()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        project.Settings.IconFile = "icon.png";
        Repository.Save(project);

        Assert.Equal("icon.png", Repository.Open(project.Layout.RootDirectory).Settings.IconFile);
        Assert.Equal("icon.png", project.Settings.Clone().IconFile);
    }

    [Fact]
    public void An_image_chosen_outside_the_project_is_copied_into_it()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        string outside = WritePng(Path.Combine(temp.Path, "my logo.png"));
        project.Settings.IconFile = outside;

        ProjectIcon.Adopt(project.Layout, project.Settings);

        Assert.Equal(ProjectIcon.DefaultFileName, project.Settings.IconFile);
        Assert.True(File.Exists(Path.Combine(project.Layout.RootDirectory, ProjectIcon.DefaultFileName)));

        // An image already in the project keeps its place, as a relative path.
        string inside = WritePng(Path.Combine(project.Layout.TexturesDirectory, "logo.png"));
        project.Settings.IconFile = inside;
        ProjectIcon.Adopt(project.Layout, project.Settings);
        Assert.Equal("Textures/logo.png", project.Settings.IconFile);
    }

    [Fact]
    public void Only_png_files_are_icons()
    {
        using var temp = new TemporaryDirectory();
        string text = Path.Combine(temp.Path, "fake.png");
        File.WriteAllText(text, "not an image");

        Assert.False(ProjectIcon.IsPng(text));
        Assert.True(ProjectIcon.IsPng(WritePng(Path.Combine(temp.Path, "real.png"))));
        Assert.False(ProjectIcon.IsPng(Path.Combine(temp.Path, "missing.png")));
    }

    [Fact]
    public async Task Icon_is_copied_into_the_mod_and_named_in_the_metadata()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        WritePng(Path.Combine(project.Layout.RootDirectory, "icon.png"));
        project.Settings.IconFile = "icon.png";

        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;
        Assert.Equal(Path.Combine(project.Layout.RootDirectory, "icon.png"), ir.Mod.IconFile);

        string workspace = Path.Combine(temp.Path, "workspace");
        GenerationResult result = await ModGenerator.CreateDefault(new Backend()).GenerateAsync(ir, workspace, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        Assert.True(ProjectIcon.IsPng(Path.Combine(workspace, "src/main/resources/testmod_logo.png")));

        string toml = new ModsTomlTemplate("#logoFile=\"examplemod.png\" #optional\n")
            .SetOptionalString("logoFile", ModIconEmitter.ResourceName(ir.Mod.ModId))
            .ToString();
        Assert.Equal("logoFile=\"testmod_logo.png\"\n", toml);
    }

    [Fact]
    public void A_missing_icon_file_is_a_warning_and_the_mod_has_no_icon()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp.Path);
        project.Settings.IconFile = "gone.png";
        var diagnostics = new DiagnosticBag();

        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;

        Assert.Null(ir.Mod.IconFile);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("gone.png"));
    }

    private static ModProject CreateProject(string directory) =>
        Repository.Create(directory, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = "neoforge", MinecraftVersion = "1.21.1" });

    private static string WritePng(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, new PlaceholderTexture(new PngEncoder()).GetPngBytes());
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
