using MineEngine.Core.GameData;

namespace MineEngine.Assets.Mobs;

/// <summary>Taille de la boîte de collision de chaque modèle, reprise de la créature d'origine.</summary>
public static class MobModelInfo
{
    public static (float Width, float Height) DefaultHitbox(MobModel model) => model switch
    {
        MobModel.Zombie or MobModel.Villager or MobModel.Witch => (0.6f, 1.95f),
        MobModel.Skeleton => (0.6f, 1.99f),
        MobModel.Creeper => (0.6f, 1.7f),
        MobModel.Spider => (1.4f, 0.9f),
        MobModel.Cow => (0.9f, 1.4f),
        MobModel.Pig => (0.9f, 0.9f),
        MobModel.Chicken => (0.4f, 0.7f),
        MobModel.Ocelot => (0.6f, 0.7f),
        MobModel.Blaze => (0.6f, 1.8f),
        MobModel.Slime => (1.02f, 1.02f),
        MobModel.Silverfish => (0.4f, 0.3f),
        MobModel.SnowGolem => (0.7f, 1.9f),
        _ => (0.6f, 1.8f),
    };
}
