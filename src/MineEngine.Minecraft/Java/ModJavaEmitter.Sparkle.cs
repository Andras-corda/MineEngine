using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;
using MineEngine.Minecraft.Generation;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java code of the Sparkle particle systems (V0.7.5). Every system is a particle type of the mod:
/// the graphs send it to the players nearby with the particle packet of the game (no network code
/// per loader), and on the client it starts an invisible particle that runs the emitters. Every
/// emitter has a particle type of its own for its sprite; its particles move, bounce, grow and send
/// their death events like the preview of the editor (the settings are in seconds, the game counts
/// in ticks: 20 per second).
/// </summary>
public abstract partial class ModJavaEmitter
{
    /// <summary>Package of the Sparkle classes, under the root package of the mod.</summary>
    public const string SparklePackage = "particle";

    /// <summary>Particle types of the systems and of their emitters; called by the graphs (server side).</summary>
    public const string SparkleClass = "MineEngineSparkles";

    /// <summary>Settings of the emitters of every system.</summary>
    private const string SparkleDataClass = "MineEngineSparkleData";

    /// <summary>Client side: registration of the particle providers and of the sprites.</summary>
    private const string SparkleClientClass = "MineEngineSparkleClient";

    /// <summary>Invisible particle that runs a playing system.</summary>
    private const string SparkleSystemClass = "SparkleSystemParticle";

    /// <summary>A particle of an emitter.</summary>
    private const string SparkleParticleClass = "SparkleParticle";

    /// <summary>Name of the particle type (and of the sprite file) of an emitter: "fire_burst_e0".</summary>
    public static string SparkleSpriteName(IRSparkle sparkle, int emitter) => $"{sparkle.Id.Value}_e{emitter}";

    /// <summary>Name of the particle type that carries the value of a user parameter: "fire_burst_p0".</summary>
    public static string SparkleParameterName(IRSparkle sparkle, int parameter) => $"{sparkle.Id.Value}_p{parameter}";

    private static string SparklePackageOf(JavaModNaming naming) => $"{naming.RootPackage}.{SparklePackage}";

    /// <summary>True if the mod has a particle system or if a graph, a function or a method plays one.</summary>
    private static bool UsesSparkle(IRContent content)
    {
        IEnumerable<IRStatement> bodies = content.Behaviors.SelectMany(b => b.Handlers).SelectMany(h => h.Body)
            .Concat(content.Definitions.OfType<IRFunction>().SelectMany(f => f.Body.Statements))
            .Concat(content.Definitions.OfType<IRClass>().SelectMany(c => c.Constructor.Statements));
        return content.Sparkles.Count > 0
               || bodies.SelectMany(AllStatements).OfType<IRActionStatement>()
                   .Any(a => a.Action is GraphNodeKind.PlaySparkle or GraphNodeKind.PlaySparkleOnEntity
                       or (>= GraphNodeKind.SetSparkleNumber and <= GraphNodeKind.StopSparklesAt));
    }

    private void WriteSparkles(GenerationContext context, JavaModNaming naming)
    {
        IReadOnlyList<IRSparkle> sparkles = context.Ir.Content.Sparkles;
        Write(context, naming, SparklePackageOf(naming), SparkleClass, WriteSparkleClass(naming, sparkles));
        Write(context, naming, SparklePackageOf(naming), SparkleDataClass, WriteSparkleDataClass(naming, sparkles));
        Write(context, naming, naming.ClientPackage, SparkleClientClass, WriteSparkleClientClass(naming, sparkles));
        Write(context, naming, naming.ClientPackage, SparkleSystemClass, WriteSparkleSystemClass(naming));
        Write(context, naming, naming.ClientPackage, SparkleParticleClass, WriteSparkleParticleClass(naming));
    }

    /// <summary>Imports of the main class for the Sparkle registrations.</summary>
    private IEnumerable<string> SparkleMainImports(JavaModNaming naming) =>
        [$"{SparklePackageOf(naming)}.{SparkleClass}", $"{naming.ClientPackage}.{SparkleClientClass}", .. ClientEnvironmentImports];

    /// <summary>The particle types on the mod bus; the client registers their providers and sprites.</summary>
    private void WriteSparkleRegistrations(JavaSourceWriter w) =>
        w.Line($"{SparkleClass}.PARTICLE_TYPES.register(modEventBus);")
            .OpenBlock($"if ({ClientEnvironmentCheck})")
            .Line($"modEventBus.addListener({SparkleClientClass}::registerProviders);")
            .CloseBlock();

    private static string SparkleConstant(string name) => name.ToUpperInvariant();

