using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Dial.Models;
using Dial.Services;

namespace Dial;

public partial class RadialWindow : Window
{
    private const double DesignSize = DialGeometry.Size;
    private const int ExtendedStyleIndex = -20;
    private const int ToolWindowStyle = 0x00000080;
    private const uint MonitorDefaultToNearest = 2;
    private const int MonitorDpiTypeEffective = 0;
    private const int MaxItems = DialGeometry.MaximumItems;
    private static readonly Duration SectorSelectionDuration = new(TimeSpan.FromMilliseconds(90));

    private readonly List<MenuItemModel> _items = new();
    private readonly List<Path> _wedges = new();
    private readonly List<SolidColorBrush> _sectorFills = new();
    private readonly List<ScaleTransform> _sectorScales = new();
    private readonly List<Canvas> _sectorContents = new();
    private readonly Dictionary<string, (double Radius, double FontSize, double Width, double Height)> _centerLayouts = new();
    private readonly FontFamily _fluentIcons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private double _cx, _cy, _innerR, _outerR;
    private int _selectedIndex = -1;

    private Color _hoverColor = Color.FromRgb(53, 126, 199);
    private IconMode _iconMode = IconMode.IconOutside;
    private bool _showCenter = true;
    private int _requestVersion;
    private Task? _dismissTask;
    private TaskCompletionSource? _dismissCompletion;
    private IntPtr _previousForeground;
    private bool _closed;
    private bool _executing;
    private TextBlock? _centerText;
    private Path? _centerClose;
    private Grid? _centerGrid;
    private EllipseGeometry? _centerGeometry;
    private double _centerTargetRadius = DialGeometry.CenterRadius;
    private bool _centerAnimating;
    private int _centerAnimationVersion;
    private int _selectionAnimationVersion;
    private bool _sectorAnimating;
    private static readonly DependencyProperty CenterRadiusProperty = DependencyProperty.Register(
        "AnimatedCenterRadius", typeof(double), typeof(RadialWindow),
        new PropertyMetadata(DialGeometry.CenterRadius,
            (sender, _) => ((RadialWindow)sender).UpdateCenterLayout()));
    private double CurrentCenterRadius => (double)GetValue(CenterRadiusProperty);
    private string _hoverColorKey = "windowsBlue";
    private NativeBackdrop? _backdrop;
    private bool _trackingAnimation;
    private Color _normalColor;
    private Color _selectionColor;
    private readonly bool _allowBackdrop;
    private EventHandler? _pendingEntry;
    private EventHandler? _pendingDismiss;
    private int _presentationVersion;

    public BackdropKind BackdropKind => _backdrop?.Kind ?? BackdropKind.Opaque;

    public void RefreshAppearance()
    {
        if (!IsVisible || _dismissTask is { IsCompleted: false }) return;
        _hoverColor = (Color)ColorConverter.ConvertFromString(HoverPalette.Resolve(_hoverColorKey, ThemeService.IsDark));
        _backdrop!.Apply(_items.Count, _showCenter);
        MenuCanvas.Children.Clear();
        _wedges.Clear();
        int selection = _selectedIndex;
        _selectedIndex = -1;
        RenderMenu();
        SetSelected(selection);
        if (!NativeBackdrop.AnimationsEnabled)
        {
            MenuCanvas.BeginAnimation(OpacityProperty, null);
            MenuCanvas.Opacity = 1;
            MenuCanvas.RenderTransform = new ScaleTransform(1, 1, _cx, _cy);
            StopTrackingAnimation();
        }
        UpdateNativeShape();
        _backdrop.Show();
    }

