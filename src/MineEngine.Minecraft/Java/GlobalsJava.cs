using System.Globalization;
using MineEngine.Assets;
using MineEngine.Assets.Graphs;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>Where the value of a global variable is kept in the generated class.</summary>
public enum GlobalStorage
{
    /// <summary>A static field (global_session) or a field of the server data (global_server): no argument.</summary>
    Static,

    /// <summary>Saved data of the save or of a dimension: the accessors take the world.</summary>
    Level,

    /// <summary>A value per player: the accessors take the player.</summary>
    Player,

    /// <summary>A table in the saved data of the save, found by a key computed from the world and the player, the entity or the position.</summary>
    Keyed,
}

/// <summary>
/// Class MineEngineGlobals of the package "code": the global variables of the Global tab, each one
/// kept according to its scope. global_session: static fields, back to their default value when the
/// server starts. global_map: saved data of the overworld. global_dimension, world_dimension: saved
/// data of each dimension. player_persistent, player_lifetime: data written in the persistent data
/// of the player (at death, the lifetime variables go back to their default value). player_session:
/// in memory while the player is connected. global_server, player_account: a file of the config
/// folder, shared by every save. The other scopes (player_dimension, player_guild, player_team,
/// entity_*, global_chunk, world_structure, world_biome) are tables of the saved data of the
/// overworld, found by a key ("uuid|dimension", the name of the team...). Every type is written as
/// NBT, collections included; entities, players and objects of the project are kept in memory only.
/// </summary>
public sealed class GlobalsJava
{
    private readonly JavaProgramNames _names;
    private readonly JavaGameApi _api;
    private readonly SortedSet<string> _imports = new(StringComparer.Ordinal);
    private int _counter;