    private JavaSourceWriter WriteSparkleClass(JavaModNaming naming, IReadOnlyList<IRSparkle> sparkles)
    {
        var imports = new List<string>(RegistryImports)
        {
            $"{naming.RootPackage}.{naming.MainClass}",
            "java.util.HashMap",
            "java.util.Map",
            "java.util.function.Supplier",
            "net.minecraft.core.BlockPos",
            "net.minecraft.core.particles.ParticleType",
            "net.minecraft.core.particles.SimpleParticleType",
            "net.minecraft.core.registries.Registries",
            "net.minecraft.server.level.ServerLevel",
            "net.minecraft.world.entity.Entity",
            "net.minecraft.world.level.Level",
            "net.minecraft.world.phys.Vec3",
        };

        string holder = RegistryHolderType("ParticleType<?>", "SimpleParticleType");
        JavaSourceWriter w = WriteHeader(SparklePackageOf(naming), imports,
                "Systèmes de particules Sparkle : un type de particule par système (joué par les graphes) et par émetteur (son sprite).")
            .OpenBlock($"public final class {SparkleClass}")
            .Line($"public static final DeferredRegister<ParticleType<?>> PARTICLE_TYPES = DeferredRegister.create(Registries.PARTICLE_TYPE, {naming.MainClass}.MOD_ID);")
            .Line()
            .Line("/** Systems by identifier, for the graphs. */")
            .Line("private static final Map<String, Supplier<SimpleParticleType>> SYSTEMS = new HashMap<>();")
            .Line("/** User parameters by system and by name: each one has a particle type that carries its value to the players. */")
            .Line("private static final Map<String, Map<String, Supplier<SimpleParticleType>>> PARAMETERS = new HashMap<>();")
            .Line("/** Values given by the graphs, sent just before the next play of the system. */")
            .Line("private static final Map<String, Map<String, double[]>> PENDING = new HashMap<>();")
            .Line()
            .Line("/** Tells the players nearby to stop systems: the ones of an entity, or the ones around a place. */")
            .Line($"public static final {holder} STOP = PARTICLE_TYPES.register(\"sparkle_stop\", () -> new SimpleParticleType(true));");

        foreach (IRSparkle sparkle in sparkles)
        {
            // The type of the system is always shown (even far away or with few particles): it only starts the emitters.
            w.Line()
                .Line($"public static final {holder} {SparkleConstant(sparkle.Id.Value)} = system({JavaNames.StringLiteral(sparkle.Id.Value)});");
            for (int i = 0; i < sparkle.Emitters.Count; i++)
            {
                string name = SparkleSpriteName(sparkle, i);
                w.Line($"public static final {holder} {SparkleConstant(name)} = PARTICLE_TYPES.register({JavaNames.StringLiteral(name)}, () -> new SimpleParticleType(false));");
            }

            for (int p = 0; p < sparkle.Parameters.Count; p++)
            {
                w.Line($"public static final {holder} {SparkleConstant(SparkleParameterName(sparkle, p))} = parameter({JavaNames.StringLiteral(sparkle.Id.Value)}, "
                       + $"{JavaNames.StringLiteral(sparkle.Parameters[p].Name)}, {p});");
            }
        }

        string[] lines =
        [
            "",
            $"private static {holder} system(String id) {{",
            $"    {holder} type = PARTICLE_TYPES.register(id, () -> new SimpleParticleType(true));",
            "    SYSTEMS.put(id, type);",
            "    return type;",
            "}",
            "",
            $"private static {holder} parameter(String system, String name, int index) {{",
            $"    {holder} type = PARTICLE_TYPES.register(system + \"_p\" + index, () -> new SimpleParticleType(true));",
            "    PARAMETERS.computeIfAbsent(system, s -> new HashMap<>()).put(name, type);",
            "    return type;",
            "}",
            "",
            "/** Identifier of a system without the namespace of the mod (\"testmod:fire\" and \"fire\" are the same). */",
            "private static String key(String system) {",
            "    if (system == null) {",
            "        return \"\";",
            "    }",
            "",
            $"    String prefix = {naming.MainClass}.MOD_ID + \":\";",
            "    return system.startsWith(prefix) ? system.substring(prefix.length()) : system;",
            "}",
            "",
            "/** Gives a number to a user parameter of the system, for its next play. */",
            "public static void setNumber(String system, String parameter, double value) {",
            "    store(system, parameter, value, 0.0, 0.0);",
            "}",
            "",
            "/** Gives a vector (an offset in blocks) to a user parameter of the system, for its next play. */",
            "public static void setVector(String system, String parameter, Vec3 value) {",
            "    if (value != null) {",
            "        store(system, parameter, value.x, value.y, value.z);",
            "    }",
            "}",
            "",
            "private static void store(String system, String parameter, double x, double y, double z) {",
            "    Map<String, Supplier<SimpleParticleType>> parameters = PARAMETERS.get(key(system));",
            "    if (parameters != null && parameter != null && parameters.containsKey(parameter.trim())) {",
            "        PENDING.computeIfAbsent(key(system), s -> new HashMap<>()).put(parameter.trim(), new double[] { x, y, z });",
            "    }",
            "}",
            "",
            "/** Sends the values given to the parameters of the system, just before it plays at the same place (they arrive first). */",
            "private static void sendParameters(ServerLevel server, String system, double x, double y, double z) {",
            "    Map<String, double[]> values = PENDING.remove(system);",
            "    Map<String, Supplier<SimpleParticleType>> parameters = PARAMETERS.get(system);",
            "    if (values == null || parameters == null) {",
            "        return;",
            "    }",
            "",
            "    for (Map.Entry<String, double[]> value : values.entrySet()) {",
            "        double[] v = value.getValue();",
            "        server.sendParticles(parameters.get(value.getKey()).get(), x, y, z, 0, v[0], v[1], v[2], 1.0);",
            "    }",
            "}",
            "",
            "/** Plays the system at the center of the block, for the players nearby. */",
            "public static void play(Level level, String system, BlockPos position) {",
            "    Supplier<SimpleParticleType> type = SYSTEMS.get(key(system));",
            "    if (type != null && position != null && level instanceof ServerLevel server) {",
            "        double x = position.getX() + 0.5, y = position.getY() + 0.5, z = position.getZ() + 0.5;",
            "        sendParameters(server, key(system), x, y, z);",
            "        server.sendParticles(type.get(), x, y, z, 0, 0.0, 0.0, 0.0, 1.0);",
            "    }",
            "}",
            "",
            "/** Plays the system on the entity: the number of the entity travels in the speed of the particle. */",
            "public static void playOn(Entity entity, String system) {",
            "    Supplier<SimpleParticleType> type = SYSTEMS.get(key(system));",
            "    if (type != null && entity != null && entity.level() instanceof ServerLevel server) {",
            "        sendParameters(server, key(system), entity.getX(), entity.getY(), entity.getZ());",
            "        server.sendParticles(type.get(), entity.getX(), entity.getY(), entity.getZ(), 0, entity.getId() + 1.0, 0.0, 0.0, 1.0);",
            "    }",
            "}",
            "",
            "/** Plays the system at the first block, with the vector parameter set to the way to the second one. */",
            "public static void playBetween(Level level, String system, String parameter, BlockPos from, BlockPos to) {",
            "    if (from != null && to != null) {",
            "        store(system, parameter, to.getX() - from.getX(), to.getY() - from.getY(), to.getZ() - from.getZ());",
            "        play(level, system, from);",
            "    }",
            "}",
            "",
            "/** Stops the systems played on the entity (their particles end their life). */",
            "public static void stopOn(Entity entity) {",
            "    if (entity != null && entity.level() instanceof ServerLevel server) {",
            "        server.sendParticles(STOP.get(), entity.getX(), entity.getY(), entity.getZ(), 0, entity.getId() + 1.0, 0.0, 0.0, 1.0);",
            "    }",
            "}",
            "",
            "/** Stops the systems playing around the block, up to the radius (in blocks). */",
            "public static void stopAt(Level level, BlockPos position, double radius) {",
            "    if (position != null && level instanceof ServerLevel server) {",
            "        server.sendParticles(STOP.get(), position.getX() + 0.5, position.getY() + 0.5, position.getZ() + 0.5, 0, 0.0, Math.max(0.0, radius), 0.0, 1.0);",
            "    }",
            "}",
        ];
        foreach (string line in lines)
        {
            w.Line(line);
        }

        return WritePrivateConstructor(w, SparkleClass);
    }

