using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Resources;

/// <summary>Déclaration des sons (sounds.json) et copie des fichiers OGG.</summary>
public sealed class SoundsEmitter : IFileEmitter
{
    public string Name => "Sons";

    public void Emit(GenerationContext context)
    {
        IReadOnlyList<IRSound> sounds = context.Ir.Content.Sounds;
        if (sounds.Count == 0)
        {
            return;
        }

        ResourcePaths paths = context.Paths;
        var definitions = new JsonObject();
        foreach (IRSound sound in sounds)
        {
            var definition = new JsonObject
            {
                ["sounds"] = new JsonArray { new JsonObject { ["name"] = paths.Reference(sound.Id.Value) } },
            };
            if (sound.Subtitle.Length > 0)
            {
                definition["subtitle"] = paths.SubtitleTranslationKey(sound.Id);
            }

            definitions[sound.Id.Value] = definition;
            context.Files.CopyFile(sound.SourceFile, paths.SoundFile(sound.Id));
        }

        context.Files.WriteText(paths.SoundsDefinition, JsonFormatting.ToText(definitions));
    }
}
