using System.Globalization;
using MineEngine.Assets.Graphs;
using MineEngine.Assets;
using MineEngine.IR.Model;

namespace MineEngine.Minecraft.Java;

/// <summary>
/// Traduction Java des nœuds d'un graphe : expressions, instructions et petites méthodes
/// utilitaires (donner un item, faire apparaître un item...) ajoutées à la classe générée.
/// Le code est le même pour Forge et NeoForge ; seuls les événements et le bus diffèrent
/// (voir <see cref="ModJavaEmitter"/>). Les appels, objets, champs et variables du
/// programme sont nommés par <see cref="JavaProgramNames"/>.
/// </summary>
public sealed class BehaviorJava
{
    private readonly JavaGameApi _api;
    private readonly JavaProgramNames _names;
    private readonly SortedSet<string> _imports = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _helpers = new(StringComparer.Ordinal);

    public BehaviorJava(JavaGameApi api, JavaProgramNames names)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _names = names ?? throw new ArgumentNullException(nameof(names));
    }

    /// <summary>Fonction dont on écrit le corps (pour le type de ses valeurs renvoyées), ou null.</summary>
    public IRFunction? CurrentFunction { get; set; }

    /// <summary>Imports nécessaires au code traduit jusqu'ici.</summary>
    public IReadOnlyCollection<string> Imports => _imports;

    /// <summary>Lignes d'en-tête d'un gestionnaire : filtrage client/serveur, puis variables player, level et position.</summary>
    public IReadOnlyList<string> Prologue(GraphNodeKind eventKind)
    {
        Use("net.minecraft.core.BlockPos", "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level");
        switch (eventKind)
        {
            case GraphNodeKind.OnRightClickBlock or GraphNodeKind.OnUseItem:
                Use("net.minecraft.world.InteractionHand");
                return
                [
                    // L'événement est appelé pour chaque main, et aussi côté client : on ne garde que le serveur et la main principale.
                    "if (event.getLevel().isClientSide() || event.getHand() != InteractionHand.MAIN_HAND) {",
                    "    return;",
                    "}",
                    "Player player = event.getEntity();",
                    "Level level = event.getLevel();",
                    eventKind == GraphNodeKind.OnRightClickBlock ? "BlockPos position = event.getPos();" : "BlockPos position = player.blockPosition();",
                ];
            case GraphNodeKind.OnPlayerJoin or GraphNodeKind.OnBlockBroken:
                return
                [
                    eventKind == GraphNodeKind.OnPlayerJoin ? "Player player = event.getEntity();" : "Player player = event.getPlayer();",
                    "Level level = player.level();",
                    "if (level.isClientSide()) {",
                    "    return;",
                    "}",
                    eventKind == GraphNodeKind.OnPlayerJoin ? "BlockPos position = player.blockPosition();" : "BlockPos position = event.getPos();",
                ];
            case GraphNodeKind.OnPlayerLeave or GraphNodeKind.OnPlayerTick:
                return
                [
                    "Player player = event.getEntity();",
                    "Level level = player.level();",
                    "if (level.isClientSide()) {",
                    "    return;",
                    "}",
                    "BlockPos position = player.blockPosition();",
                ];
            case GraphNodeKind.OnPlayerHurt:
                return
                [
                    "if (!(event.getEntity() instanceof Player player)) {",
                    "    return;",
                    "}",
                    "Level level = player.level();",
                    "if (level.isClientSide()) {",
                    "    return;",
                    "}",
                    "BlockPos position = player.blockPosition();",
                    "int damage = Math.round(event.getAmount());",
                ];
            case GraphNodeKind.OnEntitySpawn or GraphNodeKind.OnEntityDeath:
                Use("net.minecraft.world.entity.EntityType");
                return
                [
                    eventKind == GraphNodeKind.OnEntitySpawn ? "Level level = event.getLevel();" : "Level level = event.getEntity().level();",
                    "if (level.isClientSide()) {",
                    "    return;",
                    "}",
                    "BlockPos position = event.getEntity().blockPosition();",
                    "String entity = EntityType.getKey(event.getEntity().getType()).toString();",
                ];
            default:
                throw new NotSupportedException($"Événement {eventKind} non traduit en Java.");
        }
    }

    /// <summary>Lignes Java d'une instruction autre qu'une condition (un bloc est indenté de quatre espaces).</summary>
    public IReadOnlyList<string> Statement(IRStatement statement) => statement switch
    {
        IRActionStatement action => [Action(action)],
        IRCallStatement call => CallStatement(call),
        IRNewStatement created => [$"{created.Result} = {New(created.TypeId, created.Arguments)};"],
        IRSetVariableStatement set => [$"{_names.Variable(set.VariableId)} = {Expression(set.Value)};"],
        IRSetFieldStatement set => [$"{Owner(set.Target)}.{_names.Variable(set.FieldId)} = {Expression(set.Value)};"],
        IRSetGlobalStatement set => [GlobalAccess(set.GlobalId, set.Key, "set") + (HasScopeArgument(set.GlobalId) ? ", " : string.Empty) + Expression(set.Value) + ");"],
        IRReturnStatement ret => [Return(ret.Values)],
        IRSuperStatement super => [$"super({Arguments(super.Arguments, withLevel: true)});"],
        _ => throw new NotSupportedException($"Instruction {statement.GetType().Name} non traduite en Java."),
    };

    /// <summary>Instruction "return" (valeurs par défaut si <paramref name="values"/> est vide pour une fonction).</summary>
    public string Return(IReadOnlyList<IRExpression> values)
    {
        if (CurrentFunction is not { Returns.Count: > 0 } function)
        {
            return "return;";
        }

        IReadOnlyList<IRExpression> actual = values.Count == function.Returns.Count
            ? values
            : [.. function.Returns.Select(r => (IRExpression)new IRDefaultValue(r.Type))];
        return function.Returns.Count == 1
            ? $"return {Expression(actual[0])};"
            : $"return new {_names.ResultRecordType(function)}({string.Join(", ", actual.Select(Expression))});";
    }

    private string Imported(string import, string code)
    {
        Use(import);
        return code;
    }

    /// <summary>"0.5", "(-2.0)": a Java double literal.</summary>
    public static string DecimalLiteral(double value)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!text.Contains('.', StringComparison.Ordinal) && !text.Contains('E', StringComparison.Ordinal))
        {
            text += ".0";
        }

        return value < 0 ? $"({text})" : text;
    }

    /// <summary>"MineEngineGlobals.getScore(level" (without the closing parenthesis): the arguments depend on the scope.</summary>
    private string GlobalAccess(Guid id, IRExpression? key, string verb)
    {
        if (_names.FindGlobal(id) is not ({ } global, { } name))
        {
            throw new NotSupportedException(Tr.T("Global variable not found."));
        }

        Use($"{_names.Package}.{JavaProgramNames.GlobalsClass}");
        string value = key is null ? "null" : Expression(key);
        string argument = GlobalsJava.Storage(global.Scope) switch
        {
            GlobalStorage.Static => string.Empty,
            GlobalStorage.Level => JavaProgramNames.LevelParameter,
            GlobalStorage.Player => value,
            _ => $"{JavaProgramNames.LevelParameter}, {value}",
        };
        return $"{JavaProgramNames.GlobalsClass}.{verb}{JavaProgramNames.Accessor(name)}({argument}";
    }

    private bool HasScopeArgument(Guid id) => _names.FindGlobal(id) is ({ } global, _) && GlobalsJava.Storage(global.Scope) != GlobalStorage.Static;

    /// <summary>Valeur Java par défaut d'un type (0, false, "", BlockPos.ZERO, première constante, null).</summary>
    public string DefaultValue(DataType type)
    {
        switch (type.Kind)
        {
            case PinType.Decimal:
                return "0.0";
            case PinType.Char:
                return "' '";
            case PinType.Long:
                return "0L";
            case PinType.Float:
                return "0.0f";
            case PinType.Byte:
                return "((byte) 0)";
            case PinType.Uuid:
                Use("java.util.UUID");
                return "new UUID(0L, 0L)";
            case PinType.TextComponent:
                Use("net.minecraft.network.chat.Component");
                return "Component.empty()";
            case PinType.Identifier or PinType.Fluid or PinType.EquipmentSlot or PinType.Hand or PinType.Rarity:
                return JavaLiterals.Literal(type.Kind, string.Empty, _api, import => Use(import))!;
            case PinType.BoundingBox:
                Use("net.minecraft.world.phys.AABB");
                return "new AABB(0, 0, 0, 0, 0, 0)";
            case PinType.NbtTag:
                Use("net.minecraft.nbt.CompoundTag");
                return "new CompoundTag()";
            case PinType.Direction:
                Use("net.minecraft.core.Direction");
                return "Direction.NORTH";
            case PinType.BlockState:
                Use("net.minecraft.world.level.block.Blocks");
                return "Blocks.AIR.defaultBlockState()";
            case PinType.Vector2i:
                Use("org.joml.Vector2i");
                return "new Vector2i()";
            case PinType.Vector3i:
                Use("net.minecraft.core.Vec3i");
                return "Vec3i.ZERO";
            case PinType.Vector2f:
                Use("net.minecraft.world.phys.Vec2");
                return "Vec2.ZERO";
            case PinType.Vector3f:
                Use("net.minecraft.world.phys.Vec3");
                return "Vec3.ZERO";
            case PinType.Array or PinType.List:
                Use("java.util.ArrayList");
                return "new ArrayList<>()";
            case PinType.Set:
                Use("java.util.HashSet");
                return "new HashSet<>()";
            case PinType.Map:
                Use("java.util.HashMap");
                return "new HashMap<>()";
            case PinType.Boolean:
                return "false";
            case PinType.Number:
                return "0";
            case PinType.Text:
                return "\"\"";
            case PinType.Position:
                Use("net.minecraft.core.BlockPos");
                return "BlockPos.ZERO";
            case PinType.ItemStack:
                Use(Stack);
                return "ItemStack.EMPTY";
            case PinType.Enum when type.Definition is { } id && _names.Find(id) is IREnum { Constants.Count: > 0 } type2:
                return _names.Constant(id, type2.Constants[0].Id);
            default:
                return "null";
        }
    }

    /// <summary>Instruction Java d'une action (terminée par ';').</summary>
    public string Action(IRActionStatement action)
    {
        string[] args = [.. action.Arguments.Select(Expression)];
        return action.Action switch
        {
            GraphNodeKind.SpawnItem => Helper("spawnItem", $"spawnItem(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.GiveItem => Helper("giveItem", $"giveItem({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.RemoveItem => Helper("removeItem", $"removeItem({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SendMessage => Helper("sendMessage", $"sendMessage({args[0]}, {args[1]});"),
            GraphNodeKind.PlaySound => Helper("playSound", $"playSound(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetBlock => Helper("setBlock", $"setBlock(level, {args[0]}, {args[1]});"),
            GraphNodeKind.Explosion => Helper("explode", $"explode(level, {args[0]}, {args[1]});"),
            GraphNodeKind.CancelEvent => "event.setCanceled(true);",
            GraphNodeKind.RemoveBlock => $"level.removeBlock({args[0]}, false);",
            GraphNodeKind.ReplaceBlock => Helper("replaceBlock", $"replaceBlock(level, {args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.UpdateBlock => Helper("updateBlock", $"updateBlock(level, {args[0]});"),
            GraphNodeKind.BreakBlock => $"level.destroyBlock({args[0]}, {args[1]});",
            GraphNodeKind.SetBlockNbtNumber => Helper("setBlockNbtNumber", $"setBlockNbtNumber(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SetBlockNbtText => Helper("setBlockNbtText", $"setBlockNbtText(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SetBlockNbtBoolean => Helper("setBlockNbtBoolean", $"setBlockNbtBoolean(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SetBlockProperty => Helper("setBlockProperty", $"setBlockProperty(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.UseBlock => Helper("useBlock", $"useBlock(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetBlockDirection => Helper("setBlockDirection", $"setBlockDirection(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetItemCooldown => $"{args[0]}.getCooldowns().addCooldown({args[1]}, {args[2]});",
            GraphNodeKind.AddEnchantment => Helper("addEnchantment", $"addEnchantment(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.RemoveEnchantment => Helper("removeEnchantment", $"removeEnchantment(level, {args[0]}, {args[1]});"),
            GraphNodeKind.RemoveAllEnchantments => Helper("removeAllEnchantments", $"removeAllEnchantments({args[0]});"),
            GraphNodeKind.SetItemDisplayName => Helper("setItemName", $"setItemName({args[0]}, {args[1]});"),
            GraphNodeKind.SetWorldTime => Helper("setTime", $"setTime(level, {args[0]});"),
            GraphNodeKind.SetWeather => Helper("setWeather", $"setWeather(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SpawnEntity => Helper("spawnEntity", $"spawnEntity(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SpawnParticles => Helper("spawnParticles", $"spawnParticles(level, {args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SetPlayerHealth => $"{args[0]}.setHealth({args[1]});",
            GraphNodeKind.HealPlayer => $"{args[0]}.heal({args[1]});",
            GraphNodeKind.DamagePlayer => $"{args[0]}.hurt({args[0]}.damageSources().generic(), {args[1]});",
            GraphNodeKind.TeleportPlayer => Helper("teleport", $"teleport({args[0]}, {args[1]});"),
            GraphNodeKind.ClearSlot => Helper("clearSlot", $"clearSlot({args[0]}, {args[1]});"),
            GraphNodeKind.SetItemNbtNumber => Helper("setItemNbtNumber", $"setItemNbtNumber({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SetItemNbtText => Helper("setItemNbtText", $"setItemNbtText({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.SetItemAppearance => Helper("setItemAppearance", $"setItemAppearance({args[0]}, {args[1]});"),
            GraphNodeKind.SetBlockAppearance => Helper("setBlockAppearance", $"setBlockAppearance(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetCreatureAppearance => Helper("setCreatureAppearance", $"setCreatureAppearance(level, {args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.RunCommandAsPlayer => Helper("runCommandAsPlayer", $"runCommandAsPlayer({args[0]}, {args[1]});"),
            GraphNodeKind.RunCommandAsServer => Helper("runCommandAsServer", $"runCommandAsServer(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetVelocity => Helper("setVelocity", $"setVelocity({args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.AddVelocity => Helper("addVelocity", $"addVelocity({args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.LaunchForward => Helper("launchForward", $"launchForward({args[0]}, {args[1]});"),
            GraphNodeKind.SetGravity => $"{args[0]}.setNoGravity(!{args[1]});",
            GraphNodeKind.PlaySoundAdvanced => Helper("playSoundAt", $"playSoundAt(level, {args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.PlaySoundToPlayer => Helper("playSoundTo", $"playSoundTo({args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.StopSounds => Helper("stopSounds", $"stopSounds({args[0]}, false);"),
            GraphNodeKind.PlayMusic => Helper("playMusic", $"playMusic({args[0]}, {args[1]});"),
            GraphNodeKind.StopMusic => Helper("stopSounds", $"stopSounds({args[0]}, true);"),
            GraphNodeKind.SetGameRuleBoolean => Helper("setGameRuleBoolean", $"setGameRuleBoolean(level, {args[0]}, {args[1]});"),
            GraphNodeKind.SetGameRuleNumber => Helper("setGameRuleNumber", $"setGameRuleNumber(level, {args[0]}, {args[1]});"),
            GraphNodeKind.GiveEffect => Helper("giveEffect", $"giveEffect({args[0]}, {args[1]}, {args[2]}, {args[3]});"),
            GraphNodeKind.RemoveEffect => Helper("removeEffect", $"removeEffect({args[0]}, {args[1]});"),
            GraphNodeKind.CollectionClear => $"{args[0]}.clear();",
            GraphNodeKind.CollectionRemove => action.Arguments[0].Type.Kind == PinType.Map ? $"{args[0]}.remove({args[1]});" : $"{args[0]}.remove((Object) {args[1]});",
            GraphNodeKind.ListSet => Helper("listSet", $"listSet({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.ListAdd => $"{args[0]}.add({args[1]});",
            GraphNodeKind.ListRemoveAt => Helper("listRemoveAt", $"listRemoveAt({args[0]}, {args[1]});"),
            GraphNodeKind.MapPut => $"{args[0]}.put({args[1]}, {args[2]});",
            GraphNodeKind.ArrayResize => Helper("arrayResize", $"arrayResize({args[0]}, {args[1]}, {DefaultValue(action.Arguments[0].Type.Element ?? DataType.Number)});"),
            GraphNodeKind.SetBlockState => $"level.setBlockAndUpdate({args[0]}, {args[1]});",
            GraphNodeKind.RemoveEntity => Helper("removeEntity", $"removeEntity({args[0]});"),
            GraphNodeKind.TeleportEntity => Helper("teleportEntity", $"teleportEntity({args[0]}, {args[1]});"),
            GraphNodeKind.DamageEntity => Helper("damageEntity", $"damageEntity({args[0]}, {args[1]});"),
            GraphNodeKind.SendRichMessage => $"{args[0]}.sendSystemMessage({args[1]});",
            GraphNodeKind.SwingHand => $"{args[0]}.swing({args[1]}, true);",
            GraphNodeKind.PlaceFluid => $"level.setBlockAndUpdate({args[0]}, {args[1]}.defaultFluidState().createLegacyBlock());",
            GraphNodeKind.DamageWithSource => Helper("damageWithSource", $"damageWithSource({args[0]}, {args[1]}, {args[2]});"),
            GraphNodeKind.ApplyEffect => Helper("applyEffect", $"applyEffect({args[0]}, {args[1]});"),
            GraphNodeKind.NbtPutNumber => $"{args[0]}.putInt({args[1]}, {args[2]});",
            GraphNodeKind.NbtPutText => $"{args[0]}.putString({args[1]}, {args[2]});",
            GraphNodeKind.SetPlayerGuild => $"{args[0]}.getPersistentData().putString(\"mineengine_guild\", {args[1]});",
            GraphNodeKind.SetEntityGroup => Helper("setEntityGroup", $"setEntityGroup({args[0]}, {args[1]});"),
            _ => throw new NotSupportedException($"Action {action.Action} non traduite en Java."),
        };
    }

    public string Expression(IRExpression expression) => expression switch
    {
        IRNumberLiteral number => number.Value < 0
            ? string.Create(CultureInfo.InvariantCulture, $"({number.Value})")
            : number.Value.ToString(CultureInfo.InvariantCulture),
        IRTextLiteral text => JavaNames.StringLiteral(text.Value),
        IRBooleanLiteral flag => flag.Value ? "true" : "false",
        IRDecimalLiteral number => DecimalLiteral(number.Value),
        IRValueLiteral literal => JavaLiterals.Literal(literal.ValueType.Kind, literal.Text, _api, import => Use(import)) ?? "null",
        IRDirectionLiteral direction => Imported("net.minecraft.core.Direction", "Direction." + direction.Name.ToUpperInvariant()),
        IRGlobalValue global => GlobalAccess(global.GlobalId, global.Key, "get") + ")",
        IRContentLiteral content => Content(content),
        IREventValue value => value.Name,
        IRToText toText => $"String.valueOf({Expression(toText.Value)})",
        IRCallExpression call => Call(call),
        IRParameterValue parameter => _names.Variable(parameter.ParameterId),
        IRLocalValue local => local.Name,
        IRVariableValue variable => _names.Variable(variable.VariableId),
        IRFieldValue field => $"{Owner(field.Target)}.{_names.Variable(field.FieldId)}",
        IRSelfValue => "this",
        IREnumValue constant => constant.EnumType.Definition is { } enumId ? _names.Constant(enumId, constant.ConstantId) : "null",
        IRPureCall pure => PureCall(pure),
        IRDefaultValue value => DefaultValue(value.ValueType),
        _ => throw new NotSupportedException($"Expression {expression.GetType().Name} non traduite en Java."),
    };

    /// <summary>Méthodes utilitaires utilisées par le code traduit, prêtes à écrire dans la classe.</summary>
    public IEnumerable<IReadOnlyList<string>> HelperMethods() => _helpers.Select(HelperMethod);

    private string Call(IRCallExpression call)
    {
        string[] a = [.. call.Arguments.Select(Expression)];
        return call.Kind switch
        {
            GraphNodeKind.HasItem => $"{a[0]}.getInventory().countItem({a[1]}) >= {a[2]}",
            GraphNodeKind.HoldsItem => $"{a[0]}.getMainHandItem().is({a[1]})",
            GraphNodeKind.IsSneaking => $"{a[0]}.isShiftKeyDown()",
            GraphNodeKind.Chance => $"level.random.nextInt(100) < {a[0]}",
            GraphNodeKind.PlayerPosition => $"{a[0]}.blockPosition()",
            GraphNodeKind.PlayerName => $"{a[0]}.getName().getString()",
            GraphNodeKind.OffsetPosition => $"{a[0]}.offset({a[1]}, {a[2]}, {a[3]})",
            GraphNodeKind.RandomNumber => Helper("randomBetween", $"randomBetween(level, {a[0]}, {a[1]})"),
            GraphNodeKind.JoinText => $"({a[0]} + {a[1]})",
            GraphNodeKind.GreaterThan => $"({a[0]} > {a[1]})",
            GraphNodeKind.NumbersEqual => $"({a[0]} == {a[1]})",
            GraphNodeKind.Not => $"!({a[0]})",
            GraphNodeKind.EnumEquals => $"({a[0]} == {a[1]})",
            GraphNodeKind.Add => $"({a[0]} + {a[1]})",
            GraphNodeKind.Subtract => $"({a[0]} - {a[1]})",
            GraphNodeKind.Multiply => $"({a[0]} * {a[1]})",
            GraphNodeKind.LessThan => $"({a[0]} < {a[1]})",
            GraphNodeKind.GetBlockAt => $"level.getBlockState({a[0]}).getBlock()",
            GraphNodeKind.GetBlockProperty => Helper("blockProperty", $"blockProperty(level, {a[0]}, {a[1]})"),
            GraphNodeKind.GetBlockDirection => Helper("blockDirection", $"blockDirection(level, {a[0]})"),
            GraphNodeKind.GetBlockOpacity => $"level.getBlockState({a[0]}).getLightBlock(level, {a[0]})",
            GraphNodeKind.GetBlockHardness => $"Math.round(level.getBlockState({a[0]}).getDestroySpeed(level, {a[0]}))",
            GraphNodeKind.GetLightLevel => $"level.getMaxLocalRawBrightness({a[0]})",
            GraphNodeKind.GetEnchantPowerBonus => $"Math.round(level.getBlockState({a[0]}).getEnchantPowerBonus(level, {a[0]}))",
            GraphNodeKind.IsBlockSolid => $"level.getBlockState({a[0]}).isSolid()",
            GraphNodeKind.GetBlockNbtNumber => Helper("blockNbtNumber", $"blockNbtNumber(level, {a[0]}, {a[1]})"),
            GraphNodeKind.GetBlockNbtText => Helper("blockNbtText", $"blockNbtText(level, {a[0]}, {a[1]})"),
            GraphNodeKind.GetBlockNbtBoolean => Helper("blockNbtBoolean", $"blockNbtBoolean(level, {a[0]}, {a[1]})"),
            GraphNodeKind.MainHandItem => $"{a[0]}.getMainHandItem()",
            GraphNodeKind.NewItemStack => Uses($"new ItemStack({a[0]}, {a[1]})", Stack),
            GraphNodeKind.ItemOfStack => $"{a[0]}.getItem()",
            GraphNodeKind.ItemCooldownPercent => $"Math.round({a[0]}.getCooldowns().getCooldownPercent({a[1]}, 0f) * 100)",
            GraphNodeKind.IsItemOnCooldown => $"{a[0]}.getCooldowns().isOnCooldown({a[1]})",
            GraphNodeKind.CopyItemStack => $"{a[0]}.copy()",
            GraphNodeKind.ItemMaxStackSize => $"{a[0]}.getMaxStackSize()",
            GraphNodeKind.ItemCount => $"{a[0]}.getCount()",
            GraphNodeKind.IsItemStackable => $"{a[0]}.isStackable()",
            GraphNodeKind.ItemDamage => $"{a[0]}.getDamageValue()",
            GraphNodeKind.ItemMaxDamage => $"{a[0]}.getMaxDamage()",
            GraphNodeKind.IsItemDamageable => $"{a[0]}.isDamageableItem()",
            GraphNodeKind.IsItemDamaged => $"{a[0]}.isDamaged()",
            GraphNodeKind.EnchantmentLevel => Helper("enchantmentLevel", $"enchantmentLevel(level, {a[0]}, {a[1]})"),
            GraphNodeKind.HasEnchantment => Helper("enchantmentLevel", $"(enchantmentLevel(level, {a[0]}, {a[1]}) > 0)"),
            GraphNodeKind.IsItemEnchantable => $"{a[0]}.isEnchantable()",
            GraphNodeKind.IsItemEnchanted => $"{a[0]}.isEnchanted()",
            GraphNodeKind.ItemFoodValue => Helper("foodValue", $"foodValue({a[0]})"),
            GraphNodeKind.ItemSaturation => Helper("saturationValue", $"saturationValue({a[0]})"),
            GraphNodeKind.IsItemFood => Helper("isFood", $"isFood({a[0]})"),
            GraphNodeKind.CanHarvestBlock => $"{a[0]}.isCorrectToolForDrops({a[1]}.defaultBlockState())",
            GraphNodeKind.ItemHasRarity => $"{a[0]}.getRarity().name().equalsIgnoreCase({a[1]})",
            GraphNodeKind.ItemRarity => $"{a[0]}.getRarity().name().toLowerCase(java.util.Locale.ROOT)",
            GraphNodeKind.ItemDisplayName => $"{a[0]}.getHoverName().getString()",
            GraphNodeKind.ItemRegistryName => Uses($"BuiltInRegistries.ITEM.getKey({a[0]}.getItem()).toString()", "net.minecraft.core.registries.BuiltInRegistries"),
            GraphNodeKind.CanBeSmelted => Helper("canBeSmelted", $"canBeSmelted(level, {a[0]})"),
            GraphNodeKind.And => $"({a[0]} && {a[1]})",
            GraphNodeKind.Or => $"({a[0]} || {a[1]})",
            GraphNodeKind.NumbersNotEqual => $"({a[0]} != {a[1]})",
            GraphNodeKind.GreaterOrEqual => $"({a[0]} >= {a[1]})",
            GraphNodeKind.LessOrEqual => $"({a[0]} <= {a[1]})",
            GraphNodeKind.TextEquals => $"{a[0]}.equals({a[1]})",
            GraphNodeKind.Divide => Helper("divide", $"divide({a[0]}, {a[1]})"),
            GraphNodeKind.Modulo => Helper("modulo", $"modulo({a[0]}, {a[1]})"),
            GraphNodeKind.Power => $"((int) Math.pow({a[0]}, {a[1]}))",
            GraphNodeKind.SquareRoot => $"((int) Math.round(Math.sqrt(Math.max(0, {a[0]}))))",
            GraphNodeKind.Minimum => $"Math.min({a[0]}, {a[1]})",
            GraphNodeKind.Maximum => $"Math.max({a[0]}, {a[1]})",
            GraphNodeKind.Absolute => $"Math.abs({a[0]})",
            GraphNodeKind.TextLength => $"{a[0]}.length()",
            GraphNodeKind.TextContains => $"{a[0]}.contains({a[1]})",
            GraphNodeKind.ReplaceText => $"{a[0]}.replace({a[1]}, {a[2]})",
            GraphNodeKind.TextToNumber => Helper("toNumber", $"toNumber({a[0]})"),
            GraphNodeKind.NumberToText => $"String.valueOf({a[0]})",
            GraphNodeKind.Dimension => "level.dimension().location().toString()",
            GraphNodeKind.WorldTime => "((int) (level.getDayTime() % 24000L))",
            GraphNodeKind.IsDay => "level.isDay()",
            GraphNodeKind.IsRaining => "level.isRaining()",
            GraphNodeKind.IsThundering => "level.isThundering()",
            GraphNodeKind.PlayerHealth => $"Math.round({a[0]}.getHealth())",
            GraphNodeKind.PlayerMaxHealth => $"Math.round({a[0]}.getMaxHealth())",
            GraphNodeKind.PlayerFood => $"{a[0]}.getFoodData().getFoodLevel()",
            GraphNodeKind.IsPlayerAlive => $"{a[0]}.isAlive()",
            GraphNodeKind.ArmorItem => Helper("armorItem", $"armorItem({a[0]}, {a[1]})"),
            GraphNodeKind.OffhandItem => $"{a[0]}.getOffhandItem()",
            GraphNodeKind.LookedAtBlock => Helper("lookedAtBlock", $"lookedAtBlock({a[0]})"),
            GraphNodeKind.ItemInSlot => Helper("slotItem", $"slotItem({a[0]}, {a[1]})"),
            GraphNodeKind.CountItemInInventory => $"{a[0]}.getInventory().countItem({a[1]})",
            GraphNodeKind.GetItemNbtNumber => Helper("itemNbtNumber", $"itemNbtNumber({a[0]}, {a[1]})"),
            GraphNodeKind.GetItemNbtText => Helper("itemNbtText", $"itemNbtText({a[0]}, {a[1]})"),
            GraphNodeKind.PositionX => $"{a[0]}.getX()",
            GraphNodeKind.PositionY => $"{a[0]}.getY()",
            GraphNodeKind.PositionZ => $"{a[0]}.getZ()",
            GraphNodeKind.MakePosition => $"new BlockPos({a[0]}, {a[1]}, {a[2]})",
            GraphNodeKind.Distance => $"((int) Math.round(Math.sqrt({a[0]}.distSqr({a[1]}))))",
            GraphNodeKind.PositionsEqual => $"{a[0]}.equals({a[1]})",
            GraphNodeKind.IsInArea => Helper("isInArea", $"isInArea({a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.OffsetInDirection => Helper("offsetInDirection", $"offsetInDirection({a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.ItemAppearance => Helper("itemAppearance", $"itemAppearance({a[0]})"),
            GraphNodeKind.BlockAppearance => Helper("blockAppearance", $"blockAppearance(level, {a[0]})"),
            GraphNodeKind.CreatureAppearance => Helper("creatureAppearance", $"creatureAppearance(level, {a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.RaycastHitsBlock => Helper("raycastBlock", $"(raycastBlock({a[0]}, {a[1]}, {a[2]}).getType() == HitResult.Type.BLOCK)"),
            GraphNodeKind.RaycastBlockPosition => Helper("raycastPosition", $"raycastPosition({a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.LooksAtCreature => Helper("raycastEntity", $"(raycastEntity({a[0]}, {a[1]}) != null)"),
            GraphNodeKind.CreatureLookedAt => Helper("lookedAtCreature", $"lookedAtCreature({a[0]}, {a[1]})"),
            GraphNodeKind.IsOnGround => $"{a[0]}.onGround()",
            GraphNodeKind.FallDistance => $"Math.round({a[0]}.fallDistance)",
            GraphNodeKind.GetGameRuleBoolean => Helper("gameRuleBoolean", $"gameRuleBoolean(level, {a[0]})"),
            GraphNodeKind.GetGameRuleNumber => Helper("gameRuleNumber", $"gameRuleNumber(level, {a[0]})"),
            GraphNodeKind.HasEffect => Helper("hasEffect", $"hasEffect({a[0]}, {a[1]})"),
            GraphNodeKind.CollectionSize => $"{a[0]}.size()",
            GraphNodeKind.CollectionContains => call.Arguments[0].Type.Kind == PinType.Map ? $"{a[0]}.containsKey({a[1]})" : $"{a[0]}.contains({a[1]})",
            GraphNodeKind.ListGet => Helper("listGet", $"listGet({a[0]}, {a[1]}, {DefaultValue(call.ResultType)})"),
            GraphNodeKind.MapGet => $"{a[0]}.getOrDefault({a[1]}, {DefaultValue(call.ResultType)})",
            >= GraphNodeKind.MakeVector and <= GraphNodeKind.VectorToPosition => Vector(call, a),
            GraphNodeKind.PositionToVector => Uses($"Vec3.atCenterOf({a[0]})", "net.minecraft.world.phys.Vec3"),
            GraphNodeKind.RoundDecimal => $"((int) Math.round({a[0]}))",
            GraphNodeKind.FloorDecimal => $"((int) Math.floor({a[0]}))",
            GraphNodeKind.DecimalAdd => $"({a[0]} + {a[1]})",
            GraphNodeKind.DecimalSubtract => $"({a[0]} - {a[1]})",
            GraphNodeKind.DecimalMultiply => $"({a[0]} * {a[1]})",
            GraphNodeKind.DecimalDivide => Helper("divideDecimal", $"divideDecimal({a[0]}, {a[1]})"),
            GraphNodeKind.DecimalGreater => $"({a[0]} > {a[1]})",
            GraphNodeKind.DecimalLess => $"({a[0]} < {a[1]})",
            GraphNodeKind.RandomDecimal => "level.random.nextDouble()",
            GraphNodeKind.PlayerFacing => $"{a[0]}.getDirection()",
            GraphNodeKind.OppositeDirection => $"{a[0]}.getOpposite()",
            GraphNodeKind.OffsetByDirection => $"{a[0]}.relative({a[1]}, {a[2]})",
            GraphNodeKind.DirectionName => $"{a[0]}.getName()",
            GraphNodeKind.BlockStateAt => $"level.getBlockState({a[0]})",
            GraphNodeKind.BlockOfState => $"{a[0]}.getBlock()",
            GraphNodeKind.DefaultBlockState => $"{a[0]}.defaultBlockState()",
            GraphNodeKind.StateProperty => Helper("stateProperty", $"stateProperty({a[0]}, {a[1]})"),
            GraphNodeKind.StateWithProperty => Helper("stateWithProperty", $"stateWithProperty({a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.EntityPosition => Helper("entityPosition", $"entityPosition({a[0]})"),
            GraphNodeKind.EntityTypeId => Helper("entityTypeId", $"entityTypeId({a[0]})"),
            GraphNodeKind.IsEntityAlive => Helper("isEntityAlive", $"isEntityAlive({a[0]})"),
            GraphNodeKind.NearestEntity => Helper("nearestEntity", $"nearestEntity(level, {a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.EntityLookedAt => Helper("entityLookedAt", $"entityLookedAt({a[0]}, {a[1]})"),
            GraphNodeKind.EntityHealth => Helper("entityHealth", $"entityHealth({a[0]})"),
            GraphNodeKind.EntityIsPlayer => $"({a[0]} instanceof Player)",
            >= GraphNodeKind.CastToNumber and <= GraphNodeKind.CastToPlayer => Cast(call, a[0]),
            GraphNodeKind.CharAt => Helper("charAt", $"charAt({a[0]}, {a[1]})"),
            GraphNodeKind.EntityUuid => Helper("entityUuid", $"entityUuid({a[0]})"),
            GraphNodeKind.EntityByUuid => Helper("entityByUuid", $"entityByUuid(level, {a[0]})"),
            GraphNodeKind.RandomUuid => Uses("UUID.randomUUID()", "java.util.UUID"),
            GraphNodeKind.StyledText => Helper("styledText", $"styledText({a[0]}, {a[1]}, {a[2]}, {a[3]})"),
            GraphNodeKind.IdentifierNamespace => $"{a[0]}.getNamespace()",
            GraphNodeKind.IdentifierPath => $"{a[0]}.getPath()",
            GraphNodeKind.EquipmentItem => Helper("equipmentItem", $"equipmentItem({a[0]}, {a[1]})"),
            GraphNodeKind.ItemInHand => $"{a[0]}.getItemInHand({a[1]})",
            GraphNodeKind.StackRarity => $"{a[0]}.getRarity()",
            GraphNodeKind.FluidAt => $"level.getFluidState({a[0]}).getType()",
            GraphNodeKind.EntityBox => Helper("entityBox", $"entityBox({a[0]})"),
            GraphNodeKind.MakeBox => Helper("makeBox", $"makeBox({a[0]}, {a[1]})"),
            GraphNodeKind.EntitiesInBox => Helper("entitiesInBox", $"entitiesInBox(level, {a[0]})"),
            GraphNodeKind.BoxContains => $"{a[0]}.contains({a[1]})",
            GraphNodeKind.GenericDamage => Helper("genericDamage", $"genericDamage(level, {a[0]})"),
            GraphNodeKind.MakeEffect => Helper("makeEffect", $"makeEffect({a[0]}, {a[1]}, {a[2]})"),
            GraphNodeKind.NewNbt => Uses("new CompoundTag()", "net.minecraft.nbt.CompoundTag"),
            GraphNodeKind.NbtGetNumber => $"{a[0]}.getInt({a[1]})",
            GraphNodeKind.NbtGetText => $"{a[0]}.getString({a[1]})",
            GraphNodeKind.CurrentWorld => "level",
            GraphNodeKind.WorldOf => Helper("worldOf", $"worldOf(level, {a[0]})"),
            GraphNodeKind.WorldDimension => $"{a[0]}.dimension().location().toString()",
            GraphNodeKind.GameTime => "level.getGameTime()",
            GraphNodeKind.PlayerGuild => $"{a[0]}.getPersistentData().getString(\"mineengine_guild\")",
            GraphNodeKind.EntityGroup => Helper("entityGroup", $"entityGroup({a[0]})"),
            GraphNodeKind.IsBlockInTag => Helper("blockInTag", $"blockInTag(level, {a[0]}, {a[1]})"),
            GraphNodeKind.IsItemInTag => Helper("itemInTag", $"itemInTag({a[0]}, {a[1]})"),
            GraphNodeKind.IsEntityInTag => Helper("entityInTag", $"entityInTag({a[0]}, {a[1]})"),
            GraphNodeKind.IsBiomeInTag => Helper("biomeInTag", $"biomeInTag(level, {a[0]}, {a[1]})"),
            _ => throw new NotSupportedException($"Nœud {call.Kind} non traduit en Java."),
        };
    }

    /// <summary>Java of a cast node: a primitive cast, a conversion from a text, or a test of the class of an entity.</summary>
    private string Cast(IRCallExpression call, string value)
    {
        PinType from = call.Arguments[0].Type.Kind;
        return (call.Kind, from) switch
        {
            (GraphNodeKind.CastToNumber, PinType.Text) => Helper("toNumber", $"toNumber({value})"),
            (GraphNodeKind.CastToNumber, PinType.Boolean) => $"({value} ? 1 : 0)",
            (GraphNodeKind.CastToNumber, _) => $"((int) ({value}))",
            (GraphNodeKind.CastToLong, PinType.Text) => Helper("toLong", $"toLong({value})"),
            (GraphNodeKind.CastToLong, _) => $"((long) ({value}))",
            (GraphNodeKind.CastToFloat, PinType.Text) => Helper("toDecimal", $"((float) toDecimal({value}))"),
            (GraphNodeKind.CastToFloat, _) => $"((float) ({value}))",
            (GraphNodeKind.CastToDecimal, PinType.Text) => Helper("toDecimal", $"toDecimal({value})"),
            (GraphNodeKind.CastToDecimal, _) => $"((double) ({value}))",
            (GraphNodeKind.CastToByte, _) => $"((byte) ({value}))",
            (GraphNodeKind.CastToChar, PinType.Text) => Helper("charAt", $"charAt({value}, 0)"),
            (GraphNodeKind.CastToChar, _) => $"((char) ({value}))",
            (GraphNodeKind.CastToText, PinType.TextComponent) => $"{value}.getString()",
            (GraphNodeKind.CastToText, _) => $"String.valueOf({value})",
            (GraphNodeKind.CastToComponent, PinType.Text) => Uses($"Component.literal({value})", "net.minecraft.network.chat.Component"),
            (GraphNodeKind.CastToComponent, _) => Uses($"Component.literal(String.valueOf({value}))", "net.minecraft.network.chat.Component"),
            (GraphNodeKind.CastToIdentifier, _) => Helper("identifier", $"identifier({value})"),
            (GraphNodeKind.CastToUuid, PinType.Text) => Helper("uuid", $"uuid({value})"),
            (GraphNodeKind.CastToUuid, _) => Helper("entityUuid", $"entityUuid({value})"),
            (GraphNodeKind.CastToLiving, _) => Helper("toLiving", $"toLiving({value})"),
            _ => Helper("toPlayer", $"toPlayer({value})"),
        };
    }

    /// <summary>Vector nodes: each kind of vector has its own Java class (JOML Vector2i, Vec3i, Vec2, Vec3).</summary>
    private string Vector(IRCallExpression call, string[] a)
    {
        DataType type = call.Kind == GraphNodeKind.MakeVector ? call.ResultType : call.Arguments[0].Type;
        foreach (string import in JavaProgramNames.ImportsFor(type))
        {
            Use(import);
        }

        return (call.Kind, type.Kind) switch
        {
            (GraphNodeKind.MakeVector, PinType.Vector2i) => $"new Vector2i({a[0]}, {a[1]})",
            (GraphNodeKind.MakeVector, PinType.Vector3i) => $"new Vec3i({a[0]}, {a[1]}, {a[2]})",
            (GraphNodeKind.MakeVector, PinType.Vector2f) => $"new Vec2((float) {a[0]}, (float) {a[1]})",
            (GraphNodeKind.MakeVector, _) => $"new Vec3({a[0]}, {a[1]}, {a[2]})",
            (GraphNodeKind.VectorX, PinType.Vector3i) => $"{a[0]}.getX()",
            (GraphNodeKind.VectorY, PinType.Vector3i) => $"{a[0]}.getY()",
            (GraphNodeKind.VectorZ, PinType.Vector3i) => $"{a[0]}.getZ()",
            (GraphNodeKind.VectorX, PinType.Vector2f) => $"((double) {a[0]}.x)",
            (GraphNodeKind.VectorY, PinType.Vector2f) => $"((double) {a[0]}.y)",
            (GraphNodeKind.VectorX, _) => $"{a[0]}.x",
            (GraphNodeKind.VectorY, _) => $"{a[0]}.y",
            (GraphNodeKind.VectorZ, _) => $"{a[0]}.z",
            (GraphNodeKind.VectorLength, PinType.Vector3i) => $"Math.sqrt({a[0]}.distSqr(Vec3i.ZERO))",
            (GraphNodeKind.VectorLength, PinType.Vector2f) => $"((double) {a[0]}.length())",
            (GraphNodeKind.VectorLength, _) => $"{a[0]}.length()",
            (GraphNodeKind.AddVectors, PinType.Vector2i) => $"new Vector2i({a[0]}).add({a[1]})",
            (GraphNodeKind.AddVectors, PinType.Vector3i) => $"{a[0]}.offset({a[1]})",
            (GraphNodeKind.AddVectors, _) => $"{a[0]}.add({a[1]})",
            (_, PinType.Vector3i) => Uses($"new BlockPos({a[0]})", "net.minecraft.core.BlockPos"),
            _ => Uses($"BlockPos.containing({a[0]})", "net.minecraft.core.BlockPos"),
        };
    }

    /// <summary>Items, blocs et sons sont retrouvés par identifiant : même code pour le jeu et pour le mod.</summary>
    private string Content(IRContentLiteral content)
    {
        string id = JavaNames.StringLiteral(content.Id.ToString());
        return content.ContentType.Kind switch
        {
            PinType.Item => Helper("item", $"item({id})"),
            PinType.Block => Helper("block", $"block({id})"),
            PinType.Sound => Helper("sound", $"sound({id})"),
            _ => throw new NotSupportedException($"Contenu {content.ContentType} non traduit en Java."),
        };
    }

    /// <summary>Objet d'un champ ou d'une méthode : l'expression, sinon "this".</summary>
    private string Owner(IRExpression? target) => target is null ? "this" : Expression(target);

    private string Arguments(IEnumerable<IRExpression> arguments, bool withLevel) =>
        string.Join(", ", (withLevel ? [JavaProgramNames.LevelParameter] : Array.Empty<string>()).Concat(arguments.Select(Expression)));

    /// <summary>Appel Java (sans point-virgule) d'une fonction, d'une méthode ou d'une méthode d'interface.</summary>
    private string Invoke(IRCallee callee, IRExpression? target, IReadOnlyList<IRExpression> arguments)
    {
        string args = Arguments(arguments, withLevel: true);
        if (callee.InterfaceMethod is { } method)
        {
            return $"{Owner(target)}.{_names.InterfaceMethod(callee.Definition, method)}({args})";
        }

        if (_names.Find(callee.Definition) is not IRFunction function)
        {
            throw new NotSupportedException("Appel d'une définition qui n'est pas une fonction.");
        }

        return $"{_names.CallPrefix(function, target is null ? null : Expression(target))}({args})";
    }

    private string PureCall(IRPureCall call)
    {
        string invocation = Invoke(call.Callee, call.Target, call.Arguments);
        return call.Component is { } component ? $"{invocation}.{_names.Variable(component)}()" : invocation;
    }

    private IReadOnlyList<string> CallStatement(IRCallStatement call)
    {
        string invocation = Invoke(call.Callee, call.Target, call.Arguments);
        IReadOnlyList<VariableDefinition> returns = call.Callee.InterfaceMethod is { } method
            ? (_names.Find(call.Callee.Definition) as IRInterface)?.Methods.FirstOrDefault(m => m.Id == method)?.Returns ?? []
            : (_names.Find(call.Callee.Definition) as IRFunction)?.Returns ?? [];

        if (call.Results.Count == 0 || returns.Count == 0)
        {
            return [invocation + ";"];
        }

        if (returns.Count == 1)
        {
            return [$"{call.Results[0]} = {invocation};"];
        }

        // Plusieurs valeurs renvoyées : un record, dont chaque composant va dans sa variable locale.
        var lines = new List<string> { "{", $"    var result = {invocation};" };
        lines.AddRange(returns.Zip(call.Results).Select(pair => $"    {pair.Second} = result.{_names.Variable(pair.First.Id)}();"));
        lines.Add("}");
        return lines;
    }

    private string New(Guid typeId, IReadOnlyList<IRExpression> arguments) =>
        $"new {_names.TypeName(typeId)}({Arguments(arguments, withLevel: _names.Find(typeId) is IRClass)})";

    private const string Stack = "net.minecraft.world.item.ItemStack";

    /// <summary>Call that tells whether a Flip Flop node runs A this time (its state is kept per node).</summary>
    public string FlipFlop(Guid nodeId) => Helper("flipFlop", $"flipFlop(\"{nodeId:N}\")");

    private string Helper(string name, string code)
    {
        if (_helpers.Add(name))
        {
            Use([.. HelperImports(name)]);
            if (name is "setCreatureAppearance" or "creatureAppearance" or "appearanceHolders")
            {
                // Interface of the mobs with appearances, in the entity package of the mod.
                Use($"{_names.Package[..^".code".Length]}.entity.{ModJavaEmitter.AppearanceInterface}");
            }

            // Helpers take the level, the position or the player: their types are imported even in a function without event prologue.
            Use("net.minecraft.core.BlockPos", "net.minecraft.world.entity.player.Player", "net.minecraft.world.level.Level");
            foreach (string dependency in GraphHelpersJava.Dependencies(name))
            {
                Helper(dependency, string.Empty);
            }
        }

        return code;
    }

    private string Uses(string code, params string[] imports)
    {
        Use(imports);
        return code;
    }

    private void Use(params string[] imports) => _imports.UnionWith(imports);

    private IReadOnlyList<string> HelperImports(string name) => GraphHelpersJava.Imports(name, _api) ?? CoreHelperImports(name);

    private static string[] CoreHelperImports(string name) => name switch
    {
        "item" => ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.item.Item"],
        "block" => ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.world.level.block.Block"],
        "sound" => ["net.minecraft.core.registries.BuiltInRegistries", "net.minecraft.resources.ResourceLocation", "net.minecraft.sounds.SoundEvent"],
        "spawnItem" => ["net.minecraft.world.entity.item.ItemEntity", "net.minecraft.world.item.Item", "net.minecraft.world.item.ItemStack"],
        "giveItem" => ["net.minecraft.world.item.Item", "net.minecraft.world.item.ItemStack"],
        "removeItem" => ["net.minecraft.world.item.Item"],
        "sendMessage" => ["net.minecraft.network.chat.Component"],
        "playSound" => ["net.minecraft.sounds.SoundEvent", "net.minecraft.sounds.SoundSource"],
        "setBlock" => ["net.minecraft.world.level.block.Block"],
        _ => [],
    };

    private IReadOnlyList<string> HelperMethod(string name) => name switch
    {
        "item" =>
        [
            "private static Item item(String id) {",
            $"    return BuiltInRegistries.ITEM.get({_api.ParseResourceLocation("id")});",
            "}",
        ],
        "block" =>
        [
            "private static Block block(String id) {",
            $"    return BuiltInRegistries.BLOCK.get({_api.ParseResourceLocation("id")});",
            "}",
        ],
        "sound" =>
        [
            "private static SoundEvent sound(String id) {",
            $"    return BuiltInRegistries.SOUND_EVENT.get({_api.ParseResourceLocation("id")});",
            "}",
        ],
        "spawnItem" =>
        [
            "private static void spawnItem(Level level, BlockPos position, Item item, int count) {",
            "    if (count > 0) {",
            "        level.addFreshEntity(new ItemEntity(level, position.getX() + 0.5, position.getY() + 1.0, position.getZ() + 0.5, new ItemStack(item, count)));",
            "    }",
            "}",
        ],
        "giveItem" =>
        [
            "private static void giveItem(Player player, Item item, int count) {",
            "    if (count <= 0) {",
            "        return;",
            "    }",
            "    ItemStack stack = new ItemStack(item, count);",
            "    if (!player.getInventory().add(stack)) {",
            "        player.drop(stack, false);",
            "    }",
            "}",
        ],
        "removeItem" =>
        [
            "private static void removeItem(Player player, Item item, int count) {",
            "    if (count > 0) {",
            "        player.getInventory().clearOrCountMatchingItems(stack -> stack.is(item), count, player.inventoryMenu.getCraftSlots());",
            "    }",
            "}",
        ],
        "sendMessage" =>
        [
            "private static void sendMessage(Player player, String message) {",
            "    player.sendSystemMessage(Component.literal(message));",
            "}",
        ],
        "playSound" =>
        [
            "private static void playSound(Level level, BlockPos position, SoundEvent sound) {",
            "    if (sound != null) {",
            "        level.playSound(null, position, sound, SoundSource.BLOCKS, 1.0f, 1.0f);",
            "    }",
            "}",
        ],
        "setBlock" =>
        [
            "private static void setBlock(Level level, BlockPos position, Block block) {",
            "    level.setBlockAndUpdate(position, block.defaultBlockState());",
            "}",
        ],
        "explode" =>
        [
            "private static void explode(Level level, BlockPos position, int power) {",
            "    if (power > 0) {",
            "        level.explode(null, position.getX() + 0.5, position.getY() + 0.5, position.getZ() + 0.5, power, Level.ExplosionInteraction.TNT);",
            "    }",
            "}",
        ],
        "randomBetween" =>
        [
            "private static int randomBetween(Level level, int first, int second) {",
            "    int low = Math.min(first, second);",
            "    int high = Math.max(first, second);",
            "    return low + level.random.nextInt(high - low + 1);",
            "}",
        ],
        _ => GraphHelpersJava.Method(name, _api) ?? throw new NotSupportedException($"Méthode utilitaire {name} inconnue."),
    };
}
