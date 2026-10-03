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
                throw new ArgumentException($"Deux traductions sont déclarées pour le type {lowering.AssetType}.");
            }
        }
    }

    public static ModIRBuilder CreateDefault() => new(
    [
        new ItemLowering(), new BlockLowering(), new MobLowering(), new RecipeLowering(), new TextureLowering(), new SoundLowering(),
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
                diagnostics.Warning("Ce type d'asset n'est pas encore pris en charge par le générateur ; il est ignoré.", asset.ToString());
            }
        }

        return diagnostics.HasErrors ? null : new ModIR(CreateModInfo(project.Settings), context.BuildContent());
    }

    private static IRModInfo CreateModInfo(ProjectSettings settings) => new(
        settings.ModId,
        settings.ModName,
        settings.ModVersion,
        settings.Authors,
        settings.Description,
        settings.License,
        settings.Website,
        settings.MinecraftVersion,
        settings.LoaderId);
}
