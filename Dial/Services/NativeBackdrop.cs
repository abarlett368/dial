using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Dial.Models;
using Microsoft.Win32;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;
using CompositionTarget = System.Windows.Media.CompositionTarget;

namespace Dial.Services;

public enum BackdropKind { Opaque, Windows10Acrylic, Windows11Acrylic }

public sealed class NativeBackdrop : IDisposable
{
    private readonly Window _window;
    private readonly IntPtr _handle;
    private readonly bool _allowBackdrop;
    private IntPtr _host;
    [ThreadStatic] private static IntPtr _queueController;
    private Compositor? _compositor;
    private DesktopWindowTarget? _target;
    private Windows.UI.Composition.ContainerVisual? _visual;
    private Windows.UI.Composition.ContainerVisual? _root;
    private EllipseGeometry? _centerGeometry;
    private SpriteVisual? _centerTintVisual;
    private CompositionColorBrush? _centerTintBrush;
    private CompositionPathGeometry? _centerTintPath;
    private CompositionGeometricClip? _centerTintClip;
    private CompositionGeometrySource? _centerTintSource;
    private GeometryGroup? _centerOutlineGeometry;
    private SpriteVisual? _centerOutlineVisual;
    private CompositionColorBrush? _centerOutlineBrush;
    private CompositionPathGeometry? _centerOutlinePath;
    private CompositionGeometricClip? _centerOutlineClip;
    private CompositionGeometrySource? _centerOutlineSource;
    private SpriteVisual? _sectorVisual;
    private CompositionBackdropBrush? _material;
    private bool _synchronizingOrder;
    private bool _unavailable;
    private Vector2 _visualSize;
    private int _maskCount;
    private int _shapeCount;
    private bool _maskShowCenter;
    private Vector2 _maskDpi;
    private GeometryGroup? _sectorSurface;
    private readonly List<ScaleTransform> _sectorTransforms = [];
    private CompositionPathGeometry? _surfacePath;
    private CompositionGeometricClip? _surfaceClip;
    private CompositionGeometrySource? _surfaceSource;
    private bool _surfacePending;
    private Point[][] _contours = [];
    private readonly Dictionary<int, Point[][]> _selectionContours = [];
    private readonly SubclassProcedure _hostProcedure = static (window, message, wParam, lParam, id, data) =>
        message == 0x0084 ? new IntPtr(-1) : DefSubclassProc(window, message, wParam, lParam);

    public NativeBackdrop(Window window, bool allowBackdrop)
    {
        _window = window;
        _allowBackdrop = allowBackdrop;
        _handle = new WindowInteropHelper(window).EnsureHandle();
    }

