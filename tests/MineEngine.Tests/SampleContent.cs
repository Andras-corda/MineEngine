using MineEngine.Assets;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Minecraft.Resources.Imaging;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Contenu d'exemple qui utilise toutes les options : items, blocs, mobs (avec IA,
/// sons, butin et apparition), recettes des trois types, textures et son.
/// </summary>
internal static class SampleContent
{
    public static void AddTo(ModProject project)
    {
        TextureAsset swordTexture = AddTexture(project, "magic_sword_texture");
        TextureAsset goblinTexture = AddTexture(project, "goblin");
        SoundAsset goblinHurt = AddSound(project, "goblin_hurt", "Le gobelin souffre");

        var ruby = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("ruby"), "Rubis");
        var cookedBeast = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("cooked_beast"), "Viande de bête")
        {
            IsFood = true,
            Nutrition = 8,
            Saturation = 0.8f,
            AlwaysEdible = true,
            IsMeat = true,
            MaxStackSize = 16,
            CreativeTabs = new HashSet<CreativeTab> { CreativeTab.FoodAndDrinks },
        };
        var sword = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("magic_sword"), "Épée magique")
        {
            TextureId = swordTexture.Id,
            Durability = 250,
            HasGlint = true,
            Rarity = ItemRarity.Rare,
            FireResistant = true,
            TooltipLines = ["Forgée dans les étoiles", "Ne pas laisser aux enfants"],
            CreativeTabs = new HashSet<CreativeTab> { CreativeTab.Combat },
        };
        project.Assets.Add(sword);
        project.Assets.Add(cookedBeast);
        project.Assets.Add(ruby);

        var rubyOre = new BlockAsset(Guid.NewGuid(), ResourceId.Parse("ruby_ore"), "Minerai de rubis")
        {
            Hardness = 3f,
            Resistance = 3f,
            HarvestTool = HarvestTool.Pickaxe,
            ToolTier = ToolTier.Iron,
            RequiresCorrectTool = true,
            DropKind = BlockDropKind.OtherItem,
            DropItem = ContentReference.ToAsset(ruby.Id),
            DropMin = 1,
            DropMax = 3,
            ExperienceMin = 2,
            ExperienceMax = 5,
            CreativeTabs = new HashSet<CreativeTab> { CreativeTab.NaturalBlocks },
        };
        project.Assets.Add(rubyOre);
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("star_log"), "Bûche étoilée")
        {
            Model = BlockModelKind.Column,
            Sound = BlockSoundType.Wood,
            Hardness = 2f,
            Resistance = 2f,
            HarvestTool = HarvestTool.Axe,
            CreativeTabs = new HashSet<CreativeTab> { CreativeTab.BuildingBlocks },
        });
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("star_flower"), "Fleur étoilée")
        {
            Model = BlockModelKind.Cross,
            RenderType = BlockRenderType.Cutout,
            Sound = BlockSoundType.Grass,
            Hardness = 0f,
            Resistance = 0f,
            LightLevel = 7,
            TooltipLines = ["Brille dans le noir"],
        });
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("rune_block"), "Bloc runique")
        {
            Model = BlockModelKind.CubeFaces,
            RenderType = BlockRenderType.Translucent,
            Sound = BlockSoundType.Glass,
            Rarity = ItemRarity.Epic,
            MaxStackSize = 16,
        });
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("slippery_lamp"), "Lampe glissante")
        {
            LightLevel = 15,
            Friction = 0.98f,
            SpeedFactor = 1.2f,
            JumpFactor = 0.5f,
            Unbreakable = true,
            HasItemFormEnabled = false,
            DropKind = BlockDropKind.Nothing,
        });

        AddMobs(project, goblinTexture, goblinHurt, ruby, cookedBeast);
        AddRecipes(project, ruby, sword, cookedBeast, rubyOre);
    }

    private static void AddMobs(ModProject project, TextureAsset goblinTexture, SoundAsset goblinHurt, ItemAsset ruby, ItemAsset cookedBeast)
    {
        ContentReference wheat = Id("minecraft:wheat");

        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("goblin"), "Gobelin")
        {
            TextureId = goblinTexture.Id,
            Model = MobModel.Zombie,
            Kind = MobKind.Monster,
            Health = 24,
            AttackDamage = 4,
            MovementSpeed = 0.28,
            Armor = 2,
            BurnsInDaylight = true,
            Goals =
            [
                .. MobGoalCatalog.DefaultGoals(MobKind.Monster),
                MobGoal.Create(MobGoalKind.LeapAtTarget),
                MobGoal.Create(MobGoalKind.FleeSun),
                MobGoal.Create(MobGoalKind.AttackNearest) with { Target = MobTarget.Villager, Flag = false },
            ],
            AmbientSound = Id("minecraft:entity.zombie.ambient"),
            HurtSound = ContentReference.ToAsset(goblinHurt.Id),
            Drops = [new MobDrop(ContentReference.ToAsset(ruby.Id), 0, 2), new MobDrop(Id("minecraft:bone"), 1, 1)],
            EggPrimaryColor = 0x3A6B1F,
            EggSecondaryColor = 0xC8A24B,
            SpawnsNaturally = true,
            SpawnBiomes = new HashSet<SpawnBiome> { SpawnBiome.Overworld, SpawnBiome.Forest },
            SpawnWeight = 40,
            SpawnMinGroup = 1,
            SpawnMaxGroup = 4,
        });

        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("star_cow"), "Vache étoilée")
        {
            Model = MobModel.Cow,
            Kind = MobKind.Animal,
            Health = 12,
            MovementSpeed = 0.2,
            FireImmune = true,
            Persistent = true,
            UseCustomHitbox = true,
            Width = 1.1f,
            Height = 1.5f,
            BreedingItem = wheat,
            Goals =
            [
                .. MobGoalCatalog.DefaultGoals(MobKind.Animal),
                MobGoal.Create(MobGoalKind.Tempt) with { Item = wheat },
                MobGoal.Create(MobGoalKind.AvoidEntity) with { Target = MobTarget.Monster },
            ],
            Drops = [new MobDrop(ContentReference.ToAsset(cookedBeast.Id), 1, 3)],
            SpawnsNaturally = true,
            SpawnBiomes = new HashSet<SpawnBiome> { SpawnBiome.Plains },
            SpawnWeight = 8,
        });

        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("snow_guard"), "Garde des neiges")
        {
            Model = MobModel.SnowGolem,
            Kind = MobKind.Creature,
            KnockbackResistance = 0.5,
            HasSpawnEgg = false,
            Goals =
            [
                MobGoal.Create(MobGoalKind.Swim),
                MobGoal.Create(MobGoalKind.MeleeAttack) with { Flag = true },
                MobGoal.Create(MobGoalKind.MoveTowardsTarget),
                MobGoal.Create(MobGoalKind.HurtByTarget) with { Flag = true },
                MobGoal.Create(MobGoalKind.AttackNearest) with { Target = MobTarget.Monster },
            ],
            DeathSound = Id("minecraft:entity.snow_golem.death"),
        });
    }

    private static void AddRecipes(ModProject project, ItemAsset ruby, ItemAsset sword, ItemAsset cookedBeast, BlockAsset rubyOre)
    {
        ContentReference rubyRef = ContentReference.ToAsset(ruby.Id);
        project.Assets.Add(new RecipeAsset(Guid.NewGuid(), ResourceId.Parse("magic_sword"), "Épée magique")
        {
            Kind = RecipeKind.Shaped,
            Grid = [null, rubyRef, null, null, rubyRef, null, null, Id("minecraft:stick"), null],
            Result = ContentReference.ToAsset(sword.Id),
        });
        project.Assets.Add(new RecipeAsset(Guid.NewGuid(), ResourceId.Parse("cooked_beast_from_beef"), "Viande de bête")
        {
            Kind = RecipeKind.Shapeless,
            Grid = [Id("minecraft:beef"), ContentReference.ToTag(NamespacedId("minecraft:coals")), null, null, null, null, null, null, null],
            Result = ContentReference.ToAsset(cookedBeast.Id),
            ResultCount = 2,
        });
        project.Assets.Add(new RecipeAsset(Guid.NewGuid(), ResourceId.Parse("ruby_from_ore"), "Rubis au four")
        {
            Kind = RecipeKind.Smelting,
            SmeltingInput = ContentReference.ToAsset(rubyOre.Id),
            Result = rubyRef,
            Experience = 0.7f,
            CookingTime = 200,
        });
    }

    private static TextureAsset AddTexture(ModProject project, string id)
    {
        Directory.CreateDirectory(project.Layout.TexturesDirectory);
        string file = Path.Combine(project.Layout.TexturesDirectory, id + ".png");
        File.WriteAllBytes(file, new PlaceholderTexture(new PngEncoder()).GetPngBytes());
        var texture = new TextureAsset(Guid.NewGuid(), ResourceId.Parse(id), id, project.Layout.ToRelativePath(file));
        project.Assets.Add(texture);
        return texture;
    }

    /// <summary>Le contenu n'est pas lu par la génération : seul l'en-tête OGG compte pour l'import.</summary>
    private static SoundAsset AddSound(ModProject project, string id, string subtitle)
    {
        Directory.CreateDirectory(project.Layout.SoundsDirectory);
        string file = Path.Combine(project.Layout.SoundsDirectory, id + ".ogg");
        File.WriteAllBytes(file, [.. "OggS"u8.ToArray(), 0, 2, 0, 0]);
        var sound = new SoundAsset(Guid.NewGuid(), ResourceId.Parse(id), id, project.Layout.ToRelativePath(file)) { Subtitle = subtitle };
        project.Assets.Add(sound);
        return sound;
    }

    private static ContentReference Id(string id) => ContentReference.ToId(NamespacedId(id));

    private static NamespacedId NamespacedId(string id) =>
        Core.Identifiers.NamespacedId.TryParse(id, out NamespacedId? result) ? result! : throw new ArgumentException(id);
}
