namespace MineEngine.Assets.Sparkle;

/// <summary>Weight of an emitter or a system on the computer of the player.</summary>
public enum SparkleCost
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Estimate of the resources taken by an emitter (Performance module): particles alive at the same
/// time, work of the processor on every tick (collisions cost more) and squares drawn by the
/// graphics card (translucent ones cost more).
/// </summary>
public sealed record SparklePerformance(int MaxParticles, double CpuLoad, double GpuLoad)
{
    /// <summary>Above this number of particles alive, Minecraft starts removing the oldest ones (16384 in all).</summary>
    public const int EngineLimit = 16384;

    public SparkleCost CpuCost => Cost(CpuLoad);

    public SparkleCost GpuCost => Cost(GpuLoad);

    public SparkleCost Cost(double load) => load switch
    {
        < 500 => SparkleCost.Low,
        < 3000 => SparkleCost.Medium,
        _ => SparkleCost.High,
    };

    public static string CostLabel(SparkleCost cost) => cost switch
    {
        SparkleCost.Low => Tr.T("Low"),
        SparkleCost.Medium => Tr.T("Medium"),
        _ => Tr.T("High"),
    };

    /// <summary>Summary shown in the Performance module.</summary>
    public string Summary => Tr.F("Up to {0} particles at the same time. Processor: {1}. Graphics card: {2}.",
        MaxParticles, CostLabel(CpuCost), CostLabel(GpuCost));

    public static SparklePerformance operator +(SparklePerformance a, SparklePerformance b) =>
        new(a.MaxParticles + b.MaxParticles, a.CpuLoad + b.CpuLoad, a.GpuLoad + b.GpuLoad);

    /// <summary>Estimate of every emitter of the system, in the order of the list.</summary>
    public static IReadOnlyList<SparklePerformance> Estimate(IReadOnlyList<SparkleEmitter> emitters)
    {
        var alive = new Dictionary<Guid, int>();
        // An emitter driven by events depends on its source: resolved in passes (no loop is followed twice).
        for (int pass = 0; pass <= emitters.Count; pass++)
        {
            foreach (SparkleEmitter emitter in emitters)
            {
                alive[emitter.Id] = MaxAlive(emitter, emitters, alive);
            }
        }

        return [.. emitters.Select(e => Load(e, alive[e.Id]))];
    }

    private static int MaxAlive(SparkleEmitter emitter, IReadOnlyList<SparkleEmitter> emitters, Dictionary<Guid, int> known)
    {
        SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
        double lifetime = Math.Max(spawn.Number(SparkleCatalog.LifetimeMin), spawn.Number(SparkleCatalog.LifetimeMax));
        if (emitter.EventSource is { } source && emitter.ReceivedEvent is { } kind)
        {
            // Every event of a particle of the source spawns some here, during the life of these ones.
            SparkleEmitter? sender = emitters.FirstOrDefault(e => e.EventId == source && e.Id != emitter.Id && e.Sends(kind));
            if (sender is null || !known.TryGetValue(sender.Id, out int senderAlive))
            {
                return 0;
            }

            SparkleModule senderSpawn = sender.Get(SparkleModuleKind.SpawnParticles);
            double senderLifetime = Math.Max(0.05, Math.Max(senderSpawn.Number(SparkleCatalog.LifetimeMin), senderSpawn.Number(SparkleCatalog.LifetimeMax)));
            SparkleModule generator = sender.Get(SparkleEmitter.GeneratorOf(kind));
            double chance = generator.Number(SparkleCatalog.Chance) / 100;
            double count = emitter.Get(SparkleModuleKind.ReceiveDeathEvent).Number(SparkleCatalog.CountPerEvent);
            // A repeated time event spawns again at every interval: as many bursts alive as intervals in a life of these particles.
            double bursts = kind == SparkleEventKind.Time && generator.Choice(SparkleCatalog.TimeMode) == "Repeat"
                ? Math.Max(1, lifetime / Math.Max(0.05, generator.Number(SparkleCatalog.EventTime)))
                : Math.Max(1, lifetime / senderLifetime);
            return Clamp(senderAlive * chance * count * bursts);
        }

        double total = spawn.Number(SparkleCatalog.Count);
        if (spawn.Choice(SparkleCatalog.SpawnMode) == "Burst")
        {
            return Clamp(total);
        }

        double duration = Math.Max(0.05, emitter.Get(SparkleModuleKind.Lifecycle).Number(SparkleCatalog.Duration));
        return Clamp(Math.Min(total, total / duration * lifetime));
    }

    private static int Clamp(double count) => (int)Math.Min(EngineLimit, Math.Ceiling(Math.Max(0, count)));

    private static SparklePerformance Load(SparkleEmitter emitter, int alive)
    {
        double cpu = alive * (emitter.Uses(SparkleModuleKind.Collision) ? 4 : 1) * (Enum.GetValues<SparkleEventKind>().Any(emitter.Sends) ? 1.2 : 1);
        double gpu = alive * (emitter.Get(SparkleModuleKind.Texture).Boolean(SparkleCatalog.Translucent) ? 1.5 : 1);
        return new SparklePerformance(alive, cpu, gpu);
    }
}
