using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Assets.Mobs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Minecraft.Resources.Imaging;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Contenu d'exemple qui utilise toutes les options : items, blocs, mobs (avec IA,
/// sons, butin et apparition), recettes des trois types, textures, son et graphes nodaux.
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
        AddGraphs(project, ruby, sword, rubyOre, goblinHurt);
        AddEquipment(project, ruby);
        ProgramSample.AddTo(project);
        PuppeteerSample.AddTo(project, goblinHurt);
        NodeLibrarySample.AddTo(project);
        BlockbenchSample.AddTo(project, [swordTexture, goblinTexture]);
        GameplaySample.AddTo(project, ruby, swordTexture);
        LibrarySample.AddTo(project);
        AppearanceSample.AddTo(project);
        GlobalsSample.AddTo(project);
        TypesSample.AddTo(project);
        ScopesSample.AddTo(project);
        BlockOptionsSample.AddTo(project);
    }

    /// <summary>V0.4.1 : outils, armures, nourriture à effets, combustible, tags et événements d'items.</summary>
    private static void AddEquipment(ModProject project, ItemAsset ruby)
    {
        (string Id, ItemKind Kind, ItemTier Tier)[] tools =
        [
            ("ruby_sword", ItemKind.Sword, ItemTier.Diamond), ("ruby_pickaxe", ItemKind.Pickaxe, ItemTier.Iron),
            ("ruby_axe", ItemKind.Axe, ItemTier.Stone), ("ruby_shovel", ItemKind.Shovel, ItemTier.Gold),
            ("ruby_hoe", ItemKind.Hoe, ItemTier.Netherite),
        ];
        var items = new Dictionary<string, ItemAsset>();
        foreach ((string id, ItemKind kind, ItemTier tier) in tools)
        {
            (float damage, float speed) = kind.DefaultCombat();
            items[id] = new ItemAsset(Guid.NewGuid(), ResourceId.Parse(id), id)
            {
                Kind = kind, Tier = tier, AttackDamage = damage, AttackSpeed = speed, Handheld = true, Folder = "Équipement/Outils",
                CreativeTabs = new HashSet<CreativeTab> { CreativeTab.ToolsAndUtilities },
            };
        }

        items["ruby_sword"].ItemTags = [NamespacedId("c:tools/swords"), NamespacedId("minecraft:piglin_loved")];
        items["ruby_sword"].NoRepair = true;

        (string Id, ItemKind Kind, ArmorMaterialKind Material)[] armor =
        [
            ("ruby_helmet", ItemKind.Helmet, ArmorMaterialKind.Leather), ("ruby_chestplate", ItemKind.Chestplate, ArmorMaterialKind.Chainmail),
            ("ruby_leggings", ItemKind.Leggings, ArmorMaterialKind.Turtle), ("ruby_boots", ItemKind.Boots, ArmorMaterialKind.Netherite),
        ];
        foreach ((string id, ItemKind kind, ArmorMaterialKind material) in armor)
        {
            items[id] = new ItemAsset(Guid.NewGuid(), ResourceId.Parse(id), id) { Kind = kind, ArmorMaterial = material, Folder = "Équipement/Armure" };
        }

        items["star_stew"] = new ItemAsset(Guid.NewGuid(), ResourceId.Parse("star_stew"), "Ragoût étoilé")
        {
            IsFood = true,
            Nutrition = 6,
            FastFood = true,
            FoodEffects = [new FoodEffect(FoodEffectKind.Regeneration, 5, 1, 1f), new FoodEffect(FoodEffectKind.Speed, 30, 0, 0.5f)],
            CraftRemainder = Id("minecraft:bowl"),
            BurnTime = 400,
            ItemTags = [NamespacedId("c:foods")],
            MaxStackSize = 16,
        };

        foreach (ItemAsset item in items.Values)
        {
            project.Assets.Add(item);
        }

        project.Settings.Folders = ["Équipement", "Vide"];

        // Un événement de chaque sorte, sur les items ci-dessus.
        var b = new GraphBuilder();
        string Ref(string id) => ContentReference.ToAsset(items[id].Id).ToStorageText();
        GraphNode hit = b.Add(GraphNodeKind.OnItemHitEntity, 0, 0, ("item", Ref("ruby_sword")));
        GraphNode hitMessage = b.Add(GraphNodeKind.SendMessage, 300, 0, ("message", "Touché !"));
        b.Exec(hit, hitMessage).Data(hit, "player", hitMessage, "player");

        GraphNode crafted = b.Add(GraphNodeKind.OnItemCrafted, 0, 200, ("item", Ref("ruby_sword")));
        GraphNode give = b.Add(GraphNodeKind.GiveItem, 300, 200, ("item", ContentReference.ToAsset(ruby.Id).ToStorageText()));
        b.Exec(crafted, give).Data(crafted, "player", give, "player");

        GraphNode held = b.Add(GraphNodeKind.OnItemHeldTick, 0, 400, ("item", Ref("ruby_sword")));
        GraphNode luck = b.Add(GraphNodeKind.Chance, 300, 400, ("percent", "1"));
        GraphNode sound = b.Add(GraphNodeKind.PlaySound, 600, 400);
        b.Exec(held, luck).Exec(luck, sound, NodeInfo.ExecTrue).Data(held, "position", sound, "position");

        GraphNode useOn = b.Add(GraphNodeKind.OnItemUseOnBlock, 0, 600, ("item", Ref("ruby_pickaxe")));
        GraphNode setBlock = b.Add(GraphNodeKind.SetBlock, 300, 600, ("block", "minecraft:gold_block"));
        b.Exec(useOn, setBlock).Data(useOn, "position", setBlock, "position");

        GraphNode mined = b.Add(GraphNodeKind.OnItemBlockMined, 0, 800, ("item", Ref("ruby_pickaxe")));
        GraphNode bonus = b.Add(GraphNodeKind.SpawnItem, 300, 800);
        b.Exec(mined, bonus).Data(mined, "position", bonus, "position");

        GraphNode eaten = b.Add(GraphNodeKind.OnItemConsumed, 0, 1000, ("item", Ref("star_stew")));
        GraphNode boom = b.Add(GraphNodeKind.Explosion, 300, 1000, ("power", "1"));
        b.Exec(eaten, boom).Data(eaten, "position", boom, "position");

        GraphNode click = b.Add(GraphNodeKind.OnItemRightClick, 0, 1200, ("item", Ref("star_stew")));
        GraphNode hello = b.Add(GraphNodeKind.SendMessage, 300, 1200);
        b.Exec(click, hello).Data(click, "player", hello, "player");

        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("item_events"), "Événements d'items")
        {
            Graph = b.Build(),
            Folder = "Équipement",
        });
    }

    /// <summary>Le graphe du critère de réussite de la V0.4 : clic droit, possède l'item, faire apparaître un item.</summary>
    public static GraphAsset CreateAltarGraph(ContentReference requiredItem, ContentReference reward)
    {
        var builder = new GraphBuilder();
        GraphNode click = builder.Add(GraphNodeKind.OnRightClickBlock, 0, 0);
        GraphNode hasItem = builder.Add(GraphNodeKind.HasItem, 300, 0, ("item", requiredItem.ToStorageText()), ("count", "3"));
        GraphNode spawn = builder.Add(GraphNodeKind.SpawnItem, 600, 0, ("item", reward.ToStorageText()));
        builder.Exec(click, hasItem).Exec(hasItem, spawn, NodeInfo.ExecTrue)
            .Data(click, "player", hasItem, "player")
            .Data(click, "position", spawn, "position");
        return new GraphAsset(Guid.NewGuid(), ResourceId.Parse("ruby_altar"), "Autel de rubis") { Graph = builder.Build() };
    }

    private static void AddGraphs(ModProject project, ItemAsset ruby, ItemAsset sword, BlockAsset rubyOre, SoundAsset sound)
    {
        project.Assets.Add(CreateAltarGraph(ContentReference.ToAsset(ruby.Id), Id("minecraft:diamond")));

        // Un second graphe qui utilise tous les autres nœuds, pour vérifier le Java produit.
        var b = new GraphBuilder();
        GraphNode broken = b.Add(GraphNodeKind.OnBlockBroken, 0, 0);
        GraphNode holds = b.Add(GraphNodeKind.HoldsItem, 300, 0, ("item", ContentReference.ToAsset(sword.Id).ToStorageText()));
        GraphNode cancel = b.Add(GraphNodeKind.CancelEvent, 600, 0);
        GraphNode setBlock = b.Add(GraphNodeKind.SetBlock, 900, 0, ("block", ContentReference.ToAsset(rubyOre.Id).ToStorageText()));
        GraphNode random = b.Add(GraphNodeKind.RandomNumber, 900, 200, ("min", "1"), ("max", "3"));
        GraphNode give = b.Add(GraphNodeKind.GiveItem, 1200, 0, ("item", ContentReference.ToAsset(ruby.Id).ToStorageText()));
        GraphNode chance = b.Add(GraphNodeKind.Chance, 600, 200, ("percent", "25"));
        GraphNode above = b.Add(GraphNodeKind.OffsetPosition, 600, 400);
        GraphNode explode = b.Add(GraphNodeKind.Explosion, 900, 400, ("power", "1"));
        GraphNode sound1 = b.Add(GraphNodeKind.PlaySound, 1200, 400, ("sound", ContentReference.ToAsset(sound.Id).ToStorageText()));
        b.Exec(broken, holds).Exec(holds, cancel, NodeInfo.ExecTrue).Exec(cancel, setBlock).Exec(setBlock, give)
            .Exec(holds, chance, NodeInfo.ExecFalse).Exec(chance, explode, NodeInfo.ExecTrue).Exec(explode, sound1)
            .Data(broken, "player", holds, "player").Data(broken, "position", setBlock, "position")
            .Data(broken, "player", give, "player").Data(random, "value", give, "count")
            .Data(broken, "position", above, "position").Data(above, "position", explode, "position")
            .Data(broken, "position", sound1, "position");

        GraphNode join = b.Add(GraphNodeKind.OnPlayerJoin, 0, 600);
        GraphNode greater = b.Add(GraphNodeKind.GreaterThan, 0, 800);
        GraphNode dice = b.Add(GraphNodeKind.RandomNumber, -300, 800, ("min", "1"), ("max", "10"));
        GraphNode five = b.Add(GraphNodeKind.NumberValue, -300, 950, ("value", "5"));
        GraphNode branch = b.Add(GraphNodeKind.Branch, 300, 600);
        GraphNode name = b.Add(GraphNodeKind.PlayerName, 300, 800);
        GraphNode welcome = b.Add(GraphNodeKind.TextValue, 300, 950, ("value", "Bienvenue \"héros\" "));
        GraphNode joinText = b.Add(GraphNodeKind.JoinText, 600, 800);
        GraphNode message = b.Add(GraphNodeKind.SendMessage, 600, 600);
        GraphNode luck = b.Add(GraphNodeKind.SendMessage, 600, 700);
        b.Exec(join, branch).Exec(branch, message, NodeInfo.ExecTrue).Exec(branch, luck, NodeInfo.ExecFalse)
            .Data(dice, "value", greater, "a").Data(five, "value", greater, "b").Data(greater, "result", branch, "condition")
            .Data(join, "player", name, "player").Data(welcome, "value", joinText, "first").Data(name, "name", joinText, "second")
            .Data(join, "player", message, "player").Data(joinText, "text", message, "message")
            .Data(join, "player", luck, "player").Data(dice, "value", luck, "message");

        GraphNode use = b.Add(GraphNodeKind.OnUseItem, 0, 1200);
        GraphNode sneaking = b.Add(GraphNodeKind.IsSneaking, 300, 1200);
        GraphNode notNode = b.Add(GraphNodeKind.Not, 300, 1400);
        GraphNode flag = b.Add(GraphNodeKind.BooleanValue, 0, 1400, ("value", "false"));
        GraphNode equal = b.Add(GraphNodeKind.NumbersEqual, 0, 1500, ("a", "-2"), ("b", "-2"));
        GraphNode check = b.Add(GraphNodeKind.Branch, 600, 1200);
        GraphNode remove = b.Add(GraphNodeKind.RemoveItem, 900, 1200, ("item", "minecraft:dirt"), ("count", "2"));
        GraphNode spawnAtFeet = b.Add(GraphNodeKind.SpawnItem, 1200, 1200);
        GraphNode feet = b.Add(GraphNodeKind.PlayerPosition, 900, 1400);
        GraphNode equalCheck = b.Add(GraphNodeKind.Branch, 1500, 1200);
        b.Exec(use, sneaking).Exec(sneaking, check, NodeInfo.ExecTrue).Exec(check, remove, NodeInfo.ExecTrue).Exec(remove, spawnAtFeet)
            .Exec(spawnAtFeet, equalCheck)
            .Data(use, "player", sneaking, "player").Data(flag, "value", notNode, "value").Data(notNode, "result", check, "condition")
            .Data(use, "player", remove, "player").Data(use, "player", feet, "player").Data(feet, "position", spawnAtFeet, "position")
            .Data(equal, "result", equalCheck, "condition");

        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("all_nodes"), "Tous les nœuds") { Graph = b.Build() });
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
