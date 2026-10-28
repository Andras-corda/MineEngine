using MineEngine.Assets.Sparkle;
using MineEngine.Core.Assets;
using MineEngine.Core.Diagnostics;
using MineEngine.Core.Identifiers;

namespace MineEngine.Assets;

/// <summary>
/// Sparkle particle system (V0.7.5): emitters side by side, like Unreal's Niagara systems. Each
/// emitter has a head (shown in the preview or not, name) and a body of modules (lifecycle, spawn,
/// velocity, gravity, collision, scale, texture, death events). The graphs play the system at a
/// position or on a creature. The list is immutable: replacing it is a single change, undoable
/// like the others.
/// </summary>
public sealed class SparkleAsset : Asset
{
    private IReadOnlyList<SparkleEmitter> _emitters = [];
    private IReadOnlyList<SparkleUserParameter> _userParameters = [];
    private IReadOnlyList<SparkleNote> _notes = [];

    public SparkleAsset(Guid id, ResourceId resourceId, string displayName)
        : base(id, resourceId, displayName)
    {
    }

    public override AssetType Type => AssetType.Sparkle;

    public IReadOnlyList<SparkleEmitter> Emitters
    {
        get => _emitters;
        set => SetField(ref _emitters, value ?? []);
    }

    /// <summary>Named values of the system that module settings are linked to (V0.7.6).</summary>
    public IReadOnlyList<SparkleUserParameter> UserParameters
    {
        get => _userParameters;
        set => SetField(ref _userParameters, value ?? []);
    }

    /// <summary>Comments and tooltips of the canvas (V0.7.6).</summary>
    public IReadOnlyList<SparkleNote> Notes
    {
        get => _notes;
        set => SetField(ref _notes, value ?? []);
    }

    /// <summary>The emitters with the values of the user parameters in their linked settings: what plays.</summary>
    public IReadOnlyList<SparkleEmitter> ResolvedEmitters => SparkleUserParameter.Resolve(Emitters, UserParameters);

    /// <summary>First emitter of a new system: a burst of stars that rise and fall.</summary>
    public void ApplyStarterSystem()
    {
        SparkleEmitter sparks = SparkleEmitter.Create(Tr.T("Sparks"), 1)
            .With(SparkleModuleKind.Velocity, m => m.WithValue(SparkleCatalog.VelocityMode, "Cone").WithValue(SparkleCatalog.SpeedMin, "3").WithValue(SparkleCatalog.SpeedMax, "5"))
            .With(SparkleModuleKind.Lifecycle, m => m.WithValue(SparkleCatalog.LoopMode, "Infinite"))
            .With(SparkleModuleKind.Scale, m => m.WithValue(SparkleCatalog.ScaleMode, "OverLife"));
        Emitters = [sparks];
    }

    /// <summary>Lowest event identifier not used by an emitter of the system.</summary>
    public int NextEventId()
    {
        var used = Emitters.Select(e => e.EventId).ToHashSet();
        int id = 1;
        while (used.Contains(id))
        {
            id++;
        }

        return id;
    }

    public SparkleEmitter? FindEmitter(Guid id) => _emitters.FirstOrDefault(e => e.Id == id);

    public override IEnumerable<AssetReference> GetReferences()
    {
        foreach (SparkleEmitter emitter in Emitters)
        {
            foreach (string sprite in SparkleCatalog.SplitTextures(emitter.Get(SparkleModuleKind.Texture).GetValue(SparkleCatalog.Sprite)))
            {
                if (ContentReference.TryParseStorageText(sprite, out ContentReference? texture) && texture!.Kind == ContentReferenceKind.Asset)
                {
                    yield return new AssetReference(texture.AssetId, AssetReferenceKind.Texture, Tr.F("texture of the emitter \"{0}\"", emitter.Name));
                }
            }
        }
    }

