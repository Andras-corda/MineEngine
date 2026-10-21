using MineEngine.Assets;

namespace MineEngine.Project.Localization;

/// <summary>Origine d'un texte traduisible du mod.</summary>
public enum TranslationSource
{
    CreativeTab,
    Item,
    Block,
    Tooltip,
    Mob,
    SpawnEgg,
    Sound,
    ProjectCreativeTab,
    Keybind,
    KeyCategory,
    Enchantment,
    Fluid,
    Bucket,
    GameRule,
    GameRuleDescription,
    Effect,
    Potion,
    SplashPotion,
    LingeringPotion,
    TippedArrow,
}

/// <summary>Un texte du mod écrit dans les fichiers de langue : sa clé, le texte saisi et l'élément qui le porte.</summary>
public sealed record TranslationEntry(string Key, string Text, TranslationSource Source, Guid? AssetId)
{
    /// <summary>Texte écrit par défaut dans une langue : le texte saisi (le nom de l'œuf d'apparition est formé dans la langue).</summary>
    public string DefaultFor(string locale) => ModTranslationKeys.DerivedName(Source, locale, Text);
}

/// <summary>
/// Les textes que Mine Engine écrit dans les fichiers de langue du mod, calculés à partir des
/// éléments du projet : nom de l'onglet créatif, des items, des blocs, des mobs et de leurs œufs,
/// lignes d'info-bulle, sous-titres des sons. Le générateur écrit les mêmes clés.
/// </summary>
public static class ModTranslationKeys
{
    /// <summary>Nom de l'œuf d'apparition d'un mob, formé dans la langue (anglais par défaut).</summary>
    public static string SpawnEggName(string locale, string mobName) =>
        locale.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
            ? $"Œuf d'apparition de {mobName}"
            : $"{mobName} Spawn Egg";

    /// <summary>
    /// Text of a name formed in the language from the name of an element: the spawn egg of a mob,
    /// the bucket of a fluid, the potions and tipped arrow of an effect. Other texts stay as typed.
    /// </summary>
    public static string DerivedName(TranslationSource source, string locale, string name)
    {
        bool french = locale.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
        return source switch
        {
            TranslationSource.SpawnEgg => SpawnEggName(locale, name),
            TranslationSource.Bucket => french ? $"Seau de {name}" : $"{name} Bucket",
            TranslationSource.Potion => french ? $"Potion de {name}" : $"Potion of {name}",
            TranslationSource.SplashPotion => french ? $"Potion jetable de {name}" : $"Splash Potion of {name}",
            TranslationSource.LingeringPotion => french ? $"Potion persistante de {name}" : $"Lingering Potion of {name}",
            TranslationSource.TippedArrow => french ? $"Flèche de {name}" : $"Arrow of {name}",
            _ => name,
        };
    }

    /// <summary>Forms of a potion and their key prefix ("item.minecraft.potion.effect.&lt;id&gt;").</summary>
    public static IReadOnlyList<(string Form, TranslationSource Source)> PotionForms { get; } =
    [
        ("potion", TranslationSource.Potion),
        ("splash_potion", TranslationSource.SplashPotion),
        ("lingering_potion", TranslationSource.LingeringPotion),
        ("tipped_arrow", TranslationSource.TippedArrow),
    ];

    public static IReadOnlyList<TranslationEntry> Collect(ModProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        string ns = project.Settings.ModId.Value;
        var entries = new List<TranslationEntry> { new($"itemGroup.{ns}", project.Settings.ModName, TranslationSource.CreativeTab, null) };

        foreach (ItemAsset item in project.Assets.OfType<ItemAsset>().OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal))
        {
            entries.Add(new TranslationEntry($"item.{ns}.{item.ResourceId}", item.DisplayName, TranslationSource.Item, item.Id));
            AddTooltip(entries, ns, item);
        }

