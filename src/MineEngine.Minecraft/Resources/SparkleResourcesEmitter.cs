using System.Text.Json.Nodes;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;
using MineEngine.Minecraft.Java;
using MineEngine.Minecraft.Resources.Imaging;

namespace MineEngine.Minecraft.Resources;

/// <summary>
/// Sprites of the Sparkle particle systems (V0.7.5): one particle type per emitter, whose
/// assets/&lt;mod&gt;/particles/&lt;system&gt;_e&lt;n&gt;.json names its textures (of the game, or images
/// of the project copied to textures/particle/ as &lt;system&gt;_e&lt;n&gt;, then _t1, _t2...).
/// </summary>
public sealed class SparkleResourcesEmitter : ResourceEmitter
{
    public SparkleResourcesEmitter(PlaceholderTexture placeholder)
        : base(placeholder)
    {
    }

    public override string Name => Tr.T("Particle systems");

    public override void Emit(GenerationContext context)
    {
        ResourcePaths paths = context.Paths;
        foreach (IRSparkle sparkle in context.Ir.Content.Sparkles)
        {
            for (int i = 0; i < sparkle.Emitters.Count; i++)
            {
                IRSparkleEmitter emitter = sparkle.Emitters[i];
                string name = ModJavaEmitter.SparkleSpriteName(sparkle, i);
                var sprites = new JsonArray();
                for (int t = 0; t < emitter.Sprites.Count; t++)
                {
                    IRSparkleSprite sprite = emitter.Sprites[t];
                    if (sprite.Texture is { } texture)
                    {
                        string file = t == 0 ? name : $"{name}_t{t}";
                        WriteTexture(context, texture, paths.ParticleTexture(file));
                        sprites.Add(paths.Reference(file));
                    }
                    else
                    {
                        sprites.Add(sprite.Sprite);
                    }
                }

                WriteJson(context, paths.ParticleDefinition(name), new JsonObject { ["textures"] = sprites });
            }
        }
    }
}
