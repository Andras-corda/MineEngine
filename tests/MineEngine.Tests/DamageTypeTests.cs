using System.Text.Json.Nodes;
using MineEngine.Assets;
using MineEngine.Assets.Definitions;
using MineEngine.Assets.Serialization;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Core.Logging;
using MineEngine.Generator;
using MineEngine.IR;
using MineEngine.IR.Model;
using MineEngine.Minecraft;
using MineEngine.Minecraft.Forge;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.NeoForge;
using MineEngine.Project;
using MineEngine.Project.Serialization;

namespace MineEngine.Tests;

/// <summary>Damage types of V0.7.10: the element, its data, its messages, its tags, the node and the immunity of a mob.</summary>
public sealed class DamageTypeTests
{
    private static readonly AssetSerializer Serializer = new(AssetCatalog.CreateDefault());

    private static ModProject CreateProject(TemporaryDirectory temp, ModLoaderKind loader, string version)
    {
        var repository = new ProjectRepository(Serializer, new ProjectSettingsSerializer());
        ModProject project = repository.Create(temp.Path, new ProjectSettings(ModId.Parse("testmod"), "Test Mod") { LoaderId = loader.ToId(), MinecraftVersion = version });
        SampleContent.AddTo(project);
        return project;
    }

    [Fact]
    public void A_damage_type_survives_a_save_and_reload()
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, ModLoaderKind.NeoForge, "1.21.1");
        DamageTypeAsset zap = project.Assets.OfType<DamageTypeAsset>().Single();
        MobAsset guardian = project.Assets.OfType<MobAsset>().Single(m => m.ResourceId.Value == "stone_guardian");

        var reloaded = (DamageTypeAsset)Serializer.Deserialize(Serializer.Serialize(zap), "rune_zap");
        var mob = (MobAsset)Serializer.Deserialize(Serializer.Serialize(guardian), "stone_guardian");

        Assert.Equal(zap.DeathMessage, reloaded.DeathMessage);
        Assert.Equal(zap.KillerMessage, reloaded.KillerMessage);
        Assert.Equal(0.3f, reloaded.Exhaustion);
        Assert.Equal(DamageScaling.Never, reloaded.Scaling);
        Assert.Equal(DamageEffects.Burning, reloaded.Effects);
        Assert.True(reloaded.Flags.SetEquals([DamageTypeFlag.BypassesArmor, DamageTypeFlag.IsLightning]));
        Assert.Equal([zap.Id], mob.ImmuneDamageTypes);
    }

    [Fact]
    public void A_killer_message_without_the_killer_is_reported()
    {
        var damage = new DamageTypeAsset(Guid.NewGuid(), ResourceId.Parse("odd"), "Odd") { KillerMessage = "{victim} died oddly" };
        var diagnostics = new DiagnosticBag();

        damage.Validate(diagnostics);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Info && d.Message.Contains("{killer}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ModLoaderKind.NeoForge, "1.21.1")]
    [InlineData(ModLoaderKind.Forge, "1.20.1")]
    public async Task A_damage_type_becomes_data_messages_and_tags(ModLoaderKind loader, string version)
    {
        using var temp = new TemporaryDirectory();
        ModProject project = CreateProject(temp, loader, version);
        bool modern = version != "1.20.1";
        var diagnostics = new DiagnosticBag();
        ModIR ir = ModIRBuilder.CreateDefault().Build(project, diagnostics)!;
        Assert.False(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error), string.Join('\n', diagnostics));
        IFileEmitter emitter = loader == ModLoaderKind.NeoForge ? new NeoForgeJavaEmitter() : new ForgeJavaEmitter();
        GenerationResult result = await ModGenerator.CreateDefault(new ProgramTests.SourceBackend(loader, version, emitter))
            .GenerateAsync(ir, project.Layout.WorkspaceDirectory, NullLog.Instance, CancellationToken.None);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        string resources = Path.Combine(project.Layout.WorkspaceDirectory, "src/main/resources");
        string java = Path.Combine(project.Layout.JavaSourceDirectory, "com/mineengine/mods/testmod");

        JsonObject data = JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "data/testmod/damage_type/rune_zap.json")))!.AsObject();
        Assert.Equal("testmod.rune_zap", data["message_id"]!.GetValue<string>());
        Assert.Equal("never", data["scaling"]!.GetValue<string>());
        Assert.Equal("burning", data["effects"]!.GetValue<string>());
        Assert.Equal("default", data["death_message_type"]!.GetValue<string>());
        Assert.Contains("testmod:rune_zap", File.ReadAllText(Path.Combine(resources, "data/minecraft/tags/damage_type/bypasses_armor.json")));
        Assert.Contains("testmod:rune_zap", File.ReadAllText(Path.Combine(resources, "data/minecraft/tags/damage_type/is_lightning.json")));

        JsonObject lang = JsonNode.Parse(File.ReadAllText(Path.Combine(resources, "assets/testmod/lang/en_us.json")))!.AsObject();
        Assert.Equal("%1$s was zapped by a rune (100%%)", lang["death.attack.testmod.rune_zap"]!.GetValue<string>());
        Assert.Equal("%1$s was zapped by %2$s", lang["death.attack.testmod.rune_zap.player"]!.GetValue<string>());

        string guardian = File.ReadAllText(Path.Combine(java, "entity/StoneGuardianEntity.java"));
        Assert.Contains(modern
            ? "source.is(ResourceKey.create(Registries.DAMAGE_TYPE, ResourceLocation.parse(\"testmod:rune_zap\")))"
            : "source.is(ResourceKey.create(Registries.DAMAGE_TYPE, new ResourceLocation(\"testmod:rune_zap\")))", guardian);

        string behavior = File.ReadAllText(Path.Combine(java, "behavior/ZapEventsBehavior.java"));
        Assert.Contains("damageOfType(level, \"rune_zap\", player, \"testmod\")", behavior);
        Assert.Contains("private static DamageSource damageOfType(Level level, String type, Entity attacker, String mod) {", behavior);
    }
}