    public override void Validate(DiagnosticBag diagnostics)
    {
        base.Validate(diagnostics);
        string source = ToString();
        void Add(DiagnosticSeverity severity, string message) => diagnostics.Add(new Diagnostic(severity, message, source, Id));

        if (Emitters.Count == 0)
        {
            Add(DiagnosticSeverity.Warning, Tr.T("The system has no emitter: it shows nothing."));
        }

        foreach (IGrouping<int, SparkleEmitter> twins in Emitters.GroupBy(e => e.EventId).Where(g => g.Count() > 1))
        {
            Add(DiagnosticSeverity.Error, Tr.F("Several emitters have the ID {0}: {1}.", twins.Key, string.Join(", ", twins.Select(e => e.Name))));
        }

        foreach (IGrouping<string, SparkleUserParameter> twins in UserParameters.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            Add(DiagnosticSeverity.Warning, Tr.F("Several user parameters are named \"{0}\".", twins.Key));
        }

        foreach (SparkleEmitter emitter in Emitters)
        {
            foreach (SparkleModule module in emitter.Modules)
            {
                foreach ((string name, string value) in module.Values)
                {
                    if (SparkleUserParameter.BoundId(value) is not { } id || module.Info.FindParameter(name) is not { } info)
                    {
                        continue;
                    }

                    SparkleUserParameter? parameter = UserParameters.FirstOrDefault(p => p.Id == id);
                    if (parameter is null)
                    {
                        Add(DiagnosticSeverity.Error, Tr.F("Emitter \"{0}\": the setting \"{1}\" is linked to a user parameter that no longer exists.", emitter.Name, info.Label));
                    }
                    else if (!parameter.Accepts(info.Type))
                    {
                        Add(DiagnosticSeverity.Warning, Tr.F("Emitter \"{0}\": the setting \"{1}\" is linked to \"{2}\", which has another type.", emitter.Name, info.Label, parameter.Name));
                    }
                }
            }
        }

        IReadOnlyList<SparkleEmitter> resolved = ResolvedEmitters;
        foreach (SparkleEmitter emitter in resolved)
        {
            if (string.IsNullOrWhiteSpace(emitter.Name))
            {
                Add(DiagnosticSeverity.Warning, Tr.T("An emitter has no name."));
            }

            SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
            if (spawn.Number(SparkleCatalog.LifetimeMin) > spawn.Number(SparkleCatalog.LifetimeMax))
            {
                Add(DiagnosticSeverity.Warning, Tr.F("Emitter \"{0}\": the minimum lifetime is above the maximum.", emitter.Name));
            }

            SparkleModule velocity = emitter.Get(SparkleModuleKind.Velocity);
            if (velocity.Number(SparkleCatalog.SpeedMin) > velocity.Number(SparkleCatalog.SpeedMax))
            {
                Add(DiagnosticSeverity.Warning, Tr.F("Emitter \"{0}\": the minimum speed is above the maximum.", emitter.Name));
            }

            if (emitter.EventSource is { } sourceId)
            {
                SparkleEmitter? sender = resolved.FirstOrDefault(e => e.EventId == sourceId && e.Id != emitter.Id);
                if (sender is null)
                {
                    Add(DiagnosticSeverity.Error, Tr.F("Emitter \"{0}\" receives the events of the ID {1}, but no other emitter has it.", emitter.Name, sourceId));
                }
                else if (emitter.ReceivedEvent is { } kind && !sender.Sends(kind))
                {
                    Add(DiagnosticSeverity.Warning, kind switch
                    {
                        SparkleEventKind.Time => Tr.F("Emitter \"{0}\" receives the time events of \"{1}\", which does not generate any.", emitter.Name, sender.Name),
                        SparkleEventKind.Location => Tr.F("Emitter \"{0}\" receives the location events of \"{1}\", which does not generate any.", emitter.Name, sender.Name),
                        _ => Tr.F("Emitter \"{0}\" receives the death events of \"{1}\", which does not generate any.", emitter.Name, sender.Name),
                    });
                }
            }
        }

        if (HasEventLoop())
        {
            Add(DiagnosticSeverity.Warning, Tr.T("The events of the emitters form a loop: the particles could multiply without end."));
        }

        if (SparklePerformance.Estimate(resolved).Aggregate(new SparklePerformance(0, 0, 0), (a, b) => a + b).MaxParticles >= SparklePerformance.EngineLimit)
        {
            Add(DiagnosticSeverity.Warning, Tr.F("The system can have more than {0} particles at once: Minecraft will remove some.", SparklePerformance.EngineLimit));
        }
    }

    /// <summary>True if following the death events from an emitter can come back to it.</summary>
    public bool HasEventLoop()
    {
        foreach (SparkleEmitter start in Emitters)
        {
            var seen = new HashSet<Guid>();
            SparkleEmitter? current = start;
            while (current is not null && seen.Add(current.Id))
            {
                // Each emitter receives from one source at most: going up the sources is a chain.
                current = current.EventSource is { } source ? Emitters.FirstOrDefault(e => e.EventId == source && e.Id != current.Id) : null;
                if (current?.Id == start.Id)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