    public BackdropKind Kind { get; private set; }
    public static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    public void Apply(int count, bool showCenter = true)
    {
        Kind = BackdropKind.Opaque;
        if (_visual is not null) _visual.Opacity = 0;
        if (_shapeCount != count)
        {
            var surface = DialGeometry.CreateSurface(count, false);
            Geometry envelope = surface;
            for (int index = 0; index < count; index++)
            {
                var expanded = DialGeometry.CreateSectorSurface(index, count, DialGeometry.SelectedOutlineThickness).Clone();
                Point origin = DialGeometry.SectorScaleCenter(index, count);
                double scale = DialGeometry.GetSelectedSectorScale(index, count);
                expanded.Transform = new ScaleTransform(scale, scale, origin.X, origin.Y);
                envelope = new CombinedGeometry(GeometryCombineMode.Union, envelope, expanded);
            }
            SetSurface(envelope);
            _selectionContours.Clear();
            _shapeCount = count;
        }
        try
        {
            if (!_allowBackdrop || _unavailable || SystemParameters.HighContrast || !TransparencyEnabled() ||
                Windows.System.Power.PowerManager.EnergySaverStatus == Windows.System.Power.EnergySaverStatus.On)
                return;
            EnsureComposition();
            var dpi = VisualTreeHelper.GetDpi(_window);
            var dpiScale = new Vector2((float)dpi.DpiScaleX, (float)dpi.DpiScaleY);
            if (_maskCount != count || _maskDpi != dpiScale || _maskShowCenter != showCenter)
            {
                ReleaseSectors();
                _sectorSurface = new GeometryGroup { FillRule = FillRule.Nonzero };
                double materialOutline = Math.Max(0, DialGeometry.OutlineThickness -
                    1 / Math.Min(dpi.DpiScaleX, dpi.DpiScaleY));
                for (int index = 0; index < count; index++)
                {
                    // Keep the soft native clip half a physical pixel inside the painted outline.
                    var shape = DialGeometry.CreateSectorSurface(index, count, materialOutline).Clone();
                    shape.Transform = new ScaleTransform(dpi.DpiScaleX, dpi.DpiScaleY);
                    var flattened = shape.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
                    Point sectorOrigin = DialGeometry.SectorScaleCenter(index, count);
                    var sectorTransform = new ScaleTransform(1, 1,
                        sectorOrigin.X * dpi.DpiScaleX, sectorOrigin.Y * dpi.DpiScaleY);
                    flattened.Transform = sectorTransform;
                    _sectorTransforms.Add(sectorTransform);
                    _sectorSurface.Children.Add(flattened);
                }
                if (showCenter)
                {
                    _centerGeometry = new EllipseGeometry();
                    _sectorSurface.Children.Add(_centerGeometry);
                    SetCenterRadius(DialGeometry.CenterRadius, dpi);
                }
                _surfaceSource = new CompositionGeometrySource(_sectorSurface);
                _surfacePath = _compositor!.CreatePathGeometry(new CompositionPath(_surfaceSource.AsGeometry()));
                _surfaceClip = _compositor.CreateGeometricClip(_surfacePath);
                _sectorVisual = _compositor.CreateSpriteVisual();
                _sectorVisual.Brush = _material;
                _sectorVisual.Clip = _surfaceClip;
                _sectorVisual.Size = _visualSize;
                _visual!.Children.InsertAtTop(_sectorVisual);
                if (_centerGeometry is not null)
                {
                    _centerTintSource = new CompositionGeometrySource(CreateCenterFill(dpiScale));
                    _centerTintPath = _compositor.CreatePathGeometry(new CompositionPath(_centerTintSource.AsGeometry()));
                    _centerTintClip = _compositor.CreateGeometricClip(_centerTintPath);
                    _centerTintBrush = _compositor.CreateColorBrush();
                    _centerTintVisual = _compositor.CreateSpriteVisual();
                    _centerTintVisual.Brush = _centerTintBrush;
                    _centerTintVisual.Clip = _centerTintClip;
                    _centerTintVisual.Size = _visualSize;
                    _visual.Children.InsertAtTop(_centerTintVisual);
                    _centerOutlineGeometry = CreateCenterOutline(dpiScale);
                    _centerOutlineSource = new CompositionGeometrySource(_centerOutlineGeometry);
                    _centerOutlinePath = _compositor.CreatePathGeometry(new CompositionPath(_centerOutlineSource.AsGeometry()));
                    _centerOutlineClip = _compositor.CreateGeometricClip(_centerOutlinePath);
                    _centerOutlineBrush = _compositor.CreateColorBrush();
                    _centerOutlineVisual = _compositor.CreateSpriteVisual();
                    _centerOutlineVisual.Brush = _centerOutlineBrush;
                    _centerOutlineVisual.Clip = _centerOutlineClip;
                    _centerOutlineVisual.Size = _visualSize;
                    _visual.Children.InsertAtTop(_centerOutlineVisual);
                }
                _maskCount = count;
                _maskDpi = dpiScale;
                _maskShowCenter = showCenter;
            }
            Kind = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
                ? BackdropKind.Windows11Acrylic : BackdropKind.Windows10Acrylic;
        }
        catch (Exception exception) when (exception is COMException or Win32Exception or ArgumentException
            or TypeLoadException or DllNotFoundException or EntryPointNotFoundException)
        {
            Trace.TraceWarning("Masked system composition is unavailable; using opaque surfaces: {0}", exception);
            if (_visual is not null) _visual.Opacity = 0;
            _unavailable = true;
            ReleaseComposition();
        }
    }