    private static JavaSourceWriter WriteSparkleDataClass(JavaModNaming naming, IReadOnlyList<IRSparkle> sparkles)
    {
        JavaSourceWriter w = WriteHeader(SparklePackageOf(naming), ["java.util.HashMap", "java.util.Map"],
                "Réglages des émetteurs des systèmes Sparkle (en ticks et en blocs par tick).")
            .OpenBlock($"public final class {SparkleDataClass}")
            .Line("public static final int POINT = 0;")
            .Line("public static final int SPHERE = 1;")
            .Line("public static final int BOX = 2;")
            .Line("public static final int LINE = 3;")
            .Line()
            .Line("public static final int DEATH = 0;")
            .Line("public static final int TIME = 1;")
            .Line("public static final int LOCATION = 2;")
            .Line()
            .Line("public static final int DIRECTION = 0;")
            .Line("public static final int CONE = 1;")
            .Line("public static final int RADIAL = 2;")
            .Line()
            .Line("public static final int FIXED = 0;")
            .Line("public static final int OVER_LIFE = 1;")
            .Line("public static final int RANDOM = 2;")
            .Line()
            .Line("/** Emitters of every system, by identifier. */")
            .Line("public static final Map<String, Emitter[]> SYSTEMS = new HashMap<>();")
            .Line()
            .OpenBlock("public static final class Emitter implements Cloneable");

        string[] fields =
        [
            "/** Particle type of its sprite (\"fire_burst_e0\"). */",
            "public final String sprite;",
            "/** Number used by the events. */",
            "public final int id;",
            "public int delay;",
            "public int duration = 40;",
            "/** Integer.MAX_VALUE: infinite. */",
            "public int loops = 1;",
            "public boolean burst = true;",
            "public int count = 1;",
            "public int shape = POINT;",
            "public double offsetX, offsetY, offsetZ;",
            "public double radius;",
            "public double sizeX, sizeY, sizeZ;",
            "/** Other end of the LINE shape (the offset is the first one). */",
            "public double lineX, lineY, lineZ;",
            "public int lifetimeMin = 20, lifetimeMax = 20;",
            "public int direction = DIRECTION;",
            "public double directionX, directionY = 1.0, directionZ;",
            "public double coneAngle;",
            "public double speedMin, speedMax;",
            "public double gravityX, gravityY, gravityZ;",
            "public boolean collides;",
            "public double bounce;",
            "public boolean diesOnCollision;",
            "public int scaleMode = FIXED;",
            "public float scaleA = 0.2f, scaleB = 0.2f;",
            "public float red = 1.0f, green = 1.0f, blue = 1.0f;",
            "public boolean glow;",
            "public boolean translucent = true;",
            "/** Several textures: the particle goes through them over its life (otherwise one at random). */",
            "public boolean animated;",
            "/** Chance (0-100) of a death event; below 0: none. */",
            "public double deathChance = -1.0;",
            "/** Chance (0-100) of a time event; below 0: none. */",
            "public double timeChance = -1.0;",
            "/** Age of the particle (ticks) when it sends its time event, or the interval between two. */",
            "public int timeTicks = 10;",
            "public boolean timeRepeats;",
            "/** Chance (0-100) of a location event when a particle enters the zone; below 0: none. */",
            "public double locationChance = -1.0;",
            "public int zoneShape = SPHERE;",
            "public double zoneX, zoneY, zoneZ;",
            "public double zoneRadius = 1.0;",
            "public double zoneSizeX = 2.0, zoneSizeY = 2.0, zoneSizeZ = 2.0;",
            "/** Emitter whose events spawn these particles; below 0: its own lifecycle. */",
            "public int eventSource = -1;",
            "/** DEATH, TIME or LOCATION: the events of the source it receives. */",
            "public int eventType = DEATH;",
            "public int countPerEvent = 1;",
            "public boolean atDeath = true;",
            "public double inheritVelocity;",
            "",
            "public Emitter(String sprite, int id) {",
            "    this.sprite = sprite;",
            "    this.id = id;",
            "}",
            "",
            "/** A copy for a playing system, whose parameters the graphs can change. */",
            "public Emitter copy() {",
            "    try {",
            "        return (Emitter) clone();",
            "    } catch (CloneNotSupportedException exception) {",
            "        throw new IllegalStateException(exception);",
            "    }",
            "}",
        ];
        foreach (string line in fields)
        {
            w.Line(line);
        }

        w.CloseBlock().Line().OpenBlock("static");
        for (int s = 0; s < sparkles.Count; s++)
        {
            IRSparkle sparkle = sparkles[s];
            if (s > 0)
            {
                w.Line();
            }

            w.Line($"Emitter[] s{s} = new Emitter[{sparkle.Emitters.Count}];");
            for (int i = 0; i < sparkle.Emitters.Count; i++)
            {
                IRSparkleEmitter emitter = sparkle.Emitters[i];
                string e = $"s{s}e{i}";
                w.Line($"// {emitter.Name.ReplaceLineEndings(" ")}")
                    .Line($"Emitter {e} = new Emitter({JavaNames.StringLiteral(SparkleSpriteName(sparkle, i))}, {emitter.EventId});");
                foreach (string setting in SparkleSettings(emitter))
                {
                    w.Line($"{e}.{setting};");
                }

                w.Line($"s{s}[{i}] = {e};");
            }

            w.Line($"SYSTEMS.put({JavaNames.StringLiteral(sparkle.Id.Value)}, s{s});");
        }

        w.CloseBlock();
        WriteApplyParameter(w, sparkles);
        return WritePrivateConstructor(w, SparkleDataClass);
    }

    /// <summary>
    /// The value of a user parameter given by a graph goes into the settings linked to it, in the
    /// copies of the emitters of a playing system (converted like the settings: ticks, blocks per tick...).
    /// </summary>
    private static void WriteApplyParameter(JavaSourceWriter w, IReadOnlyList<IRSparkle> sparkles)
    {
        w.Line()
            .Line("/** Gives the value of a user parameter (x, or x y z for a vector) to the settings linked to it. */")
            .OpenBlock("public static void applyParameter(String system, Emitter[] e, int parameter, double x, double y, double z)")
            .OpenBlock("switch (system)");
        foreach (IRSparkle sparkle in sparkles)
        {
            var byParameter = sparkle.Emitters
                .SelectMany((emitter, i) => emitter.Bindings.SelectMany(b => SparkleBindingAssignments(emitter, b).Select(a => (b.Parameter, Line: $"e[{i}].{a};"))))
                .GroupBy(a => a.Parameter)
                .OrderBy(g => g.Key)
                .ToList();
            if (byParameter.Count == 0)
            {
                continue;
            }

            w.Line($"case {JavaNames.StringLiteral(sparkle.Id.Value)} -> {{").Indent().OpenBlock("switch (parameter)");
            foreach (var group in byParameter)
            {
                w.Line($"case {group.Key} -> {{").Indent();
                foreach (var assignment in group)
                {
                    w.Line(assignment.Line);
                }

                w.Unindent().Line("}");
            }

            w.Line("default -> {").Line("}").CloseBlock().Unindent().Line("}");
        }

        w.Line("default -> {").Line("}").CloseBlock().CloseBlock();
    }

