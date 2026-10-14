using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Commands;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>
/// V0.4.2 : le projet est lui-même le projet Gradle du mod (comme un workspace MCreator).
/// Le code généré et le code de l'utilisateur cohabitent dans src/main/java.
/// </summary>
public sealed class ProjectArchitectureTests
{
    private static ProjectRepository Repository => new(new AssetSerializer(AssetCatalog.CreateDefault()), new ProjectSettingsSerializer());

    [Fact]
    public void New_projects_are_named_after_their_folder_and_reopen_from_the_project_file()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = Repository.Create(temp.Path, new ProjectSettings(ModId.Parse("ruby"), "Mon mod"));

        Assert.Equal("Mon mod.mineengine", project.Layout.ProjectFileName);
        Assert.True(Directory.Exists(project.Layout.ElementsDirectory));
        Assert.True(Directory.Exists(project.Layout.JavaSourceDirectory));
        Assert.True(ProjectLayout.ContainsProject(project.Layout.RootDirectory));

        project.Settings.LockedFiles = ["src\\main\\java\\A.java", "src/main/java/A.java"];
        Repository.Save(project);
        ModProject reopened = Repository.Open(project.Layout.ProjectFile);

        Assert.Equal(project.Layout.ProjectFile, reopened.Layout.ProjectFile);
        Assert.Equal(["src/main/java/A.java"], reopened.Settings.LockedFiles);
        Assert.True(reopened.Settings.IsLocked("src\\main\\java\\a.java"));
    }

    [Fact]
    public void Legacy_projects_move_scripts_into_their_package_and_keep_test_worlds()
    {
        using var temp = new TemporaryDirectory();
        string root = Path.Combine(temp.Path, "Ancien");
        Directory.CreateDirectory(Path.Combine(root, "Content", "Items"));
        File.WriteAllText(Path.Combine(root, "Project.json"), new ProjectSettingsSerializer().Serialize(new ProjectSettings(ModId.Parse("old"), "Ancien")));
        File.WriteAllText(Path.Combine(root, "Content", "Items", "ruby.asset.json"),
            $$$"""{"formatVersion":2,"guid":"{{{Guid.NewGuid()}}}","type":"Item","resourceId":"ruby","displayName":"Rubis","properties":{}}""");
        Directory.CreateDirectory(Path.Combine(root, "Scripts", "sub"));
        File.WriteAllText(Path.Combine(root, "Scripts", "sub", "Helper.java"), "package com.example.tools;\npublic class Helper {}\n");
        Directory.CreateDirectory(Path.Combine(root, "Generated", "run", "saves", "Monde"));
        Directory.CreateDirectory(Path.Combine(root, "Generated", "src", "main", "java"));

        ModProject project = Repository.Open(root);

        Assert.Equal("ruby", Assert.Single(project.Assets).ResourceId.Value);
        Assert.Equal("Ancien.mineengine", project.Layout.ProjectFileName);
        Assert.False(File.Exists(Path.Combine(root, "Project.json")));
        Assert.True(File.Exists(Path.Combine(root, "elements", "items", "ruby.asset.json")));
        Assert.True(File.Exists(Path.Combine(root, "src", "main", "java", "com", "example", "tools", "Helper.java")));
        Assert.False(Directory.Exists(Path.Combine(root, "Scripts")));
        Assert.True(Directory.Exists(Path.Combine(root, "run", "saves", "Monde")));
        Assert.False(Directory.Exists(Path.Combine(root, "Generated")));
        Assert.Contains(project.LoadNotes, n => n.Contains("MCreator", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Generation_keeps_user_code_and_locked_files_and_removes_stale_generated_files()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = Repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod")
        {
            LoaderId = "neoforge",
            MinecraftVersion = "1.21.1",
        });
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis"));
        var sapphire = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("sapphire"), "Saphir");
        project.Assets.Add(sapphire);
        string workspace = project.Layout.WorkspaceDirectory;

        await GenerateAsync(project);
        string userClass = Path.Combine(project.Layout.JavaSourceDirectory, "com", "example", "MyCode.java");
        Directory.CreateDirectory(Path.GetDirectoryName(userClass)!);
        File.WriteAllText(userClass, "package com.example; public class MyCode {}");

        const string itemsClass = "src/main/java/com/mineengine/mods/testmod/registry/ModItems.java";
        string sapphireModel = Path.Combine(workspace, "src/main/resources/assets/testmod/models/item/sapphire.json");
        Assert.True(File.Exists(sapphireModel));
        Assert.True(GeneratedFileManifest.Load(workspace).Contains(itemsClass));
        Assert.False(GeneratedFileManifest.Load(workspace).Contains("src/main/java/com/example/MyCode.java"));

        ChangeLockedFilesCommand.Lock(project.Settings, itemsClass).Execute();
        File.WriteAllText(Path.Combine(workspace, itemsClass), "// modifié à la main");
        project.Assets.Remove(sapphire);
        await GenerateAsync(project);

        Assert.Equal("// modifié à la main", File.ReadAllText(Path.Combine(workspace, itemsClass)));
        Assert.True(File.Exists(userClass));
        Assert.False(File.Exists(sapphireModel));
        Assert.True(GeneratedFileManifest.Load(workspace).Contains(itemsClass));

        ChangeLockedFilesCommand.Unlock(project.Settings, itemsClass).Execute();
        await GenerateAsync(project);
        Assert.Contains("RUBY", File.ReadAllText(Path.Combine(workspace, itemsClass)));
    }

    [Theory]
    [InlineData(ModLoaderKind.Forge, "@Mod.EventBusSubscriber(modid = \"ruby\")")]
    [InlineData(ModLoaderKind.NeoForge, "@EventBusSubscriber(modid = \"ruby\")")]
    public void Java_class_templates_follow_the_loader_and_the_folder_package(ModLoaderKind loader, string annotation)
    {
        string package = JavaClassTemplates.DefaultPackage(ModId.Parse("ruby"));
        string source = JavaClassTemplates.Create(JavaClassKind.EventListener, package, "Welcome", ModId.Parse("ruby"), loader);

        Assert.StartsWith("package com.mineengine.mods.ruby.custom;", source);
        Assert.Contains(annotation, source);
        Assert.Contains("public final class Welcome {", source);
        Assert.Equal("src/main/java/com/mineengine/mods/ruby/custom/Welcome.java", JavaClassTemplates.SourcePath(package, "Welcome"));
        Assert.Equal("a.b", JavaClassTemplates.PackageForFolder("src/main/java/a/b"));
        Assert.Equal(string.Empty, JavaClassTemplates.PackageForFolder("src/main/java"));
        Assert.Null(JavaClassTemplates.PackageForFolder("elements/items"));
        Assert.True(JavaClassTemplates.IsValidClassName("Welcome2"));
        Assert.False(JavaClassTemplates.IsValidClassName("class"));
        Assert.False(JavaClassTemplates.IsValidClassName("2Fast"));
    }

    private static async Task GenerateAsync(ModProject project)
    {
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, new DiagnosticBag())!;
        GenerationResult result = await ModGenerator.CreateDefault(new SourceOnlyBackend())
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None, project.Settings.LockedFiles);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
    }

    /// <summary>Backend NeoForge 1.21.1 sans MDK réel : seules les sources sont générées.</summary>
    private sealed class SourceOnlyBackend : IModLoaderBackend
    {
        public MdkDescriptor Mdk { get; } = new(
            "test", ModLoaderKind.NeoForge, MinecraftVersion.Parse("1.21.1"), "0", 21, "8.8", Path.GetTempPath(), "test", DateTime.Now);

        public string LoaderId => Mdk.Loader.ToId();

        public string DisplayName => Mdk.DisplayName;

        public MinecraftVersion MinecraftVersion => Mdk.MinecraftVersion;

        public IReadOnlyList<string> BuildTasks => ["build"];

        public IReadOnlyList<string> RunClientTasks => ["runClient"];

        public Task PrepareWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<IFileEmitter> CreateEmitters() => [new NeoForgeJavaEmitter()];

        public string GetOutputJarPath(string workspaceDirectory, IRModInfo mod) => string.Empty;
    }
}