        foreach (BlockAsset block in project.Assets.OfType<BlockAsset>().OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal))
        {
            entries.Add(new TranslationEntry($"block.{ns}.{block.ResourceId}", block.DisplayName, TranslationSource.Block, block.Id));
            if (block.HasItemForm)
            {
                AddTooltip(entries, ns, block);
            }
        }

        foreach (MobAsset mob in project.Assets.OfType<MobAsset>().OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal))
        {
            entries.Add(new TranslationEntry($"entity.{ns}.{mob.ResourceId}", mob.DisplayName, TranslationSource.Mob, mob.Id));
            if (mob.HasSpawnEgg)
            {
                entries.Add(new TranslationEntry($"item.{ns}.{mob.SpawnEggId}", mob.DisplayName, TranslationSource.SpawnEgg, mob.Id));
            }
        }

        foreach (SoundAsset sound in project.Assets.OfType<SoundAsset>().Where(s => s.Subtitle.Length > 0).OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal))
        {
            entries.Add(new TranslationEntry($"subtitles.{ns}.{sound.ResourceId}", sound.Subtitle, TranslationSource.Sound, sound.Id));
        }

        // V0.6.2 and V0.6.3: tabs, keybinds, enchantments, fluids, game rules and effects.
        foreach (CreativeTabAsset tab in Sorted<CreativeTabAsset>(project))
        {
            entries.Add(new TranslationEntry($"itemGroup.{ns}.{tab.ResourceId}", tab.DisplayName, TranslationSource.ProjectCreativeTab, tab.Id));
        }

        List<KeybindAsset> keybinds = Sorted<KeybindAsset>(project);
        if (keybinds.Count > 0)
        {
            entries.Add(new TranslationEntry($"key.categories.{ns}", project.Settings.ModName, TranslationSource.KeyCategory, null));
        }

        foreach (KeybindAsset keybind in keybinds)
        {
            entries.Add(new TranslationEntry($"key.{ns}.{keybind.ResourceId}", keybind.DisplayName, TranslationSource.Keybind, keybind.Id));
        }

        foreach (EnchantmentAsset enchantment in Sorted<EnchantmentAsset>(project))
        {
            entries.Add(new TranslationEntry($"enchantment.{ns}.{enchantment.ResourceId}", enchantment.DisplayName, TranslationSource.Enchantment, enchantment.Id));
        }

        foreach (FluidAsset fluid in Sorted<FluidAsset>(project))
        {
            entries.Add(new TranslationEntry($"block.{ns}.{fluid.ResourceId}", fluid.DisplayName, TranslationSource.Fluid, fluid.Id));
            entries.Add(new TranslationEntry($"fluid_type.{ns}.{fluid.ResourceId}", fluid.DisplayName, TranslationSource.Fluid, fluid.Id));
            if (fluid.HasBucket)
            {
                entries.Add(new TranslationEntry($"item.{ns}.{fluid.BucketId}", fluid.DisplayName, TranslationSource.Bucket, fluid.Id));
            }
        }

        foreach (GameRuleAsset rule in Sorted<GameRuleAsset>(project))
        {
            entries.Add(new TranslationEntry($"gamerule.{rule.ResourceId}", rule.DisplayName, TranslationSource.GameRule, rule.Id));
            if (rule.Description.Length > 0)
            {
                entries.Add(new TranslationEntry($"gamerule.{rule.ResourceId}.description", rule.Description, TranslationSource.GameRuleDescription, rule.Id));
            }
        }

        foreach (EffectAsset effect in Sorted<EffectAsset>(project))
        {
            entries.Add(new TranslationEntry($"effect.{ns}.{effect.ResourceId}", effect.DisplayName, TranslationSource.Effect, effect.Id));
            if (effect.HasPotion)
            {
                entries.AddRange(PotionForms.Select(f => new TranslationEntry($"item.minecraft.{f.Form}.effect.{effect.ResourceId}", effect.DisplayName, f.Source, effect.Id)));
            }
        }

        return entries;
    }

    private static List<T> Sorted<T>(ModProject project)
        where T : Core.Assets.Asset =>
        [.. project.Assets.OfType<T>().OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal)];

    private static void AddTooltip(List<TranslationEntry> entries, string ns, InventoryAsset asset)
    {
        for (int line = 0; line < asset.TooltipLines.Count; line++)
        {
            entries.Add(new TranslationEntry($"tooltip.{ns}.{asset.ResourceId}.{line}", asset.TooltipLines[line], TranslationSource.Tooltip, asset.Id));
        }
    }
}
