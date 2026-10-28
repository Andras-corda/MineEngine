using System.Globalization;
using System.Windows.Input;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.Mvvm;

namespace MineEngine.Editor.ViewModels.Sparkle;

/// <summary>
/// Playback of the preview, driven by the timeline: play forward or backward, pause, stop, step
/// frame by frame, scrub. The simulation advances by fixed steps from a fixed seed, so a moment of
/// the timeline always shows the same particles: going back simulates again from the start.
/// </summary>
public sealed class SparklePlayer : ObservableObject
{
    /// <summary>Length of a step of the simulation (the timeline moves frame by frame).</summary>
    public const double Step = 1.0 / 60;

    /// <summary>Length of the timeline for a system that never ends (an emitter loops forever).</summary>
    public const double InfiniteWindow = 10;

    /// <summary>Seconds shown at the end before playing again from the start (or from the end, backward).</summary>
    private const double LoopPause = 0.5;

    private const int Seed = 7;

    private IReadOnlyList<SparkleEmitter> _emitters = [];
    private SparkleSimulation _simulation;
    private int _tick;
    private int _lastTick;
    private bool _isPlaying = true;
    private bool _isReversed;
    private bool _isLooping = true;
    private bool _isInfinite;
    private double _pending;
    private double _waited;
    private TimeSpan? _lastFrame;

    public SparklePlayer()
    {
        _simulation = Create();
        PlayCommand = new RelayCommand(() => Play(reversed: false));
        ReverseCommand = new RelayCommand(() => Play(reversed: true));
        PauseCommand = new RelayCommand(() => IsPlaying = false);
        PlayPauseCommand = new RelayCommand(() =>
        {
            if (IsPlaying)
            {
                IsPlaying = false;
            }
            else
            {
                Play(_isReversed);
            }
        });
        StopCommand = new RelayCommand(() =>
        {
            IsPlaying = false;
            SeekTick(0);
        });
        StepForwardCommand = new RelayCommand(() =>
        {
            IsPlaying = false;
            SeekTick(_tick + 1);
        });
        StepBackCommand = new RelayCommand(() =>
        {
            IsPlaying = false;
            SeekTick(_tick - 1);
        });
        ToStartCommand = new RelayCommand(() => SeekTick(0));
        ToEndCommand = new RelayCommand(() => SeekTick(_lastTick));
    }

    /// <summary>The particles changed (a frame was played, the timeline moved, the system changed).</summary>
    public event EventHandler? FrameChanged;

    /// <summary>Emitters played (the preview draws their textures).</summary>
    public IReadOnlyList<SparkleEmitter> Emitters => _emitters;

    public SparkleSimulation Simulation => _simulation;

    /// <summary>Moment shown, in seconds; setting it (the slider of the timeline) pauses there.</summary>
    public double Time
    {
        get => _tick * Step;
        set
        {
            IsPlaying = false;
            SeekTick((int)Math.Round(value / Step));
        }
    }

    /// <summary>Length of the timeline: until the last particle dies, or a window for a system that never ends.</summary>
    public double Duration => _lastTick * Step;

    /// <summary>An emitter loops forever: the timeline shows its first seconds.</summary>
    public bool IsInfinite
    {
        get => _isInfinite;
        private set => SetProperty(ref _isInfinite, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                _pending = 0;
                _waited = 0;
                _lastFrame = null;
            }
        }
    }

    /// <summary>Plays backward.</summary>
    public bool IsReversed
    {
        get => _isReversed;
        private set => SetProperty(ref _isReversed, value);
    }

    /// <summary>At the end, plays again from the start; otherwise stops there.</summary>
    public bool IsLooping
    {
        get => _isLooping;
        set => SetProperty(ref _isLooping, value);
    }

    public string TimeText => string.Create(CultureInfo.CurrentCulture, $"{Time:0.00} s / {Duration:0.00} s");

    public int ParticleCount => _simulation.Particles.Count;

    public string ParticleText => ParticleCount == 1 ? Tr.T("1 particle") : Tr.F("{0} particles", ParticleCount);

    public ICommand PlayCommand { get; }

    public ICommand ReverseCommand { get; }

    public ICommand PauseCommand { get; }

    public ICommand PlayPauseCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand StepForwardCommand { get; }

    public ICommand StepBackCommand { get; }

    public ICommand ToStartCommand { get; }

    public ICommand ToEndCommand { get; }

    /// <summary>The system changed: the same moment is simulated again with the new settings.</summary>
    public void Load(IReadOnlyList<SparkleEmitter> emitters)
    {
        _emitters = emitters ?? [];
        double? length = SparkleSimulation.Length(_emitters, e => e.Preview);
        IsInfinite = length is null;
        _lastTick = Math.Max(30, (int)Math.Ceiling((length ?? InfiniteWindow) / Step));
        OnPropertyChanged(nameof(Emitters));
        OnPropertyChanged(nameof(Duration));
        Rebuild(Math.Min(_tick, _lastTick));
    }

    /// <summary>
    /// Called by the preview at each frame of the screen (several previews of the same frame count
    /// once): plays the time elapsed since the previous frame.
    /// </summary>
    public void Advance(TimeSpan frameTime)
    {
        if (_lastFrame == frameTime)
        {
            return;
        }

        double elapsed = _lastFrame is { } last ? Math.Clamp((frameTime - last).TotalSeconds, 0, 0.1) : 0;
        _lastFrame = frameTime;
        if (!_isPlaying || elapsed <= 0)
        {
            return;
        }

        int end = _isReversed ? 0 : _lastTick;
        if (_tick == end)
        {
            // At the end: a short pause, then again from the other end (or the playback stops).
            _waited += elapsed;
            if (!_isLooping)
            {
                IsPlaying = false;
            }
            else if (_waited >= LoopPause)
            {
                _waited = 0;
                _pending = 0;
                SeekTick(_isReversed ? _lastTick : 0);
            }

            return;
        }

        _pending += elapsed;
        int steps = (int)(_pending / Step);
        if (steps == 0)
        {
            return;
        }

        _pending -= steps * Step;
        SeekTick(_isReversed ? Math.Max(0, _tick - steps) : Math.Min(_lastTick, _tick + steps));
    }

    private void Play(bool reversed)
    {
        IsReversed = reversed;
        if (_tick == (reversed ? 0 : _lastTick))
        {
            SeekTick(reversed ? _lastTick : 0);
        }

        IsPlaying = true;
    }

    /// <summary>Shows a moment: forward from the current one, or simulated again from the start to go back.</summary>
    private void SeekTick(int tick)
    {
        tick = Math.Clamp(tick, 0, _lastTick);
        if (tick < _tick)
        {
            Rebuild(tick);
            return;
        }

        for (; _tick < tick; _tick++)
        {
            _simulation.Step(Step);
        }

        Changed();
    }

    private void Rebuild(int tick)
    {
        _simulation = Create();
        _tick = 0;
        for (; _tick < tick; _tick++)
        {
            _simulation.Step(Step);
        }

        Changed();
    }

    private SparkleSimulation Create() => new(_emitters, e => e.Preview, seed: Seed);

    private void Changed()
    {
        OnPropertyChanged(nameof(Time));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(ParticleCount));
        OnPropertyChanged(nameof(ParticleText));
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }
}
