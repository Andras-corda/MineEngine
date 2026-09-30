namespace MineEngine.Minecraft.Generation;

/// <summary>Produit une famille de fichiers du projet Minecraft à partir du Mod IR.</summary>
public interface IFileEmitter
{
    /// <summary>Nom affiché dans le journal de génération.</summary>
    string Name { get; }

    void Emit(GenerationContext context);
}
