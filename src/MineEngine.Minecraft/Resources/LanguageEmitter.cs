using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Fichiers de langue : noms affichés des items, des blocs et de l'onglet créatif.
/// En V0.1, le même texte est utilisé pour toutes les langues produites.
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

        string text = JsonFormatting.ToText(entries);
        foreach (string locale in _locales)
        {
            context.Files.WriteText(paths.Language(locale), text);
        }
    }
}
