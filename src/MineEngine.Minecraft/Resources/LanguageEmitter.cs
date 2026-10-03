using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Fichiers de langue : noms affichés des items, des blocs, des mobs, de l'onglet créatif
/// et sous-titres des sons. Les noms saisis sont les mêmes dans toutes les langues ;
/// seuls les textes ajoutés par Mine Engine (œufs d'apparition) sont traduits.
/// </summary>
public sealed class LanguageEmitter : IFileEmitter
{
    private readonly IReadOnlyList<string> _locales;

    public LanguageEmitter(IReadOnlyList<string> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);
        _locales = locales;
    }

    public string Name => "Fichiers de langue";

    private static string SpawnEggName(string locale, string mobName) =>
        locale.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
            ? $"Œuf d'apparition de {mobName}"
            : $"{mobName} Spawn Egg";

    public void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        var entries = new JsonObject { [paths.CreativeTabTranslationKey] = context.Mod.Name };

        foreach (IRItem item in context.Ir.Content.Items)
        {
            entries[paths.ItemTranslationKey(item.Id)] = item.DisplayName;
        }

        foreach (IRBlock block in context.Ir.Content.Blocks)
        {
            entries[paths.BlockTranslationKey(block.Id)] = block.DisplayName;
        }

        // Lignes d'info-bulle ("Special information" dans MCreator).
        foreach (IRElement element in context.Ir.Content.ElementsWithItemForm)
        {
            IReadOnlyList<string> lines = element.ItemForm!.TooltipLines;
            for (int i = 0; i < lines.Count; i++)
            {
                entries[paths.TooltipTranslationKey(element.Id, i)] = lines[i];
            }
        }

        foreach (IRMob mob in context.Ir.Content.Mobs)
        {
            entries[paths.EntityTranslationKey(mob.Id)] = mob.DisplayName;
        }

        foreach (IRSound sound in context.Ir.Content.Sounds.Where(s => s.Subtitle.Length > 0))
        {
            entries[paths.SubtitleTranslationKey(sound.Id)] = sound.Subtitle;
        }

        foreach (string locale in _locales)
        {
            var localized = (JsonObject)entries.DeepClone();
            foreach (IRMob mob in context.Ir.Content.MobsWithSpawnEgg)
            {
                localized[paths.ItemTranslationKey(mob.SpawnEgg!.ItemId)] = SpawnEggName(locale, mob.DisplayName);
            }

            context.Files.WriteText(paths.Language(locale), JsonFormatting.ToText(localized));
        }
    }
}
