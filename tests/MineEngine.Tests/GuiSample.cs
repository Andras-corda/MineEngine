using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>A GUI of V0.8.0 with one component of each kind and the player's inventory: the rune table.</summary>
public static class GuiSample
{
    public static GuiAsset AddTo(ModProject project, TextureAsset texture)
    {
        var gui = new GuiAsset(Guid.NewGuid(), ResourceId.Parse("rune_table"), "Rune table")
        {
            HasInventory = true,
            PauseGame = false,
            Components =
            [
                GuiComponent.Create(GuiComponentKind.Text, "title", 8, 6) with
                {
                    Text = "Rune table", Color = "#3F3F3F",
                    Style = new GuiStyle
                    {
                        Horizontal = GuiAlign.Center, Justify = GuiAlign.Center, FontScale = 150, Wrap = true, Opacity = 90,
                        HoverText = "Engrave a rune", HoverColor = "#FFAA00",
                    },
                },
                GuiComponent.Create(GuiComponentKind.Image, "rune", 8, 20) with
                {
                    TextureId = texture.Id, Width = 16, Height = 16, Tooltip = "The rune",
                    Style = new GuiStyle
                    {
                        ImageSize = GuiImageSize.Fit, Tint = "#FF8080", Border = 1, BorderColor = "#202020", Highlight = true,
                        HoverEffect = GuiHoverEffect.Blur, Rotation = 45,
                    },
                },
                GuiComponent.Create(GuiComponentKind.Slot, "input", 44, 34),
                GuiComponent.Create(GuiComponentKind.Slot, "output", 116, 34) with { SlotMode = GuiSlotMode.Output },
                GuiComponent.Create(GuiComponentKind.Button, "engrave", 62, 56) with { Text = "Engrave", Width = 50 },
                GuiComponent.Create(GuiComponentKind.Checkbox, "glowing", 118, 4) with { Text = "Glow", Checked = true, Width = 50 },
                GuiComponent.Create(GuiComponentKind.TextInput, "word", 62, 18) with { Text = "Word...", Width = 50, Height = 12, MaxLength = 12 },
                GuiComponent.Create(GuiComponentKind.Slot, "preview", 152, 34) with { SlotMode = GuiSlotMode.Display },
                GuiComponent.Create(GuiComponentKind.Checkbox, "alone", 152, 56) with { Text = string.Empty, Width = 20 },
                GuiComponent.Create(GuiComponentKind.Button, "carve", 8, 56) with
                {
                    Text = "Carve", TextureId = texture.Id, Width = 40,
                    Style = GuiStyle.For(GuiComponentKind.Button) with { Tiling = true, HoverEffect = GuiHoverEffect.Tint, HoverTint = "#80FF80" },
                },
            ],
        };
        project.Assets.Add(gui);

        // A graph opens it when a player joins, and the "Close the GUI" node is used too.
        var events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("rune_table_events"), "Rune table events") { Kind = GraphKind.Events };
        project.Assets.Add(events);
        var graph = new ProgramGraphBuilder(events, () => ProgramModel.From(project.Assets));
        GraphNode join = graph.Add(GraphNodeKind.OnPlayerJoin, 0, 0);
        GraphNode close = graph.Add(GraphNodeKind.CloseGui, 300, 0);
        graph.Data(join, "player", close, "player");
        graph.Exec(join, close, NodeInfo.ExecThen);
        GraphNode open = graph.Add(GraphNodeKind.OpenGui, 600, 0, values: ("gui", "rune_table"));
        graph.Data(join, "player", open, "player");
        graph.Exec(close, open, NodeInfo.ExecThen);
        graph.Commit();

