using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.IR.Lowering;
using MineEngine.IR.Model;
using MineEngine.Project;

namespace MineEngine.IR;

/// <summary>Construit le Mod IR à partir d'un projet, après l'avoir validé.</summary>
public sealed class ModIRBuilder
{
    private readonly Dictionary<AssetType, IAssetLowering> _lowerings = [];

    public ModIRBuilder(IEnumerable<IAssetLowering> lowerings)
    {
        foreach (IAssetLowering lowering in lowerings)
        {
            if (!_lowerings.TryAdd(lowering.AssetType, lowering))
            {
                throw new ArgumentException(Tr.F("Two translations are declared for type {0}.", lowering.AssetType));
            }
        }
    }

    public static ModIRBuilder CreateDefault() => new(
    [
        new ItemLowering(), new BlockLowering(), new MobLowering(), new RecipeLowering(), new TextureLowering(), new SoundLowering(),
        new GraphLowering(), new BehaviorTreeLowering(), new ModelLowering(),
        new CommandLowering(), new CreativeTabLowering(), new KeybindLowering(), new EnchantmentLowering(), new FluidLowering(),
        new GameRuleLowering(), new EffectLowering(),
    ]);

    /// <summary>
    /// Valide le projet puis le traduit. Retourne null si des erreurs empêchent
    /// la génération ; les problèmes sont ajoutés à <paramref name="diagnostics"/>.
    /// </summary>
    public ModIR? Build(ModProject project, DiagnosticBag diagnostics)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(diagnostics);

        project.Assets.Validate(diagnostics);
        if (diagnostics.HasErrors)
        {
            return null;
        }

        var context = new LoweringContext(project, diagnostics);
        foreach (Asset asset in project.Assets.OrderBy(a => a.ResourceId))
        {
            if (_lowerings.TryGetValue(asset.Type, out IAssetLowering? lowering))
            {
                lowering.Lower(asset, context);
            }
            else
            {
                diagnostics.Warning(Tr.T("This asset type is not supported by the generator yet; it is ignored."), asset.ToString());
            }
        }

        return diagnostics.HasErrors ? null : new ModIR(CreateModInfo(project, diagnostics), context.BuildContent(), project.Localization);
    }

    private static IRModInfo CreateModInfo(ModProject project, DiagnosticBag diagnostics)
    {
        ProjectSettings settings = project.Settings;
        string? icon = null;
        if (settings.IconFile.Length > 0)
        {
            icon = project.Layout.ToAbsolutePath(settings.IconFile);
            if (!File.Exists(icon))
            {
                diagnostics.Warning(Tr.F("The icon of the mod ({0}) cannot be found: the mod will have no icon.", settings.IconFile), Tr.T("Project"));
                icon = null;
            }
        }

        return new IRModInfo(
            settings.ModId,
            settings.ModName,
            settings.ModVersion,
            settings.Authors,
            settings.Description,
            settings.License,
            settings.Website,
            settings.MinecraftVersion,
            settings.LoaderId)
        {
            IconFile = icon,
        };
    }
}
