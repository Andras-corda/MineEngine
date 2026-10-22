using System.IO.Compression;
using System.Text;
using MineEngine.Project.Vanilla;

namespace MineEngine.Tests;

/// <summary>Textures and models of Minecraft offered in the asset browser.</summary>
public sealed class VanillaAssetTests
{
    [Theory]
    [InlineData("assets/minecraft/textures/block/stone.png", VanillaAssetKind.Texture, "block/stone")]
    [InlineData("assets/minecraft/textures/item/diamond.png", VanillaAssetKind.Texture, "item/diamond")]
    [InlineData("assets/minecraft/models/block/cube_all.json", VanillaAssetKind.Model, "block/cube_all")]
    public void Entries_of_the_jar_become_assets(string entry, VanillaAssetKind kind, string path)
    {
        VanillaAsset asset = VanillaAssetLibrary.Parse(entry)!;

        Assert.Equal(kind, asset.Kind);
        Assert.Equal(path, asset.Path);
        Assert.Equal("minecraft:" + path, asset.Reference);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/gui/title.png")]
    [InlineData("assets/minecraft/textures/block/stone.png.mcmeta")]
    [InlineData("assets/minecraft/models/item/handheld")]
    [InlineData("net/minecraft/Block.class")]
    public void Other_entries_are_ignored(string entry) => Assert.Null(VanillaAssetLibrary.Parse(entry));

    [Fact]
    public void A_model_is_shown_by_the_first_texture_of_its_parents()
    {
        using var temp = new TemporaryDirectory();
        string jar = Path.Combine(temp.Path, "client.jar");
        using (ZipArchive zip = ZipFile.Open(jar, ZipArchiveMode.Create))
        {
            Write(zip, "assets/minecraft/models/item/stone.json", """{ "parent": "minecraft:block/stone" }""");
            Write(zip, "assets/minecraft/models/block/stone.json", """{ "parent": "minecraft:block/cube_all", "textures": { "all": "minecraft:block/stone" } }""");
            Write(zip, "assets/minecraft/textures/block/stone.png", "png");
        }

        VanillaAssetLibrary library = VanillaAssetLibrary.Open(jar, "1.21.1", Path.Combine(temp.Path, "cache"));
        library.ExtractTextures();
        VanillaAsset item = library.Models.Single(m => m.Path == "item/stone");

        VanillaAsset texture = library.PreviewTexture(item)!;

        Assert.Equal("block/stone", texture.Path);
        Assert.True(File.Exists(library.FileOf(texture)));
        Assert.Equal(3, library.Assets.Count);
    }

    [Fact]
    public void The_minecraft_of_this_computer_is_read_when_it_is_installed()
    {
        using var temp = new TemporaryDirectory();
        if (VanillaAssetLibrary.For("1.21.1", temp.Path) is not { } library)
        {
            return; // Minecraft is not on this computer.
        }

        Assert.Contains(library.Textures, t => t.Path == "block/stone");
        Assert.Contains(library.Models, m => m.Path == "block/stone");
        Assert.Equal("block/stone", library.PreviewTexture(library.Models.First(m => m.Path == "item/stone"))?.Path);
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        using Stream stream = zip.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}