        // Its events (V0.8.0 step 3): the button is a graph, the others are Java.
        GuiComponent Component(string name) => gui.Components.Single(c => c.Name == name);
        MethodSlot click = GuiMethodSlots.Find(gui, GuiMethodSlots.ComponentKey(Component("engrave")))!;
        var procedure = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("rune_table_on_engrave"), "onEngraveClicked")
        {
            Kind = GraphKind.Procedure,
            ElementId = gui.Id,
            Parameters = [.. click.Parameters.Select(p => VariableDefinition.Create(p.Name, p.Type))],
        };
        project.Assets.Add(procedure);
        var builder = new ProgramGraphBuilder(procedure, () => ProgramModel.From(project.Assets));
        GraphNode entry = builder.Add(GraphNodeKind.FunctionEntry, 0, 0);
        string player = ProgramSample.Pin(procedure.Parameters[0]);
        GraphNode word = builder.Add(GraphNodeKind.GuiText, 300, 120, values: ("component", "word"));
        GraphNode setTitle = builder.Add(GraphNodeKind.SetGuiText, 600, 0, values: ("component", "title"));
        GraphNode input = builder.Add(GraphNodeKind.GuiSlotItem, 300, 240, values: ("component", "input"));
        GraphNode setOutput = builder.Add(GraphNodeKind.SetGuiSlotItem, 900, 0, values: ("component", "output"));
        GraphNode ticked = builder.Add(GraphNodeKind.GuiTicked, 600, 240, values: ("component", "glowing"));
        GraphNode tick = builder.Add(GraphNodeKind.SetGuiTicked, 1200, 0, values: ("component", "glowing"));
        GraphNode isOpen = builder.Add(GraphNodeKind.IsGuiOpen, 900, 240, values: ("gui", "rune_table"));
        builder.Data(entry, player, word, "player").Data(entry, player, setTitle, "player").Data(word, "text", setTitle, "text")
            .Data(entry, player, input, "player").Data(entry, player, setOutput, "player").Data(input, "stack", setOutput, "stack")
            .Data(entry, player, ticked, "player").Data(entry, player, tick, "player").Data(entry, player, isOpen, "player").Data(isOpen, "open", tick, "ticked")
            .Exec(entry, setTitle).Exec(setTitle, setOutput).Exec(setOutput, tick).Commit();

        // The graph of the GUI (V0.8.1): its event nodes, the image as a variable, the visibility.
        gui.Components = [.. gui.Components.Select(c => c.Name == "rune" ? c with { IsVariable = true } : c)];
        var nodes = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("rune_table_nodes"), "Rune table (nodes)") { Kind = GraphKind.Events, ElementId = gui.Id };
        project.Assets.Add(nodes);
        var n = new ProgramGraphBuilder(nodes, () => ProgramModel.From(project.Assets));
        double row = 0;
        GraphNode Event(GraphNodeKind kind, string? component, GraphNodeKind action, params (string Pin, string Value)[] values)
        {
            GraphNode on = component is null ? n.Add(kind, 0, row) : n.Add(kind, 0, row, values: ("component", component));
            GraphNode act = n.Add(action, 400, row, values: values);
            n.Exec(on, act).Data(on, "player", act, "player");
            row += 160;
            return act;
        }

        GraphNode hide = Event(GraphNodeKind.OnGuiPressed, "engrave", GraphNodeKind.SetGuiVisible, ("visible", "false"));
        GraphNode rune = n.Add(GraphNodeKind.GuiComponentRef, 200, 80, target: gui.Id, member: gui.Components.Single(c => c.Name == "rune").Id);
        n.Data(rune, "component", hide, "component");
        Event(GraphNodeKind.OnGuiReleased, "engrave", GraphNodeKind.SetGuiVisible, ("component", "rune"), ("visible", "true"));
        Event(GraphNodeKind.OnGuiHovered, "carve", GraphNodeKind.SetGuiText, ("component", "title"), ("text", "Carve it"));
        Event(GraphNodeKind.OnGuiUnhovered, "carve", GraphNodeKind.SetGuiText, ("component", "title"), ("text", "Rune table"));
        Event(GraphNodeKind.OnGuiImageClicked, "rune", GraphNodeKind.SetGuiTicked, ("component", "alone"), ("ticked", "true"));
        GraphNode typed = Event(GraphNodeKind.OnGuiTextChanged, "word", GraphNodeKind.SetGuiText, ("component", "title"));
        n.Data(n.Add(GraphNodeKind.OnGuiTick, -400, -400), "player", n.Add(GraphNodeKind.GuiText, -200, -400, values: ("component", "word")), "player");
        Event(GraphNodeKind.OnGuiTicked, "glowing", GraphNodeKind.CloseGui);
        Event(GraphNodeKind.OnGuiSlotChanged, "input", GraphNodeKind.SetGuiText, ("component", "title"), ("text", "Slot"));
        Event(GraphNodeKind.OnGuiOpened, null, GraphNodeKind.SetGuiText, ("component", "title"), ("text", "Hello"));
        Event(GraphNodeKind.OnGuiClosed, null, GraphNodeKind.SendMessage, ("message", "Bye"));
        n.Data(n.Add(GraphNodeKind.TextValue, 200, 900, values: ("value", "typed")), "value", typed, "text");
        n.Commit();

        gui.Methods =
        [
            ElementMethod.Java(GuiMethodSlots.Opened, "player.sendSystemMessage(net.minecraft.network.chat.Component.literal(\"Rune table opened\"));"),
            ElementMethod.Java(GuiMethodSlots.Tick, string.Empty),
            ElementMethod.Java(GuiMethodSlots.Closed, "ModGuis.setText(player, \"title\", \"closed\");"),
            ElementMethod.Nodal(click.Key, procedure.Id),
            ElementMethod.Java(GuiMethodSlots.ComponentKey(Component("glowing")), "ModGuis.setText(player, \"title\", ticked ? \"Glowing\" : \"Rune table\");"),
            ElementMethod.Java(GuiMethodSlots.ComponentKey(Component("word")), "ModGuis.setTicked(player, \"glowing\", text.length() > 3);"),
            ElementMethod.Java(GuiMethodSlots.ComponentKey(Component("input")), "if (!stack.isEmpty()) {\n    ModGuis.setSlotItem(player, \"output\", stack.copyWithCount(1));\n}"),
        ];

        // A right click opens it (V0.8.1): on a block that keeps its items, with an item in the hand, on a mob.
        project.Assets.Add(new BlockAsset(Guid.NewGuid(), ResourceId.Parse("rune_table_block"), "Rune table block") { TextureId = texture.Id, Gui = gui.Id });
        project.Assets.Add(new ItemAsset(Guid.NewGuid(), ResourceId.Parse("rune_book"), "Rune book") { TextureId = texture.Id, Gui = gui.Id });
        project.Assets.Add(new MobAsset(Guid.NewGuid(), ResourceId.Parse("rune_keeper"), "Rune keeper") { TextureId = texture.Id, Gui = gui.Id });
        return gui;
    }
}
