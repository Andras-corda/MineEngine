using MineEngine.IR.Model;
using MineEngine.Minecraft.Backends;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;
using MineEngine.Minecraft.Resources;

namespace MineEngine.Minecraft.Forge;

/// <summary>Backend Forge, basé sur le MDK officiel ForgeGradle.</summary>
public sealed class ForgeBackend : MdkBackend
{
    public ForgeBackend(MdkDescriptor mdk)
        : base(mdk)
    {
    }

    public override string LoaderId => ModLoaderKind.Forge.ToId();

    public override IReadOnlyList<IFileEmitter> CreateEmitters() =>
        [new ForgeJavaEmitter(), new ForgeMetadataEmitter(Mdk), new SpawnBiomeModifierEmitter("forge")];

    /// <summary>Forge lit les auteurs et la description dans gradle.properties.</summary>
    protected override void ConfigureProperties(GradlePropertiesFile properties, IRModInfo mod)
    {
        properties.Set("mod_authors", mod.Authors.Replace('"', '\''));
        properties.Set("mod_description", string.IsNullOrWhiteSpace(mod.Description)
            ? "Mod créé avec Mine Engine."
            : mod.Description.Trim().Replace("'''", "'", StringComparison.Ordinal));
    }
}

/// <summary>
/// Recopie les métadonnées du MDK Forge (META-INF/mods.toml et pack.mcmeta),
/// qui se trouvent dans les ressources réécrites à chaque génération.
/// </summary>
public sealed class ForgeMetadataEmitter : IFileEmitter
{
    private const string ModsToml = "src/main/resources/META-INF/mods.toml";
    private const string PackMcmeta = "src/main/resources/pack.mcmeta";

    private readonly MdkDescriptor _mdk;

    public ForgeMetadataEmitter(MdkDescriptor mdk)
    {
        _mdk = mdk ?? throw new ArgumentNullException(nameof(mdk));
    }

    public string Name => "Métadonnées Forge";

    public void Emit(GenerationContext context)
    {
        string modsToml = Path.Combine(_mdk.Directory, ModsToml.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(modsToml))
        {
            context.Diagnostics.Error($"Le MDK {_mdk.DisplayName} ne contient pas {ModsToml}.");
            return;
        }

        string content = new ModsTomlTemplate(File.ReadAllText(modsToml))
            .SetOptionalString("displayURL", context.Mod.Website)
            .ToString();
        context.Files.WriteText(ModsToml, content);

        string packMcmeta = Path.Combine(_mdk.Directory, PackMcmeta.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(packMcmeta))
        {
            context.Files.CopyFile(packMcmeta, PackMcmeta);
        }
    }
}

public sealed class ForgeBackendProvider : IModLoaderBackendProvider
{
    private static readonly string[] SupportedVersions = ["1.20.1", "1.21.1"];

    public string SupportDescription => "Forge 1.20.1, Forge 1.21.1";

    public bool Supports(MdkDescriptor mdk) =>
        mdk.Loader == ModLoaderKind.Forge && SupportedVersions.Any(v => mdk.MinecraftVersion.Equals(MinecraftVersion.Parse(v)));

    public IModLoaderBackend Create(MdkDescriptor mdk) => new ForgeBackend(mdk);
}
