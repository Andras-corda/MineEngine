using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;
using MineEngine.IR.Model;
using MineEngine.Project;

namespace MineEngine.IR.Lowering;

/// <summary>État partagé pendant la construction du Mod IR.</summary>
public sealed class LoweringContext
{
    private readonly List<IRItem> _items = [];
    private readonly List<IRBlock> _blocks = [];
    private readonly List<IRMob> _mobs = [];
    private readonly List<IRRecipe> _recipes = [];
    private readonly List<IRSound> _sounds = [];

    public LoweringContext(ModProject project, DiagnosticBag diagnostics)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public ModProject Project { get; }

    public DiagnosticBag Diagnostics { get; }

    private string ModNamespace => Project.Settings.ModId.Value;

    public void AddItem(IRItem item) => _items.Add(item);

    public void AddBlock(IRBlock block) => _blocks.Add(block);

    public void AddMob(IRMob mob) => _mobs.Add(mob);

    public void AddRecipe(IRRecipe recipe) => _recipes.Add(recipe);

    public void AddSound(IRSound sound) => _sounds.Add(sound);

    /// <summary>
    /// Fichier d'une texture du projet. Sans texture, ou si son fichier manque, la
    /// texture de remplacement est utilisée (avec un avertissement dans ce dernier cas).
    /// </summary>
    public IRTexture ResolveTexture(Asset owner, Guid? textureId) =>
        FindTextureFile(owner, textureId) is { } file ? IRTexture.FromFile(file) : IRTexture.Placeholder;

    /// <summary>Comme <see cref="ResolveTexture"/>, mais null quand aucune texture utilisable n'existe.</summary>
    public IRTexture? ResolveOptionalTexture(Asset owner, Guid? textureId) =>
        FindTextureFile(owner, textureId) is { } file ? IRTexture.FromFile(file) : null;

    /// <summary>Identifiant en jeu d'un item ; null (et une erreur) si la référence ne désigne pas un item.</summary>
    public NamespacedId? ResolveItem(Asset owner, ContentReference? reference, string role)
    {
        if (reference is null)
        {
            return null;
        }

        if (reference.Kind == ContentReferenceKind.Tag)
        {
            Diagnostics.Error($"{role} : un tag n'est pas accepté ici.", owner.ToString(), owner.Id);
            return null;
        }

        return reference.Kind == ContentReferenceKind.Id ? reference.Id : ResolveAsset(owner, reference.AssetId, role);
    }

    /// <summary>Ingrédient de recette : item précis ou tag.</summary>
    public IRIngredient? ResolveIngredient(Asset owner, ContentReference reference, string role) =>
        reference.Kind == ContentReferenceKind.Tag
            ? new IRIngredient(reference.Id!, IsTag: true)
            : ResolveItem(owner, reference, role) is { } item ? new IRIngredient(item, IsTag: false) : null;

    /// <summary>Identifiant d'un événement sonore (son du projet ou du jeu).</summary>
    public NamespacedId? ResolveSound(Asset owner, ContentReference? reference, string role) => reference?.Kind switch
    {
        null => null,
        ContentReferenceKind.Id => reference.Id,
        ContentReferenceKind.Asset => ResolveAsset(owner, reference.AssetId, role),
        _ => null,
    };

    /// <summary>Propriétés d'inventaire communes aux items et aux blocs.</summary>
    public IRItemForm CreateItemForm(InventoryAsset asset)
    {
        bool hasDurability = asset is ItemAsset { HasDurability: true };
        return new IRItemForm(
            hasDurability ? 1 : asset.MaxStackSize,
            asset.Rarity,
            asset.FireResistant,
            asset.TooltipLines,
            asset.InModCreativeTab,
            asset.CreativeTabs);
    }

    public IRContent BuildContent() => new(_items, _blocks, _mobs, _recipes, _sounds);

    private NamespacedId? ResolveAsset(Asset owner, Guid assetId, string role)
    {
        Asset? target = Project.Assets.Find(assetId);
        if (target is null)
        {
            Diagnostics.Error($"{role} : l'asset visé a été supprimé.", owner.ToString(), owner.Id);
            return null;
        }

        return NamespacedId.TryParse($"{ModNamespace}:{target.ResourceId}", out NamespacedId? id) ? id : null;
    }

    private string? FindTextureFile(Asset owner, Guid? textureId)
    {
        if (textureId is null || Project.Assets.Find(textureId.Value) is not TextureAsset texture)
        {
            return null;
        }

        string? file = AssetFileImporter.GetExistingFile(Project, texture);
        if (file is null)
        {
            Diagnostics.Warning(
                $"Le fichier de la texture '{texture.ResourceId}' ({texture.FilePath}) est introuvable ; une texture de remplacement sera utilisée.",
                owner.ToString(),
                owner.Id);
        }

        return file;
    }
}
