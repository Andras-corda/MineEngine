using MineEngine.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.IR.Model;
using MineEngine.Project;

namespace MineEngine.IR.Lowering;

/// <summary>État partagé pendant la construction du Mod IR.</summary>
public sealed class LoweringContext
{
    private readonly List<IRItem> _items = [];
    private readonly List<IRBlock> _blocks = [];

    public LoweringContext(ModProject project, DiagnosticBag diagnostics)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public ModProject Project { get; }

    public DiagnosticBag Diagnostics { get; }

    public void AddItem(IRItem item) => _items.Add(item);

    public void AddBlock(IRBlock block) => _blocks.Add(block);

    /// <summary>Trouve le fichier de texture d'un asset, ou signale son absence.</summary>
    public IRTexture ResolveTexture(TexturedAsset asset)
    {
        if (asset.TexturePath is null)
        {
            return IRTexture.Placeholder;
        }

        string absolutePath = Project.Layout.ToAbsolutePath(asset.TexturePath);
        if (!File.Exists(absolutePath))
        {
            Diagnostics.Warning(
                $"La texture '{asset.TexturePath}' est introuvable ; une texture de remplacement sera utilisée.",
                asset.ToString());
            return IRTexture.Placeholder;
        }

        return IRTexture.FromFile(absolutePath);
    }

    public IRContent BuildContent() => new(_items, _blocks);
}