    public RadialWindow(bool allowBackdrop = true)
    {
        _allowBackdrop = allowBackdrop;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        int ex = GetWindowLong(hwnd, ExtendedStyleIndex);
        SetWindowLong(hwnd, ExtendedStyleIndex, ex | ToolWindowStyle);
        int disableTransitions = 1;
        Marshal.ThrowExceptionForHR(DwmSetWindowAttribute(hwnd, 3, ref disableTransitions, sizeof(int)));
        _backdrop = new NativeBackdrop(this, _allowBackdrop);
        HwndSource.FromHwnd(hwnd).AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0047 && !_closed && IsVisible) _backdrop?.SynchronizeOrder();
        if (message == 0x0201 && IsVisible)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            long coordinates = lParam.ToInt64();
            double x = (short)(coordinates & 0xFFFF) / dpi.DpiScaleX - _cx;
            double y = (short)((coordinates >> 16) & 0xFFFF) / dpi.DpiScaleY - _cy;
            double dismissRadius = _showCenter ? CurrentCenterRadius : DialGeometry.InnerRadius;
            if (x * x + y * y <= dismissRadius * dismissRadius)
            {
                Dismiss();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public async void ShowMenu(IEnumerable<MenuItemModel> items, bool showAtMouse, string hoverColorKey, IconMode iconMode, bool showCenter = true)
    {
        if (_closed) return;
        int version = ++_requestVersion;
        var snapshot = items.Where(i => i.Enabled).Take(MaxItems).ToArray();
        if (!IsVisible) _previousForeground = GetForegroundWindow();
        try
        {
            await DismissAsync();
            if (version != _requestVersion || snapshot.Length == 0) return;

            _items.Clear();
            _items.AddRange(snapshot);
            _selectedIndex = -1;
            _executing = false;
            _hoverColor = (Color)ColorConverter.ConvertFromString(HoverPalette.Resolve(hoverColorKey, ThemeService.IsDark));
            _hoverColorKey = hoverColorKey;
            _iconMode = iconMode;
            _showCenter = showCenter;

            MenuCanvas.Children.Clear();
            _wedges.Clear();
            ComputeGeometry();
            Position(showAtMouse);
            _backdrop!.Apply(_items.Count, _showCenter);
            RenderMenu();
            MenuCanvas.Measure(new Size(DesignSize, DesignSize));
            MenuCanvas.Arrange(new Rect(0, 0, DesignSize, DesignSize));
            MenuCanvas.UpdateLayout();
            PrepareAnimationState();
            UpdateNativeShape();
            Show();
            if (version == _requestVersion && IsVisible) QueueEntryAnimation(version);
        }
        catch (Exception exception) when (exception is Win32Exception or COMException)
        {
            Hide();
            _backdrop?.Hide();
            StopTrackingAnimation();
            ErrorService.Report("DisplayError", exception);
        }
    }

    private void ComputeGeometry()
    {
        _cx = DialGeometry.Center;
        _cy = DialGeometry.Center;
        _innerR = DialGeometry.InnerRadius;
        _outerR = DialGeometry.OuterRadius;
        BeginAnimation(CenterRadiusProperty, null);
        SetValue(CenterRadiusProperty, DialGeometry.CenterRadius);
    }

    private NativeRect Position(bool showAtMouse)
    {
        if (!GetCursorPos(out NativePoint p)) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr monitor = MonitorFromPoint(p, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Marshal.ThrowExceptionForHR(GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out uint dpiX, out uint dpiY));
        int width = (int)Math.Round(DesignSize * dpiX / 96);
        int height = (int)Math.Round(DesignSize * dpiY / 96);
        int x = showAtMouse ? p.X - width / 2 : (info.WorkArea.Left + info.WorkArea.Right - width) / 2;
        int y = showAtMouse ? p.Y - height / 2 : (info.WorkArea.Top + info.WorkArea.Bottom - height) / 2;
        x = Math.Clamp(x, info.WorkArea.Left, Math.Max(info.WorkArea.Left, info.WorkArea.Right - width));
        y = Math.Clamp(y, info.WorkArea.Top, Math.Max(info.WorkArea.Top, info.WorkArea.Bottom - height));
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        if (!SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, 0x0014))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return new NativeRect { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }

    private void RenderMenu()
    {
        foreach (var scale in _sectorScales)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        _sectorScales.Clear();
        ++_selectionAnimationVersion;
        _sectorAnimating = false;
        _sectorFills.Clear();
        _sectorContents.Clear();
        bool dark = ThemeService.IsDark;
        _centerLayouts.Clear();
        if (_showCenter)
            foreach (var item in _items)
                if (!_centerLayouts.ContainsKey(item.Label))
                    _centerLayouts.Add(item.Label, GetCenterLayout(item.Label));

        int n = _items.Count;
        bool opaque = BackdropKind == BackdropKind.Opaque;
        _normalColor = SystemParameters.HighContrast ? SystemColors.WindowColor
            : opaque ? (dark ? Color.FromRgb(32, 32, 32) : Color.FromRgb(245, 245, 245))
            : Color.FromArgb(dark ? (byte)224 : (byte)208, dark ? (byte)32 : (byte)245,
                dark ? (byte)32 : (byte)245, dark ? (byte)32 : (byte)245);
        _selectionColor = SystemParameters.HighContrast ? SystemColors.HighlightColor
            : Color.FromArgb(opaque ? (byte)255 : (byte)Math.Round(
                255 - (255 - _normalColor.A) * (1 - 72 / 255.0)), _hoverColor.R, _hoverColor.G, _hoverColor.B);
        for (int i = 0; i < n; i++)
        {
            int index = i;
            Point origin = DialGeometry.SectorScaleCenter(i, n);
            var transform = CreateUniformScaleTransform(1, origin.X, origin.Y);
            transform.Changed += (_, _) =>
            {
                if (_closed) return;
                double outline = DialGeometry.GetSectorOutline(index, n, transform.ScaleX);
                if (index < _wedges.Count) _wedges[index].StrokeThickness = outline;
                _backdrop?.SetSectorScale(index, transform.ScaleX);
            };
            _sectorScales.Add(transform);
            _backdrop?.SetSectorScale(index, 1);
            var sector = DialGeometry.CreateSector(i, n);
            var fill = new SolidColorBrush(_normalColor);
            _sectorFills.Add(fill);
            var path = new Path
            {
                Data = sector,
                Fill = fill,
                Stroke = SystemParameters.HighContrast ? SystemColors.WindowTextBrush
                    : new SolidColorBrush(SectorOutlineColor(_normalColor)),
                StrokeThickness = DialGeometry.OutlineThickness,
                RenderTransform = transform,
            };
            MenuCanvas.Children.Add(path);
            _wedges.Add(path);
        }

        _centerText = null;
        _centerClose = null;
        _centerGrid = null;
        _centerGeometry = null;
        // Shapes already define their silhouette; clipping it again multiplies edge coverage.
        MenuCanvas.Clip = null;
        if (_showCenter) AddCenterFill();

        for (int i = 0; i < n; i++)
        {
            AddItemContent(_items[i], i, _wedges[i].Data);
        }

        if (_showCenter) AddCenterText();
    }

    private void AddItemContent(MenuItemModel item, int index, Geometry clip)
    {
        bool dark = ThemeService.IsDark;
        double midRadius = (_innerR + _outerR) / 2;

        bool showIcons = _iconMode != IconMode.LabelsOnly;
        bool showLabels = _iconMode != IconMode.IconsOnly;
        var content = new Canvas { Width = DesignSize, Height = DesignSize, Clip = clip,
            RenderTransform = _sectorScales[index], IsHitTestVisible = false };
        MenuCanvas.Children.Add(content);
        _sectorContents.Add(content);

        double iconRadius, labelRadius;
        if (_iconMode == IconMode.IconOutside)
        {
            iconRadius = DialGeometry.OuterContentRadius;
            labelRadius = DialGeometry.InnerContentRadius;
        }
        else
        {
            iconRadius = DialGeometry.InnerContentRadius;
            labelRadius = DialGeometry.OuterContentRadius;
        }
        if (_iconMode == IconMode.IconsOnly) iconRadius = midRadius;
        if (_iconMode == IconMode.LabelsOnly) labelRadius = midRadius;
        var stacked = showIcons && showLabels
            ? DialGeometry.GetStackedContentBounds(index, _items.Count, _iconMode == IconMode.IconOutside) : null;
        if (showIcons)
        {
            Rect bounds = stacked?.Icon ?? DialGeometry.GetContentBounds(index, _items.Count, iconRadius, DialGeometry.PreferredIconWidth);
            double iconSize = Math.Min(DialGeometry.IconSize, Math.Min(bounds.Width, bounds.Height));
            var icon = IconHelper.Resolve(item);
            FrameworkElement iconEl;
            if (icon.IsImage)
            {
                iconEl = new Image
                {
                    Source = icon.Image,
                    Width = iconSize,
                    Height = iconSize,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false,
                };
            }
            else if (icon.IsText)
            {
                iconEl = new TextBlock
                {
                    Text = icon.Glyph,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = FitTextFontSize(icon.Glyph!, bounds.Width, bounds.Height, 14, 5),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ContentBrush(),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Language = System.Windows.Markup.XmlLanguage.GetLanguage(LocalizationService.CurrentCulture.IetfLanguageTag),
                    MaxWidth = bounds.Width,
                    MaxHeight = bounds.Height,
                    IsHitTestVisible = false,
                };
            }
            else
            {
                var glyph = new FormattedText(icon.Glyph!, LocalizationService.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(_fluentIcons, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    iconSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip).BuildGeometry(new Point());
                glyph.Freeze();
                iconEl = new Path
                {
                    Data = glyph,
                    Fill = ContentBrush(),
                    Width = iconSize,
                    Height = iconSize,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false,
                };
            }

            var iconGrid = new Grid { Width = bounds.Width, Height = bounds.Height, IsHitTestVisible = false };
            iconEl.HorizontalAlignment = HorizontalAlignment.Center;
            iconEl.VerticalAlignment = VerticalAlignment.Center;
            iconGrid.Children.Add(iconEl);
            Canvas.SetLeft(iconGrid, bounds.Left);
            Canvas.SetTop(iconGrid, bounds.Top);
            content.Children.Add(iconGrid);
        }

        if (!showLabels) return;

        string text = item.Label;
        if (text.Length > 64) text = text[..64];
        var (labelBounds, fontSize) = GetLabelLayout(text, index, labelRadius, stacked?.Label);

        var label = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = ContentBrush(),
            TextAlignment = TextAlignment.Center,
            TextWrapping = labelBounds.Height >= 24 ? TextWrapping.WrapWithOverflow : TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineHeight = 12,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            MaxWidth = labelBounds.Width,
            Width = labelBounds.Width,
            MaxHeight = labelBounds.Height,
            IsHitTestVisible = false,
        };
        var labelGrid = new Grid
        {
            Width = labelBounds.Width,
            Height = labelBounds.Height,
            IsHitTestVisible = false,
            ClipToBounds = true
        };
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        labelGrid.Children.Add(label);
        Canvas.SetLeft(labelGrid, labelBounds.Left);
        Canvas.SetTop(labelGrid, labelBounds.Top);
        content.Children.Add(labelGrid);
    }

    private void AddCenterFill()
    {
        bool native = BackdropKind != BackdropKind.Opaque;
        if (native) _backdrop!.SetCenterTint(_normalColor, SectorOutlineColor(_normalColor));
        _centerGeometry = new EllipseGeometry(new Point(_cx, _cy), CurrentCenterRadius, CurrentCenterRadius);
        var centerEllipse = new Path
        {
            Data = _centerGeometry,
            Fill = native ? null : new SolidColorBrush(_normalColor),
            Stroke = native ? null : SystemParameters.HighContrast ? SystemColors.WindowTextBrush
                : new SolidColorBrush(SectorOutlineColor(_normalColor)),
            StrokeThickness = DialGeometry.OutlineThickness,
            IsHitTestVisible = false,
        };
        MenuCanvas.Children.Add(centerEllipse);
    }

    private void AddCenterText()
    {
        bool dark = ThemeService.IsDark;
        double d = DialGeometry.MaximumCenterRadius * 2;
        _centerTargetRadius = DialGeometry.CenterRadius;

        _centerText = new TextBlock
        {
            Text = "",
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(LocalizationService.CurrentCulture.IetfLanguageTag),
            FontSize = 14,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Light,
            Foreground = ContentBrush(),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.WrapWithOverflow,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            MaxWidth = d - 20,
            MaxHeight = d - 32,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        System.Windows.Automation.AutomationProperties.SetLiveSetting(_centerText, System.Windows.Automation.AutomationLiveSetting.Polite);
        var centerGrid = new Grid
        {
            Width = d,
            Height = d,
            IsHitTestVisible = false,
            RenderTransform = CreateUniformScaleTransform(1, d / 2, d / 2)
        };
        _centerText.HorizontalAlignment = HorizontalAlignment.Center;
        _centerText.VerticalAlignment = VerticalAlignment.Center;
        centerGrid.Children.Add(_centerText);
        _centerClose = new Path
        {
            Data = Geometry.Parse("M1,1 L13,13 M13,1 L1,13"),
            Width = 14,
            Height = 14,
            Stroke = ContentBrush(),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        centerGrid.Children.Add(_centerClose);
        Canvas.SetLeft(centerGrid, _cx - d / 2);
        Canvas.SetTop(centerGrid, _cy - d / 2);
        MenuCanvas.Children.Add(centerGrid);
        _centerGrid = centerGrid;
        UpdateCenterLayout();
    }

    private (double Radius, double FontSize, double Width, double Height) GetCenterLayout(string text)
    {
        var probe = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.WrapWithOverflow,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(LocalizationService.CurrentCulture.IetfLanguageTag)
        };
        for (double fontSize = 14; fontSize >= 12; fontSize -= 0.5)
        {
            probe.FontSize = fontSize;
            probe.LineHeight = Math.Ceiling(fontSize * 1.25);
            double wordWidth = GetLongestWordWidth(text, fontSize);
            (double Radius, double FontSize, double Width, double Height)? best = null;
            for (double width = 24; width <= DialGeometry.MaximumCenterRadius * 2 - 20; width += 4)
            {
                probe.Width = width;
                probe.Measure(new Size(width, double.PositiveInfinity));
                if (wordWidth > width) continue;
                double height = Math.Ceiling(probe.DesiredSize.Height) + 1;
                double radius = Math.Sqrt(Math.Pow(width / 2 + 10, 2) + Math.Pow(height / 2 + 8, 2));
                if (radius <= DialGeometry.MaximumCenterRadius && (best is null || radius < best.Value.Radius))
                    best = (Math.Max(radius, DialGeometry.CenterRadius), fontSize, width, height);
            }
            if (best is { } fitted) return fitted;
        }
        double side = Math.Floor(DialGeometry.MaximumCenterRadius * Math.Sqrt(2) - 20);
        return (DialGeometry.MaximumCenterRadius, 12, side, Math.Floor(side / 15) * 15 + 1);
    }

    private void UpdateCenterLayout()
    {
        double radius = CurrentCenterRadius;
        if (_centerGeometry is not null)
            _centerGeometry.RadiusX = _centerGeometry.RadiusY = radius;
        if (_centerGrid?.RenderTransform is ScaleTransform transform)
            transform.ScaleX = transform.ScaleY = radius / _centerTargetRadius;
        _backdrop?.SetCenterRadius(radius, VisualTreeHelper.GetDpi(this));
    }

    private (Rect Bounds, double FontSize) GetLabelLayout(string text, int index, double radius, Rect? stackedBounds)
    {
        var probe = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.WrapWithOverflow,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            LineHeight = 12,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };
        var candidates = stackedBounds is { } stacked ? new[] { stacked } : new[] { 12.0, 24, 36, 48 }
            .Select(height => DialGeometry.GetContentBounds(index, _items.Count, radius, DialGeometry.PreferredLabelWidth, height))
            .Distinct().ToArray();
        for (double size = DialGeometry.MaximumLabelFontSize; size >= DialGeometry.MinimumLabelFontSize; size -= 0.25)
        {
            probe.FontSize = size;
            double wordWidth = GetLongestWordWidth(text, size);
            foreach (Rect bounds in candidates)
            {
                probe.Width = bounds.Width;
                probe.Measure(new Size(bounds.Width, double.PositiveInfinity));
                if (probe.DesiredSize.Height <= bounds.Height && wordWidth <= bounds.Width)
                    return (bounds, size);
            }
        }
        return (candidates.MaxBy(bounds => bounds.Width * Math.Floor(bounds.Height / 12)), DialGeometry.MinimumLabelFontSize);
    }

    private double GetLongestWordWidth(string text, double fontSize) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"[\p{IsBasicLatin}\p{IsLatin-1Supplement}]+")
            .Select(match => match.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .SelectMany(words => words)
            .Select(word => MeasureText(word, fontSize, 10000).WidthIncludingTrailingWhitespace).DefaultIfEmpty(0).Max();

    private double FitTextFontSize(string text, double maxWidth, double maxHeight, double maximum, double minimum)
    {
        for (double size = maximum; size >= minimum; size -= 0.5)
        {
            var ft = MeasureText(text, size, maxWidth);
            if (ft.Width <= maxWidth && ft.Height <= maxHeight)
            {
                return size;
            }
        }
        return minimum;
    }

    private FormattedText MeasureText(string text, double size, double width) => new(
        text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
        size, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
    {
        MaxTextWidth = width,
        TextAlignment = TextAlignment.Center,
    };

    private void PrepareAnimationState()
    {
        ++_presentationVersion;
        _backdrop!.BeginAnimation();
        MenuCanvas.CacheMode = null;
        MenuCanvas.BeginAnimation(OpacityProperty, null);
        MenuCanvas.Opacity = 0;
        Opacity = 0;

        double initial = NativeBackdrop.AnimationsEnabled ? 0.94 : 1;
        var transform = CreateUniformScaleTransform(initial, _cx, _cy);
        MenuCanvas.RenderTransform = transform;
    }

    private void BeginEntryAnimation()
    {
        int presentationVersion = _presentationVersion;
        TrackAnimation();
        var scaleAnim = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 },
        };
        var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        scaleAnim.Completed += (_, _) =>
        {
            if (presentationVersion == _presentationVersion && _dismissTask is not { IsCompleted: false })
                StopTrackingAnimation();
        };

        var transform = (ScaleTransform)MenuCanvas.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        MenuCanvas.BeginAnimation(OpacityProperty, fadeAnim);
        _backdrop!.Show();
        Opacity = 1;
        Activate();
    }

