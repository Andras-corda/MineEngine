using MineEngine.Assets;
using MineEngine.Core.Assets;
using MineEngine.Core.Identifiers;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Assets;

/// <summary>Ce qu'un sélecteur de référence propose.</summary>
public enum ReferenceDomain
{
    /// <summary>Items et blocs avec forme item.</summary>
    Item,

    /// <summary>Items, blocs et tags d'items (ingrédients de recette).</summary>
    Ingredient,

    /// <summary>Sons.</summary>
    Sound,
}

/// <summary>
/// Champ de saisie avec suggestions pour désigner un item ou un son : un asset du
/// projet ("ruby", suivi par son guid), un élément du jeu ("minecraft:diamond") ou,
/// pour un ingrédient, un tag ("#minecraft:planks"). Un nom sans espace de noms désigne
/// l'asset du projet s'il existe, sinon l'élément de Minecraft.
/// </summary>
public sealed class ReferencePickerViewModel : ValidatingObservableObject
{
    private static readonly string[] VanillaItems =
    [
        "minecraft:apple", "minecraft:beef", "minecraft:blaze_rod", "minecraft:bone", "minecraft:bread",
        "minecraft:carrot", "minecraft:chicken", "minecraft:coal", "minecraft:cobblestone", "minecraft:copper_ingot",
        "minecraft:diamond", "minecraft:egg", "minecraft:emerald", "minecraft:ender_pearl", "minecraft:feather",
        "minecraft:glass", "minecraft:gold_ingot", "minecraft:gunpowder", "minecraft:iron_ingot", "minecraft:leather",
        "minecraft:netherite_ingot", "minecraft:oak_planks", "minecraft:porkchop", "minecraft:potato",
        "minecraft:redstone", "minecraft:rotten_flesh", "minecraft:sand", "minecraft:slime_ball", "minecraft:stick",
        "minecraft:stone", "minecraft:string", "minecraft:sugar", "minecraft:wheat", "minecraft:wheat_seeds",
    ];

    private static readonly string[] VanillaTags =
    [
        "#minecraft:coals", "#minecraft:logs", "#minecraft:planks", "#minecraft:stone_crafting_materials",
        "#minecraft:wool", "#minecraft:leaves", "#minecraft:sand",
    ];

    private static readonly string[] VanillaSounds =
    [
        "minecraft:entity.blaze.ambient", "minecraft:entity.chicken.ambient", "minecraft:entity.cow.ambient",
        "minecraft:entity.cow.hurt", "minecraft:entity.cow.death", "minecraft:entity.creeper.hurt",
        "minecraft:entity.enderman.ambient", "minecraft:entity.pig.ambient", "minecraft:entity.skeleton.ambient",
        "minecraft:entity.skeleton.hurt", "minecraft:entity.skeleton.death", "minecraft:entity.spider.ambient",
        "minecraft:entity.villager.ambient", "minecraft:entity.villager.hurt", "minecraft:entity.villager.death",
        "minecraft:entity.wolf.growl", "minecraft:entity.zombie.ambient", "minecraft:entity.zombie.hurt",
        "minecraft:entity.zombie.death",
    ];

    private readonly AssetEditingContext _context;
    private readonly ReferenceDomain _domain;
    private readonly Func<ContentReference?> _getter;
    private readonly Action<ContentReference?> _apply;
    private string _text;

    /// <param name="getter">Valeur actuelle dans l'asset.</param>
    /// <param name="apply">Applique une nouvelle valeur (par une commande annulable).</param>
    public ReferencePickerViewModel(
        AssetEditingContext context, ReferenceDomain domain, Func<ContentReference?> getter, Action<ContentReference?> apply)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _domain = domain;
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _text = ToText(getter());
    }

    /// <summary>Texte saisi ; appliqué à l'asset dès qu'il désigne quelque chose de valide.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value ?? string.Empty))
            {
                return;
            }

            if (!TryParse(_text, out ContentReference? reference, out string? error))
            {
                SetError(nameof(Text), error!);
                return;
            }

            ClearErrors(nameof(Text));
            if (!Equals(reference, _getter()))
            {
                _apply(reference);
            }
        }
    }

    /// <summary>Assets du projet d'abord, puis éléments courants du jeu.</summary>
    public IReadOnlyList<string> Suggestions
    {
        get
        {
            IEnumerable<string> project = _context.Project.Assets
                .Where(IsCandidate)
                .Select(a => a.ResourceId.Value)
                .Order(StringComparer.Ordinal);
            IEnumerable<string> game = _domain switch
            {
                ReferenceDomain.Sound => VanillaSounds,
                ReferenceDomain.Ingredient => VanillaItems.Concat(VanillaTags),
                _ => VanillaItems,
            };
            return [.. project, .. game];
        }
    }

    /// <summary>
    /// À appeler quand la valeur de l'asset ou le contenu du projet a changé. Le texte
    /// saisi n'est remplacé que s'il ne désigne plus la valeur actuelle (annulation,
    /// asset renommé ou supprimé), pour ne pas gêner la frappe.
    /// </summary>
    public void Refresh()
    {
        ContentReference? current = _getter();
        bool textMatches = TryParse(_text, out ContentReference? typed, out _) && Equals(typed, current);
        if (!textMatches)
        {
            _text = ToText(current);
            ClearErrors(nameof(Text));
            OnPropertyChanged(nameof(Text));
        }

        OnPropertyChanged(nameof(Suggestions));
    }

    private bool IsCandidate(Asset asset) => _domain switch
    {
        ReferenceDomain.Sound => asset is SoundAsset,
        _ => asset is ItemAsset or BlockAsset { HasItemForm: true },
    };

    private string ToText(ContentReference? reference) => reference?.Kind switch
    {
        null => string.Empty,
        ContentReferenceKind.Asset => _context.Project.Assets.Find(reference.AssetId)?.ResourceId.Value ?? "(asset supprimé)",
        _ => reference.ToStorageText(),
    };

    private bool TryParse(string text, out ContentReference? reference, out string? error)
    {
        reference = null;
        error = null;
        string trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.StartsWith('#'))
        {
            if (_domain != ReferenceDomain.Ingredient)
            {
                error = "Un tag n'est accepté que comme ingrédient de recette.";
                return false;
            }

            if (!NamespacedId.TryParse(trimmed[1..], out NamespacedId? tag))
            {
                error = "Tag invalide (exemple : #minecraft:planks).";
                return false;
            }

            reference = ContentReference.ToTag(tag!);
            return true;
        }

        if (!NamespacedId.TryParse(trimmed, out NamespacedId? id))
        {
            error = "Identifiant invalide : minuscules, chiffres et '_' (exemple : minecraft:diamond).";
            return false;
        }

        // "ruby" ou "monmod:ruby" désignent l'asset du projet s'il existe.
        bool modNamespace = !trimmed.Contains(':') || id!.Namespace == _context.Project.Settings.ModId.Value;
        Asset? asset = modNamespace && ResourceId.TryParse(id!.Path, out ResourceId? resourceId)
            ? _context.Project.Assets.FirstOrDefault(a => a.ResourceId == resourceId && IsCandidate(a))
            : null;

        reference = asset is not null ? ContentReference.ToAsset(asset.Id) : ContentReference.ToId(id!);
        return true;
    }
}
