namespace MineEngine.Assets.Sparkle;

/// <summary>A particle of the simulation: where it is, where it goes, its age and its size.</summary>
public sealed class SparkleParticle
{
    internal SparkleParticle(int emitter, SparkleVector position, SparkleVector velocity, double lifetime, double randomScale)
    {
        Emitter = emitter;
        Position = position;
        Velocity = velocity;
        Lifetime = lifetime;
        RandomScale = randomScale;
    }

    /// <summary>Index of its emitter in the system.</summary>
    public int Emitter { get; }

    public SparkleVector Position { get; internal set; }

    public SparkleVector Velocity { get; internal set; }

    public double Age { get; internal set; }

    public double Lifetime { get; }

    /// <summary>Size drawn when it was born (Random scale mode).</summary>
    internal double RandomScale { get; }

    /// <summary>0 at birth, 1 at death.</summary>
    public double Progress => Lifetime <= 0 ? 1 : Math.Clamp(Age / Lifetime, 0, 1);

    /// <summary>Size of its square, in blocks.</summary>
    public double Size { get; internal set; }

    /// <summary>Texture taken at random among those of its emitter (Random mode with several textures).</summary>
    public int Sprite { get; internal set; }

    internal bool Dead { get; set; }

    /// <summary>Time events already sent (Generate time event).</summary>
    internal int TimeEvents { get; set; }

    /// <summary>Inside the zone of its Generate location event at the last step (it sends the event when it enters).</summary>
    internal bool InZone { get; set; }
}

/// <summary>
/// Simulation of a Sparkle system, the same as the one of the mod (in seconds instead of ticks):
/// lifecycle of the emitters, spawn, velocity, gravity, collision with the ground (y = 0 in the
/// preview, the blocks in the game), scale and death events. Used by the preview of the editor and
/// by the tests.
/// </summary>
public sealed class SparkleSimulation
{
    /// <summary>Particles beyond this number are not spawned (like the limit of Minecraft).</summary>
    public const int MaxParticles = SparklePerformance.EngineLimit;

    private readonly IReadOnlyList<SparkleEmitter> _emitters;
    private readonly EmitterState[] _states;
    private readonly List<SparkleParticle> _particles = [];
    private readonly Random _random;

    /// <param name="emitters">Emitters of the system.</param>
    /// <param name="included">Emitters simulated (the preview leaves out those whose box is unchecked); null: all.</param>
    /// <param name="ground">Height of the ground the particles collide with; null: no ground.</param>
    public SparkleSimulation(IReadOnlyList<SparkleEmitter> emitters, Func<SparkleEmitter, bool>? included = null, double? ground = 0, int seed = 1)
    {
        _emitters = emitters;
        _random = new Random(seed);
        Ground = ground;
        _states = [.. emitters.Select(e => new EmitterState(included?.Invoke(e) ?? true))];
    }

    public double? Ground { get; }

    /// <summary>Time since the start, in seconds.</summary>
    public double Time { get; private set; }

    public IReadOnlyList<SparkleParticle> Particles => _particles;

    /// <summary>Number of death events sent since the start.</summary>
    public int DeathEvents { get; private set; }

    /// <summary>Number of events of every kind sent since the start.</summary>
    public int Events { get; private set; }

    /// <summary>True when no emitter will spawn again and every particle is dead.</summary>
    public bool IsFinished =>
        _particles.Count == 0 && _emitters.Select((e, i) => (e, i)).All(x => !_states[x.i].Included || x.e.IsEventDriven || Finished(x.e, Time));

