using System.Text.Json.Nodes;
using MineEngine.Core.Json;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>Base des générateurs de ressources : écriture de JSON et de textures.</summary>
public abstract class ResourceEmitter : IFileEmitter
{
    private readonly PlaceholderTexture _placeholder;

    protected ResourceEmitter(PlaceholderTexture placeholder)
    {
        _placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
    }

    public abstract string Name { get; }

    public abstract void Emit(GenerationContext context);

    protected static void WriteJson(GenerationContext context, string relativePath, JsonNode content) =>
        context.Files.WriteText(relativePath, JsonFormatting.ToText(content));

    protected void WriteTexture(GenerationContext context, IRTexture texture, string relativePath)
    {
        if (texture.IsPlaceholder)
        {
            context.Files.WriteBytes(relativePath, _placeholder.GetPngBytes());
        }
        else
        {
            context.Files.CopyFile(texture.SourceFile!, relativePath);
        }
    }
}
