using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Assets;
using MineEngine.Core.GameData;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// Content of V0.6.2 and V0.6.3: commands (without argument, with a number, with a player), a
/// keybind, a creative tab, enchantments, fluids, game rules and effects, and a graph that reacts to
/// the command and the key and uses the game rule and effect nodes.
/// </summary>
internal static class GameplaySample
{
    public static void AddTo(ModProject project, ItemAsset item, TextureAsset texture)
    {
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("heal"), "Heal") { PermissionLevel = 2, Feedback = "Healed!" });
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("boost"), "Boost")
        {
            Argument = CommandArgumentKind.Number, ArgumentName = "seconds", Minimum = 1, Maximum = 600,
        });
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("bless"), "Bless") { Argument = CommandArgumentKind.Player, ArgumentName = "target" });
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("shout"), "Shout") { Argument = CommandArgumentKind.Message, ArgumentName = "message" });
        project.Assets.Add(new KeybindAsset(Guid.NewGuid(), ResourceId.Parse("dash"), "Dash") { DefaultKey = "G" });

        project.Assets.Add(new CreativeTabAsset(Guid.NewGuid(), ResourceId.Parse("treasures"), "Trésors")
        {
            Icon = ContentReference.ToAsset(item.Id),
            Items = [ContentReference.ToAsset(item.Id), Id("minecraft:diamond"), Id("minecraft:emerald")],
        });

        project.Assets.Add(new EnchantmentAsset(Guid.NewGuid(), ResourceId.Parse("frost_edge"), "Tranchant de givre")
        {
            Target = EnchantmentTarget.Sword, Rarity = EnchantmentRarity.Rare, MaxLevel = 3, DamageBonus = 1.5f,
        });
        project.Assets.Add(new EnchantmentAsset(Guid.NewGuid(), ResourceId.Parse("heavy_boots"), "Bottes de plomb")
        {
            Target = EnchantmentTarget.Boots, Treasure = true, Curse = true, MaxLevel = 1,
        });

        project.Assets.Add(new FluidAsset(Guid.NewGuid(), ResourceId.Parse("mana"), "Mana") { Color = "#5A3FE4", LightLevel = 8, FlowSpeed = 10 });
        project.Assets.Add(new FluidAsset(Guid.NewGuid(), ResourceId.Parse("tar"), "Goudron")
        {
            HasBucket = false, Infinite = false, StillTextureId = texture.Id, FlowingTextureId = texture.Id, Density = 3000, Viscosity = 6000,
        });

        project.Assets.Add(new GameRuleAsset(Guid.NewGuid(), ResourceId.Parse("magic_enabled"), "Magie activée") { Description = "La magie fonctionne" });
        project.Assets.Add(new GameRuleAsset(Guid.NewGuid(), ResourceId.Parse("mana_regen"), "Régénération de mana")
        {
            Kind = GameRuleKind.Number, DefaultNumber = 5, Category = GameRuleCategory.Player,
        });

        project.Assets.Add(new EffectAsset(Guid.NewGuid(), ResourceId.Parse("frenzy"), "Frénésie")
        {
            SpeedPercent = 30, AttackDamage = 2, Armor = 1, MaxHealth = 4, HealAmount = 1, Interval = 20, IconTextureId = texture.Id,
        });
        project.Assets.Add(new EffectAsset(Guid.NewGuid(), ResourceId.Parse("decay"), "Pourriture")
        {
            Category = EffectCategory.Harmful, DamageAmount = 1, SpeedPercent = -20, HasPotion = false,
        });

        // A graph: the command and the key give effects and read the rules.
        var b = new GraphBuilder();
        GraphNode command = b.Add(GraphNodeKind.OnCommand, 0, 0, ("command", "boost"));
        GraphNode rule = b.Add(GraphNodeKind.GetGameRuleBoolean, 300, 200, ("rule", "magic_enabled"));
        GraphNode branch = b.Add(GraphNodeKind.Branch, 300, 0);
        GraphNode give = b.Add(GraphNodeKind.GiveEffect, 600, 0, ("effect", "e2emod:frenzy"));
        GraphNode setRule = b.Add(GraphNodeKind.SetGameRuleNumber, 900, 0, ("rule", "mana_regen"));
        GraphNode regen = b.Add(GraphNodeKind.GetGameRuleNumber, 600, 200, ("rule", "mana_regen"));
        b.Exec(command, branch).Exec(branch, give, NodeInfo.ExecTrue).Exec(give, setRule)
            .Data(rule, "result", branch, "condition")
            .Data(command, "target", give, "player").Data(command, "number", give, "seconds")
            .Data(regen, "value", setRule, "value");

        GraphNode heal = b.Add(GraphNodeKind.OnCommand, 0, 400, ("command", "heal"));
        GraphNode remove = b.Add(GraphNodeKind.RemoveEffect, 300, 400, ("effect", "minecraft:poison"));
        b.Exec(heal, remove).Data(heal, "player", remove, "player");

        GraphNode key = b.Add(GraphNodeKind.OnKeyPressed, 0, 700, ("key", "dash"));
        GraphNode has = b.Add(GraphNodeKind.HasEffect, 300, 900, ("effect", "minecraft:speed"));
        GraphNode check = b.Add(GraphNodeKind.Branch, 300, 700);
        GraphNode setBool = b.Add(GraphNodeKind.SetGameRuleBoolean, 600, 700, ("rule", "keepInventory"), ("value", "true"));
        b.Exec(key, check).Exec(check, setBool, NodeInfo.ExecFalse)
            .Data(key, "player", has, "player").Data(has, "result", check, "condition");

        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("gameplay"), "Gameplay") { Graph = b.Build() });
    }

    private static ContentReference Id(string id) =>
        ContentReference.ToId(NamespacedId.TryParse(id, out NamespacedId? result) ? result! : throw new ArgumentException(id));
}