    /// <summary>
    /// Seconds from the start until every particle of the system is dead, at the latest: the end of
    /// the lifecycles plus the longest lifetime, and again for each level of death events. Null when
    /// an emitter loops forever.
    /// </summary>
    public static double? Length(IReadOnlyList<SparkleEmitter> emitters, Func<SparkleEmitter, bool>? included = null)
    {
        var ends = new Dictionary<int, double>();
        double length = 0;
        foreach (SparkleEmitter emitter in emitters.Where(e => (included?.Invoke(e) ?? true) && !e.IsEventDriven))
        {
            if (LoopCount(emitter) == int.MaxValue)
            {
                return null;
            }

            SparkleModule lifecycle = emitter.Get(SparkleModuleKind.Lifecycle);
            double end = lifecycle.Number(SparkleCatalog.Delay) + lifecycle.Number(SparkleCatalog.Duration) * LoopCount(emitter) + LongestLife(emitter);
            ends[emitter.EventId] = Math.Max(ends.GetValueOrDefault(emitter.EventId), end);
            length = Math.Max(length, end);
        }

        // Each pass adds one level of events (a chain of n emitters needs n passes; a loop stops at the count).
        for (int pass = 0; pass < emitters.Count; pass++)
        {
            foreach (SparkleEmitter emitter in emitters.Where(e => (included?.Invoke(e) ?? true) && e.IsEventDriven))
            {
                if (emitter.EventSource is { } source && ends.TryGetValue(source, out double sourceEnd))
                {
                    double end = sourceEnd + LongestLife(emitter);
                    ends[emitter.EventId] = Math.Max(ends.GetValueOrDefault(emitter.EventId), end);
                    length = Math.Max(length, end);
                }
            }
        }

        return length;
    }

    private static double LongestLife(SparkleEmitter emitter)
    {
        SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
        return Math.Max(spawn.Number(SparkleCatalog.LifetimeMin), spawn.Number(SparkleCatalog.LifetimeMax));
    }

    /// <summary>Moves the simulation forward by <paramref name="delta"/> seconds.</summary>
    public void Step(double delta)
    {
        if (delta <= 0)
        {
            return;
        }

        double from = Time;
        Time += delta;
        for (int i = 0; i < _emitters.Count; i++)
        {
            if (_states[i].Included && !_emitters[i].IsEventDriven)
            {
                SpawnOwn(i, from, Time);
            }
        }

        // The events spawn particles: they are sent once every particle has moved.
        var dying = new List<SparkleParticle>();
        var events = new List<(SparkleParticle Particle, SparkleEventKind Kind)>();
        foreach (SparkleParticle particle in _particles)
        {
            Update(particle, delta);
            CollectEvents(particle, events);
            if (particle.Dead || particle.Age >= particle.Lifetime)
            {
                particle.Dead = true;
                dying.Add(particle);
            }
        }

        _particles.RemoveAll(p => p.Dead);
        foreach ((SparkleParticle particle, SparkleEventKind kind) in events)
        {
            Send(particle, kind);
        }

        foreach (SparkleParticle particle in dying)
        {
            Send(particle, SparkleEventKind.Death);
        }
    }

    // ----- Emitters -----

    private sealed class EmitterState(bool included)
    {
        public bool Included { get; } = included;

        /// <summary>Particles already spawned in the current loop (continuous spawn).</summary>
        public double Spawned { get; set; }

        public int Loop { get; set; } = -1;
    }

    /// <summary>Number of loops of the lifecycle (int.MaxValue: infinite).</summary>
    private static int LoopCount(SparkleEmitter emitter)
    {
        SparkleModule lifecycle = emitter.Get(SparkleModuleKind.Lifecycle);
        return lifecycle.Choice(SparkleCatalog.LoopMode) switch
        {
            "Loop" => lifecycle.Integer(SparkleCatalog.Loops),
            "Infinite" => int.MaxValue,
            _ => 1,
        };
    }

    private static bool Finished(SparkleEmitter emitter, double time)
    {
        SparkleModule lifecycle = emitter.Get(SparkleModuleKind.Lifecycle);
        int loops = LoopCount(emitter);
        return loops != int.MaxValue
               && time >= lifecycle.Number(SparkleCatalog.Delay) + lifecycle.Number(SparkleCatalog.Duration) * loops;
    }

