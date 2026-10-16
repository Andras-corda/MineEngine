using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Project.Localization;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Fichiers de langue : noms affichés des items, des blocs, des mobs, de l'onglet créatif
/// et sous-titres des sons, dans chaque langue du mod (onglet Langues). Un texte prend la
/// traduction saisie dans la langue, sinon le nom saisi sur l'élément ; les traductions de
/// clés que Mine Engine n'écrit pas (textes du code Java) sont ajoutées telles quelles.
/// </summary>
public sealed class LanguageEmitter : IFileEmitter
{
    public string Name => Tr.T("Fichiers de langue");

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

        ModLocalization localization = context.Ir.Localization;
        foreach (string locale in localization.Locales)
        {
            var localized = (JsonObject)entries.DeepClone();
            foreach (IRMob mob in context.Ir.Content.MobsWithSpawnEgg)
            {
                localized[paths.ItemTranslationKey(mob.SpawnEgg!.ItemId)] = ModTranslationKeys.SpawnEggName(locale, mob.DisplayName);
            }

            foreach ((string key, string text) in localization.TranslationsFor(locale).OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                localized[key] = text;
            }

            context.Files.WriteText(paths.Language(locale), JsonFormatting.ToText(localized));
        }
    }
}
