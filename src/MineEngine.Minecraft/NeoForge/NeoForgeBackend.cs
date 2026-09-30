using MineEngine.IR.Model;
using MineEngine.Minecraft.Backends;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Mdk;

namespace MineEngine.Minecraft.NeoForge;

/// <summary>Backend NeoForge, basé sur le MDK ModDevGradle.</summary>
public sealed class NeoForgeBackend : MdkBackend
{
    /// <summary>Le MDK NeoForge génère ce fichier à partir d'un modèle placé hors des ressources.</summary>
    private const string ModsTomlRelativePath = "src/main/templates/META-INF/neoforge.mods.toml";

    public NeoForgeBackend(MdkDescriptor mdk)
        : base(mdk)
    {
    }

    public override string LoaderId => ModLoaderKind.NeoForge.ToId();

    protected override IReadOnlyList<string> AdditionalExclusions { get; } = [ModsTomlRelativePath];

    public override IReadOnlyList<IFileEmitter> CreateEmitters() => [new NeoForgeJavaEmitter()];

    protected override async Task ConfigureWorkspaceAsync(GenerationContext context, CancellationToken cancellationToken)
    {
        IRModInfo mod = context.Mod;
        string template = await ReadMdkFileAsync(ModsTomlRelativePath, cancellationToken).ConfigureAwait(false);
        string modsToml = new ModsTomlTemplate(template)
            .ReplaceExampleDescription(mod.Description)
            .SetOptionalString("authors", mod.Authors)
            .SetOptionalString("displayURL", mod.Website)
            .ToString();
        context.Files.WriteText(ModsTomlRelativePath, modsToml);
    }
}

public sealed class NeoForgeBackendProvider : IModLoaderBackendProvider
{
    private static readonly string[] SupportedVersions = ["1.21", "1.21.1"];

    public string SupportDescription => "NeoForge 1.21.1";

    public bool Supports(MdkDescriptor mdk) =>
        mdk.Loader == ModLoaderKind.NeoForge && SupportedVersions.Any(v => mdk.MinecraftVersion.Equals(MinecraftVersion.Parse(v)));

    public IModLoaderBackend Create(MdkDescriptor mdk) => new NeoForgeBackend(mdk);
}