    private void SpawnOwn(int index, double from, double to)
    {
        SparkleEmitter emitter = _emitters[index];
        EmitterState state = _states[index];
        SparkleModule lifecycle = emitter.Get(SparkleModuleKind.Lifecycle);
        SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
        double delay = lifecycle.Number(SparkleCatalog.Delay);
        double duration = lifecycle.Number(SparkleCatalog.Duration);
        int loops = LoopCount(emitter);
        int count = spawn.Integer(SparkleCatalog.Count);
        bool burst = spawn.Choice(SparkleCatalog.SpawnMode) == "Burst";

        // Walks through the loops crossed by this step (several when the step is long).
        for (double time = Math.Max(from, delay); time < to;)
        {
            int loop = (int)Math.Floor((time - delay) / duration);
            if (loop >= loops)
            {
                return;
            }

            double loopStart = delay + loop * duration;
            double loopEnd = Math.Min(to, loopStart + duration);
            if (state.Loop != loop)
            {
                state.Loop = loop;
                state.Spawned = 0;
            }

            double wanted = burst ? count : Math.Min(count, count * (loopEnd - loopStart) / duration);
            for (int n = (int)state.Spawned; n < (int)wanted; n++)
            {
                Spawn(index, null, null);
            }

            state.Spawned = Math.Max(state.Spawned, wanted);
            time = loopEnd == to ? to : loopEnd + 1e-9;
        }
    }

    // ----- Particles -----

    private void Spawn(int index, SparkleVector? at, SparkleVector? inherited)
    {
        if (_particles.Count >= MaxParticles)
        {
            return;
        }

        SparkleEmitter emitter = _emitters[index];
        SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
        SparkleVector center = (at ?? SparkleVector.Zero) + spawn.Vector(SparkleCatalog.Offset);
        SparkleVector position = center + ShapePoint(spawn);
        SparkleVector velocity = Velocity(emitter.Get(SparkleModuleKind.Velocity), position - center);
        if (inherited is { } parent)
        {
            velocity += parent * (emitter.Get(SparkleModuleKind.ReceiveDeathEvent).Number(SparkleCatalog.InheritVelocity) / 100);
        }

        double lifetime = Between(spawn.Number(SparkleCatalog.LifetimeMin), spawn.Number(SparkleCatalog.LifetimeMax));
        SparkleModule scale = emitter.Get(SparkleModuleKind.Scale);
        double randomScale = Between(scale.Number(SparkleCatalog.RangeMin), scale.Number(SparkleCatalog.RangeMax));
        var particle = new SparkleParticle(index, position, velocity, lifetime, randomScale);
        particle.Size = Size(emitter, particle);
        SparkleModule texture = emitter.Get(SparkleModuleKind.Texture);
        int textures = SparkleCatalog.SplitTextures(texture.GetValue(SparkleCatalog.Sprite)).Count;
        if (textures > 1 && texture.Choice(SparkleCatalog.SpriteMode) == "Random")
        {
            particle.Sprite = _random.Next(textures);
        }

        // Born inside its zone, a particle has not entered it: it sends the event once it comes back in.
        particle.InZone = emitter.Uses(SparkleModuleKind.GenerateLocationEvent) && InZone(emitter.Get(SparkleModuleKind.GenerateLocationEvent), position);
        _particles.Add(particle);
    }

    private SparkleVector ShapePoint(SparkleModule spawn)
    {
        switch (spawn.Choice(SparkleCatalog.Shape))
        {
            case "Sphere":
                // Uniform in the volume of the ball.
                return RandomUnit() * (spawn.Number(SparkleCatalog.Radius) * Math.Cbrt(_random.NextDouble()));
            case "Box":
                SparkleVector size = spawn.Vector(SparkleCatalog.BoxSize);
                return new SparkleVector(size.X * (_random.NextDouble() - 0.5), size.Y * (_random.NextDouble() - 0.5), size.Z * (_random.NextDouble() - 0.5));
            case "Line":
                // From the coordinates (the center) to the end of the line.
                return (spawn.Vector(SparkleCatalog.LineEnd) - spawn.Vector(SparkleCatalog.Offset)) * _random.NextDouble();
            default:
                return SparkleVector.Zero;
        }
    }

