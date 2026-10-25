using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java code of the camera nodes (V0.7.4). The rotation and the point of view of the player are
/// changed on the server; the view, the zoom, the shake, the tilt and the interface only exist on
/// the client, so the server sends them in a message of the mod (its network differs per loader).
/// Written only when a graph uses a camera action.
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>Package of the camera class, under the root package of the mod.</summary>
    public const string CameraPackage = "network";

    /// <summary>Class called by the camera nodes of the graphs (server side).</summary>
    public const string CameraClass = "MineEngineCamera";

    /// <summary>Client side of the camera: applies the effects received from the server.</summary>
    protected const string CameraClientClass = "MineEngineCameraClient";

    /// <summary>Package of the client events (ViewportEvent, ClientPlayerNetworkEvent) of the loader.</summary>
    protected abstract string ClientEventPackage { get; }

    /// <summary>
    /// Members of the camera class that depend on the loader: the message, its registration in
    /// "public static void register(IEventBus modEventBus)" and
    /// "private static void sendTo(ServerPlayer player, int action, float a, float b)".
    /// </summary>
    protected abstract (IReadOnlyList<string> Imports, IReadOnlyList<string> Members) CameraNetwork(JavaModNaming naming, JavaGameApi api);

    private static string CameraPackageOf(JavaModNaming naming) => $"{naming.RootPackage}.{CameraPackage}";

    /// <summary>True if a graph, a function or a method of the mod runs a camera action.</summary>
    private static bool UsesCamera(IRContent content)
    {
        IEnumerable<IRStatement> bodies = content.Behaviors.SelectMany(b => b.Handlers).SelectMany(h => h.Body)
            .Concat(content.Definitions.OfType<IRFunction>().SelectMany(f => f.Body.Statements))
            .Concat(content.Definitions.OfType<IRClass>().SelectMany(c => c.Constructor.Statements));
        return bodies.SelectMany(AllStatements).OfType<IRActionStatement>()
            .Any(a => a.Action is >= GraphNodeKind.SetCameraRotation and <= GraphNodeKind.ResetCamera);
    }

    /// <summary>The statement and every statement nested in it (conditions, loops, switches, flip flops).</summary>
    private static IEnumerable<IRStatement> AllStatements(IRStatement statement)
    {
        IEnumerable<IRStatement> nested = statement switch
        {
            IRIfStatement condition => condition.Then.Concat(condition.Else),
            IRLoopStatement loop => loop.Body,
            IRSwitchStatement choice => choice.Bodies.SelectMany(b => b).Concat(choice.Default),
            IRFlipFlopStatement flipFlop => flipFlop.A.Concat(flipFlop.B),
            _ => [],
        };
        return nested.SelectMany(AllStatements).Prepend(statement);
    }

    private void WriteCamera(GenerationContext context, JavaModNaming naming, JavaGameApi api)
    {
        Write(context, naming, CameraPackageOf(naming), CameraClass, WriteCameraClass(naming, api));
        Write(context, naming, naming.ClientPackage, CameraClientClass, WriteCameraClientClass(naming));
    }

    /// <summary>Imports of the main class for the camera registrations.</summary>
    private IEnumerable<string> CameraMainImports(JavaModNaming naming) =>
        [$"{CameraPackageOf(naming)}.{CameraClass}", $"{naming.ClientPackage}.{CameraClientClass}", GameEventBus.Import, .. ClientEnvironmentImports];

    /// <summary>The message of the camera on the mod bus; the client listens to the events of the view.</summary>
    private void WriteCameraRegistrations(JavaSourceWriter w)
    {
        string gameBus = GameEventBus.Expression;
        w.Line($"{CameraClass}.register(modEventBus);")
            .OpenBlock($"if ({ClientEnvironmentCheck})")
            .Line($"{gameBus}.addListener({CameraClientClass}::onComputeFov);")
            .Line($"{gameBus}.addListener({CameraClientClass}::onComputeCameraAngles);")
            .Line($"{gameBus}.addListener({CameraClientClass}::onLoggingOut);")
            .CloseBlock();
    }

    private JavaSourceWriter WriteCameraClass(JavaModNaming naming, JavaGameApi api)
    {
        (IReadOnlyList<string> networkImports, IReadOnlyList<string> members) = CameraNetwork(naming, api);
        var imports = new SortedSet<string>(networkImports, StringComparer.Ordinal)
        {
            $"{naming.ClientPackage}.{CameraClientClass}",
            "java.util.EnumSet",
            "net.minecraft.commands.arguments.EntityAnchorArgument",
            "net.minecraft.core.BlockPos",
            "net.minecraft.server.level.ServerPlayer",
            "net.minecraft.util.Mth",
            "net.minecraft.world.entity.Entity",
            "net.minecraft.world.entity.RelativeMovement",
            "net.minecraft.world.entity.player.Player",
            "net.minecraft.world.phys.Vec3",
        };

        JavaSourceWriter w = WriteHeader(CameraPackageOf(naming), imports,
                "Caméra du joueur (nœuds caméra des graphes) : rotation et point de vue sur le serveur, effets envoyés au client.")
            .OpenBlock($"public final class {CameraClass}")
            .Line("/** Effects of the client, in the messages. */")
            .Line("public static final int PERSPECTIVE = 0;")
            .Line("public static final int ZOOM = 1;")
            .Line("public static final int SHAKE = 2;")
            .Line("public static final int TILT = 3;")
            .Line("public static final int HUD = 4;")
            .Line("public static final int RESET = 5;")
            .Line();
        foreach (string line in members)
        {
            w.Line(line);
        }

        string[] lines =
        [
            "",
            "/** Yaw: 0 south, 90 west, 180 north, -90 east. Pitch: -90 up, 90 down. The position does not change. */",
            "public static void setRotation(Player player, double yaw, double pitch) {",
            "    if (player instanceof ServerPlayer server) {",
            "        server.connection.teleport(0.0, 0.0, 0.0, (float) yaw, (float) Mth.clamp(pitch, -90.0, 90.0),",
            "                EnumSet.of(RelativeMovement.X, RelativeMovement.Y, RelativeMovement.Z));",
            "    }",
            "}",
            "",
            "public static void turn(Player player, double yaw, double pitch) {",
            "    setRotation(player, player.getYRot() + yaw, player.getXRot() + pitch);",
            "}",
            "",
            "public static void lookAt(Player player, BlockPos position) {",
            "    player.lookAt(EntityAnchorArgument.Anchor.EYES, Vec3.atCenterOf(position));",
            "}",
            "",
            "/** The player sees through the eyes of the entity (sneak to come back); null: the player's own eyes. */",
            "public static void viewFrom(Player player, Entity entity) {",
            "    if (player instanceof ServerPlayer server) {",
            "        server.setCamera(entity == null ? server : entity);",
            "    }",
            "}",
            "",
            "/** 0: first person, 1: third person from behind, 2: third person from the front. */",
            "public static void setPerspective(Player player, int perspective) {",
            "    send(player, PERSPECTIVE, perspective, 0.0f);",
            "}",
            "",
            "/** 100: normal, 200: twice closer, 50: wider. */",
            "public static void setZoom(Player player, int percent) {",
            "    send(player, ZOOM, Math.max(1, percent) / 100.0f, 0.0f);",
            "}",
            "",
            "public static void shake(Player player, double strength, double seconds) {",
            "    send(player, SHAKE, (float) strength, (float) seconds);",
            "}",
            "",
            "public static void tilt(Player player, double angle) {",
            "    send(player, TILT, (float) angle, 0.0f);",
            "}",
            "",
            "public static void setHudVisible(Player player, boolean visible) {",
            "    send(player, HUD, visible ? 1.0f : 0.0f, 0.0f);",
            "}",
            "",
            "public static void reset(Player player) {",
            "    viewFrom(player, null);",
            "    send(player, RESET, 0.0f, 0.0f);",
            "}",
            "",
            "private static void send(Player player, int action, float a, float b) {",
            "    if (player instanceof ServerPlayer server) {",
            "        sendTo(server, action, a, b);",
            "    }",
            "}",
        ];
        foreach (string line in lines)
        {
            w.Line(line);
        }

        return WritePrivateConstructor(w, CameraClass);
    }

    private JavaSourceWriter WriteCameraClientClass(JavaModNaming naming)
    {
        string[] imports =
        [
            $"{CameraPackageOf(naming)}.{CameraClass}",
            $"{ClientEventPackage}.ClientPlayerNetworkEvent",
            $"{ClientEventPackage}.ViewportEvent",
            "net.minecraft.Util",
            "net.minecraft.client.CameraType",
            "net.minecraft.client.Minecraft",
        ];

        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports,
                "Caméra du joueur (client) : vue, zoom, tremblement, inclinaison et interface demandés par le serveur.")
            .OpenBlock($"public final class {CameraClientClass}")
            .Line("private static float zoom = 1.0f;")
            .Line("private static float tilt = 0.0f;")
            .Line("private static float shakeStrength = 0.0f;")
            .Line("private static long shakeStart = 0L;")
            .Line("private static long shakeEnd = 0L;");

        string[] lines =
        [
            "",
            "/** An effect received from the server (see the constants of the camera class). */",
            "public static void apply(int action, float a, float b) {",
            "    Minecraft minecraft = Minecraft.getInstance();",
            "    switch (action) {",
            "        case MineEngineCamera.PERSPECTIVE -> minecraft.options.setCameraType(CameraType.values()[Math.floorMod((int) a, CameraType.values().length)]);",
            "        case MineEngineCamera.ZOOM -> zoom = Math.max(0.01f, a);",
            "        case MineEngineCamera.SHAKE -> {",
            "            shakeStrength = Math.max(0.0f, a);",
            "            shakeStart = Util.getMillis();",
            "            shakeEnd = shakeStart + (long) (Math.max(0.0f, b) * 1000.0f);",
            "        }",
            "        case MineEngineCamera.TILT -> tilt = a;",
            "        case MineEngineCamera.HUD -> minecraft.options.hideGui = a == 0.0f;",
            "        case MineEngineCamera.RESET -> {",
            "            clear();",
            "            minecraft.options.setCameraType(CameraType.FIRST_PERSON);",
            "        }",
            "        default -> {",
            "        }",
            "    }",
            "}",
            "",
            "/** The zoom narrows the field of view of the world (not the one of the hand). */",
            "public static void onComputeFov(ViewportEvent.ComputeFov event) {",
            "    if (zoom != 1.0f && event.usedConfiguredFov()) {",
            "        event.setFOV(event.getFOV() / zoom);",
            "    }",
            "}",
            "",
            "/** The shake fades out until its end; the tilt turns the view around its axis. */",
            "public static void onComputeCameraAngles(ViewportEvent.ComputeCameraAngles event) {",
            "    float roll = tilt;",
            "    long now = Util.getMillis();",
            "    if (now < shakeEnd) {",
            "        float strength = shakeStrength * (shakeEnd - now) / (float) (shakeEnd - shakeStart);",
            "        float time = now / 1000.0f;",
            "        event.setYaw(event.getYaw() + (float) Math.sin(time * 37.0f) * strength);",
            "        event.setPitch(event.getPitch() + (float) Math.cos(time * 41.0f) * strength);",
            "        roll += (float) Math.sin(time * 29.0f) * strength * 0.5f;",
            "    }",
            "    if (roll != 0.0f) {",
            "        event.setRoll(event.getRoll() + roll);",
            "    }",
            "}",
            "",
            "/** Leaving the world gives the normal camera back. */",
            "public static void onLoggingOut(ClientPlayerNetworkEvent.LoggingOut event) {",
            "    clear();",
            "}",
            "",
            "private static void clear() {",
            "    zoom = 1.0f;",
            "    tilt = 0.0f;",
            "    shakeEnd = 0L;",
            "    Minecraft.getInstance().options.hideGui = false;",
            "}",
        ];
        foreach (string line in lines)
        {
            w.Line(line);
        }

        return WritePrivateConstructor(w, CameraClientClass);
    }
}