    /// <summary>
    /// Java assignments of a setting linked to a parameter (x, y, z: its value). A setting that has no
    /// effect with the other choices of the emitter (the size at death in Fixed mode...) gives none.
    /// </summary>
    private static IEnumerable<string> SparkleBindingAssignments(IRSparkleEmitter e, IRSparkleBinding binding)
    {
        const string Ticks = "Math.max(1, (int) Math.round(x * 20.0))";
        const string Whole = "Math.max(1, (int) Math.round(x))";
        string[] vector(string prefix, string scale = "") => [$"{prefix}X = x{scale}", $"{prefix}Y = y{scale}", $"{prefix}Z = z{scale}"];

        string[] assignments = (binding.Module, binding.Setting) switch
        {
            ("Lifecycle", "duration") => [$"duration = {Ticks}"],
            ("Lifecycle", "loops") when e.Loop == IRSparkleLoop.Loop => [$"loops = {Whole}"],
            ("Lifecycle", "delay") => ["delay = Math.max(0, (int) Math.round(x * 20.0))"],
            ("SpawnParticles", "count") => [$"count = {Whole}"],
            ("SpawnParticles", "offset") => vector("offset"),
            ("SpawnParticles", "radius") => ["radius = x"],
            ("SpawnParticles", "size") => vector("size"),
            ("SpawnParticles", "lineEnd") => vector("line"),
            ("SpawnParticles", "lifetimeMin") => [$"lifetimeMin = {Ticks}"],
            ("SpawnParticles", "lifetimeMax") => [$"lifetimeMax = {Ticks}"],
            ("Velocity", "direction") => vector("direction"),
            ("Velocity", "angle") => ["coneAngle = Math.toRadians(x)"],
            ("Velocity", "speedMin") => ["speedMin = x / 20.0"],
            ("Velocity", "speedMax") => ["speedMax = x / 20.0"],
            ("Gravity", "acceleration") when e.Gravity is not null => vector("gravity", " / 400.0"),
            ("Collision", "bounce") when e.Collides => ["bounce = x"],
            ("Collision", "die") when e.Collides => ["diesOnCollision = x != 0.0"],
            ("Scale", "value") when e.ScaleMode == IRSparkleScale.Fixed => ["scaleA = (float) x", "scaleB = (float) x"],
            ("Scale", "start") when e.ScaleMode == IRSparkleScale.OverLife => ["scaleA = (float) x"],
            ("Scale", "end") when e.ScaleMode == IRSparkleScale.OverLife => ["scaleB = (float) x"],
            ("Scale", "min") when e.ScaleMode == IRSparkleScale.Random => ["scaleA = (float) x"],
            ("Scale", "max") when e.ScaleMode == IRSparkleScale.Random => ["scaleB = (float) x"],
            ("Texture", "tint") => ["red = (((int) x >> 16) & 0xFF) / 255.0f", "green = (((int) x >> 8) & 0xFF) / 255.0f", "blue = ((int) x & 0xFF) / 255.0f"],
            ("Texture", "glow") => ["glow = x != 0.0"],
            ("Texture", "translucent") => ["translucent = x != 0.0"],
            ("GenerateDeathEvent", "chance") when e.DeathEventChance is not null => ["deathChance = x"],
            ("GenerateTimeEvent", "chance") when e.TimeEventChance is not null => ["timeChance = x"],
            ("GenerateTimeEvent", "time") => [$"timeTicks = {Ticks}"],
            ("GenerateLocationEvent", "chance") when e.LocationEventChance is not null => ["locationChance = x"],
            ("GenerateLocationEvent", "center") => vector("zone"),
            ("GenerateLocationEvent", "radius") => ["zoneRadius = x"],
            ("GenerateLocationEvent", "size") => [.. vector("zoneSize")],
            ("ReceiveDeathEvent", "count") => [$"countPerEvent = {Whole}"],
            ("ReceiveDeathEvent", "atDeath") => ["atDeath = x != 0.0"],
            ("ReceiveDeathEvent", "inheritVelocity") => ["inheritVelocity = x / 100.0"],
            _ => [],
        };
        return assignments;
    }

