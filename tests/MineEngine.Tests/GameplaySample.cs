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
            Parameters = [new CommandParameter(Guid.NewGuid(), "seconds", CommandParameterKind.Integer, 1, 600)],
        });
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("bless"), "Bless") { Parameters = [Parameter("target", CommandParameterKind.Player)] });
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("shout"), "Shout") { Parameters = [Parameter("message", CommandParameterKind.Message)] });

        // A parameter of each kind, and the console or a command block may run it.
        project.Assets.Add(new CommandAsset(Guid.NewGuid(), ResourceId.Parse("send_to"), "Send to")
        {
            AnySource = true,
            PermissionLevel = 2,
            Parameters =
            [
                Parameter("who", CommandParameterKind.Entity), Parameter("where", CommandParameterKind.Position),
                Parameter("push", CommandParameterKind.Vector), new CommandParameter(Guid.NewGuid(), "speed", CommandParameterKind.Decimal, 0.5, 4),
                Parameter("silent", CommandParameterKind.Boolean), Parameter("gift", CommandParameterKind.Item), Parameter("marker", CommandParameterKind.Block),
                Parameter("world", CommandParameterKind.Dimension), Parameter("tag", CommandParameterKind.Word), Parameter("label", CommandParameterKind.Text),
                Parameter("guest", CommandParameterKind.Player), Parameter("note", CommandParameterKind.Message),
            ],
        });
        project.Assets.Add(new KeybindAsset(Guid.NewGuid(), ResourceId.Parse("dash"), "Dash") { DefaultKey = "G" });
        project.Assets.Add(new KeybindAsset(Guid.NewGuid(), ResourceId.Parse("charge"), "Charge")
        {
            DefaultKey = "MOUSE_RIGHT", HoldSeconds = 0.75, PulseSeconds = 0.25, ComboKey = "LEFT_CONTROL",
        });

        project.Assets.Add(new CreativeTabAsset(Guid.NewGuid(), ResourceId.Parse("treasures"), "Trésors")
        {
            Icon = ContentReference.ToAsset(item.Id),
            Items = [ContentReference.ToAsset(item.Id), Id("minecraft:diamond"), Id("minecraft:emerald")],
            HasSearchBar = true,
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

        // Every trigger of a keybind (V0.7.x): the times and the pulses become messages.
        double y = 1100;
        foreach (GraphNodeKind trigger in NodeCatalog.KeyEvents.Skip(1))
        {
            GraphNode keyEvent = b.Add(trigger, 0, y += 200, ("key", "charge"));
            GraphNode message = b.Add(GraphNodeKind.SendMessage, 600, y, ("message", trigger.ToString()));
            b.Exec(keyEvent, message).Data(keyEvent, "player", message, "player");
            NodeInfo info = NodeCatalog.Get(trigger);
            if (info.FindOutput("seconds") is not null)
            {
                GraphNode round = b.Add(GraphNodeKind.RoundDecimal, 200, y + 100);
                GraphNode text = b.Add(GraphNodeKind.NumberToText, 400, y + 100);
                b.Data(keyEvent, "seconds", round, "a").Data(round, "value", text, "number").Data(text, "text", message, "message");
            }
            else if (info.FindOutput("count") is not null)
            {
                GraphNode text = b.Add(GraphNodeKind.NumberToText, 400, y + 100);
                b.Data(keyEvent, "count", text, "number").Data(text, "text", message, "message");
            }
        }

        project.Assets.Add(new GraphAsset(Guid.NewGuid(), ResourceId.Parse("gameplay"), "Gameplay") { Graph = b.Build() });

        // The parameters of a command are outputs of its event (described with the commands of the project).
        var commandGraph = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("send_to_events"), "Send to") { Kind = GraphKind.Events };
        project.Assets.Add(commandGraph);
        var c = new ProgramGraphBuilder(commandGraph, () => ProgramModel.From(project.Assets));
        GraphNode sendTo = c.Add(GraphNodeKind.OnCommand, 0, 0, values: ("command", "send_to"));
        GraphNode teleport = c.Add(GraphNodeKind.TeleportEntity, 300, 0);
        GraphNode place = c.Add(GraphNodeKind.SetBlockState, 600, 0);
        c.Exec(sendTo, teleport).Exec(teleport, place)
            .Data(sendTo, "arg_who", teleport, "entity").Data(sendTo, "arg_where", teleport, "position")
            .Data(sendTo, "arg_where", place, "position").Data(sendTo, "arg_marker", place, "state");
        c.Commit();
    }

    private static CommandParameter Parameter(string name, CommandParameterKind kind) => new(Guid.NewGuid(), name, kind);

    private static ContentReference Id(string id) =>
        ContentReference.ToId(NamespacedId.TryParse(id, out NamespacedId? result) ? result! : throw new ArgumentException(id));
}
