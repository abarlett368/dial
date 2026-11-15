using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dial.Services;
using Microsoft.Win32;

namespace Dial;

public partial class IconPickerWindow : Window
{
    private sealed record IconItem(int Index, ImageSource? Image, string Glyph, string Name) : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static IconItem[]? _systemIcons;
    private IconItem[] _icons = [];
    private IconItem? _selectedIcon;
    private int _columns;
    private string _selectedResourcePath = "";
    private bool _loading;
    private bool _closed;
    private CancellationTokenSource? _loadCancellation;

    public IconPickerWindow(string currentIcon = "")
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += (_, _) => ThemeService.ApplyWindowAppearance(this);
        ThemeService.WindowAppearanceChanged += ApplyWindowAppearance;
        Closed += (_, _) =>
        {
            _closed = true;
            _loadCancellation?.Cancel();
            ThemeService.WindowAppearanceChanged -= ApplyWindowAppearance;
        };

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await LoadSystemIconsAsync(currentIcon);
        }
    }

    public string SelectedIcon { get; private set; } = "";
    public int SelectedIconIndex { get; private set; } = -1;

    private void ApplyWindowAppearance() => ThemeService.ApplyWindowAppearance(this);

    private async void SystemIconsButton_Click(object sender, RoutedEventArgs e) => await LoadSystemIconsAsync();

    private Task LoadSystemIconsAsync(string currentIcon = "") => LoadIconsAsync(
        _ => _systemIcons is { } cached ? Task.FromResult(cached) : Task.Run(CreateSystemIcons),
        "", currentIcon);

    private static IconItem[] CreateSystemIcons()
    {
        var characters = new SortedSet<int>();
        foreach (string familyName in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            var family = Fonts.SystemFontFamilies.FirstOrDefault(font =>
                font.Source.Equals(familyName, StringComparison.OrdinalIgnoreCase));
            if (family is null) continue;

            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyphTypeface)) continue;
            foreach (var entry in glyphTypeface.CharacterToGlyphMap)
            {
                if (entry.Key is >= 0xE000 and <= 0xF8FF && entry.Value != 0)
                    characters.Add(entry.Key);
            }
        }

        return characters.Select(code => new IconItem(-1, null, ((char)code).ToString(), $"U+{code:X4}"))
            .ToArray();
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.T("SelectIconFile"),
            Filter = LocalizationService.T("IconFilesFilter"),
        };
        if (dialog.ShowDialog(this) != true) return;

        string path = Path.GetFullPath(dialog.FileName);
        string extension = Path.GetExtension(path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".icl", StringComparison.OrdinalIgnoreCase))
        {
            await LoadResourceIconsAsync(path);
            return;
        }

        await LoadIconsAsync(token => Task.Run(() =>
        {
            var image = IconHelper.LoadImageFile(path);
            return image is null ? [] : new[] { new IconItem(-1, image, "", Path.GetFileName(path)) };
        }, token), path, imageFile: true);
    }

    private Task LoadResourceIconsAsync(string path) => LoadIconsAsync(token => Task.Run(() =>
    {
        int count = IconHelper.GetResourceIconCount(path);
        var icons = new List<IconItem>();
        for (int index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            if (IconHelper.LoadResourceIcon(path, index) is { } image)
            {
                icons.Add(new IconItem(index, image, "", (index + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        return icons.ToArray();
    }, token), path);

    private async Task LoadIconsAsync(Func<CancellationToken, Task<IconItem[]>> load,
        string path, string currentIcon = "", bool imageFile = false)
    {
        if (_closed || _loading) return;
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _icons = [];
        SetSelectedIcon(null);
        _selectedResourcePath = "";
        IconList.ItemsSource = null;
        SetLoading(true);
        try
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            cancellation.Token.ThrowIfCancellationRequested();
            var icons = await load(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (imageFile)
            {
                if (icons.Length == 0)
                {
                    StatusText.Text = LocalizationService.T("IconLoadFailed");
                    return;
                }
                SelectedIcon = path;
                SelectedIconIndex = -1;
                DialogResult = true;
                return;
            }

            if (path.Length == 0)
            {
                _systemIcons = icons;
                icons = icons.Select(icon => icon with { IsSelected = false }).ToArray();
            }
            else
                icons = icons.Select(icon => icon with
                {
                    Name = string.Format(LocalizationService.CurrentCulture,
                        LocalizationService.T("IconResourceItem"), icon.Name)
                }).ToArray();
            _selectedResourcePath = path;
            _icons = icons;
            int selected = path.Length == 0
                ? Array.FindIndex(icons, icon => icon.Glyph == currentIcon)
                : icons.Length > 0 ? 0 : -1;
            SetSelectedIcon(selected >= 0 ? icons[selected] : null);
            UpdateIconRows();
            await Dispatcher.Yield(DispatcherPriority.Background);
            cancellation.Token.ThrowIfCancellationRequested();
            ScrollToSelectedIcon();
            StatusText.Text = icons.Length == 0
                ? LocalizationService.T(path.Length == 0 ? "SystemIconsUnavailable" : "NoIconResources")
                : "";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Closing the dialog cancels work without updating its controls.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            ErrorService.LogError("IconLoadFailed", exception);
            if (!_closed) StatusText.Text = ErrorService.GetMessage("IconLoadFailed", exception);
        }
        finally
        {
            _loadCancellation = null;
            if (!_closed) SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        _loading = loading;
        if (loading) StatusText.Text = LocalizationService.T("LoadingIcons");
        SystemIconsButton.IsEnabled = BrowseButton.IsEnabled = IconList.IsEnabled = !loading;
        SelectButton.IsEnabled = !loading && _selectedIcon is not null;
    }

    private void UpdateIconRows()
    {
        _columns = Math.Max(1, (int)((IconList.ActualWidth - 32) / 72));
        IconList.ItemsSource = _icons.Chunk(_columns).ToArray();
    }

    private void ScrollToSelectedIcon()
    {
        if (_selectedIcon is { } selected)
            IconList.ScrollIntoView(IconList.Items[Array.IndexOf(_icons, selected) / _columns]);
    }

    private void IconList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_icons.Length > 0 && Math.Max(1, (int)((IconList.ActualWidth - 32) / 72)) != _columns)
        {
            bool hadKeyboardFocus = IconList.IsKeyboardFocusWithin;
            UpdateIconRows();
            ScrollToSelectedIcon();
            if (hadKeyboardFocus) FocusSelectedIcon();
        }
    }

    private void SetSelectedIcon(IconItem? item)
    {
        if (_selectedIcon is { } previous) previous.IsSelected = false;
        _selectedIcon = item;
        if (item is not null) item.IsSelected = true;
        SelectButton.IsEnabled = !_loading && item is not null;
    }

    private void IconButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_loading && sender is Button { DataContext: IconItem item })
            SetSelectedIcon(item);
    }

    private void IconButton_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_loading && sender is Button { DataContext: IconItem item })
            SetSelectedIcon(item);
    }

    private void FocusSelectedIcon()
    {
        if (_selectedIcon is not { } item) return;
        ScrollToSelectedIcon();
        IconList.UpdateLayout();
        var row = IconList.ItemContainerGenerator.ContainerFromIndex(Array.IndexOf(_icons, item) / _columns);
        Button? FindButton(DependencyObject root)
        {
            if (root is Button button && ReferenceEquals(button.DataContext, item)) return button;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                if (FindButton(VisualTreeHelper.GetChild(root, index)) is { } found) return found;
            return null;
        }
        if (row is not null && FindButton(row) is { } target)
            target.Focus();
    }

    private void IconList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_loading || _icons.Length == 0) return;
        if (e.Key == Key.Enter)
        {
            SelectButton_Click(sender, e);
            e.Handled = true;
            return;
        }
        int current = _selectedIcon is { } selected ? Array.IndexOf(_icons, selected) : 0;
        int next = e.Key switch
        {
            Key.Left => current - 1,
            Key.Right => current + 1,
            Key.Up => current - _columns,
            Key.Down => current + _columns,
            Key.Home => 0,
            Key.End => _icons.Length - 1,
            _ => -1,
        };
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)) return;
        next = Math.Clamp(next, 0, _icons.Length - 1);
        SetSelectedIcon(_icons[next]);
        FocusSelectedIcon();
        e.Handled = true;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loading || _selectedIcon is not { } item) return;
        SelectedIcon = item.Index < 0 ? item.Glyph : _selectedResourcePath;
        SelectedIconIndex = item.Index;
        DialogResult = true;
    }

    private void IconList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: IconItem item })
        {
            SetSelectedIcon(item);
            SelectButton_Click(sender, e);
        }
    }
}