    private SparkleVector Velocity(SparkleModule velocity, SparkleVector fromCenter)
    {
        double speed = Between(velocity.Number(SparkleCatalog.SpeedMin), velocity.Number(SparkleCatalog.SpeedMax));
        SparkleVector axis = velocity.Vector(SparkleCatalog.Direction).Normalized();
        SparkleVector direction = velocity.Choice(SparkleCatalog.VelocityMode) switch
        {
            "Cone" => InCone(axis.Length == 0 ? new SparkleVector(0, 1, 0) : axis, velocity.Number(SparkleCatalog.ConeAngle) * Math.PI / 180),
            "Radial" => fromCenter.Length > 1e-9 ? fromCenter.Normalized() : RandomUnit(),
            _ => axis,
        };
        return direction * speed;
    }

    /// <summary>Random direction at most <paramref name="angle"/> radians away from <paramref name="axis"/> (uniform on the cap).</summary>
    private SparkleVector InCone(SparkleVector axis, double angle)
    {
        double cos = 1 - _random.NextDouble() * (1 - Math.Cos(angle));
        double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
        double turn = _random.NextDouble() * 2 * Math.PI;
        SparkleVector helper = Math.Abs(axis.Y) < 0.99 ? new SparkleVector(0, 1, 0) : new SparkleVector(1, 0, 0);
        SparkleVector u = Cross(axis, helper).Normalized();
        SparkleVector v = Cross(axis, u);
        return axis * cos + u * (sin * Math.Cos(turn)) + v * (sin * Math.Sin(turn));
    }