    private void EnsureComposition()
    {
        if (_compositor is not null) return;
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is null && _queueController == IntPtr.Zero)
        {
            var options = new QueueOptions { Size = Marshal.SizeOf<QueueOptions>(), ThreadType = 2, ApartmentType = 2 };
            Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out _queueController));
        }
        if (_host == IntPtr.Zero)
        {
            _host = CreateWindowEx(0x082000A0, "Static", "DialBackdropWindow", 0x80000000,
                0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_host == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            int disableTransitions = 1;
            Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(_host, 3, ref disableTransitions, sizeof(int)));
            if (!SetWindowSubclass(_host, _hostProcedure, new UIntPtr(1), UIntPtr.Zero))
                throw new Win32Exception("The material host could not be made input-transparent.");
        }
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            int enabled = 1;
            Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(_host, 17, ref enabled, sizeof(int)));
        }
        else
        {
            var policy = new AccentPolicy { State = 5 };
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf<AccentPolicy>());
            try
            {
                Marshal.StructureToPtr(policy, memory, false);
                var data = new AttributeData { Attribute = 19, Data = memory, Size = Marshal.SizeOf<AccentPolicy>() };
                if (SetWindowCompositionAttribute(_host, ref data) == 0)
                    throw new Win32Exception("Windows rejected host-backdrop composition.");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        _compositor = new Compositor();
        var compositor = _compositor;
        compositor.As<ICompositorDesktopInterop>().CreateDesktopWindowTarget(_host, true, out IntPtr target);
        try { _target = MarshalInterface<DesktopWindowTarget>.FromAbi(target); }
        finally { Marshal.Release(target); }
        _visual = compositor.CreateContainerVisual();
        _visual.BorderMode = CompositionBorderMode.Soft;
        _visual.Opacity = 0;
        _material = compositor.CreateHostBackdropBrush();
        _root = compositor.CreateContainerVisual();
        _root.Children.InsertAtBottom(_visual);
        _target.Root = _root;
        new WindowInteropHelper(_window).Owner = _host;
    }

    private void SetSurface(Geometry surface)
    {
        _contours = FlattenContours(surface);
    }

    private static Point[][] FlattenContours(Geometry surface) =>
        surface.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute).Figures
            .Select(figure => new[] { figure.StartPoint }.Concat(figure.Segments.SelectMany(segment => segment switch
            {
                PolyLineSegment lines => lines.Points.AsEnumerable(),
                LineSegment line => [line.Point],
                _ => throw new InvalidOperationException("The surface contains an unflattened segment.")
            })).ToArray()).Where(points => points.Length >= 3).ToArray();

    public void Show()
    {
        if (Kind == BackdropKind.Opaque) return;
        if (!GetWindowRect(_handle, out NativeRect rectangle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!SetWindowPos(_host, _handle, rectangle.Left, rectangle.Top,
            rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, 0x0050))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Hide()
    {
        if (_host != IntPtr.Zero) ShowWindow(_host, 0);
    }

    public void SynchronizeOrder()
    {
        if (_synchronizingOrder || _host == IntPtr.Zero || !IsWindowVisible(_host) ||
            GetWindow(_handle, 2) == _host) return;
        _synchronizingOrder = true;
        try
        {
            if (!SetWindowPos(_host, _handle, 0, 0, 0, 0, 0x0213))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { _synchronizingOrder = false; }
    }

    public void SetScale(double scale, double opacity, DpiScale dpi)
    {
        if (_visual is not null)
        {
            var size = new Vector2((float)(DialGeometry.Size * dpi.DpiScaleX), (float)(DialGeometry.Size * dpi.DpiScaleY));
            if (size != _visualSize)
            {
                _visualSize = size;
                _visual.Size = size;
                _visual.CenterPoint = new Vector3(size / 2, 0);
                if (_sectorVisual is not null) _sectorVisual.Size = size;
                if (_centerTintVisual is not null) _centerTintVisual.Size = size;
                if (_centerOutlineVisual is not null) _centerOutlineVisual.Size = size;
            }
            _visual.Scale = new Vector3((float)scale, (float)scale, 1);
            _visual.Opacity = Kind == BackdropKind.Opaque ? 0 : (float)opacity;
        }
    }

    public void SetSectorScale(int index, double scale)
    {
        if (index >= 0 && index < _sectorTransforms.Count)
        {
            var transform = _sectorTransforms[index];
            if (Math.Abs(transform.ScaleX - scale) > 0.0000001)
            {
                transform.ScaleX = transform.ScaleY = scale;
                QueueSurfaceUpdate();
            }
        }
    }

    private void QueueSurfaceUpdate()
    {
        if (_surfacePending) return;
        _surfacePending = true;
        CompositionTarget.Rendering += UpdateSectorSurface;
    }

    private void UpdateSectorSurface(object? sender, EventArgs args)
    {
        CompositionTarget.Rendering -= UpdateSectorSurface;
        _surfacePending = false;
        if (_sectorSurface is null || _surfacePath is null) return;
        var source = new CompositionGeometrySource(_sectorSurface);
        bool committed = false;
        try
        {
            _surfacePath.Path = new CompositionPath(source.AsGeometry());
            var previous = _surfaceSource;
            _surfaceSource = source;
            committed = true;
            previous?.Dispose();
        }
        finally { if (!committed) source.Dispose(); }
        if (_centerGeometry is not null && _centerTintPath is not null)
        {
            var centerSource = new CompositionGeometrySource(CreateCenterFill(_maskDpi));
            bool centerCommitted = false;
            try
            {
                _centerTintPath.Path = new CompositionPath(centerSource.AsGeometry());
                var previous = _centerTintSource;
                _centerTintSource = centerSource;
                centerCommitted = true;
                previous?.Dispose();
            }
            finally { if (!centerCommitted) centerSource.Dispose(); }
            if (_centerOutlinePath is not null)
            {
                var geometry = CreateCenterOutline(_maskDpi);
                var outlineSource = new CompositionGeometrySource(geometry);
                bool outlineCommitted = false;
                try
                {
                    _centerOutlinePath.Path = new CompositionPath(outlineSource.AsGeometry());
                    var previous = _centerOutlineSource;
                    _centerOutlineSource = outlineSource;
                    _centerOutlineGeometry = geometry;
                    outlineCommitted = true;
                    previous?.Dispose();
                }
                finally { if (!outlineCommitted) outlineSource.Dispose(); }
            }
        }
    }

    private GeometryGroup CreateCenterOutline(Vector2 dpi)
    {
        var outer = _centerGeometry!.Clone();
        var inner = new EllipseGeometry(outer.Center,
            Math.Max(0, outer.RadiusX - DialGeometry.OutlineThickness * dpi.X),
            Math.Max(0, outer.RadiusY - DialGeometry.OutlineThickness * dpi.Y));
        return new GeometryGroup { FillRule = FillRule.EvenOdd, Children = { outer, inner } };
    }

    private EllipseGeometry CreateCenterFill(Vector2 dpi) => new(_centerGeometry!.Center,
        Math.Max(0, _centerGeometry.RadiusX - DialGeometry.OutlineThickness * dpi.X / 2),
        Math.Max(0, _centerGeometry.RadiusY - DialGeometry.OutlineThickness * dpi.Y / 2));

    public void SetCenterTint(Color color, Color outline)
    {
        if (_centerTintBrush is not null)
            _centerTintBrush.Color = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        if (_centerOutlineBrush is not null)
            _centerOutlineBrush.Color = Windows.UI.Color.FromArgb(outline.A, outline.R, outline.G, outline.B);
    }

    public void SetCenterRadius(double radius, DpiScale dpi)
    {
        if (_centerGeometry is null) return;
        double edge = radius + DialGeometry.OutlineThickness / 2;
        var center = new Point(DialGeometry.Center * dpi.DpiScaleX, DialGeometry.Center * dpi.DpiScaleY);
        double x = edge * dpi.DpiScaleX, y = edge * dpi.DpiScaleY;
        if (_centerGeometry.Center == center && _centerGeometry.RadiusX == x && _centerGeometry.RadiusY == y) return;
        _centerGeometry.Center = center;
        _centerGeometry.RadiusX = x;
        _centerGeometry.RadiusY = y;
        QueueSurfaceUpdate();
    }

    public void BeginAnimation()
    {
        if (SetWindowRgn(_handle, IntPtr.Zero, false) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void SetHitRegion(DpiScale dpi, double centerRadius, bool includeCenterMoat = false, int? selectedIndex = null)
    {
        var contours = _contours;
        if (selectedIndex is { } index && !_selectionContours.TryGetValue(index, out contours))
        {
            Geometry surface = DialGeometry.CreateSurface(_shapeCount, false);
            if (index >= 0)
            {
                var selected = DialGeometry.CreateSectorSurface(index, _shapeCount, DialGeometry.SelectedOutlineThickness).Clone();
                Point origin = DialGeometry.SectorScaleCenter(index, _shapeCount);
                double scale = DialGeometry.GetSelectedSectorScale(index, _shapeCount);
                selected.Transform = new ScaleTransform(scale, scale, origin.X, origin.Y);
                surface = new CombinedGeometry(GeometryCombineMode.Union, surface, selected);
            }
            contours = FlattenContours(surface);
            _selectionContours.Add(index, contours);
        }
        var counts = contours.Select(points => points.Length).ToArray();
        var points = contours.SelectMany(contour => contour.Select(point => new NativePoint
        {
            X = (int)Math.Round(point.X * dpi.DpiScaleX),
            Y = (int)Math.Round(point.Y * dpi.DpiScaleY)
        })).ToArray();
        IntPtr region = CreatePolyPolygonRgn(points, counts, counts.Length, 1);
        if (region == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        double edge = (includeCenterMoat ? DialGeometry.InnerRadius : centerRadius) +
            DialGeometry.OutlineThickness / 2;
        IntPtr center = CreateEllipticRgn(
            (int)Math.Floor((DialGeometry.Center - edge) * dpi.DpiScaleX),
            (int)Math.Floor((DialGeometry.Center - edge) * dpi.DpiScaleY),
            (int)Math.Ceiling((DialGeometry.Center + edge) * dpi.DpiScaleX),
            (int)Math.Ceiling((DialGeometry.Center + edge) * dpi.DpiScaleY));
        if (center == IntPtr.Zero)
        {
            DeleteObject(region);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        int combined = CombineRgn(region, region, center, 2);
        DeleteObject(center);
        if (combined == 0)
        {
            DeleteObject(region);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        try { PreserveAntialiasedEdges(region); }
        catch { DeleteObject(region); throw; }
        if (SetWindowRgn(_handle, region, false) == 0)
        {
            DeleteObject(region);
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void PreserveAntialiasedEdges(IntPtr region)
    {
        IntPtr expanded = CreateRectRgn(0, 0, 0, 0);
        IntPtr shifted = CreateRectRgn(0, 0, 0, 0);
        try
        {
            if (expanded == IntPtr.Zero || shifted == IntPtr.Zero ||
                CombineRgn(expanded, region, IntPtr.Zero, 5) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            // Integer regions must also cover antialiasing at rounded and mitered corners.
            for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
            {
                if (x == 0 && y == 0) continue;
                if (CombineRgn(shifted, region, IntPtr.Zero, 5) == 0 ||
                    OffsetRgn(shifted, x, y) == 0 || CombineRgn(expanded, expanded, shifted, 2) == 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (CombineRgn(region, expanded, IntPtr.Zero, 5) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (shifted != IntPtr.Zero) DeleteObject(shifted);
            if (expanded != IntPtr.Zero) DeleteObject(expanded);
        }
    }

    public void Dispose()
    {
        Hide();
        ReleaseComposition();
        if (_host != IntPtr.Zero)
        {
            RemoveWindowSubclass(_host, _hostProcedure, new UIntPtr(1));
            DestroyWindow(_host);
            _host = IntPtr.Zero;
        }
    }

    public static void ReleaseDispatcherQueue()
    {
        if (_queueController != IntPtr.Zero) { Marshal.Release(_queueController); _queueController = IntPtr.Zero; }
    }

    public string DescribeState(IntPtr background)
    {
        var order = new List<string>();
        for (IntPtr window = GetTopWindow(IntPtr.Zero); window != IntPtr.Zero; window = GetWindow(window, 2))
        {
            if (window == _handle) order.Add("content");
            if (window == _host) order.Add("material");
            if (window == background) order.Add("background");
        }
        return $"kind={Kind}, hostVisible={IsWindowVisible(_host)}, sectors={_sectorTransforms.Count}, " +
            $"size={_visualSize}, opacity={_visual?.Opacity}, scale={_visual?.Scale}, zOrder={string.Join(">", order)}";
    }

    private void ReleaseComposition()
    {
        if (_target is not null) _target.Root = null;
        _target?.Dispose();
        ReleaseSectors();
        _visual?.Dispose();
        _material?.Dispose();
        _root?.Dispose();
        _compositor?.Dispose();
        _target = null;
        _visual = null;
        _centerGeometry = null;
        _root = null;
        _material = null;
        _compositor = null;
        _maskCount = 0;
        _visualSize = default;
    }

    private void ReleaseSectors()
    {
        CompositionTarget.Rendering -= UpdateSectorSurface;
        _surfacePending = false;
        _visual?.Children.RemoveAll();
        _centerOutlineVisual?.Dispose();
        _centerOutlineBrush?.Dispose();
        _centerOutlineClip?.Dispose();
        _centerOutlinePath?.Dispose();
        _centerOutlineSource?.Dispose();
        _centerOutlineGeometry = null;
        _centerOutlineVisual = null;
        _centerOutlineBrush = null;
        _centerOutlineClip = null;
        _centerOutlinePath = null;
        _centerOutlineSource = null;
        _centerTintVisual?.Dispose();
        _centerTintBrush?.Dispose();
        _centerTintClip?.Dispose();
        _centerTintPath?.Dispose();
        _centerTintSource?.Dispose();
        _centerTintVisual = null;
        _centerTintBrush = null;
        _centerTintClip = null;
        _centerTintPath = null;
        _centerTintSource = null;
        _sectorVisual?.Dispose();
        _sectorVisual = null;
        _surfaceClip?.Dispose();
        _surfacePath?.Dispose();
        _surfaceSource?.Dispose();
        _surfaceClip = null;
        _surfacePath = null;
        _surfaceSource = null;
        _sectorSurface = null;
        _centerGeometry = null;
        _sectorTransforms.Clear();
    }

    private static bool TransparencyEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("EnableTransparency") is not int value || value != 0;
    }

    [ComImport, Guid("29e691fa-4567-4dca-b319-d0f207eb6807"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool topmost, out IntPtr target);
        void EnsureOnThread(uint thread);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct QueueOptions { public int Size, ThreadType, ApartmentType; }
    [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int State, Flags; public uint GradientColor; public int Animation; }
    [StructLayout(LayoutKind.Sequential)] private struct AttributeData { public int Attribute; public IntPtr Data; public int Size; }

    [DllImport("CoreMessaging.dll")] private static extern int CreateDispatcherQueueController(QueueOptions options, out IntPtr controller);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr window, ref AttributeData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string name, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreatePolyPolygonRgn(NativePoint[] points, int[] counts, int count, int mode);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int OffsetRgn(IntPtr region, int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    private delegate IntPtr SubclassProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window, SubclassProcedure callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProcedure callback, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
