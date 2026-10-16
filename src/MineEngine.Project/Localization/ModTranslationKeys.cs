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
}

/// <summary>Un texte du mod écrit dans les fichiers de langue : sa clé, le texte saisi et l'élément qui le porte.</summary>
public sealed record TranslationEntry(string Key, string Text, TranslationSource Source, Guid? AssetId)
{
    /// <summary>Texte écrit par défaut dans une langue : le texte saisi (le nom de l'œuf d'apparition est formé dans la langue).</summary>
    public string DefaultFor(string locale) =>
        Source == TranslationSource.SpawnEgg ? ModTranslationKeys.SpawnEggName(locale, Text) : Text;
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

        return entries;
    }

    private static void AddTooltip(List<TranslationEntry> entries, string ns, InventoryAsset asset)
    {
        for (int line = 0; line < asset.TooltipLines.Count; line++)
        {
            entries.Add(new TranslationEntry($"tooltip.{ns}.{asset.ResourceId}.{line}", asset.TooltipLines[line], TranslationSource.Tooltip, asset.Id));
        }
    }
}
