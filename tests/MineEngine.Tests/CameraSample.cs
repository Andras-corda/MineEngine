using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.Core.Identifiers;
using MineEngine.Project;

namespace MineEngine.Tests;

/// <summary>
/// V0.7.4: an event graph that runs every camera action of the player, fed by the camera values
/// (yaw, pitch, direction, position) and by the nearest entity. Added after everything else, so
/// that the end-to-end builds compile the camera classes and their network on each loader.
/// </summary>
internal sealed class CameraSample
{
    private CameraSample()
    {
    }

    public GraphAsset Events { get; private set; } = null!;

    public static CameraSample AddTo(ModProject project)
    {
        var sample = new CameraSample
        {
            Events = new GraphAsset(Guid.NewGuid(), ResourceId.Parse("camera_tricks"), "Effets de caméra") { Kind = GraphKind.Events },
        };
        project.Assets.Add(sample.Events);
        var graph = new ProgramGraphBuilder(sample.Events, () => ProgramModel.From(project.Assets));
        GraphNode use = graph.Add(GraphNodeKind.OnUseItem, 0, 0);
        GraphNode yaw = graph.Add(GraphNodeKind.CameraYaw, 0, 200);
        GraphNode pitch = graph.Add(GraphNodeKind.CameraPitch, 0, 300);
        GraphNode direction = graph.Add(GraphNodeKind.CameraDirection, 0, 400);
        GraphNode directionX = graph.Add(GraphNodeKind.VectorX, 200, 400);
        GraphNode eyes = graph.Add(GraphNodeKind.CameraPosition, 0, 500);
        GraphNode eyesY = graph.Add(GraphNodeKind.VectorY, 200, 500);
        GraphNode nearest = graph.Add(GraphNodeKind.NearestEntity, 0, 600, values: ("entity", "minecraft:cow"));
        foreach (GraphNode value in new[] { yaw, pitch, direction, eyes })
        {
            graph.Data(use, "player", value, "player");
        }

        graph.Data(direction, "direction", directionX, "vector");
        graph.Data(eyes, "position", eyesY, "vector");
        graph.Data(use, "position", nearest, "position");

        var actions = new Dictionary<GraphNodeKind, GraphNode>();
        GraphNode last = use;
        double x = 300;
        foreach ((GraphNodeKind kind, (string Pin, string Value)[] values) in new (GraphNodeKind, (string, string)[])[]
                 {
                     (GraphNodeKind.SetCameraRotation, []),
                     (GraphNodeKind.TurnCamera, [("yaw", "10.0")]),
                     (GraphNodeKind.CameraLookAt, []),
                     (GraphNodeKind.ViewFromEntity, []),
                     (GraphNodeKind.SetCameraPerspective, [("perspective", "2")]),
                     (GraphNodeKind.SetCameraZoom, [("zoom", "300")]),
                     (GraphNodeKind.ShakeCamera, [("seconds", "0.5")]),
                     (GraphNodeKind.TiltCamera, []),
                     (GraphNodeKind.SetHudVisible, [("visible", "false")]),
                     (GraphNodeKind.ResetCamera, []),
                 })
        {
            GraphNode action = graph.Add(kind, x += 250, 0, values: values);
            graph.Data(use, "player", action, "player");
            graph.Exec(last, action);
            actions[kind] = action;
            last = action;
        }

        graph.Data(yaw, "yaw", actions[GraphNodeKind.SetCameraRotation], "yaw");
        graph.Data(pitch, "pitch", actions[GraphNodeKind.SetCameraRotation], "pitch");
        graph.Data(use, "position", actions[GraphNodeKind.CameraLookAt], "position");
        graph.Data(nearest, "entity", actions[GraphNodeKind.ViewFromEntity], "entity");
        graph.Data(eyesY, "value", actions[GraphNodeKind.ShakeCamera], "strength");
        graph.Data(directionX, "value", actions[GraphNodeKind.TiltCamera], "angle");
        graph.Commit();
        return sample;
    }
}