    /// <summary>Settings of an emitter that differ from the defaults of the Java class, converted to ticks.</summary>
    private static IEnumerable<string> SparkleSettings(IRSparkleEmitter e)
    {
        static string D(double value) => JavaNames.DoubleLiteral(value);
        static string F(double value) => JavaNames.FloatLiteral((float)value);
        static int Ticks(double seconds) => (int)Math.Max(1, Math.Round(seconds * 20));

        if (e.Delay > 0)
        {
            yield return $"delay = {(int)Math.Round(e.Delay * 20)}";
        }

        yield return $"duration = {Ticks(e.Duration)}";
        yield return $"loops = {(e.Loop switch { IRSparkleLoop.Infinite => "Integer.MAX_VALUE", IRSparkleLoop.Loop => e.Loops.ToString(System.Globalization.CultureInfo.InvariantCulture), _ => "1" })}";
        yield return $"burst = {(e.Burst ? "true" : "false")}";
        yield return $"count = {e.Count}";
        yield return $"shape = {e.Shape switch { IRSparkleShape.Sphere => "SPHERE", IRSparkleShape.Box => "BOX", IRSparkleShape.Line => "LINE", _ => "POINT" }}";
        yield return $"offsetX = {D(e.Offset.X)}";
        yield return $"offsetY = {D(e.Offset.Y)}";
        yield return $"offsetZ = {D(e.Offset.Z)}";
        if (e.Shape == IRSparkleShape.Sphere)
        {
            yield return $"radius = {D(e.Radius)}";
        }

        if (e.Shape == IRSparkleShape.Box)
        {
            yield return $"sizeX = {D(e.BoxSize.X)}";
            yield return $"sizeY = {D(e.BoxSize.Y)}";
            yield return $"sizeZ = {D(e.BoxSize.Z)}";
        }

        if (e.Shape == IRSparkleShape.Line)
        {
            yield return $"lineX = {D(e.LineEnd.X)}";
            yield return $"lineY = {D(e.LineEnd.Y)}";
            yield return $"lineZ = {D(e.LineEnd.Z)}";
        }

        yield return $"lifetimeMin = {Ticks(e.LifetimeMin)}";
        yield return $"lifetimeMax = {Ticks(e.LifetimeMax)}";
        yield return $"direction = {e.DirectionMode switch { IRSparkleDirection.Cone => "CONE", IRSparkleDirection.Radial => "RADIAL", _ => "DIRECTION" }}";
        yield return $"directionX = {D(e.Direction.X)}";
        yield return $"directionY = {D(e.Direction.Y)}";
        yield return $"directionZ = {D(e.Direction.Z)}";
        yield return $"coneAngle = {D(e.ConeAngle * Math.PI / 180)}";
        // Blocks per second to blocks per tick, blocks per second squared to blocks per tick squared.
        yield return $"speedMin = {D(e.SpeedMin / 20)}";
        yield return $"speedMax = {D(e.SpeedMax / 20)}";
        if (e.Gravity is { } gravity)
        {
            yield return $"gravityX = {D(gravity.X / 400)}";
            yield return $"gravityY = {D(gravity.Y / 400)}";
            yield return $"gravityZ = {D(gravity.Z / 400)}";
        }

        if (e.Collides)
        {
            yield return "collides = true";
            yield return $"bounce = {D(e.Bounce)}";
            yield return $"diesOnCollision = {(e.DiesOnCollision ? "true" : "false")}";
        }

        yield return $"scaleMode = {e.ScaleMode switch { IRSparkleScale.OverLife => "OVER_LIFE", IRSparkleScale.Random => "RANDOM", _ => "FIXED" }}";
        yield return $"scaleA = {F(e.ScaleA)}";
        yield return $"scaleB = {F(e.ScaleB)}";
        yield return $"red = {F(((e.Tint >> 16) & 0xFF) / 255.0)}";
        yield return $"green = {F(((e.Tint >> 8) & 0xFF) / 255.0)}";
        yield return $"blue = {F((e.Tint & 0xFF) / 255.0)}";
        yield return $"glow = {(e.Glow ? "true" : "false")}";
        yield return $"translucent = {(e.Translucent ? "true" : "false")}";
        if (e.AnimatedSprites && e.Sprites.Count > 1)
        {
            yield return "animated = true";
        }
        if (e.DeathEventChance is { } chance)
        {
            yield return $"deathChance = {D(chance)}";
        }

        if (e.TimeEventChance is { } timeChance)
        {
            yield return $"timeChance = {D(timeChance)}";
            yield return $"timeTicks = {Ticks(e.TimeEventTime)}";
            yield return $"timeRepeats = {(e.TimeEventRepeats ? "true" : "false")}";
        }

        if (e.LocationEventChance is { } locationChance)
        {
            yield return $"locationChance = {D(locationChance)}";
            yield return $"zoneShape = {(e.ZoneShape == IRSparkleShape.Box ? "BOX" : "SPHERE")}";
            yield return $"zoneX = {D(e.ZoneCenter.X)}";
            yield return $"zoneY = {D(e.ZoneCenter.Y)}";
            yield return $"zoneZ = {D(e.ZoneCenter.Z)}";
            yield return $"zoneRadius = {D(e.ZoneRadius)}";
            yield return $"zoneSizeX = {D(e.ZoneSize.X)}";
            yield return $"zoneSizeY = {D(e.ZoneSize.Y)}";
            yield return $"zoneSizeZ = {D(e.ZoneSize.Z)}";
        }

        if (e.EventSource is { } source)
        {
            yield return $"eventSource = {source}";
            yield return $"eventType = {e.ReceivedEvent switch { IRSparkleEvent.Time => "TIME", IRSparkleEvent.Location => "LOCATION", _ => "DEATH" }}";
            yield return $"countPerEvent = {e.CountPerEvent}";
            yield return $"atDeath = {(e.AtDeathLocation ? "true" : "false")}";
            yield return $"inheritVelocity = {D(e.InheritVelocity)}";
        }
    }

    private JavaSourceWriter WriteSparkleClientClass(JavaModNaming naming, IReadOnlyList<IRSparkle> sparkles)
    {
        string[] imports =
        [
            $"{SparklePackageOf(naming)}.{SparkleClass}",
            "java.util.HashMap",
            "java.util.Map",
            "net.minecraft.client.particle.SpriteSet",
            $"{ClientEventPackage}.RegisterParticleProvidersEvent",
        ];
        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports,
                "Côté client des systèmes Sparkle : chaque système démarre ses émetteurs, chaque émetteur garde son sprite.")
            .OpenBlock($"public final class {SparkleClientClass}")
            .Line("private static final Map<String, SpriteSet> SPRITES = new HashMap<>();")
            .Line()
            .Line("/** Sprite of an emitter (\"fire_burst_e0\"), or null before the resources are loaded. */")
            .OpenBlock("public static SpriteSet sprites(String name)")
            .Line("return SPRITES.get(name);")
            .CloseBlock()
            .Line()
            .OpenBlock("public static void registerProviders(RegisterParticleProvidersEvent event)");

        w.Line($"event.registerSpecial({SparkleClass}.STOP.get(),")
            .Indent().Indent()
            .Line($"(type, level, x, y, z, xd, yd, zd) -> {SparkleSystemClass}.stop(x, y, z, (int) Math.round(xd) - 1, yd));")
            .Unindent().Unindent();
        foreach (IRSparkle sparkle in sparkles)
        {
            string id = JavaNames.StringLiteral(sparkle.Id.Value);
            w.Line($"event.registerSpecial({SparkleClass}.{SparkleConstant(sparkle.Id.Value)}.get(),")
                .Indent().Indent()
                .Line($"(type, level, x, y, z, xd, yd, zd) -> {SparkleSystemClass}.start(level, {id}, x, y, z, (int) Math.round(xd) - 1));")
                .Unindent().Unindent();
            for (int p = 0; p < sparkle.Parameters.Count; p++)
            {
                w.Line($"event.registerSpecial({SparkleClass}.{SparkleConstant(SparkleParameterName(sparkle, p))}.get(),")
                    .Indent().Indent()
                    .Line($"(type, level, x, y, z, xd, yd, zd) -> {SparkleSystemClass}.parameter({id}, {p}, xd, yd, zd));")
                    .Unindent().Unindent();
            }

            for (int i = 0; i < sparkle.Emitters.Count; i++)
            {
                string name = SparkleSpriteName(sparkle, i);
                w.OpenBlock($"event.registerSpriteSet({SparkleClass}.{SparkleConstant(name)}.get(), sprites ->")
                    .Line($"SPRITES.put({JavaNames.StringLiteral(name)}, sprites);")
                    .Line("return (type, level, x, y, z, xd, yd, zd) -> null;")
                    .CloseBlock(");");
            }
        }