    private void QueueEntryAnimation(int version)
    {
        CancelPendingEntry();
        int frames = 0;
        _pendingEntry = (_, _) =>
        {
            if (++frames < 2)
            {
                MenuCanvas.InvalidateVisual();
                return;
            }
            CancelPendingEntry();
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (_closed || version != _requestVersion || !IsVisible) return;
                if (NativeBackdrop.AnimationsEnabled) BeginEntryAnimation();
                else
                {
                    MenuCanvas.Opacity = 1;
                    Opacity = 1;
                    UpdateNativeShape();
                    _backdrop!.Show();
                    _backdrop.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter);
                    Activate();
                }
            });
        };
        CompositionTarget.Rendering += _pendingEntry;
    }

    private void CancelPendingEntry()
    {
        if (_pendingEntry is null) return;
        CompositionTarget.Rendering -= _pendingEntry;
        _pendingEntry = null;
    }

    private void Dismiss()
    {
        ++_requestVersion;
        _ = DismissAsync();
    }

    private Task DismissAsync()
    {
        CancelPendingEntry();
        if (_dismissTask is { IsCompleted: false }) return _dismissTask;
        if (!IsVisible) return Task.CompletedTask;
        int presentationVersion = ++_presentationVersion;
        ++_centerAnimationVersion;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dismissCompletion = completion;
        _dismissTask = completion.Task;
        bool completed = false;
        void Complete()
        {
            if (completed || _closed || presentationVersion != _presentationVersion) return;
            completed = true;
            Hide();
            _backdrop?.Hide();
            StopTrackingAnimation();
            MenuCanvas.BeginAnimation(OpacityProperty, null);
            MenuCanvas.Opacity = 0;
            if (MenuCanvas.RenderTransform is ScaleTransform scale)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            }
            MenuCanvas.Children.Clear();
            _wedges.Clear();
            _sectorFills.Clear();
            _sectorContents.Clear();
            _centerText = null;
            _centerGrid = null;
            _centerGeometry = null;
            _centerAnimating = false;
            BeginAnimation(CenterRadiusProperty, null);
            SetValue(CenterRadiusProperty, DialGeometry.CenterRadius);
            completion.TrySetResult();
        }
        void PresentTransparentFrame()
        {
            if (completed || _closed || presentationVersion != _presentationVersion || _pendingDismiss is not null) return;
            MenuCanvas.BeginAnimation(OpacityProperty, null);
            MenuCanvas.Opacity = 0;
            int frames = 0;
            _pendingDismiss = (_, _) =>
            {
                if (++frames < 2)
                {
                    MenuCanvas.InvalidateVisual();
                    return;
                }
                CompositionTarget.Rendering -= _pendingDismiss;
                _pendingDismiss = null;
                Complete();
            };
            CompositionTarget.Rendering += _pendingDismiss;
        }
        if (!NativeBackdrop.AnimationsEnabled)
        {
            PresentTransparentFrame();
            return completion.Task;
        }
        TrackAnimation();
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(MenuCanvas.Opacity, 0, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            PresentTransparentFrame();
        };
        MenuCanvas.BeginAnimation(OpacityProperty, fade);

        if (MenuCanvas.RenderTransform is ScaleTransform transform)
        {
            var scaleOut = new DoubleAnimation(transform.ScaleX, 0.94, TimeSpan.FromMilliseconds(110)) { EasingFunction = ease };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
        }
        return completion.Task;
    }

    private static SolidColorBrush FreezeBrush(long argb)
    {
        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        brush.Freeze();
        return brush;
    }

    private static Brush ContentBrush() => SystemParameters.HighContrast ? SystemColors.WindowTextBrush
        : FreezeBrush(ThemeService.IsDark ? 0xFFFFFFFF : 0xFF1A1A1A);

    private void TrackAnimation()
    {
        if (_trackingAnimation) return;
        _trackingAnimation = true;
        MenuCanvas.CacheMode = null;
        _backdrop!.BeginAnimation();
        CompositionTarget.Rendering += OnAnimationFrame;
    }

    private void StopTrackingAnimation()
    {
        if (!_trackingAnimation) return;
        _trackingAnimation = false;
        CompositionTarget.Rendering -= OnAnimationFrame;
        UpdateNativeShape();
        MenuCanvas.CacheMode = null;
        if (!_closed && !_centerAnimating && !_sectorAnimating)
            _backdrop!.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter, _selectedIndex);
    }

    private void OnAnimationFrame(object? sender, EventArgs e) => UpdateNativeShape();

    private void UpdateNativeShape()
    {
        if (!_closed && MenuCanvas.RenderTransform is ScaleTransform scale)
        {
            _backdrop?.SetCenterRadius(CurrentCenterRadius, VisualTreeHelper.GetDpi(this));
            _backdrop?.SetScale(scale.ScaleX, MenuCanvas.Opacity, VisualTreeHelper.GetDpi(this));
        }
    }

    private static ScaleTransform CreateUniformScaleTransform(double scale, double centerX, double centerY)
    {
        var transform = new ScaleTransform(scale, scale, centerX, centerY);
        bool synchronizing = false;
        transform.Changed += (_, _) =>
        {
            if (synchronizing || transform.ScaleX == transform.ScaleY) return;
            synchronizing = true;
            try { transform.ScaleY = transform.ScaleX; }
            finally { synchronizing = false; }
        };
        return transform;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = e.GetPosition(MenuCanvas);
        SetSelected(HitTest(pos));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetSelected(-1);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(MenuCanvas);

        int idx = HitTest(pos);
        if (idx >= 0) ExecuteItem(idx);
        else Dismiss();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Dismiss();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape:
                Dismiss();
                e.Handled = true;
                break;
            case Key.Enter:
                if (_selectedIndex >= 0) ExecuteItem(_selectedIndex);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Down:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                MoveSelection(1);
                e.Handled = true;
                break;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (IsVisible && _dismissTask is not { IsCompleted: false }) Dismiss();
    }

    private void MoveSelection(int step)
    {
        int n = _items.Count;
        if (n == 0) return;
        int next = _selectedIndex < 0
            ? (step > 0 ? 0 : n - 1)
            : (_selectedIndex + step + n) % n;
        SetSelected(next);
    }

    private async void ExecuteItem(int idx)
    {
        if (_executing || idx < 0 || idx >= _items.Count) return;
        _executing = true;
        var item = _items[idx];
        ++_requestVersion;
        await DismissAsync();
        if (_closed) return;
        if (_previousForeground != IntPtr.Zero) SetForegroundWindow(_previousForeground);
        ActionExecutor.Execute(item);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        ++_presentationVersion;
        CancelPendingEntry();
        MenuCanvas.BeginAnimation(OpacityProperty, null);
        BeginAnimation(CenterRadiusProperty, null);
        if (MenuCanvas.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        foreach (var sector in _sectorScales)
        {
            sector.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            sector.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
        if (_pendingDismiss is not null)
        {
            CompositionTarget.Rendering -= _pendingDismiss;
            _pendingDismiss = null;
        }
        ++_requestVersion;
        _dismissCompletion?.TrySetResult();
        StopTrackingAnimation();
        _backdrop?.Dispose();
        base.OnClosed(e);
    }

    private void SetSelected(int idx)
    {
        if (_closed || !IsVisible || _dismissTask is { IsCompleted: false }) return;
        if (idx == _selectedIndex) return;
        int previous = _selectedIndex;
        _selectedIndex = idx;
        ++_selectionAnimationVersion;
        _sectorAnimating = NativeBackdrop.AnimationsEnabled;
        if (_sectorAnimating) _backdrop!.BeginAnimation();

        if (previous >= 0) UpdateSectorSelection(previous, false);
        if (idx >= 0) UpdateSectorSelection(idx, true);
        if (!_sectorAnimating && !_trackingAnimation && !_centerAnimating)
            _backdrop!.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter, _selectedIndex);

        if (SystemParameters.HighContrast)
        {
            if (previous >= 0 && previous < _sectorContents.Count) UpdateContentSelection(_sectorContents[previous], false);
            if (idx >= 0 && idx < _sectorContents.Count) UpdateContentSelection(_sectorContents[idx], true);
        }

        if (_centerText is null) return;
        var layout = idx >= 0 && idx < _items.Count
            ? _centerLayouts[_items[idx].Label]
            : (Radius: DialGeometry.CenterRadius, FontSize: 14.0, Width: 24.0, Height: 24.0);
        _centerText.FontSize = layout.FontSize;
        _centerText.LineHeight = Math.Ceiling(layout.FontSize * 1.25);
        _centerText.MaxWidth = layout.Width;
        _centerText.MaxHeight = layout.Height;
        _centerTargetRadius = layout.Radius;
        UpdateCenterLayout();
        int centerAnimationVersion = ++_centerAnimationVersion;
        if (idx >= 0 && idx < _items.Count)
        {
            _centerText.Visibility = Visibility.Visible;
            if (_centerClose is not null) _centerClose.Visibility = Visibility.Collapsed;
            _centerText.Text = _items[idx].Label;
            _centerText.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            _centerText.Text = "";
            _centerText.Visibility = Visibility.Collapsed;
            if (_centerClose is not null) _centerClose.Visibility = Visibility.Visible;
        }
        if (NativeBackdrop.AnimationsEnabled && _dismissTask is not { IsCompleted: false })
        {
            bool resizeCenter = Math.Abs(CurrentCenterRadius - layout.Radius) > 0.5;
            _centerAnimating = resizeCenter;
            if (resizeCenter)
            {
                _backdrop!.BeginAnimation();
                var animation = new DoubleAnimation(CurrentCenterRadius, layout.Radius, TimeSpan.FromMilliseconds(210))
                {
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
                };
                animation.Completed += (_, _) =>
                {
                    if (_closed || centerAnimationVersion != _centerAnimationVersion) return;
                    _centerAnimating = false;
                    if (!_trackingAnimation && !_sectorAnimating && IsVisible)
                        _backdrop!.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter, _selectedIndex);
                };
                BeginAnimation(CenterRadiusProperty, animation);
            }
            else
            {
                BeginAnimation(CenterRadiusProperty, null);
                SetValue(CenterRadiusProperty, layout.Radius);
                UpdateCenterLayout();
                if (!_closed && !_trackingAnimation && !_sectorAnimating && IsVisible)
                    _backdrop!.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter, _selectedIndex);
            }
        }
        else
        {
            _centerAnimating = false;
            BeginAnimation(CenterRadiusProperty, null);
            SetValue(CenterRadiusProperty, layout.Radius);
            UpdateCenterLayout();
            if (!_closed && !_trackingAnimation && !_sectorAnimating && IsVisible)
                _backdrop!.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter, _selectedIndex);
        }
    }

    private void UpdateSectorSelection(int index, bool selected)
    {
        double targetScale = selected ? DialGeometry.GetSelectedSectorScale(index, _items.Count) : 1;
        var scale = _sectorScales[index];
        if (NativeBackdrop.AnimationsEnabled)
        {
            var animation = new DoubleAnimation(scale.ScaleX, targetScale, SectorSelectionDuration)
            { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } };
            int version = _selectionAnimationVersion;
            animation.Completed += (_, _) =>
            {
                if (_closed || version != _selectionAnimationVersion || (!selected && _selectedIndex >= 0)) return;
                _sectorAnimating = false;
                if (IsVisible && !_centerAnimating && !_trackingAnimation)
                    _backdrop!.SetHitRegion(VisualTreeHelper.GetDpi(this), CurrentCenterRadius, !_showCenter, _selectedIndex);
            };
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        }
        else
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = targetScale;
        }
        AnimateSectorColor(_sectorFills[index], selected ? _selectionColor : _normalColor);
        if (!SystemParameters.HighContrast && _wedges[index].Stroke is SolidColorBrush outline)
            AnimateSectorColor(outline, SectorOutlineColor(selected ? _selectionColor : _normalColor));
    }

    private static Color SectorOutlineColor(Color fill)
    {
        bool dark = ThemeService.IsDark;
        double amount = (dark ? 18 : 12) / 255.0;
        byte Channel(byte value) => (byte)Math.Round(value * (1 - amount) + (dark ? 255 : 0) * amount);
        return Color.FromArgb(fill.A, Channel(fill.R), Channel(fill.G), Channel(fill.B));
    }

    private static void AnimateSectorColor(SolidColorBrush brush, Color target)
    {
        if (NativeBackdrop.AnimationsEnabled)
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(brush.Color, target, SectorSelectionDuration)
                { EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } });
        else
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = target;
        }
    }

    private void UpdateContentSelection(Canvas content, bool selected)
    {
        Brush brush = selected ? SystemColors.HighlightTextBrush : ContentBrush();
        foreach (var row in content.Children.OfType<Grid>())
        {
            foreach (var text in row.Children.OfType<TextBlock>())
                text.Foreground = brush;
            foreach (var glyph in row.Children.OfType<Path>())
                glyph.Fill = brush;
        }
    }


    private int HitTest(Point pos)
    {
        if (_selectedIndex >= 0 && _selectedIndex < _sectorScales.Count)
        {
            var inverse = _sectorScales[_selectedIndex].Inverse;
            if (inverse is not null && DialGeometry.CreateSector(_selectedIndex, _items.Count)
                .FillContains(inverse.Transform(pos))) return _selectedIndex;
        }
        return DialGeometry.HitTest(pos, _items.Count);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorBounds;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
