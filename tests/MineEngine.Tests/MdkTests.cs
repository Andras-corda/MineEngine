using System.IO.Compression;
using MineEngine.Build.Java;
using MineEngine.Core.Logging;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Tests;

public sealed class MdkTests
{
    [Fact]
    public void Minecraft_versions_are_compared_numerically()
    {
        Assert.True(MinecraftVersion.Parse("1.21.10").CompareTo(MinecraftVersion.Parse("1.21.9")) > 0);
        Assert.Equal(MinecraftVersion.Parse("1.21"), MinecraftVersion.Parse("1.21.0"));
        Assert.Equal(17, MinecraftVersion.Parse("1.20.1").RequiredJavaVersion);
        Assert.Equal(21, MinecraftVersion.Parse("1.20.6").RequiredJavaVersion);
        Assert.False(MinecraftVersion.Parse("1.20.1").UsesSingularDataFolders);
        Assert.True(MinecraftVersion.Parse("1.21.1").UsesSingularDataFolders);
    }

    [Theory]
    [InlineData("forge_version=47.4.10", ModLoaderKind.Forge, "47.4.10")]
    [InlineData("neo_version=21.1.252", ModLoaderKind.NeoForge, "21.1.252")]
    public void Inspector_detects_loader_and_versions(string loaderLine, ModLoaderKind expectedLoader, string expectedVersion)
    {
        using var temp = new TemporaryDirectory();
        CreateFakeMdk(temp.Path, loaderLine, "1.20.1", javaVersion: 17, gradle: "8.8");

        MdkInspection inspection = new MdkInspector().Inspect(temp.Path);

        Assert.Equal(expectedLoader, inspection.Loader);
        Assert.Equal(expectedVersion, inspection.LoaderVersion);
        Assert.Equal("1.20.1", inspection.MinecraftVersion.Id);
        Assert.Equal(17, inspection.JavaVersion);
        Assert.Equal("8.8", inspection.GradleVersion);
    }

    [Fact]
    public void Folder_without_gradle_wrapper_is_rejected()
    {
        using var temp = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => new MdkInspector().Inspect(temp.Path));
    }

    [Fact]
    public void Library_imports_archives_and_detects_dropped_files()
    {
        using var temp = new TemporaryDirectory();
        var library = new MdkLibrary(Path.Combine(temp.Path, "mdks"), new MdkInspector(), new MdkManifestSerializer());

        // Archive importée depuis l'éditeur : le MDK est dans un sous-dossier de l'archive.
        string source = Path.Combine(temp.Path, "source", "forge-mdk");
        CreateFakeMdk(source, "forge_version=47.4.10", "1.20.1", 17, "8.8");
        string archive = Path.Combine(temp.Path, "forge.zip");
        ZipFile.CreateFromDirectory(Path.Combine(temp.Path, "source"), archive);
        MdkDescriptor imported = library.ImportArchive(archive, "test");
        Assert.Equal("forge-1.20.1-47.4.10", imported.Id);
        Assert.Throws<IOException>(() => library.ImportArchive(archive, "test"));

        // Archive et dossier déposés à la main dans la bibliothèque.
        string dropped = Path.Combine(temp.Path, "dropped");
        CreateFakeMdk(dropped, "neo_version=21.1.252", "1.21.1", 21, "9.2.1");
        ZipFile.CreateFromDirectory(dropped, Path.Combine(library.RootDirectory, "neoforge-mdk.zip"));
        CreateFakeMdk(Path.Combine(library.RootDirectory, "forge-1.21.1-manual"), "forge_version=52.1.0", "1.21.1", 21, "8.12.1");

        library.Refresh(NullLog.Instance);

        Assert.Equal(
            ["forge-1.21.1-52.1.0", "neoforge-1.21.1-21.1.252", "forge-1.20.1-47.4.10"],
            library.Installed.Select(m => m.Id));

        // Une seconde lecture ne réimporte pas l'archive déjà installée.
        library.Refresh(NullLog.Instance);
        Assert.Equal(3, library.Installed.Count);

        library.Remove(library.Find("forge-1.20.1-47.4.10")!);
        Assert.Null(library.Find("forge-1.20.1-47.4.10"));
        Assert.False(Directory.Exists(imported.Directory));
    }

    [Fact]
    public void Backend_registry_only_accepts_supported_versions()
    {
        ModLoaderBackendRegistry registry = ModLoaderBackendRegistry.CreateDefault();

        Assert.True(registry.Supports(Descriptor(ModLoaderKind.Forge, "1.20.1")));
        Assert.True(registry.Supports(Descriptor(ModLoaderKind.Forge, "1.21.1")));
        Assert.True(registry.Supports(Descriptor(ModLoaderKind.NeoForge, "1.21.1")));
        Assert.False(registry.Supports(Descriptor(ModLoaderKind.Forge, "1.19.2")));
        Assert.False(registry.Supports(Descriptor(ModLoaderKind.Fabric, "1.21.1")));
    }

    [Theory]
    [InlineData("8.8", 22)]
    [InlineData("8.12.1", 23)]
    [InlineData("9.2.1", 25)]
    [InlineData(null, 25)]
    public void Gradle_version_limits_the_java_used_to_run_it(string? gradle, int expected) =>
        Assert.Equal(expected, new GradleJavaCompatibility().MaximumJavaFor(gradle));

    private static MdkDescriptor Descriptor(ModLoaderKind loader, string minecraftVersion) =>
        new("test", loader, MinecraftVersion.Parse(minecraftVersion), "1", 21, null, Path.GetTempPath(), "test", DateTime.Now);

    private static void CreateFakeMdk(string directory, string loaderLine, string minecraftVersion, int javaVersion, string gradle)
    {
        Directory.CreateDirectory(Path.Combine(directory, "gradle", "wrapper"));
        File.WriteAllText(Path.Combine(directory, "gradlew.bat"), "@echo off");
        File.WriteAllText(Path.Combine(directory, "gradle.properties"), $"# test\nminecraft_version={minecraftVersion}\n{loaderLine}\n");
        File.WriteAllText(Path.Combine(directory, "build.gradle"), $"java.toolchain.languageVersion = JavaLanguageVersion.of({javaVersion})\n");
        File.WriteAllText(
            Path.Combine(directory, "gradle", "wrapper", "gradle-wrapper.properties"),
            $"distributionUrl=https\\://services.gradle.org/distributions/gradle-{gradle}-bin.zip\n");
    }
}