    public GlobalsJava(JavaProgramNames names, JavaGameApi api)
    {
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    private bool Modern => _api.UsesDataComponents;

    /// <summary>Where a variable of this scope is kept.</summary>
    public static GlobalStorage Storage(GlobalScope scope) => scope switch
    {
        GlobalScope.Session or GlobalScope.GlobalServer => GlobalStorage.Static,
        GlobalScope.Map or GlobalScope.World or GlobalScope.WorldDimension => GlobalStorage.Level,
        GlobalScope.PlayerPersistent or GlobalScope.PlayerLifetime or GlobalScope.PlayerSession or GlobalScope.PlayerAccount => GlobalStorage.Player,
        _ => GlobalStorage.Keyed,
    };

    /// <summary>Java method that computes the key of a keyed scope ("keyTeam").</summary>
    private static string KeyMethod(GlobalScope scope) => scope switch
    {
        GlobalScope.PlayerDimension => "keyPlayerDimension",
        GlobalScope.PlayerGuild => "keyGuild",
        GlobalScope.PlayerTeam => "keyTeam",
        GlobalScope.EntityType => "keyEntityType",
        GlobalScope.EntityOwner => "keyOwner",
        GlobalScope.EntityGroup => "keyGroup",
        GlobalScope.EntityDimension => "keyEntityDimension",
        GlobalScope.GlobalChunk => "keyChunk",
        GlobalScope.WorldStructure => "keyStructure",
        _ => "keyBiome",
    };

    /// <summary>Java type of what the accessors of the scope take besides the world (Player, Entity, BlockPos).</summary>
    private static string KeyType(GlobalScope scope) => GlobalVariableAsset.KeyOf(scope) switch
    {
        GlobalScopeKey.Player => "Player",
        GlobalScopeKey.Entity => "Entity",
        _ => "BlockPos",
    };

    /// <summary>A variable kept in a table found by a text key: its field is a dictionary of its type.</summary>
    private static IRGlobalVariable AsTable(IRGlobalVariable global) =>
        global with { Type = DataType.Collection(PinType.Map, global.Type, DataType.Text), DefaultValue = string.Empty };

    /// <summary>Writes the class; <paramref name="playerEvent"/> and <paramref name="serverStarting"/> are the event classes of the loader.</summary>
    public string Write(string modId, (string Expression, string Import) gameBus, string playerEvent, string serverStarting,
        string serverStopping, string fmlPaths)
    {
        List<(IRGlobalVariable Global, string Name)> globals = [.. _names.Globals];
        List<(IRGlobalVariable Global, string Name)> session = [.. globals.Where(g => g.Global.Scope == GlobalScope.Session)];
        List<(IRGlobalVariable Global, string Name)> keyed = [.. globals.Where(g => Storage(g.Global.Scope) == GlobalStorage.Keyed)];
        List<(IRGlobalVariable Global, string Name)> map =
            [.. globals.Where(g => g.Global.Scope == GlobalScope.Map), .. keyed.Select(g => (AsTable(g.Global), g.Name))];
        List<(IRGlobalVariable Global, string Name)> world = [.. globals.Where(g => g.Global.Scope is GlobalScope.World or GlobalScope.WorldDimension)];
        List<(IRGlobalVariable Global, string Name)> players = [.. globals.Where(g => g.Global.Scope is GlobalScope.PlayerPersistent or GlobalScope.PlayerLifetime)];
        List<(IRGlobalVariable Global, string Name)> playerSessions = [.. globals.Where(g => g.Global.Scope == GlobalScope.PlayerSession)];
        List<(IRGlobalVariable Global, string Name)> server =
        [
            .. globals.Where(g => g.Global.Scope == GlobalScope.GlobalServer),
            .. globals.Where(g => g.Global.Scope == GlobalScope.PlayerAccount).Select(g => (AsTable(g.Global), g.Name)),
        ];
        bool hasServer = server.Count > 0;

        foreach (string import in new[]
                 {
                     "net.minecraft.nbt.CompoundTag", "net.minecraft.nbt.ListTag", "net.minecraft.nbt.Tag", "net.minecraft.server.level.ServerLevel",
                     "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level", "net.minecraft.world.level.saveddata.SavedData",
                     "java.util.HashMap", "java.util.Map", "java.util.UUID", playerEvent, serverStarting, gameBus.Import,
                 })
        {
            _imports.Add(import);
        }

        if (Modern)
        {
            _imports.Add("net.minecraft.core.HolderLookup");
        }

        _imports.Add("java.util.UUID");
        _imports.Add("net.minecraft.world.phys.AABB");

        foreach ((IRGlobalVariable global, _) in globals)
        {
            _imports.UnionWith(JavaProgramNames.ImportsFor(global.Type));
        }

        var body = new JavaSourceWriter();
        body.Indent();
        body.Line($"private static final String KEY = {JavaNames.StringLiteral(modId + "_globals")};").Line();

        // Session
        body.Line("// ----- Session: back to the default value at each start of the server -----");
        foreach ((IRGlobalVariable global, string name) in session)
        {
            body.Line($"public static {_names.JavaType(global.Type)} {name} = {Default(global)};");
        }

        body.Line();
        WriteSavedData(body, "MapData", Tr.T("Map: one value for the whole save, stored with the overworld."), map);
        WriteSavedData(body, "WorldData", Tr.T("World: one value per dimension."), world);
        WritePlayerData(body, players);
        if (hasServer)
        {
            WriteSavedData(body, "ServerData", Tr.T("Server: shared by every save, kept in the config folder of the game."), server, savedData: false);
            _imports.UnionWith(["java.nio.file.Files", "java.nio.file.Path", "java.io.InputStream", "java.io.OutputStream", "java.io.IOException",
                "net.minecraft.nbt.NbtIo", "net.minecraft.server.MinecraftServer", fmlPaths, serverStopping]);
            if (Modern)
            {
                _imports.Add("net.minecraft.nbt.NbtAccounter");
            }
        }

        // player_session: in memory, by player, while the player is connected.
        foreach ((IRGlobalVariable global, string name) in playerSessions)
        {
            body.Line($"public static final Map<UUID, {_names.BoxedType(global.Type)}> {name} = new HashMap<>();");
        }

        if (playerSessions.Count > 0)
        {
            body.Line();
        }

        string bus = gameBus.Expression;
        string playerEventName = playerEvent[(playerEvent.LastIndexOf('.') + 1)..];
        string startingName = serverStarting[(serverStarting.LastIndexOf('.') + 1)..];
        body.Line("private static final Map<UUID, PlayerData> PLAYERS = new HashMap<>();")
            .Line("private static final MapData CLIENT_MAP = new MapData();")
            .Line("private static final WorldData CLIENT_WORLD = new WorldData();")
            .Line()
            .OpenBlock("public static void register()")
            .Line($"{bus}.addListener({JavaProgramNames.GlobalsClass}::onServerStarting);")
            .Line($"{bus}.addListener({JavaProgramNames.GlobalsClass}::onPlayerClone);")
            .Line($"{bus}.addListener({JavaProgramNames.GlobalsClass}::onPlayerSave);")
            .Line($"{bus}.addListener({JavaProgramNames.GlobalsClass}::onPlayerLeave);");
        if (hasServer)
        {
            body.Line($"{bus}.addListener({JavaProgramNames.GlobalsClass}::onServerStopping);");
        }

        body.CloseBlock()
            .Line()
            .OpenBlock("private static UUID globalUuid(String text)")
            .OpenBlock("try")
            .Line("return UUID.fromString(text);")
            .CloseBlock()
            .OpenBlock("catch (IllegalArgumentException exception)")
            .Line("return new UUID(0L, 0L);")
            .CloseBlock()
            .CloseBlock()
            .Line()
            .OpenBlock("private static AABB globalBox(String text)")
            .Line("String[] parts = text.split(\",\");")
            .OpenBlock("if (parts.length != 6)")
            .Line("return new AABB(0, 0, 0, 0, 0, 0);")
            .CloseBlock()
            .Line("double[] values = new double[6];")
            .OpenBlock("for (int i = 0; i < 6; i++)")
            .Line("values[i] = Double.parseDouble(parts[i]);")
            .CloseBlock()
            .Line("return new AABB(values[0], values[1], values[2], values[3], values[4], values[5]);")
            .CloseBlock()
            .Line()
            .OpenBlock($"private static void onServerStarting({startingName} event)");
        foreach ((IRGlobalVariable global, string name) in session)
        {
            body.Line($"{name} = {Default(global)};");
        }

        foreach ((_, string name) in playerSessions)
        {
            body.Line($"{name}.clear();");
        }

        if (hasServer)
        {
            body.Line("loadServer(event.getServer());");
        }

        body.Line("PLAYERS.clear();")
            .CloseBlock()
            .Line()
            .Line("/** A new player entity after death or after the End: persistent variables follow, lifetime ones start again at death. */")
            .OpenBlock($"private static void onPlayerClone({playerEventName}.Clone event)")
            .Line("Player original = event.getOriginal();")
            .Line("PlayerData data = PLAYERS.containsKey(original.getUUID()) ? PLAYERS.get(original.getUUID()) : PlayerData.load(original.getPersistentData().getCompound(KEY)" + (Modern ? ", original.level().registryAccess()" : string.Empty) + ");")
            .OpenBlock("if (event.isWasDeath())")
            .Line("data.resetLifetime();")
            .CloseBlock()
            .Line("PLAYERS.put(event.getEntity().getUUID(), data);")
            .Line("savePlayer(event.getEntity(), data);")
            .CloseBlock()
            .Line()
            .OpenBlock($"private static void onPlayerSave({playerEventName}.SaveToFile event)")
            .Line("PlayerData data = PLAYERS.get(event.getEntity().getUUID());")
            .OpenBlock("if (data != null)")
            .Line("savePlayer(event.getEntity(), data);")
            .CloseBlock();
        if (hasServer)
        {
            body.OpenBlock("if (event.getEntity().getServer() != null)")
                .Line("saveServer(event.getEntity().getServer());")
                .CloseBlock();
        }

        body.CloseBlock()
            .Line()
            .OpenBlock($"private static void onPlayerLeave({playerEventName}.PlayerLoggedOutEvent event)")
            .Line("PlayerData data = PLAYERS.remove(event.getEntity().getUUID());")
            .OpenBlock("if (data != null)")
            .Line("savePlayer(event.getEntity(), data);")
            .CloseBlock();
        foreach ((_, string name) in playerSessions)
        {
            body.Line($"{name}.remove(event.getEntity().getUUID());");
        }

        body.CloseBlock()
            .Line();
        if (hasServer)
        {
            string stoppingName = serverStopping[(serverStopping.LastIndexOf('.') + 1)..];
            string fmlName = fmlPaths[(fmlPaths.LastIndexOf('.') + 1)..];
            string registriesOf = Modern ? ", server.registryAccess()" : string.Empty;
            body.Line("private static ServerData SERVER = new ServerData();")
                .Line()
                .OpenBlock($"private static void onServerStopping({stoppingName} event)")
                .Line("saveServer(event.getServer());")
                .CloseBlock()
                .Line()
                .OpenBlock("private static Path serverFile()")
                .Line($"return {fmlName}.CONFIGDIR.get().resolve(KEY + \"_server.dat\");")
                .CloseBlock()
                .Line()
                .OpenBlock("private static void loadServer(MinecraftServer server)")
                .OpenBlock("try (InputStream input = Files.newInputStream(serverFile()))")
                .Line($"SERVER = ServerData.load(NbtIo.readCompressed(input{(Modern ? ", NbtAccounter.unlimitedHeap()" : string.Empty)}){registriesOf});")
                .CloseBlock()
                .OpenBlock("catch (IOException exception)")
                .Line("SERVER = new ServerData();")
                .CloseBlock()
                .CloseBlock()
                .Line()
                .OpenBlock("private static void saveServer(MinecraftServer server)")
                .OpenBlock("try (OutputStream output = Files.newOutputStream(serverFile()))")
                .Line($"NbtIo.writeCompressed(SERVER.save(new CompoundTag(){registriesOf}), output);")
                .CloseBlock()
                .OpenBlock("catch (IOException exception)")
                .Line("// The values stay in memory; they are written again at the next save.")
                .CloseBlock()
                .CloseBlock()
                .Line();
        }

        if (keyed.Count > 0)
        {
            WriteKeys(body);
        }

        string factory(string type) => Modern
            ? $"new SavedData.Factory<>({type}::new, {type}::load, null)"
            : $"{type}::load, {type}::new";
        body.OpenBlock("public static MapData map(Level level)")
            .Line("return level.getServer() == null ? CLIENT_MAP")
            .Line($"    : level.getServer().overworld().getDataStorage().computeIfAbsent({factory("MapData")}, KEY);")
            .CloseBlock()
            .Line()
            .OpenBlock("public static WorldData world(Level level)")
            .Line($"return level instanceof ServerLevel server ? server.getDataStorage().computeIfAbsent({factory("WorldData")}, KEY) : CLIENT_WORLD;")
            .CloseBlock()
            .Line()
            .OpenBlock("public static PlayerData player(Player player)")
            .Line("return PLAYERS.computeIfAbsent(player.getUUID(), id -> PlayerData.load(player.getPersistentData().getCompound(KEY)"
                  + (Modern ? ", player.level().registryAccess()" : string.Empty) + "));")
            .CloseBlock()
            .Line()
            .OpenBlock("private static void savePlayer(Player player, PlayerData data)")
            .Line("player.getPersistentData().put(KEY, data.save(new CompoundTag()" + (Modern ? ", player.level().registryAccess()" : string.Empty) + "));")
            .CloseBlock();

        // Accessors used by the graphs.
        foreach ((IRGlobalVariable global, string name) in globals)
        {
            string type = _names.JavaType(global.Type);
            string accessor = JavaProgramNames.Accessor(name);
            body.Line();
            string fallback = Default(global);
            switch (global.Scope)
            {
                case GlobalScope.GlobalServer:
                    body.Line($"public static {type} get{accessor}() {{ return SERVER.{name}; }}")
                        .Line()
                        .Line($"public static void set{accessor}({type} value) {{ SERVER.{name} = value; }}");
                    break;
                case GlobalScope.PlayerAccount:
                    body.Line($"public static {type} get{accessor}(Player player) {{ return player == null ? {fallback} : SERVER.{name}.computeIfAbsent(player.getUUID().toString(), key -> {fallback}); }}")
                        .Line()
                        .OpenBlock($"public static void set{accessor}(Player player, {type} value)")
                        .OpenBlock("if (player != null)")
                        .Line($"SERVER.{name}.put(player.getUUID().toString(), value);")
                        .CloseBlock()
                        .CloseBlock();
                    break;
                case GlobalScope.PlayerSession:
                    body.Line($"public static {type} get{accessor}(Player player) {{ return player == null ? {fallback} : {name}.computeIfAbsent(player.getUUID(), key -> {fallback}); }}")
                        .Line()
                        .OpenBlock($"public static void set{accessor}(Player player, {type} value)")
                        .OpenBlock("if (player != null)")
                        .Line($"{name}.put(player.getUUID(), value);")
                        .CloseBlock()
                        .CloseBlock();
                    break;
                case var scope when Storage(scope) == GlobalStorage.Keyed:
                    string keyType = KeyType(scope);
                    string method = KeyMethod(scope);
                    body.Line($"public static {type} get{accessor}(Level level, {keyType} key) {{ return map(level).{name}.computeIfAbsent({method}(level, key), k -> {fallback}); }}")
                        .Line()
                        .Line($"public static void set{accessor}(Level level, {keyType} key, {type} value) {{ map(level).{name}.put({method}(level, key), value); }}");
                    break;
                case GlobalScope.Session:
                    body.Line($"public static {type} get{accessor}() {{ return {name}; }}")
                        .Line()
                        .Line($"public static void set{accessor}({type} value) {{ {name} = value; }}");
                    break;
                case GlobalScope.Map or GlobalScope.World or GlobalScope.WorldDimension:
                    string holder = global.Scope == GlobalScope.Map ? "map" : "world";
                    body.Line($"public static {type} get{accessor}(Level level) {{ return {holder}(level).{name}; }}")
                        .Line()
                        .OpenBlock($"public static void set{accessor}(Level level, {type} value)")
                        .Line($"var data = {holder}(level);")
                        .Line($"data.{name} = value;")
                        .Line("data.setDirty();")
                        .CloseBlock();
                    break;
                default:
                    body.Line($"public static {type} get{accessor}(Player player) {{ return player == null ? {Default(global)} : player(player).{name}; }}")
                        .Line()
                        .OpenBlock($"public static void set{accessor}(Player player, {type} value)")
                        .OpenBlock("if (player != null)")
                        .Line($"player(player).{name} = value;")
                        .CloseBlock()
                        .CloseBlock();
                    break;
            }
        }

        var w = new JavaSourceWriter();
        w.Line($"package {_names.Package};").Line();
        foreach (string import in _imports)
        {
            w.Line($"import {import};");
        }

        w.Line()
            .Line("/** Global variables of the mod (Global tab of Mine Engine), kept according to their scope. Generated by Mine Engine. */")
            .Line($"public final class {JavaProgramNames.GlobalsClass} {{");
        foreach (string line in body.ToString().TrimEnd().Split('\n'))
        {
            w.Line(line.TrimEnd('\r'));
        }

        w.Line()
            .Line($"    private {JavaProgramNames.GlobalsClass}() {{")
            .Line("    }")
            .Line("}");
        return w.ToString();
    }

    // ----- Saved data (map, world) and players -----

    private void WriteSavedData(JavaSourceWriter w, string className, string comment, IReadOnlyList<(IRGlobalVariable Global, string Name)> globals,
        bool savedData = true)
    {
        string registries = Modern ? ", HolderLookup.Provider registries" : string.Empty;
        w.Line($"/** {comment} */")
            .OpenBlock(savedData ? $"public static final class {className} extends SavedData" : $"public static final class {className}");
        foreach ((IRGlobalVariable global, string name) in globals)
        {
            w.Line($"public {_names.JavaType(global.Type)} {name} = {Default(global)};");
        }

        w.Line()
            .OpenBlock($"public static {className} load(CompoundTag tag{registries})")
            .Line($"{className} data = new {className}();");
        foreach ((IRGlobalVariable global, string name) in globals.Where(g => GlobalVariableAsset.CanBeSaved(g.Global.Type)))
        {
            WriteRead(w, global.Type, "tag", JavaNames.StringLiteral(name), $"data.{name}");
        }

        w.Line("return data;")
            .CloseBlock()
            .Line();
        if (savedData)
        {
            w.Line("@Override");
        }

        w.OpenBlock($"public CompoundTag save(CompoundTag tag{registries})");
        foreach ((IRGlobalVariable global, string name) in globals.Where(g => GlobalVariableAsset.CanBeSaved(g.Global.Type)))
        {
            WriteWrite(w, global.Type, "tag", JavaNames.StringLiteral(name), name);
        }

        w.Line("return tag;")
            .CloseBlock();
        if (!savedData)
        {
            w.CloseBlock().Line();
            return;
        }

        w.Line()
            .Line("/** Collections change without a call to set: the data is written at every save of the world. */")
            .Line("@Override")
            .OpenBlock("public boolean isDirty()")
            .Line("return true;")
            .CloseBlock()
            .CloseBlock()
            .Line();
    }

    private void WritePlayerData(JavaSourceWriter w, IReadOnlyList<(IRGlobalVariable Global, string Name)> globals)
    {
        string registries = Modern ? ", HolderLookup.Provider registries" : string.Empty;
        w.Line($"/** {Tr.T("Players: kept with the player; the lifetime variables go back to their default value at death.")} */")
            .OpenBlock("public static final class PlayerData");
        foreach ((IRGlobalVariable global, string name) in globals)
        {
            w.Line($"public {_names.JavaType(global.Type)} {name} = {Default(global)};");
        }

        w.Line()
            .OpenBlock("public void resetLifetime()");
        foreach ((IRGlobalVariable global, string name) in globals.Where(g => g.Global.Scope == GlobalScope.PlayerLifetime))
        {
            w.Line($"this.{name} = {Default(global)};");
        }

        w.CloseBlock()
            .Line()
            .OpenBlock($"public static PlayerData load(CompoundTag tag{registries})")
            .Line("PlayerData data = new PlayerData();");
        foreach ((IRGlobalVariable global, string name) in globals.Where(g => GlobalVariableAsset.CanBeSaved(g.Global.Type)))
        {
            WriteRead(w, global.Type, "tag", JavaNames.StringLiteral(name), $"data.{name}");
        }

        w.Line("return data;")
            .CloseBlock()
            .Line()
            .OpenBlock($"public CompoundTag save(CompoundTag tag{registries})");
        foreach ((IRGlobalVariable global, string name) in globals.Where(g => GlobalVariableAsset.CanBeSaved(g.Global.Type)))
        {
            WriteWrite(w, global.Type, "tag", JavaNames.StringLiteral(name), "this." + name);
        }

        w.Line("return tag;")
            .CloseBlock()
            .CloseBlock()
            .Line();
    }

    /// <summary>Methods that compute the key of the keyed scopes ("uuid|dimension", the name of the team...).</summary>
    private void WriteKeys(JavaSourceWriter w)
    {
        _imports.UnionWith([
            "net.minecraft.core.BlockPos", "net.minecraft.world.entity.Entity", "net.minecraft.world.entity.EntityType", "net.minecraft.world.entity.OwnableEntity",
            "net.minecraft.core.registries.Registries", "net.minecraft.world.level.levelgen.structure.Structure",
            "net.minecraft.world.level.levelgen.structure.StructureStart",
        ]);
        w.Line("/** Name kept in the persistent data of a player (its guild) or of an entity (its group), set by the nodes. */")
            .Line("public static final String GUILD = \"mineengine_guild\";")
            .Line()
            .Line("public static final String GROUP = \"mineengine_group\";")
            .Line()
            .OpenBlock("private static String dimensionOf(Level level)")
            .Line("return level.dimension().location().toString();")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyPlayerDimension(Level level, Player player)")
            .Line("return player == null ? \"\" : player.getUUID() + \"|\" + dimensionOf(player.level());")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyGuild(Level level, Player player)")
            .OpenBlock("if (player == null)")
            .Line("return \"\";")
            .CloseBlock()
            .Line("String guild = player.getPersistentData().getString(GUILD);")
            .Line("return guild.isEmpty() ? \"player:\" + player.getUUID() : guild;")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyTeam(Level level, Player player)")
            .OpenBlock("if (player == null)")
            .Line("return \"\";")
            .CloseBlock()
            .Line("var team = player.getTeam();")
            .Line("return team == null ? \"player:\" + player.getUUID() : team.getName();")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyEntityType(Level level, Entity entity)")
            .Line("return entity == null ? \"\" : EntityType.getKey(entity.getType()).toString();")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyOwner(Level level, Entity entity)")
            .OpenBlock("if (entity == null)")
            .Line("return \"\";")
            .CloseBlock()
            .Line("return entity instanceof OwnableEntity ownable && ownable.getOwnerUUID() != null ? ownable.getOwnerUUID().toString() : entity.getUUID().toString();")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyGroup(Level level, Entity entity)")
            .OpenBlock("if (entity == null)")
            .Line("return \"\";")
            .CloseBlock()
            .Line("String group = entity.getPersistentData().getString(GROUP);")
            .Line("return group.isEmpty() ? \"entity:\" + entity.getUUID() : group;")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyEntityDimension(Level level, Entity entity)")
            .Line("return entity == null ? \"\" : entity.getUUID() + \"|\" + dimensionOf(entity.level());")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyChunk(Level level, BlockPos position)")
            .Line("return dimensionOf(level) + \"|\" + (position.getX() >> 4) + \",\" + (position.getZ() >> 4);")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyBiome(Level level, BlockPos position)")
            .Line("return level.getBiome(position).unwrapKey().map(key -> key.location().toString()).orElse(\"unknown\");")
            .CloseBlock()
            .Line()
            .OpenBlock("private static String keyStructure(Level level, BlockPos position)")
            .OpenBlock("if (level instanceof ServerLevel server)")
            .Line("var registry = server.registryAccess().registryOrThrow(Registries.STRUCTURE);")
            .OpenBlock("for (Structure structure : server.structureManager().getAllStructuresAt(position).keySet())")
            .Line("StructureStart start = server.structureManager().getStructureAt(position, structure);")
            .OpenBlock("if (start.isValid())")
            .Line("return registry.getKey(structure) + \"|\" + start.getChunkPos().x + \",\" + start.getChunkPos().z;")
            .CloseBlock()
            .CloseBlock()
            .CloseBlock()
            .Line("return \"none\";")
            .CloseBlock()
            .Line();
    }

    // ----- Default values -----

    private string Default(IRGlobalVariable global)
    {
        string text = global.DefaultValue;
        return global.Type.Kind switch
        {
            PinType.Number when GraphValidator.TryParseNumber(text, out int number) => number.ToString(CultureInfo.InvariantCulture),
            PinType.Decimal when GraphValidator.TryParseDecimal(text, out double number) => BehaviorJava.DecimalLiteral(number),
            PinType.Boolean when bool.TryParse(text, out bool flag) => flag ? "true" : "false",
            PinType.Text => JavaNames.StringLiteral(text),
            PinType.Direction when GraphValidator.Directions.Contains(text.ToLowerInvariant()) => "Direction." + text.ToUpperInvariant(),
            PinType.Char or PinType.Long or PinType.Float or PinType.Byte or PinType.Identifier or PinType.Fluid
                or PinType.EquipmentSlot or PinType.Hand or PinType.Rarity when text.Length > 0
                => JavaLiterals.Literal(global.Type.Kind, text, _api, import => _imports.Add(import))!,
            _ => Default(global.Type),
        };
    }

    private string Default(DataType type)
    {
        switch (type.Kind)
        {
            case PinType.Boolean:
                return "false";
            case PinType.Number:
                return "0";
            case PinType.Decimal:
                return "0.0";
            case PinType.Text:
                return "\"\"";
            case PinType.Char:
                return "' '";
            case PinType.Long:
                return "0L";
            case PinType.Float:
                return "0.0f";
            case PinType.Byte:
                return "((byte) 0)";
            case PinType.Uuid:
                return "new UUID(0L, 0L)";
            case PinType.TextComponent:
                return "Component.empty()";
            case PinType.Identifier or PinType.Fluid or PinType.EquipmentSlot or PinType.Hand or PinType.Rarity:
                return JavaLiterals.Literal(type.Kind, string.Empty, _api, import => _imports.Add(import))!;
            case PinType.BoundingBox:
                return "new AABB(0, 0, 0, 0, 0, 0)";
            case PinType.NbtTag:
                return "new CompoundTag()";
            case PinType.Direction:
                return "Direction.NORTH";
            case PinType.Position:
                return "BlockPos.ZERO";
            case PinType.ItemStack:
                return "ItemStack.EMPTY";
            case PinType.BlockState:
                _imports.Add("net.minecraft.world.level.block.Blocks");
                return "Blocks.AIR.defaultBlockState()";
            case PinType.Vector2i:
                return "new Vector2i()";
            case PinType.Vector3i:
                return "Vec3i.ZERO";
            case PinType.Vector2f:
                return "Vec2.ZERO";
            case PinType.Vector3f:
                return "Vec3.ZERO";
            case PinType.Array or PinType.List:
                return "new ArrayList<>()";
            case PinType.Set:
                return "new HashSet<>()";
            case PinType.Map:
                return "new HashMap<>()";
            case PinType.Enum when type.Definition is { } id && _names.Find(id) is IREnum { Constants.Count: > 0 } enumeration:
                return _names.Constant(id, enumeration.Constants[0].Id);
            default:
                return "null";
        }
    }

    // ----- NBT -----

    /// <summary>Writes <paramref name="value"/> in the compound <paramref name="tag"/> under <paramref name="key"/> (a Java string expression).</summary>
    private void WriteWrite(JavaSourceWriter w, DataType type, string tag, string key, string value)
    {
        switch (type.Kind)
        {
            case PinType.Boolean:
                w.Line($"{tag}.putBoolean({key}, {value});");
                break;
            case PinType.Number:
                w.Line($"{tag}.putInt({key}, {value});");
                break;
            case PinType.Decimal:
                w.Line($"{tag}.putDouble({key}, {value});");
                break;
            case PinType.Char:
                w.Line($"{tag}.putInt({key}, {value});");
                break;
            case PinType.Long:
                w.Line($"{tag}.putLong({key}, {value});");
                break;
            case PinType.Float:
                w.Line($"{tag}.putFloat({key}, {value});");
                break;
            case PinType.Byte:
                w.Line($"{tag}.putByte({key}, {value});");
                break;
            case PinType.Uuid:
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, {value}.toString()); }}");
                break;
            case PinType.TextComponent:
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, {value}.getString()); }}");
                break;
            case PinType.Identifier:
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, {value}.toString()); }}");
                break;
            case PinType.Fluid:
                _imports.Add("net.minecraft.core.registries.BuiltInRegistries");
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, BuiltInRegistries.FLUID.getKey({value}).toString()); }}");
                break;
            case PinType.EquipmentSlot or PinType.Hand or PinType.Rarity:
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, {value}.name()); }}");
                break;
            case PinType.BoundingBox:
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, {value}.minX + \",\" + {value}.minY + \",\" + {value}.minZ + \",\" + {value}.maxX + \",\" + {value}.maxY + \",\" + {value}.maxZ); }}");
                break;
            case PinType.NbtTag:
                w.Line($"if ({value} != null) {{ {tag}.put({key}, {value}.copy()); }}");
                break;
            case PinType.Text:
                w.Line($"{tag}.putString({key}, {value} == null ? \"\" : {value});");
                break;
            case PinType.Direction:
                w.Line($"{tag}.putString({key}, {value} == null ? \"north\" : {value}.getName());");
                break;
            case PinType.Enum:
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, {value}.name()); }}");
                break;
            case PinType.Position or PinType.Vector3i:
                w.Line($"if ({value} != null) {{ {tag}.putIntArray({key}, new int[] {{ {value}.getX(), {value}.getY(), {value}.getZ() }}); }}");
                break;
            case PinType.Vector2i:
                w.Line($"if ({value} != null) {{ {tag}.putIntArray({key}, new int[] {{ {value}.x, {value}.y }}); }}");
                break;
            case PinType.Vector2f:
                w.Line($"if ({value} != null) {{ {tag}.putFloat({key} + \"_x\", {value}.x); {tag}.putFloat({key} + \"_y\", {value}.y); }}");
                break;
            case PinType.Vector3f:
                w.Line($"if ({value} != null) {{ {tag}.putDouble({key} + \"_x\", {value}.x); {tag}.putDouble({key} + \"_y\", {value}.y); {tag}.putDouble({key} + \"_z\", {value}.z); }}");
                break;
            case PinType.Item or PinType.Block or PinType.Sound:
                _imports.Add("net.minecraft.core.registries.BuiltInRegistries");
                string registry = Registry(type.Kind);
                w.Line($"if ({value} != null) {{ {tag}.putString({key}, BuiltInRegistries.{registry}.getKey({value}).toString()); }}");
                break;
            case PinType.ItemStack:
                w.Line(Modern
                    ? $"if ({value} != null && !{value}.isEmpty()) {{ {tag}.put({key}, {value}.save(registries)); }}"
                    : $"if ({value} != null && !{value}.isEmpty()) {{ {tag}.put({key}, {value}.save(new CompoundTag())); }}");
                break;
            case PinType.BlockState:
                _imports.Add("net.minecraft.nbt.NbtUtils");
                w.Line($"if ({value} != null) {{ {tag}.put({key}, NbtUtils.writeBlockState({value})); }}");
                break;
            case PinType.Array or PinType.List or PinType.Set:
            {
                int n = ++_counter;
                w.OpenBlock($"if ({value} != null)")
                    .Line($"ListTag list{n} = new ListTag();")
                    .OpenBlock($"for ({_names.BoxedType(type.Element!)} item{n} : {value})")
                    .Line($"CompoundTag entry{n} = new CompoundTag();");
                WriteWrite(w, type.Element!, $"entry{n}", "\"v\"", $"item{n}");
                w.Line($"list{n}.add(entry{n});")
                    .CloseBlock()
                    .Line($"{tag}.put({key}, list{n});")
                    .CloseBlock();
                break;
            }

            case PinType.Map:
            {
                int n = ++_counter;
                w.OpenBlock($"if ({value} != null)")
                    .Line($"ListTag list{n} = new ListTag();")
                    .OpenBlock($"for (Map.Entry<{_names.BoxedType(type.Key!)}, {_names.BoxedType(type.Element!)}> item{n} : {value}.entrySet())")
                    .Line($"CompoundTag entry{n} = new CompoundTag();");
                WriteWrite(w, type.Key!, $"entry{n}", "\"k\"", $"item{n}.getKey()");
                WriteWrite(w, type.Element!, $"entry{n}", "\"v\"", $"item{n}.getValue()");
                w.Line($"list{n}.add(entry{n});")
                    .CloseBlock()
                    .Line($"{tag}.put({key}, list{n});")
                    .CloseBlock();
                break;
            }

            default:
                // Entity, player, object of the project: kept in memory only.
                break;
        }
    }

    /// <summary>Reads the value under <paramref name="key"/> into <paramref name="target"/> (kept when the key is missing).</summary>
    private void WriteRead(JavaSourceWriter w, DataType type, string tag, string key, string target)
    {
        if (type.IsCollection)
        {
            w.OpenBlock($"if ({tag}.contains({key}))");
            w.Line($"{target} = {ReadExpression(w, type, tag, key)};");
            w.CloseBlock();
            return;
        }

        if (ReadExpression(w, type, tag, key) is { } expression && expression != "null")
        {
            string condition = type.Kind is PinType.Vector2f or PinType.Vector3f ? $"{tag}.contains({key} + \"_x\")" : $"{tag}.contains({key})";
            w.Line($"if ({condition}) {{ {target} = {expression}; }}");
        }
    }

    /// <summary>Java expression of the value under <paramref name="key"/>; a collection first writes the statements that fill a local.</summary>
    private string ReadExpression(JavaSourceWriter w, DataType type, string tag, string key)
    {
        switch (type.Kind)
        {
            case PinType.Boolean:
                return $"{tag}.getBoolean({key})";
            case PinType.Number:
                return $"{tag}.getInt({key})";
            case PinType.Decimal:
                return $"{tag}.getDouble({key})";
            case PinType.Char:
                return $"((char) {tag}.getInt({key}))";
            case PinType.Long:
                return $"{tag}.getLong({key})";
            case PinType.Float:
                return $"{tag}.getFloat({key})";
            case PinType.Byte:
                return $"{tag}.getByte({key})";
            case PinType.Uuid:
                return $"globalUuid({tag}.getString({key}))";
            case PinType.TextComponent:
                return $"Component.literal({tag}.getString({key}))";
            case PinType.Identifier:
                _imports.Add("net.minecraft.resources.ResourceLocation");
                return $"java.util.Objects.requireNonNullElse(ResourceLocation.tryParse({tag}.getString({key})), {Default(type)})";
            case PinType.Fluid:
                _imports.Add("net.minecraft.core.registries.BuiltInRegistries");
                _imports.Add("net.minecraft.resources.ResourceLocation");
                return $"BuiltInRegistries.FLUID.get({_api.ParseResourceLocation($"{tag}.getString({key})")})";
            case PinType.EquipmentSlot or PinType.Hand or PinType.Rarity:
                string enumName = _names.JavaType(type);
                return $"java.util.Arrays.stream({enumName}.values()).filter(v -> v.name().equals({tag}.getString({key}))).findFirst().orElse({Default(type)})";
            case PinType.BoundingBox:
                return $"globalBox({tag}.getString({key}))";
            case PinType.NbtTag:
                return $"{tag}.getCompound({key}).copy()";
            case PinType.Text:
                return $"{tag}.getString({key})";
            case PinType.Direction:
                return $"java.util.Objects.requireNonNullElse(Direction.byName({tag}.getString({key})), Direction.NORTH)";
            case PinType.Enum when type.Definition is { } id:
                string enumType = _names.TypeName(id);
                return $"java.util.Arrays.stream({enumType}.values()).filter(v -> v.name().equals({tag}.getString({key}))).findFirst().orElse({Default(type)})";
            case PinType.Position:
                return $"new BlockPos({tag}.getIntArray({key})[0], {tag}.getIntArray({key})[1], {tag}.getIntArray({key})[2])";
            case PinType.Vector3i:
                return $"new Vec3i({tag}.getIntArray({key})[0], {tag}.getIntArray({key})[1], {tag}.getIntArray({key})[2])";
            case PinType.Vector2i:
                return $"new Vector2i({tag}.getIntArray({key})[0], {tag}.getIntArray({key})[1])";
            case PinType.Vector2f:
                return $"new Vec2({tag}.getFloat({key} + \"_x\"), {tag}.getFloat({key} + \"_y\"))";
            case PinType.Vector3f:
                return $"new Vec3({tag}.getDouble({key} + \"_x\"), {tag}.getDouble({key} + \"_y\"), {tag}.getDouble({key} + \"_z\"))";
            case PinType.Item or PinType.Block or PinType.Sound:
                _imports.Add("net.minecraft.core.registries.BuiltInRegistries");
                _imports.Add("net.minecraft.resources.ResourceLocation");
                return $"BuiltInRegistries.{Registry(type.Kind)}.get({_api.ParseResourceLocation($"{tag}.getString({key})")})";
            case PinType.ItemStack:
                return Modern
                    ? $"ItemStack.parseOptional(registries, {tag}.getCompound({key}))"
                    : $"ItemStack.of({tag}.getCompound({key}))";
            case PinType.BlockState:
                _imports.Add("net.minecraft.nbt.NbtUtils");
                _imports.Add("net.minecraft.core.registries.BuiltInRegistries");
                return $"NbtUtils.readBlockState(BuiltInRegistries.BLOCK.asLookup(), {tag}.getCompound({key}))";
            case PinType.Array or PinType.List or PinType.Set:
            {
                int n = ++_counter;
                string collection = type.Kind == PinType.Set ? "HashSet" : "ArrayList";
                _imports.Add(type.Kind == PinType.Set ? "java.util.HashSet" : "java.util.ArrayList");
                w.Line($"{_names.JavaType(type)} read{n} = new {collection}<>();")
                    .Line($"ListTag list{n} = {tag}.getList({key}, Tag.TAG_COMPOUND);")
                    .OpenBlock($"for (int i{n} = 0; i{n} < list{n}.size(); i{n}++)")
                    .Line($"CompoundTag entry{n} = list{n}.getCompound(i{n});");
                string element = ReadExpression(w, type.Element!, $"entry{n}", "\"v\"");
                w.Line($"read{n}.add({element});")
                    .CloseBlock();
                return $"read{n}";
            }

            case PinType.Map:
            {
                int n = ++_counter;
                w.Line($"{_names.JavaType(type)} read{n} = new HashMap<>();")
                    .Line($"ListTag list{n} = {tag}.getList({key}, Tag.TAG_COMPOUND);")
                    .OpenBlock($"for (int i{n} = 0; i{n} < list{n}.size(); i{n}++)")
                    .Line($"CompoundTag entry{n} = list{n}.getCompound(i{n});");
                string keyValue = ReadExpression(w, type.Key!, $"entry{n}", "\"k\"");
                string value = ReadExpression(w, type.Element!, $"entry{n}", "\"v\"");
                w.Line($"read{n}.put({keyValue}, {value});")
                    .CloseBlock();
                return $"read{n}";
            }

            default:
                return "null";
        }
    }

    private static string Registry(PinType kind) => kind switch
    {
        PinType.Item => "ITEM",
        PinType.Block => "BLOCK",
        _ => "SOUND_EVENT",
    };
}
