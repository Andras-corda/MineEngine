using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.GameData;
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
    private readonly List<IRBehavior> _behaviors = [];
    private readonly List<IRDefinition> _definitions = [];
    private readonly List<IRBehaviorTree> _behaviorTrees = [];
    private readonly List<IRModel> _models = [];
    private readonly List<IRCommand> _commands = [];
    private readonly List<IRCreativeTab> _creativeTabs = [];
    private readonly List<IRKeybind> _keybinds = [];
    private readonly List<IREnchantment> _enchantments = [];
    private readonly List<IRFluid> _fluids = [];
    private readonly List<IRGameRule> _gameRules = [];
    private readonly List<IREffect> _effects = [];
    private readonly List<IRGlobalVariable> _globals = [];

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

    public void AddBehavior(IRBehavior behavior) => _behaviors.Add(behavior);

    public void AddDefinition(IRDefinition definition) => _definitions.Add(definition);

    public void AddBehaviorTree(IRBehaviorTree tree) => _behaviorTrees.Add(tree);

    public void AddModel(IRModel model) => _models.Add(model);

    public void AddCommand(IRCommand command) => _commands.Add(command);

    public void AddCreativeTab(IRCreativeTab tab) => _creativeTabs.Add(tab);

    public void AddKeybind(IRKeybind keybind) => _keybinds.Add(keybind);

    public void AddEnchantment(IREnchantment enchantment) => _enchantments.Add(enchantment);

    public void AddFluid(IRFluid fluid) => _fluids.Add(fluid);

    public void AddGameRule(IRGameRule rule) => _gameRules.Add(rule);

    public void AddEffect(IREffect effect) => _effects.Add(effect);

    public void AddGlobal(IRGlobalVariable global) => _globals.Add(global);

    private readonly List<IRTag> _tags = [];
    private readonly List<IRBiome> _biomes = [];

    public void AddTag(IRTag tag) => _tags.Add(tag);

    public void AddBiome(IRBiome biome) => _biomes.Add(biome);

    private readonly List<IRSparkle> _sparkles = [];

    public void AddSparkle(IRSparkle sparkle) => _sparkles.Add(sparkle);

    /// <summary>Identifier of the 3D model an element is shown with, or null (missing models are reported by the validation).</summary>
    public ResourceId? ResolveCustomModel(TexturedAsset owner) =>
        owner.UsesCustomModel && owner.CustomModelId is { } id && Project.Assets.Find(id) is ModelAsset { IsUsedByTheGame: true } model
            ? model.ResourceId
            : null;

    /// <summary>Appearances of an element, in order (an empty one keeps its number: the next ones do not move).</summary>
    public IReadOnlyList<IRAppearance> ResolveAppearances(TexturedAsset owner) =>
        [.. owner.Appearances.Select(a => new IRAppearance(
            a.Name,
            ResolveOptionalTexture(owner, a.TextureId),
            owner.AppearancesCanUseModels && a.ModelId is { } id && Project.Assets.Find(id) is ModelAsset { IsUsedByTheGame: true } model ? model.ResourceId : null))];

    /// <summary>Définitions de programme du projet (fonctions, classes...), construites au premier besoin.</summary>
    public ProgramModel Program => Project.Assets.Program;

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
            Diagnostics.Error(Tr.F("{0}: a tag is not accepted here.", role), owner.ToString(), owner.Id);
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
        // Un item à durabilité, un outil ou une armure ne s'empile pas.
        bool hasDurability = asset is ItemAsset item && (item.HasDurability || item.Kind.IsEquipment());
        return new IRItemForm(
            hasDurability ? 1 : asset.MaxStackSize,
            asset.Rarity,
            asset.FireResistant,
            asset.TooltipLines,
            asset.InModCreativeTab,
            asset.CreativeTabs);
    }

    public IRContent BuildContent() => new(_items, _blocks, _mobs, _recipes, _sounds, _behaviors)
    {
        Definitions = [.. _definitions.OrderBy(d => d.ResourceId)],
        BehaviorTrees = [.. _behaviorTrees.OrderBy(t => t.Id)],
        Models = [.. _models.OrderBy(m => m.Id)],
        Commands = [.. _commands.OrderBy(c => c.Id)],
        CreativeTabs = [.. _creativeTabs.OrderBy(t => t.Id)],
        Keybinds = [.. _keybinds.OrderBy(k => k.Id)],
        Enchantments = [.. _enchantments.OrderBy(e => e.Id)],
        Fluids = [.. _fluids.OrderBy(f => f.Id)],
        GameRules = [.. _gameRules.OrderBy(r => r.Id)],
        Effects = [.. _effects.OrderBy(e => e.Id)],
        Globals = [.. _globals.OrderBy(g => g.Name, StringComparer.Ordinal)],
        Tags = [.. _tags.OrderBy(t => t.Namespace, StringComparer.Ordinal).ThenBy(t => t.Path, StringComparer.Ordinal)],
        Biomes = [.. _biomes.OrderBy(b => b.Id)],
        Sparkles = [.. _sparkles.OrderBy(s => s.Id)],
    };

    private NamespacedId? ResolveAsset(Asset owner, Guid assetId, string role)
    {
        Asset? target = Project.Assets.Find(assetId);
        if (target is null)
        {
            Diagnostics.Error(Tr.F("{0}: the target asset was deleted.", role), owner.ToString(), owner.Id);
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
                Tr.F("The texture file '{0}' ({1}) was not found; a placeholder texture will be used.", texture.ResourceId, texture.FilePath),
                owner.ToString(),
                owner.Id);
        }

        return file;
    }
}