    private static SparkleVector Cross(SparkleVector a, SparkleVector b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private SparkleVector RandomUnit()
    {
        double y = _random.NextDouble() * 2 - 1;
        double turn = _random.NextDouble() * 2 * Math.PI;
        double r = Math.Sqrt(1 - y * y);
        return new SparkleVector(r * Math.Cos(turn), y, r * Math.Sin(turn));
    }

    private double Between(double a, double b) => Math.Min(a, b) + _random.NextDouble() * Math.Abs(b - a);

    private void Update(SparkleParticle particle, double delta)
    {
        SparkleEmitter emitter = _emitters[particle.Emitter];
        particle.Age += delta;
        if (emitter.Uses(SparkleModuleKind.Gravity))
        {
            particle.Velocity += emitter.Get(SparkleModuleKind.Gravity).Vector(SparkleCatalog.Acceleration) * delta;
        }

        particle.Position += particle.Velocity * delta;
        if (Ground is { } ground && emitter.Uses(SparkleModuleKind.Collision) && particle.Position.Y < ground)
        {
            SparkleModule collision = emitter.Get(SparkleModuleKind.Collision);
            if (collision.Boolean(SparkleCatalog.DieOnCollision))
            {
                particle.Dead = true;
            }

            particle.Position = particle.Position with { Y = ground };
            particle.Velocity = particle.Velocity with { Y = -particle.Velocity.Y * collision.Number(SparkleCatalog.Bounce) };
        }

        particle.Size = Size(emitter, particle);
    }

    /// <summary>Texture drawn for a particle: its own (Random), or the one of its age (Animated over the life).</summary>
    public static int SpriteOf(SparkleEmitter emitter, SparkleParticle particle, int textures)
    {
        if (textures <= 1)
        {
            return 0;
        }

        return emitter.Get(SparkleModuleKind.Texture).Choice(SparkleCatalog.SpriteMode) == "OverLife"
            ? Math.Min(textures - 1, (int)(particle.Progress * textures))
            : Math.Clamp(particle.Sprite, 0, textures - 1);
    }

    /// <summary>Size of a particle according to the Scale module.</summary>
    public static double Size(SparkleEmitter emitter, SparkleParticle particle)
    {
        SparkleModule scale = emitter.Get(SparkleModuleKind.Scale);
        return scale.Choice(SparkleCatalog.ScaleMode) switch
        {
            "OverLife" => scale.Number(SparkleCatalog.Start) + (scale.Number(SparkleCatalog.End) - scale.Number(SparkleCatalog.Start)) * particle.Progress,
            "Random" => particle.RandomScale,
            _ => scale.Number(SparkleCatalog.Value),
        };
    }

    /// <summary>Time events due since the birth of the particle, and the entry into the zone of its location event.</summary>
    private void CollectEvents(SparkleParticle particle, List<(SparkleParticle, SparkleEventKind)> events)
    {
        SparkleEmitter emitter = _emitters[particle.Emitter];
        if (emitter.Uses(SparkleModuleKind.GenerateTimeEvent))
        {
            SparkleModule time = emitter.Get(SparkleModuleKind.GenerateTimeEvent);
            double interval = Math.Max(0.05, time.Number(SparkleCatalog.EventTime));
            int due = (int)Math.Floor(particle.Age / interval + 1e-9);
            if (time.Choice(SparkleCatalog.TimeMode) != "Repeat")
            {
                due = Math.Min(1, due);
            }

            for (; particle.TimeEvents < due; particle.TimeEvents++)
            {
                events.Add((particle, SparkleEventKind.Time));
            }
        }

        if (emitter.Uses(SparkleModuleKind.GenerateLocationEvent))
        {
            bool inside = InZone(emitter.Get(SparkleModuleKind.GenerateLocationEvent), particle.Position);
            if (inside && !particle.InZone)
            {
                events.Add((particle, SparkleEventKind.Location));
            }

            particle.InZone = inside;
        }
    }

    /// <summary>True if the point is in the zone of a Generate location event module (sphere or box around its center).</summary>
    public static bool InZone(SparkleModule zone, SparkleVector point)
    {
        SparkleVector offset = point - zone.Vector(SparkleCatalog.ZoneCenter);
        if (zone.Choice(SparkleCatalog.ZoneShape) == "Box")
        {
            SparkleVector size = zone.Vector(SparkleCatalog.ZoneSize);
            return Math.Abs(offset.X) <= size.X / 2 && Math.Abs(offset.Y) <= size.Y / 2 && Math.Abs(offset.Z) <= size.Z / 2;
        }

        return offset.Length <= zone.Number(SparkleCatalog.ZoneRadius);
    }

    /// <summary>
    /// Sends an event of a particle (with the chance of its module): the emitters that receive this
    /// kind of event from its emitter spawn their particles, where it is or at the center.
    /// </summary>
    private void Send(SparkleParticle particle, SparkleEventKind kind)
    {
        SparkleEmitter emitter = _emitters[particle.Emitter];
        if (!emitter.Sends(kind)
            || _random.NextDouble() * 100 >= emitter.Get(SparkleEmitter.GeneratorOf(kind)).Number(SparkleCatalog.Chance))
        {
            return;
        }

        Events++;
        if (kind == SparkleEventKind.Death)
        {
            DeathEvents++;
        }

        int id = emitter.EventId;
        for (int i = 0; i < _emitters.Count; i++)
        {
            SparkleEmitter receiver = _emitters[i];
            if (i == particle.Emitter || !_states[i].Included || receiver.EventSource != id || receiver.ReceivedEvent != kind)
            {
                continue;
            }

            SparkleModule receive = receiver.Get(SparkleModuleKind.ReceiveDeathEvent);
            SparkleVector? at = receive.Boolean(SparkleCatalog.AtDeathLocation) ? particle.Position : null;
            for (int n = 0; n < receive.Integer(SparkleCatalog.CountPerEvent); n++)
            {
                Spawn(i, at, particle.Velocity);
            }
        }
    }

}
