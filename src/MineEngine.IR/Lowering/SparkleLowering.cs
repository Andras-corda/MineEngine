using MineEngine.Assets;
using MineEngine.Assets.Sparkle;
using MineEngine.Core.Assets;
using MineEngine.IR.Model;

namespace MineEngine.IR.Lowering;

/// <summary>Sparkle particle system (V0.7.5): the modules of its emitters become typed settings.</summary>
public sealed class SparkleLowering : AssetLowering<SparkleAsset>
{
    public override AssetType AssetType => AssetType.Sparkle;

    protected override void Lower(SparkleAsset asset, LoweringContext context)
    {
        IReadOnlyList<SparkleEmitter> resolved = asset.ResolvedEmitters;
        context.AddSparkle(new IRSparkle(asset.Id, asset.ResourceId, asset.DisplayName)
        {
            // The linked settings take the values of the user parameters; the links stay, for the graphs.
            Emitters = [.. resolved.Select((e, i) => LowerEmitter(asset, e, context) with { Bindings = Bindings(asset, asset.Emitters[i]) })],
            Parameters = [.. asset.UserParameters.Select(p => new IRSparkleParameter(p.Name.Trim()))],
        });
    }

    private static IRSparkleEmitter LowerEmitter(SparkleAsset asset, SparkleEmitter emitter, LoweringContext context)
    {
        SparkleModule lifecycle = emitter.Get(SparkleModuleKind.Lifecycle);
        SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
        SparkleModule velocity = emitter.Get(SparkleModuleKind.Velocity);
        SparkleModule collision = emitter.Get(SparkleModuleKind.Collision);
        SparkleModule scale = emitter.Get(SparkleModuleKind.Scale);
        SparkleModule texture = emitter.Get(SparkleModuleKind.Texture);
        SparkleModule receive = emitter.Get(SparkleModuleKind.ReceiveDeathEvent);
        SparkleModule time = emitter.Get(SparkleModuleKind.GenerateTimeEvent);
        SparkleModule zone = emitter.Get(SparkleModuleKind.GenerateLocationEvent);
        IRSparkleScale scaleMode = Enum.Parse<IRSparkleScale>(scale.Choice(SparkleCatalog.ScaleMode));

        return new IRSparkleEmitter(emitter.Name, emitter.EventId)
        {
            Loop = Enum.Parse<IRSparkleLoop>(lifecycle.Choice(SparkleCatalog.LoopMode)),
            Duration = lifecycle.Number(SparkleCatalog.Duration),
            Loops = lifecycle.Integer(SparkleCatalog.Loops),
            Delay = lifecycle.Number(SparkleCatalog.Delay),
            Burst = spawn.Choice(SparkleCatalog.SpawnMode) == "Burst",
            Count = spawn.Integer(SparkleCatalog.Count),
            Shape = Enum.Parse<IRSparkleShape>(spawn.Choice(SparkleCatalog.Shape)),
            Offset = Vector(spawn.Vector(SparkleCatalog.Offset)),
            Radius = spawn.Number(SparkleCatalog.Radius),
            BoxSize = Vector(spawn.Vector(SparkleCatalog.BoxSize)),
            LineEnd = Vector(spawn.Vector(SparkleCatalog.LineEnd)),
            LifetimeMin = Math.Min(spawn.Number(SparkleCatalog.LifetimeMin), spawn.Number(SparkleCatalog.LifetimeMax)),
            LifetimeMax = Math.Max(spawn.Number(SparkleCatalog.LifetimeMin), spawn.Number(SparkleCatalog.LifetimeMax)),
            DirectionMode = Enum.Parse<IRSparkleDirection>(velocity.Choice(SparkleCatalog.VelocityMode)),
            Direction = Vector(velocity.Vector(SparkleCatalog.Direction)),
            ConeAngle = velocity.Number(SparkleCatalog.ConeAngle),
            SpeedMin = Math.Min(velocity.Number(SparkleCatalog.SpeedMin), velocity.Number(SparkleCatalog.SpeedMax)),
            SpeedMax = Math.Max(velocity.Number(SparkleCatalog.SpeedMin), velocity.Number(SparkleCatalog.SpeedMax)),
            Gravity = emitter.Uses(SparkleModuleKind.Gravity) ? Vector(emitter.Get(SparkleModuleKind.Gravity).Vector(SparkleCatalog.Acceleration)) : null,
            Collides = emitter.Uses(SparkleModuleKind.Collision),
            Bounce = collision.Number(SparkleCatalog.Bounce),
            DiesOnCollision = collision.Boolean(SparkleCatalog.DieOnCollision),
            ScaleMode = scaleMode,
            ScaleA = scaleMode switch
            {
                IRSparkleScale.OverLife => scale.Number(SparkleCatalog.Start),
                IRSparkleScale.Random => scale.Number(SparkleCatalog.RangeMin),
                _ => scale.Number(SparkleCatalog.Value),
            },
            ScaleB = scaleMode switch
            {
                IRSparkleScale.OverLife => scale.Number(SparkleCatalog.End),
                IRSparkleScale.Random => scale.Number(SparkleCatalog.RangeMax),
                _ => scale.Number(SparkleCatalog.Value),
            },
            Sprites = [.. SparkleCatalog.SplitTextures(texture.GetValue(SparkleCatalog.Sprite)).Select(s => Sprite(asset, s, context))],
            AnimatedSprites = texture.Choice(SparkleCatalog.SpriteMode) == "OverLife",
            Tint = texture.Color(SparkleCatalog.Tint),
            Glow = texture.Boolean(SparkleCatalog.Glow),
            Translucent = texture.Boolean(SparkleCatalog.Translucent),
            DeathEventChance = emitter.Uses(SparkleModuleKind.GenerateDeathEvent) ? emitter.Get(SparkleModuleKind.GenerateDeathEvent).Number(SparkleCatalog.Chance) : null,
            TimeEventChance = emitter.Uses(SparkleModuleKind.GenerateTimeEvent) ? time.Number(SparkleCatalog.Chance) : null,
            TimeEventTime = time.Number(SparkleCatalog.EventTime),
            TimeEventRepeats = time.Choice(SparkleCatalog.TimeMode) == "Repeat",
            LocationEventChance = emitter.Uses(SparkleModuleKind.GenerateLocationEvent) ? zone.Number(SparkleCatalog.Chance) : null,
            ZoneShape = zone.Choice(SparkleCatalog.ZoneShape) == "Box" ? IRSparkleShape.Box : IRSparkleShape.Sphere,
            ZoneCenter = Vector(zone.Vector(SparkleCatalog.ZoneCenter)),
            ZoneRadius = zone.Number(SparkleCatalog.ZoneRadius),
            ZoneSize = Vector(zone.Vector(SparkleCatalog.ZoneSize)),
            EventSource = emitter.EventSource,
            ReceivedEvent = emitter.ReceivedEvent switch
            {
                SparkleEventKind.Time => IRSparkleEvent.Time,
                SparkleEventKind.Location => IRSparkleEvent.Location,
                _ => IRSparkleEvent.Death,
            },
            CountPerEvent = receive.Integer(SparkleCatalog.CountPerEvent),
            AtDeathLocation = receive.Boolean(SparkleCatalog.AtDeathLocation),
            InheritVelocity = receive.Number(SparkleCatalog.InheritVelocity) / 100,
        };
    }