        w.CloseBlock();
        return WritePrivateConstructor(w, SparkleClientClass);
    }

    private static JavaSourceWriter WriteSparkleSystemClass(JavaModNaming naming)
    {
        string data = $"{SparklePackageOf(naming)}.{SparkleDataClass}";
        string[] imports =
        [
            data,
            $"{data}.Emitter",
            "net.minecraft.client.Minecraft",
            "net.minecraft.client.multiplayer.ClientLevel",
            "net.minecraft.client.particle.NoRenderParticle",
            "net.minecraft.client.particle.Particle",
            "net.minecraft.client.particle.SpriteSet",
            "net.minecraft.world.entity.Entity",
            "java.util.ArrayList",
            "java.util.HashMap",
            "java.util.List",
            "java.util.Map",
        ];
        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports,
                "Système Sparkle en cours : particule invisible qui fait vivre ses émetteurs (cycle de vie, apparition, événements).")
            .OpenBlock($"public final class {SparkleSystemClass} extends NoRenderParticle");

        string[] lines =
        [
            "/** Values of the parameters received before the play of a system (by system, then by parameter). */",
            "private static final Map<String, Map<Integer, double[]>> PENDING = new HashMap<>();",
            "/** Systems playing, so that the graphs can stop them. */",
            "private static final List<SparkleSystemParticle> LIVE = new ArrayList<>();",
            "",
            "private final Emitter[] emitters;",
            "/** Entity followed by the system; below 0: none. */",
            "private final int entityId;",
            "private final double[] spawned;",
            "private final int[] loopOf;",
            "private final int quietLimit;",
            "private int ticks;",
            "private int quietTicks;",
            "private int alive;",
            "private boolean stopped;",
            "",
            $"private {SparkleSystemClass}(ClientLevel level, Emitter[] emitters, double x, double y, double z, int entityId) {{",
            "    super(level, x, y, z);",
            "    this.emitters = emitters;",
            "    this.entityId = entityId;",
            "    this.spawned = new double[emitters.length];",
            "    this.loopOf = new int[emitters.length];",
            "    java.util.Arrays.fill(this.loopOf, -1);",
            "    int longest = 0;",
            "    for (Emitter emitter : emitters) {",
            "        longest = Math.max(longest, emitter.lifetimeMax);",
            "    }",
            "    // Particles removed by the game (too many) never tell their death: the system stops waiting after this.",
            "    this.quietLimit = longest * 4 + 40;",
            "    this.lifetime = Integer.MAX_VALUE;",
            "    LIVE.add(this);",
            "}",
            "",
            "@Override",
            "public void remove() {",
            "    super.remove();",
            "    LIVE.remove(this);",
            "}",
            "",
            "/** A value of a user parameter, received just before the play of its system. */",
            "public static Particle parameter(String system, int index, double x, double y, double z) {",
            "    PENDING.computeIfAbsent(system, s -> new HashMap<>()).put(index, new double[] { x, y, z });",
            "    return null;",
            "}",
            "",
            "/** Stops the systems of an entity (entityId at least 0), or the ones within the radius of the point. */",
            "public static Particle stop(double x, double y, double z, int entityId, double radius) {",
            "    for (SparkleSystemParticle system : LIVE) {",
            "        double dx = system.x - x, dy = system.y - y, dz = system.z - z;",
            "        if (entityId >= 0 ? system.entityId == entityId : dx * dx + dy * dy + dz * dz <= radius * radius) {",
            "            system.stopped = true;",
            "        }",
            "    }",
            "",
            "    return null;",
            "}",
            "",
            "/** Starts a system received from the server, with the values of its parameters received just before; null if it does not exist. */",
            "public static Particle start(ClientLevel level, String system, double x, double y, double z, int entityId) {",
            $"    Emitter[] settings = {SparkleDataClass}.SYSTEMS.get(system);",
            "    Map<Integer, double[]> values = PENDING.remove(system);",
            "    if (settings == null) {",
            "        return null;",
            "    }",
            "",
            "    Emitter[] emitters = new Emitter[settings.length];",
            "    for (int i = 0; i < settings.length; i++) {",
            "        emitters[i] = settings[i].copy();",
            "    }",
            "",
            "    if (values != null) {",
            "        for (Map.Entry<Integer, double[]> value : values.entrySet()) {",
            "            double[] v = value.getValue();",
            $"            {SparkleDataClass}.applyParameter(system, emitters, value.getKey(), v[0], v[1], v[2]);",
            "        }",
            "    }",
            "",
            $"    return new {SparkleSystemClass}(level, emitters, x, y, z, entityId);",
            "}",
            "",
            "@Override",
            "public void tick() {",
            "    if (entityId >= 0 && !stopped) {",
            "        Entity entity = level.getEntity(entityId);",
            "        if (entity == null || entity.isRemoved()) {",
            "            stopped = true;",
            "        } else {",
            "            setPos(entity.getX(), entity.getY(), entity.getZ());",
            "        }",
            "    }",
            "",
            "    int from = ticks++;",
            "    boolean active = false;",
            "    for (int i = 0; i < emitters.length && !stopped; i++) {",
            "        if (emitters[i].eventSource < 0 && spawnOwn(i, from, ticks)) {",
            "            active = true;",
            "        }",
            "    }",
            "",
            "    quietTicks = active ? 0 : quietTicks + 1;",
            "    if (!active && (alive <= 0 || quietTicks > quietLimit)) {",
            "        remove();",
            "    }",
            "}",
            "",
            "/** Spawns the particles of the emitter between the two ticks; false when its lifecycle is over. */",
            "private boolean spawnOwn(int index, int from, int to) {",
            "    Emitter e = emitters[index];",
            "    long end = e.loops == Integer.MAX_VALUE ? Long.MAX_VALUE : e.delay + (long) e.duration * e.loops;",
            "    int time = Math.max(from, e.delay);",
            "    if (time < to && time < end) {",
            "        int loop = (time - e.delay) / e.duration;",
            "        int loopStart = e.delay + loop * e.duration;",
            "        if (loopOf[index] != loop) {",
            "            loopOf[index] = loop;",
            "            spawned[index] = 0;",
            "        }",
            "",
            "        double wanted = e.burst ? e.count : Math.min(e.count, e.count * (double) (to - loopStart) / e.duration);",
            "        for (int n = (int) spawned[index]; n < (int) wanted; n++) {",
            "            spawn(index, x, y, z, 0.0, 0.0, 0.0);",
            "        }",
            "",
            "        spawned[index] = Math.max(spawned[index], wanted);",
            "    }",
            "",
            "    return to < end;",
            "}",
            "",
            "private void spawn(int index, double centerX, double centerY, double centerZ, double parentX, double parentY, double parentZ) {",
            "    Emitter e = emitters[index];",
            $"    SpriteSet sprites = {SparkleClientClass}.sprites(e.sprite);",
            "    if (sprites == null) {",
            "        return;",
            "    }",
            "",
            "    centerX += e.offsetX;",
            "    centerY += e.offsetY;",
            "    centerZ += e.offsetZ;",
            "    double px = 0.0, py = 0.0, pz = 0.0;",
            $"    if (e.shape == {SparkleDataClass}.SPHERE) {{",
            "        double[] unit = randomUnit();",
            "        double r = e.radius * Math.cbrt(random.nextDouble());",
            "        px = unit[0] * r;",
            "        py = unit[1] * r;",
            "        pz = unit[2] * r;",
            $"    }} else if (e.shape == {SparkleDataClass}.BOX) {{",
            "        px = e.sizeX * (random.nextDouble() - 0.5);",
            "        py = e.sizeY * (random.nextDouble() - 0.5);",
            "        pz = e.sizeZ * (random.nextDouble() - 0.5);",
            $"    }} else if (e.shape == {SparkleDataClass}.LINE) {{",
            "        double t = random.nextDouble();",
            "        px = (e.lineX - e.offsetX) * t;",
            "        py = (e.lineY - e.offsetY) * t;",
            "        pz = (e.lineZ - e.offsetZ) * t;",
            "    }",
            "",
            "    double[] direction;",
            $"    if (e.direction == {SparkleDataClass}.RADIAL) {{",
            "        double length = Math.sqrt(px * px + py * py + pz * pz);",
            "        direction = length > 1.0E-9 ? new double[] { px / length, py / length, pz / length } : randomUnit();",
            "    } else {",
            "        double length = Math.sqrt(e.directionX * e.directionX + e.directionY * e.directionY + e.directionZ * e.directionZ);",
            "        direction = length > 1.0E-9 ? new double[] { e.directionX / length, e.directionY / length, e.directionZ / length } : new double[] { 0.0, 1.0, 0.0 };",
            $"        if (e.direction == {SparkleDataClass}.CONE) {{",
            "            direction = inCone(direction, e.coneAngle);",
            "        }",
            "    }",
            "",
            "    double speed = e.speedMin + random.nextDouble() * (e.speedMax - e.speedMin);",
            "    int lifetime = e.lifetimeMin + (e.lifetimeMax > e.lifetimeMin ? random.nextInt(e.lifetimeMax - e.lifetimeMin + 1) : 0);",
            $"    Minecraft.getInstance().particleEngine.add(new {SparkleParticleClass}(level, this, index, e, sprites,",
            "            centerX + px, centerY + py, centerZ + pz,",
            "            direction[0] * speed + parentX * e.inheritVelocity,",
            "            direction[1] * speed + parentY * e.inheritVelocity,",
            "            direction[2] * speed + parentZ * e.inheritVelocity, lifetime));",
            "    alive++;",
            "}",
            "",
            "/** Called by a particle of the system when it dies: sends its death event. */",
            "void onDeath(int index, double x, double y, double z, double xd, double yd, double zd) {",
            "    alive--;",
            $"    onEvent(index, {SparkleDataClass}.DEATH, x, y, z, xd, yd, zd);",
            "}",
            "",
            "/** An event of a particle (DEATH, TIME, LOCATION): the emitters that receive this kind from its emitter spawn. */",
            "void onEvent(int index, int type, double x, double y, double z, double xd, double yd, double zd) {",
            "    Emitter e = emitters[index];",
            $"    double chance = type == {SparkleDataClass}.TIME ? e.timeChance : type == {SparkleDataClass}.LOCATION ? e.locationChance : e.deathChance;",
            "    if (!isAlive() || chance < 0.0 || random.nextDouble() * 100.0 >= chance) {",
            "        return;",
            "    }",
            "",
            "    for (int i = 0; i < emitters.length; i++) {",
            "        Emitter receiver = emitters[i];",
            "        if (i == index || receiver.eventSource != e.id || receiver.eventType != type) {",
            "            continue;",
            "        }",
            "",
            "        for (int n = 0; n < receiver.countPerEvent; n++) {",
            "            if (receiver.atDeath) {",
            "                spawn(i, x, y, z, xd, yd, zd);",
            "            } else {",
            "                spawn(i, this.x, this.y, this.z, xd, yd, zd);",
            "            }",
            "        }",
            "    }",
            "}",
            "",
            "/** True if the point is in the zone of the location event of the emitter (around the system). */",
            "boolean inZone(Emitter e, double px, double py, double pz) {",
            "    double dx = px - (x + e.zoneX), dy = py - (y + e.zoneY), dz = pz - (z + e.zoneZ);",
            $"    if (e.zoneShape == {SparkleDataClass}.BOX) {{",
            "        return Math.abs(dx) <= e.zoneSizeX / 2.0 && Math.abs(dy) <= e.zoneSizeY / 2.0 && Math.abs(dz) <= e.zoneSizeZ / 2.0;",
            "    }",
            "",
            "    return dx * dx + dy * dy + dz * dz <= e.zoneRadius * e.zoneRadius;",
            "}",
            "",
            "private double[] randomUnit() {",
            "    double y = random.nextDouble() * 2.0 - 1.0;",
            "    double turn = random.nextDouble() * Math.PI * 2.0;",
            "    double r = Math.sqrt(1.0 - y * y);",
            "    return new double[] { r * Math.cos(turn), y, r * Math.sin(turn) };",
            "}",
            "",
            "/** Random direction at most angle radians away from the axis (uniform on the cap). */",
            "private double[] inCone(double[] axis, double angle) {",
            "    double cos = 1.0 - random.nextDouble() * (1.0 - Math.cos(angle));",
            "    double sin = Math.sqrt(Math.max(0.0, 1.0 - cos * cos));",
            "    double turn = random.nextDouble() * Math.PI * 2.0;",
            "    double[] helper = Math.abs(axis[1]) < 0.99 ? new double[] { 0.0, 1.0, 0.0 } : new double[] { 1.0, 0.0, 0.0 };",
            "    double[] u = normalize(cross(axis, helper));",
            "    double[] v = cross(axis, u);",
            "    double a = sin * Math.cos(turn);",
            "    double b = sin * Math.sin(turn);",
            "    return new double[] { axis[0] * cos + u[0] * a + v[0] * b, axis[1] * cos + u[1] * a + v[1] * b, axis[2] * cos + u[2] * a + v[2] * b };",
            "}",
            "",
            "private static double[] cross(double[] a, double[] b) {",
            "    return new double[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };",
            "}",
            "",
            "private static double[] normalize(double[] a) {",
            "    double length = Math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);",
            "    return length > 1.0E-9 ? new double[] { a[0] / length, a[1] / length, a[2] / length } : a;",
            "}",
        ];
        foreach (string line in lines)
        {
            w.Line(line);
        }

        return w.CloseBlock();
    }

    private static JavaSourceWriter WriteSparkleParticleClass(JavaModNaming naming)
    {
        string data = $"{SparklePackageOf(naming)}.{SparkleDataClass}";
        string[] imports =
        [
            data,
            $"{data}.Emitter",
            "java.util.List",
            "net.minecraft.client.multiplayer.ClientLevel",
            "net.minecraft.client.particle.ParticleRenderType",
            "net.minecraft.client.particle.SpriteSet",
            "net.minecraft.client.particle.TextureSheetParticle",
            "net.minecraft.world.entity.Entity",
            "net.minecraft.world.phys.Vec3",
        ];
        JavaSourceWriter w = WriteHeader(naming.ClientPackage, imports,
                "Particule d'un émetteur Sparkle : gravité, collision avec rebond, taille, lumière et événements (mort, temps, zone).")
            .OpenBlock($"public final class {SparkleParticleClass} extends TextureSheetParticle");

        string[] lines =
        [
            $"private final {SparkleSystemClass} system;",
            "private final int index;",
            "private final Emitter emitter;",
            "private final float randomScale;",
            "private final SpriteSet sprites;",
            "/** Inside the zone of its location event at the last tick (the event is sent when it enters). */",
            "private boolean inZone;",
            "",
            $"{SparkleParticleClass}(ClientLevel level, {SparkleSystemClass} system, int index, Emitter emitter, SpriteSet sprites,",
            "        double x, double y, double z, double xd, double yd, double zd, int lifetime) {",
            "    super(level, x, y, z);",
            "    this.system = system;",
            "    this.index = index;",
            "    this.emitter = emitter;",
            "    this.xd = xd;",
            "    this.yd = yd;",
            "    this.zd = zd;",
            "    this.lifetime = Math.max(1, lifetime);",
            "    this.hasPhysics = false;",
            "    this.gravity = 0.0f;",
            "    this.randomScale = emitter.scaleA + random.nextFloat() * (emitter.scaleB - emitter.scaleA);",
            "    this.quadSize = size(0.0f);",
            "    setColor(emitter.red, emitter.green, emitter.blue);",
            "    this.sprites = sprites;",
            "    if (emitter.animated) {",
            "        setSpriteFromAge(sprites);",
            "    } else {",
            "        pickSprite(sprites);",
            "    }",
            "    this.inZone = emitter.locationChance >= 0.0 && system.inZone(emitter, x, y, z);",
            "}",
            "",
            "@Override",
            "public void tick() {",
            "    xo = x;",
            "    yo = y;",
            "    zo = z;",
            "    if (age++ >= lifetime) {",
            "        die();",
            "        return;",
            "    }",
            "",
            "    if (emitter.animated) {",
            "        setSpriteFromAge(sprites);",
            "    }",
            "",
            "    xd += emitter.gravityX;",
            "    yd += emitter.gravityY;",
            "    zd += emitter.gravityZ;",
            "    double mx = xd, my = yd, mz = zd;",
            "    if (emitter.collides) {",
            "        Vec3 moved = Entity.collideBoundingBox(null, new Vec3(xd, yd, zd), getBoundingBox(), level, List.of());",
            "        mx = moved.x;",
            "        my = moved.y;",
            "        mz = moved.z;",
            "        boolean hit = mx != xd || my != yd || mz != zd;",
            "        if (hit && emitter.diesOnCollision) {",
            "            setPos(x + mx, y + my, z + mz);",
            "            die();",
            "            return;",
            "        }",
            "",
            "        // Bounce: the speed stopped by a block goes back the other way.",
            "        if (mx != xd) {",
            "            xd = -xd * emitter.bounce;",
            "        }",
            "",
            "        if (my != yd) {",
            "            yd = -yd * emitter.bounce;",
            "        }",
            "",
            "        if (mz != zd) {",
            "            zd = -zd * emitter.bounce;",
            "        }",
            "    }",
            "",
            "    setPos(x + mx, y + my, z + mz);",
            "    if (emitter.timeChance >= 0.0 && (emitter.timeRepeats ? age % emitter.timeTicks == 0 : age == emitter.timeTicks)) {",
            $"        system.onEvent(index, {SparkleDataClass}.TIME, x, y, z, xd, yd, zd);",
            "    }",
            "",
            "    if (emitter.locationChance >= 0.0) {",
            "        boolean inside = system.inZone(emitter, x, y, z);",
            "        if (inside && !inZone) {",
            $"            system.onEvent(index, {SparkleDataClass}.LOCATION, x, y, z, xd, yd, zd);",
            "        }",
            "",
            "        inZone = inside;",
            "    }",
            "}",
            "",
            "private void die() {",
            "    remove();",
            "    system.onDeath(index, x, y, z, xd, yd, zd);",
            "}",
            "",
            "/** Half of the size of the square, in blocks. */",
            "private float size(float partialTick) {",
            "    float size;",
            $"    if (emitter.scaleMode == {SparkleDataClass}.OVER_LIFE) {{",
            "        float progress = Math.min(1.0f, (age + partialTick) / lifetime);",
            "        size = emitter.scaleA + (emitter.scaleB - emitter.scaleA) * progress;",
            $"    }} else if (emitter.scaleMode == {SparkleDataClass}.RANDOM) {{",
            "        size = randomScale;",
            "    } else {",
            "        size = emitter.scaleA;",
            "    }",
            "",
            "    return size / 2.0f;",
            "}",
            "",
            "@Override",
            "public float getQuadSize(float partialTick) {",
            "    return size(partialTick);",
            "}",
            "",
            "@Override",
            "protected int getLightColor(float partialTick) {",
            "    return emitter.glow ? 0xF000F0 : super.getLightColor(partialTick);",
            "}",
            "",
            "@Override",
            "public ParticleRenderType getRenderType() {",
            "    return emitter.translucent ? ParticleRenderType.PARTICLE_SHEET_TRANSLUCENT : ParticleRenderType.PARTICLE_SHEET_OPAQUE;",
            "}",
        ];
        foreach (string line in lines)
        {
            w.Line(line);
        }

        return w.CloseBlock();
    }
}
