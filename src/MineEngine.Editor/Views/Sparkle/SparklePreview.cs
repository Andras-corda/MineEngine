using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MineEngine.Assets.Sparkle;
using MineEngine.Editor.ViewModels.Sparkle;

namespace MineEngine.Editor.Views.Sparkle;

/// <summary>
/// Animated 3D preview of a Sparkle system: draws the particles of the player (the emitters whose
/// box is checked) as squares that face the camera, over a grid of the ground (one square per
/// block), and plays the timeline at each frame of the screen. Drag to turn around, wheel to come
/// closer. Its right-click menu adds helpers: the axes in a corner, the zones of the location
/// events, the ends of the beams, the direction (or cone) of the velocity, the collision boxes.
/// </summary>
public sealed class SparklePreview : FrameworkElement
{
    public static readonly DependencyProperty PlayerProperty = DependencyProperty.Register(
        nameof(Player), typeof(SparklePlayer), typeof(SparklePreview),
        new FrameworkPropertyMetadata(null, (d, e) => ((SparklePreview)d).OnPlayerChanged((SparklePlayer?)e.OldValue, (SparklePlayer?)e.NewValue)));

    public static readonly DependencyProperty SpriteResolverProperty = DependencyProperty.Register(
        nameof(SpriteResolver), typeof(Func<string, string?>), typeof(SparklePreview), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty ShowAxesProperty = Helper(nameof(ShowAxes));

    public static readonly DependencyProperty ShowZonesProperty = Helper(nameof(ShowZones));

    public static readonly DependencyProperty ShowBeamsProperty = Helper(nameof(ShowBeams));

    public static readonly DependencyProperty ShowVelocityProperty = Helper(nameof(ShowVelocity));

    public static readonly DependencyProperty ShowCollisionProperty = Helper(nameof(ShowCollision));

    /// <summary>Particles of colliding emitters that get a box, at most (beyond, the preview would slow down).</summary>
    private const int MaxCollisionBoxes = 150;

    private static readonly Color AxisX = Color.FromRgb(0xE5, 0x48, 0x4D);
    private static readonly Color AxisY = Color.FromRgb(0x7C, 0xC4, 0x6A);
    private static readonly Color AxisZ = Color.FromRgb(0x4C, 0x8D, 0xF6);

    private static DependencyProperty Helper(string name) => DependencyProperty.Register(
        name, typeof(bool), typeof(SparklePreview), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly Dictionary<(string Sprite, int Tint), ImageSource?> _sprites = [];
    private readonly Pen _zonePen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(200, 0xF0, 0xA0, 0x40)), 1.2));
    private readonly Pen _beamPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(180, 0x7F, 0xE7, 0xFF)), 1.2) { DashStyle = DashStyles.Dash });
    private readonly Pen _velocityPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(220, 0xFF, 0xE0, 0x66)), 1.6));
    private readonly Pen _collisionPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(150, 0xFF, 0x66, 0x66)), 1));
    private readonly Pen _gridPen;
    private readonly Pen _axisPen;
    private double _yaw = 0.6;
    private double _pitch = 0.35;
    private double _distance = 9;
    private Point? _drag;
    private bool _rendering;

    public SparklePreview()
    {
        ClipToBounds = true;
        Focusable = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        _gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 160, 170, 190)), 1);
        _gridPen.Freeze();
        _axisPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 160, 170, 190)), 1.5);
        _axisPen.Freeze();
        Loaded += (_, _) => StartRendering();
        Unloaded += (_, _) => StopRendering();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                StartRendering();
            }
            else
            {
                StopRendering();
            }
        };
    }

    public bool ShowAxes
    {
        get => (bool)GetValue(ShowAxesProperty);
        set => SetValue(ShowAxesProperty, value);
    }

    public bool ShowZones
    {
        get => (bool)GetValue(ShowZonesProperty);
        set => SetValue(ShowZonesProperty, value);
    }

    public bool ShowBeams
    {
        get => (bool)GetValue(ShowBeamsProperty);
        set => SetValue(ShowBeamsProperty, value);
    }

    public bool ShowVelocity
    {
        get => (bool)GetValue(ShowVelocityProperty);
        set => SetValue(ShowVelocityProperty, value);
    }

    public bool ShowCollision
    {
        get => (bool)GetValue(ShowCollisionProperty);
        set => SetValue(ShowCollisionProperty, value);
    }

    /// <summary>Playback whose particles are drawn.</summary>
    public SparklePlayer? Player
    {
        get => (SparklePlayer?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    /// <summary>File of a texture setting ("asset:guid", "minecraft:flame"), or null to draw a plain square.</summary>
    public Func<string, string?>? SpriteResolver
    {
        get => (Func<string, string?>?)GetValue(SpriteResolverProperty);
        set => SetValue(SpriteResolverProperty, value);
    }

    private void OnPlayerChanged(SparklePlayer? previous, SparklePlayer? player)
    {
        if (previous is not null)
        {
            previous.FrameChanged -= OnFrameChanged;
            previous.PropertyChanged -= OnPlayerPropertyChanged;
        }

        if (player is not null)
        {
            player.FrameChanged += OnFrameChanged;
            player.PropertyChanged += OnPlayerPropertyChanged;
        }

        _sprites.Clear();
        InvalidateVisual();
    }

    private void OnFrameChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>The system changed: its textures are read again.</summary>
    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SparklePlayer.Emitters))
        {
            _sprites.Clear();
        }
    }

    private void StartRendering()
    {
        if (_rendering || !IsVisible)
        {
            return;
        }

        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (_rendering)
        {
            _rendering = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs frame)
        {
            Player?.Advance(frame.RenderingTime);
        }
    }

    // ----- Camera -----

    private bool Inverted(string settingKey) => TryFindResource(settingKey) is true;

    private static Pen Frozen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        _drag = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag is not { } from || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point to = e.GetPosition(this);
        // Settings > Editor: each direction can be inverted.
        double horizontal = Inverted(Services.WpfInterfaceService.SparkleInvertHorizontalKey) ? -1 : 1;
        double vertical = Inverted(Services.WpfInterfaceService.SparkleInvertVerticalKey) ? -1 : 1;
        _yaw += (to.X - from.X) * 0.01 * horizontal;
        _pitch = Math.Clamp(_pitch + (to.Y - from.Y) * 0.01 * vertical, -1.4, 1.4);
        _drag = to;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _drag = null;
        ReleaseMouseCapture();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        bool closer = e.Delta > 0 != Inverted(Services.WpfInterfaceService.SparkleInvertZoomKey);
        _distance = Math.Clamp(_distance * (closer ? 0.9 : 1.1), 2, 80);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Point of the world seen from the camera: x to the right, y up, z depth (positive in front).</summary>
    private (double X, double Y, double Z) ToView(SparkleVector p)
    {
        // The camera turns around the point (0, 1, 0): the middle of the first block above the ground.
        double x = p.X, y = p.Y - 1, z = p.Z;
        double cosYaw = Math.Cos(_yaw), sinYaw = Math.Sin(_yaw);
        double x1 = x * cosYaw - z * sinYaw;
        double z1 = x * sinYaw + z * cosYaw;
        double cosPitch = Math.Cos(_pitch), sinPitch = Math.Sin(_pitch);
        double y2 = y * cosPitch - z1 * sinPitch;
        double z2 = y * sinPitch + z1 * cosPitch;
        return (x1, y2, z2 + _distance);
    }

    private Point? Project((double X, double Y, double Z) view, out double scale)
    {
        scale = 0;
        if (view.Z < 0.1)
        {
            return null;
        }

        double focal = Math.Min(ActualWidth, ActualHeight) * 1.2;
        scale = focal / view.Z;
        return new Point(ActualWidth / 2 + view.X * scale, ActualHeight / 2 - view.Y * scale);
    }

    // ----- Drawing -----

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        DrawGround(dc);
        if (Player is not { } player)
        {
            return;
        }

        SparkleSimulation simulation = player.Simulation;
        IReadOnlyList<SparkleEmitter> emitters = player.Emitters;
        DrawHelpers(dc, emitters);

        var drawn = new List<(double Depth, Point Center, double Size, SparkleParticle Particle)>(simulation.Particles.Count);
        foreach (SparkleParticle particle in simulation.Particles)
        {
            var view = ToView(particle.Position);
            if (Project(view, out double scale) is { } center)
            {
                drawn.Add((view.Z, center, Math.Max(1, particle.Size * scale), particle));
            }
        }

        if (ShowCollision)
        {
            DrawCollisionBoxes(dc, emitters, simulation.Particles);
        }

        // From the farthest to the nearest, like the game does with translucent particles.
        foreach (var (_, center, size, particle) in drawn.OrderByDescending(d => d.Depth))
        {
            SparkleEmitter emitter = emitters[particle.Emitter];
            SparkleModule texture = emitter.Get(SparkleModuleKind.Texture);
            IReadOnlyList<string> textures = SparkleCatalog.SplitTextures(texture.GetValue(SparkleCatalog.Sprite));
            string sprite = textures[SparkleSimulation.SpriteOf(emitter, particle, textures.Count)];
            var rect = new Rect(center.X - size / 2, center.Y - size / 2, size, size);
            if (Sprite(sprite, texture.Color(SparkleCatalog.Tint)) is { } image)
            {
                dc.DrawImage(image, rect);
            }
            else
            {
                dc.DrawRectangle(TintBrush(texture.Color(SparkleCatalog.Tint)), null, rect);
            }
        }
    }

    // ----- Helpers (right-click menu of the preview) -----

    private void DrawHelpers(DrawingContext dc, IReadOnlyList<SparkleEmitter> emitters)
    {
        foreach (SparkleEmitter emitter in emitters.Where(e => e.Preview))
        {
            SparkleModule spawn = emitter.Get(SparkleModuleKind.SpawnParticles);
            SparkleVector start = spawn.Vector(SparkleCatalog.Offset);
            if (ShowZones && emitter.Uses(SparkleModuleKind.GenerateLocationEvent))
            {
                DrawZone(dc, emitter.Get(SparkleModuleKind.GenerateLocationEvent));
            }

            if (ShowBeams && spawn.Choice(SparkleCatalog.Shape) == "Line")
            {
                SparkleVector end = spawn.Vector(SparkleCatalog.LineEnd);
                DrawLine(dc, _beamPen, start, end);
                DrawHandle(dc, start, Color.FromRgb(0x7C, 0xC4, 0x6A), "S");
                DrawHandle(dc, end, Color.FromRgb(0xE5, 0x48, 0x4D), "E");
            }

            // Event-driven emitters spawn where the events happen: their direction has no fixed place.
            if (ShowVelocity && !emitter.IsEventDriven)
            {
                DrawVelocity(dc, emitter.Get(SparkleModuleKind.Velocity), start);
            }
        }

        if (ShowAxes)
        {
            DrawAxes(dc);
        }
    }

    /// <summary>Sphere: its three great circles. Box: its twelve edges.</summary>
    private void DrawZone(DrawingContext dc, SparkleModule zone)
    {
        SparkleVector center = zone.Vector(SparkleCatalog.ZoneCenter);
        if (zone.Choice(SparkleCatalog.ZoneShape) == "Box")
        {
            DrawBox(dc, _zonePen, center, zone.Vector(SparkleCatalog.ZoneSize));
            return;
        }

        double r = zone.Number(SparkleCatalog.ZoneRadius);
        DrawCircle(dc, _zonePen, center, new SparkleVector(r, 0, 0), new SparkleVector(0, r, 0));
        DrawCircle(dc, _zonePen, center, new SparkleVector(r, 0, 0), new SparkleVector(0, 0, r));
        DrawCircle(dc, _zonePen, center, new SparkleVector(0, r, 0), new SparkleVector(0, 0, r));
    }

    /// <summary>Fixed direction: an arrow. Cone: the cone of the directions. From the center: short arrows all around.</summary>
    private void DrawVelocity(DrawingContext dc, SparkleModule velocity, SparkleVector center)
    {
        const double Length = 1.5;
        SparkleVector axis = velocity.Vector(SparkleCatalog.Direction).Normalized();
        if (axis.Length == 0)
        {
            axis = new SparkleVector(0, 1, 0);
        }

        switch (velocity.Choice(SparkleCatalog.VelocityMode))
        {
            case "Cone":
                double angle = Math.Min(80, velocity.Number(SparkleCatalog.ConeAngle)) * Math.PI / 180;
                SparkleVector helper = Math.Abs(axis.Y) < 0.99 ? new SparkleVector(0, 1, 0) : new SparkleVector(1, 0, 0);
                SparkleVector u = Cross(axis, helper).Normalized() * (Length * Math.Tan(angle));
                SparkleVector v = Cross(axis, u.Normalized()) * (Length * Math.Tan(angle));
                SparkleVector baseCenter = center + axis * Length;
                DrawCircle(dc, _velocityPen, baseCenter, u, v);
                for (int i = 0; i < 4; i++)
                {
                    double turn = i * Math.PI / 2;
                    DrawLine(dc, _velocityPen, center, baseCenter + u * Math.Cos(turn) + v * Math.Sin(turn));
                }

                DrawArrow(dc, center, axis, Length);
                break;
            case "Radial":
                foreach (SparkleVector direction in new[] { new SparkleVector(1, 0, 0), new SparkleVector(-1, 0, 0), new SparkleVector(0, 1, 0), new SparkleVector(0, -1, 0), new SparkleVector(0, 0, 1), new SparkleVector(0, 0, -1) })
                {
                    DrawArrow(dc, center, direction, 0.8);
                }

                break;
            default:
                DrawArrow(dc, center, axis, Length);
                break;
        }
    }

    private void DrawArrow(DrawingContext dc, SparkleVector from, SparkleVector direction, double length)
    {
        SparkleVector tip = from + direction * length;
        DrawLine(dc, _velocityPen, from, tip);
        if (Project(ToView(from), out _) is { } a && Project(ToView(tip), out _) is { } b && (b - a).Length > 1)
        {
            // The head is drawn on the screen, so that it always faces the camera.
            Vector back = (a - b) / (a - b).Length * 9;
            var side = new Vector(-back.Y, back.X) * 0.5;
            dc.DrawLine(_velocityPen, b, b + back + side);
            dc.DrawLine(_velocityPen, b, b + back - side);
        }
    }

    /// <summary>A small box around each particle of an emitter that collides with the blocks.</summary>
    private void DrawCollisionBoxes(DrawingContext dc, IReadOnlyList<SparkleEmitter> emitters, IReadOnlyList<SparkleParticle> particles)
    {
        int drawn = 0;
        foreach (SparkleParticle particle in particles)
        {
            if (drawn >= MaxCollisionBoxes)
            {
                return;
            }

            if (emitters[particle.Emitter].Uses(SparkleModuleKind.Collision))
            {
                double side = Math.Max(0.1, particle.Size);
                DrawBox(dc, _collisionPen, particle.Position, new SparkleVector(side, side, side));
                drawn++;
            }
        }
    }

    /// <summary>Orientation of the camera in the bottom left corner, like the gizmo of 3D software: X red, Y green (up), Z blue.</summary>
    private void DrawAxes(DrawingContext dc)
    {
        const double Size = 26;
        var center = new Point(42, ActualHeight - 42);
        var axes = new List<(double Depth, Vector Screen, Color Color, string Label, bool Positive)>();
        foreach ((SparkleVector direction, Color color, string label) in new[]
                 {
                     (new SparkleVector(1, 0, 0), AxisX, "X"),
                     (new SparkleVector(0, 1, 0), AxisY, "Y"),
                     (new SparkleVector(0, 0, 1), AxisZ, "Z"),
                 })
        {
            foreach (int sign in new[] { 1, -1 })
            {
                (double x, double y, double z) = Rotate(direction * sign);
                axes.Add((z, new Vector(x * Size, -y * Size), color, label, sign > 0));
            }
        }

        // From the back to the front: the axes that point at the camera are drawn over the others.
        foreach ((double _, Vector screen, Color color, string label, bool positive) in axes.OrderByDescending(a => a.Depth))
        {
            var brush = new SolidColorBrush(color);
            Point end = center + screen;
            if (positive)
            {
                dc.DrawLine(new Pen(brush, 2), center, end);
                dc.DrawEllipse(brush, null, end, 9, 9);
                var text = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 11, Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(text, new Point(end.X - text.Width / 2, end.Y - text.Height / 2));
            }
            else
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B)), new Pen(brush, 1.5), end, 7, 7);
            }
        }
    }

    private void DrawHandle(DrawingContext dc, SparkleVector point, Color color, string label)
    {
        if (Project(ToView(point), out _) is not { } center)
        {
            return;
        }

        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)), new Pen(Brushes.White, 1), center, 7, 7);
        var text = new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 9, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private void DrawBox(DrawingContext dc, Pen pen, SparkleVector center, SparkleVector size)
    {
        SparkleVector h = size / 2;
        SparkleVector Corner(int i) => center + new SparkleVector((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z);
        for (int i = 0; i < 8; i++)
        {
            foreach (int bit in new[] { 1, 2, 4 })
            {
                if ((i & bit) == 0)
                {
                    DrawLine(dc, pen, Corner(i), Corner(i | bit));
                }
            }
        }
    }

    /// <summary>Circle of center <paramref name="center"/> in the plane of the two radius vectors.</summary>
    private void DrawCircle(DrawingContext dc, Pen pen, SparkleVector center, SparkleVector u, SparkleVector v)
    {
        const int Segments = 40;
        SparkleVector previous = center + u;
        for (int i = 1; i <= Segments; i++)
        {
            double turn = i * 2 * Math.PI / Segments;
            SparkleVector next = center + u * Math.Cos(turn) + v * Math.Sin(turn);
            DrawLine(dc, pen, previous, next);
            previous = next;
        }
    }

    private static SparkleVector Cross(SparkleVector a, SparkleVector b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>A direction turned like the camera (no perspective): x to the right, y up, z away from the camera.</summary>
    private (double X, double Y, double Z) Rotate(SparkleVector d)
    {
        double cosYaw = Math.Cos(_yaw), sinYaw = Math.Sin(_yaw);
        double x1 = d.X * cosYaw - d.Z * sinYaw;
        double z1 = d.X * sinYaw + d.Z * cosYaw;
        double cosPitch = Math.Cos(_pitch), sinPitch = Math.Sin(_pitch);
        return (x1, d.Y * cosPitch - z1 * sinPitch, d.Y * sinPitch + z1 * cosPitch);
    }

    private void DrawGround(DrawingContext dc)
    {
        const int Half = 6;
        for (int i = -Half; i <= Half; i++)
        {
            Pen pen = i == 0 ? _axisPen : _gridPen;
            DrawLine(dc, pen, new SparkleVector(i, 0, -Half), new SparkleVector(i, 0, Half));
            DrawLine(dc, pen, new SparkleVector(-Half, 0, i), new SparkleVector(Half, 0, i));
        }
    }

    private void DrawLine(DrawingContext dc, Pen pen, SparkleVector a, SparkleVector b)
    {
        if (Project(ToView(a), out _) is { } from && Project(ToView(b), out _) is { } to)
        {
            dc.DrawLine(pen, from, to);
        }
    }

    private static readonly Dictionary<int, Brush> TintBrushes = [];

    private static Brush TintBrush(int rgb)
    {
        if (!TintBrushes.TryGetValue(rgb, out Brush? brush))
        {
            brush = new SolidColorBrush(Color.FromArgb(220, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            brush.Freeze();
            TintBrushes[rgb] = brush;
        }

        return brush;
    }

    /// <summary>Image of a texture setting multiplied by the tint (read once), or null when it cannot be read.</summary>
    private ImageSource? Sprite(string reference, int tint)
    {
        if (_sprites.TryGetValue((reference, tint), out ImageSource? cached))
        {
            return cached;
        }

        ImageSource? image = null;
        try
        {
            if (SpriteResolver?.Invoke(reference) is { } file && File.Exists(file))
            {
                var source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.UriSource = new Uri(file);
                source.EndInit();
                image = Tinted(source, tint);
            }
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
        {
            image = null;
        }

        _sprites[(reference, tint)] = image;
        return image;
    }

    private static BitmapSource Tinted(BitmapSource source, int tint)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        // Square sprites only: a strip of animation frames shows its first frame.
        int side = Math.Min(width, height);
        var pixels = new byte[side * side * 4];
        converted.CopyPixels(new Int32Rect(0, 0, side, side), pixels, side * 4, 0);
        double r = ((tint >> 16) & 0xFF) / 255.0, g = ((tint >> 8) & 0xFF) / 255.0, b = (tint & 0xFF) / 255.0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)(pixels[i] * b);
            pixels[i + 1] = (byte)(pixels[i + 1] * g);
            pixels[i + 2] = (byte)(pixels[i + 2] * r);
        }

        BitmapSource tinted = BitmapSource.Create(side, side, 96, 96, PixelFormats.Pbgra32, null, pixels, side * 4);
        tinted.Freeze();
        return tinted;
    }
}