    private static IRVector Vector(SparkleVector vector) => new(vector.X, vector.Y, vector.Z);

    /// <summary>Settings of the emitter linked to a parameter of the system of their type.</summary>
    private static IReadOnlyList<IRSparkleBinding> Bindings(SparkleAsset asset, SparkleEmitter emitter) =>
    [
        .. from module in emitter.Modules
           from value in module.Values
           let id = SparkleUserParameter.BoundId(value.Value)
           where id is not null
           let index = asset.UserParameters.ToList().FindIndex(p => p.Id == id)
           let info = module.Info.FindParameter(value.Key)
           where index >= 0 && info is not null && asset.UserParameters[index].Accepts(info.Type)
           orderby module.Kind, value.Key
           select new IRSparkleBinding(module.Kind.ToString(), value.Key, index),
    ];

    /// <summary>Sprite of the game, or file of a texture of the project (null if it is missing: the default sprite is used).</summary>
    private static IRSparkleSprite Sprite(SparkleAsset asset, string reference, LoweringContext context)
    {
        if (ContentReference.TryParseStorageText(reference, out ContentReference? parsed))
        {
            if (parsed!.Kind == ContentReferenceKind.Asset)
            {
                return new IRSparkleSprite(SparkleCatalog.DefaultSprite, context.ResolveOptionalTexture(asset, parsed.AssetId));
            }

            if (parsed.Kind == ContentReferenceKind.Id)
            {
                return new IRSparkleSprite(parsed.Id!.ToString()!, null);
            }
        }

        return new IRSparkleSprite(SparkleCatalog.DefaultSprite, null);
    }
}
