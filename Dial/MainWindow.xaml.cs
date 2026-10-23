using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Dial.Models;
using Dial.Services;
using Microsoft.Win32;

namespace Dial;

public partial class MainWindow : Window
{
    private sealed record TypeItem(MenuItemType Type, string LocKey)
    {
        public string Name => LocalizationService.T(LocKey);
    }

    private sealed record CommandItem(SystemCommand Command, string LocKey)
    {
        public string Name => LocalizationService.T(LocKey);
    }

    private sealed record ThemeItem(string Code, string LocKey)
    {
        public string Name => LocalizationService.T(LocKey);
    }

    private sealed record LanguageItem(string Code, string LocKey)
    {
        public string Name => LocalizationService.T(LocKey);
    }

    private sealed record HoverColorItem(string Key, string LocKey)
    {
        public string Name => LocalizationService.T(LocKey);
    }

    private sealed record IconModeItem(IconMode Mode, string LocKey)
    {
        public string Name => LocalizationService.T(LocKey);
    }

    private readonly MenuConfig _config;
    private readonly ObservableCollection<MenuItemModel> _items;
    private bool _loading;
    private bool _recordingHotkey;
    private string _previousHotkey = "";
    public event Action<bool>? ShortcutRecordingChanged;

    private TypeItem[] _typeItems = Array.Empty<TypeItem>();
    private CommandItem[] _commandItems = Array.Empty<CommandItem>();
    private ThemeItem[] _themeItems = Array.Empty<ThemeItem>();
    private LanguageItem[] _languageItems = Array.Empty<LanguageItem>();
    private HoverColorItem[] _hoverColorItems = Array.Empty<HoverColorItem>();
    private IconModeItem[] _iconModeItems = Array.Empty<IconModeItem>();

    public Action<MenuConfig>? SaveRequested { get; init; }

    public MainWindow(MenuConfig config)
    {
        InitializeComponent();

        _config = ConfigService.Clone(config);
        _items = new ObservableCollection<MenuItemModel>(_config.Items);
        ItemList.ItemsSource = _items;

        _themeItems = new[]
        {
            new ThemeItem("Light", "ThemeLight"),
            new ThemeItem("Dark", "ThemeDark"),
        };
        ThemeBox.ItemsSource = _themeItems;
        ThemeBox.DisplayMemberPath = nameof(ThemeItem.Name);

        _languageItems = new[]
        {
            new LanguageItem("en", "LanguageEnglish"),
            new LanguageItem("zh-CN", "LanguageChinese"),
        };
        LanguageBox.ItemsSource = _languageItems;
        LanguageBox.DisplayMemberPath = nameof(LanguageItem.Name);

        _hoverColorItems = HoverPalette.Options
            .Select(o => new HoverColorItem(o.Key, o.LocKey))
            .ToArray();
        HoverColorBox.ItemsSource = _hoverColorItems;
        HoverColorBox.DisplayMemberPath = nameof(HoverColorItem.Name);

        _iconModeItems = new[]
        {
            new IconModeItem(Models.IconMode.IconOutside, "IconModeOutside"),
            new IconModeItem(Models.IconMode.IconInside, "IconModeInside"),
            new IconModeItem(Models.IconMode.LabelsOnly, "IconModeLabelsOnly"),
            new IconModeItem(Models.IconMode.IconsOnly, "IconModeIconsOnly"),
        };
        IconModeBox.ItemsSource = _iconModeItems;
        IconModeBox.DisplayMemberPath = nameof(IconModeItem.Name);

        RefreshItemIcons();
        ShowVersion();

        RebuildLocalizedLists();
        LoadFromConfig();
        UpdateSaveAccent();

        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(OnEdited));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnEdited));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnEdited));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnEdited));
        Closed += (_, _) =>
        {
            EndShortcutRecording(false);
            _statusTimer?.Stop();
        };
        SourceInitialized += (_, _) => ApplyTitleBarTheme();
    }

    private void RefreshItemIcons()
    {
        foreach (var item in _items)
        {
            item.RefreshDisplayIcon();
        }
    }

    private void ShowVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
    }

    private MenuItemModel? Selected => ItemList.SelectedItem as MenuItemModel;

    public void ApplyLanguage()
    {
        RebuildLocalizedLists();
        if (Selected is not null) UpdateFieldVisibility(Selected.Type);
    }

    public void ApplyTheme()
    {
        RebuildLocalizedLists();
        UpdateSaveAccent();
        ItemList.Items.Refresh();
        ApplyTitleBarTheme();
    }

    private void ApplyTitleBarTheme()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = ThemeService.IsDark ? 1 : 0;
        int result = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        if (result < 0) Trace.TraceWarning("The title bar theme is unavailable: 0x{0:X8}", result);
    }

    private void RebuildLocalizedLists()
    {
        string theme = (ThemeBox.SelectedItem as ThemeItem)?.Code ?? _config.Theme;
        string language = (LanguageBox.SelectedItem as LanguageItem)?.Code ?? _config.Language;
        string accent = (HoverColorBox.SelectedItem as HoverColorItem)?.Key ?? _config.HoverColor;
        IconMode layout = (IconModeBox.SelectedItem as IconModeItem)?.Mode ?? _config.IconMode;
        bool wasLoading = _loading;
        _loading = true;

        _typeItems = new[]
        {
            new TypeItem(MenuItemType.LaunchApp, "TypeLaunchApp"),
            new TypeItem(MenuItemType.OpenFile, "TypeOpenFile"),
            new TypeItem(MenuItemType.OpenUrl, "TypeOpenUrl"),
            new TypeItem(MenuItemType.SystemCommand, "TypeSystemCommand"),
            new TypeItem(MenuItemType.RunScript, "TypeRunScript"),
            new TypeItem(MenuItemType.ClipboardSnippet, "TypeClipboard"),
        };

        _commandItems = new[]
        {
            new CommandItem(SystemCommand.Lock, "CmdLock"),
            new CommandItem(SystemCommand.Sleep, "CmdSleep"),
            new CommandItem(SystemCommand.Restart, "CmdRestart"),
            new CommandItem(SystemCommand.Shutdown, "CmdShutdown"),
            new CommandItem(SystemCommand.SignOut, "CmdSignOut"),
            new CommandItem(SystemCommand.VolumeUp, "CmdVolumeUp"),
            new CommandItem(SystemCommand.VolumeDown, "CmdVolumeDown"),
            new CommandItem(SystemCommand.VolumeMute, "CmdVolumeMute"),
            new CommandItem(SystemCommand.Screenshot, "CmdScreenshot"),
            new CommandItem(SystemCommand.TaskManager, "CmdTaskManager"),
            new CommandItem(SystemCommand.ShowDesktop, "CmdShowDesktop"),
            new CommandItem(SystemCommand.EmptyRecycleBin, "CmdEmptyRecycleBin"),
            new CommandItem(SystemCommand.OpenSettings, "CmdOpenSettings"),
            new CommandItem(SystemCommand.ControlPanel, "CmdControlPanel"),
            new CommandItem(SystemCommand.ClipboardHistory, "CmdClipboardHistory"),
            new CommandItem(SystemCommand.EmojiPanel, "CmdEmojiPanel"),
            new CommandItem(SystemCommand.RunDialog, "CmdRunDialog"),
        };

        TypeBox.ItemsSource = _typeItems;
        TypeBox.DisplayMemberPath = nameof(TypeItem.Name);
        CommandBox.ItemsSource = _commandItems;
        CommandBox.DisplayMemberPath = nameof(CommandItem.Name);

        var selected = Selected;
        TypeBox.SelectedItem = _typeItems.FirstOrDefault(t => t.Type == selected?.Type) ?? _typeItems[0];
        CommandBox.SelectedItem = _commandItems.FirstOrDefault(c => c.Command == selected?.Command) ?? _commandItems[0];

        ThemeBox.ItemsSource = null;
        ThemeBox.ItemsSource = _themeItems;
        ThemeBox.SelectedItem = _themeItems.FirstOrDefault(t => t.Code == theme) ?? _themeItems[0];
        LanguageBox.ItemsSource = null;
        LanguageBox.ItemsSource = _languageItems;
        LanguageBox.SelectedItem = _languageItems.FirstOrDefault(l => l.Code == language) ?? _languageItems[0];
        HoverColorBox.ItemsSource = null;
        HoverColorBox.ItemsSource = _hoverColorItems;
        HoverColorBox.SelectedItem = _hoverColorItems.FirstOrDefault(
            h => h.Key == accent) ?? _hoverColorItems[0];
        IconModeBox.ItemsSource = null;
        IconModeBox.ItemsSource = _iconModeItems;
        IconModeBox.SelectedItem = _iconModeItems.FirstOrDefault(
            m => m.Mode == layout) ?? _iconModeItems[0];

        _loading = wasLoading;
    }

    private void LoadFromConfig()
    {
        _loading = true;

        HotkeyBox.Text = _config.Hotkey;
        StartupBox.IsChecked = _config.LaunchAtStartup;
        ShowAtMouseBox.IsChecked = _config.ShowAtMouse;
        ShowCenterBox.IsChecked = _config.ShowCenter;
        IconModeBox.SelectedItem = _iconModeItems.FirstOrDefault(
            m => m.Mode == _config.IconMode) ?? _iconModeItems[0];
        ThemeBox.SelectedItem = _themeItems.FirstOrDefault(t => t.Code == _config.Theme);
        LanguageBox.SelectedItem = _languageItems.FirstOrDefault(l => l.Code == _config.Language);
        HoverColorBox.SelectedItem = _hoverColorItems.FirstOrDefault(
            h => h.Key == _config.HoverColor) ?? _hoverColorItems[0];

        if (_items.Count > 0) ItemList.SelectedIndex = 0;
        else LoadDetail();

        _loading = false;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count >= DialGeometry.MaximumItems)
        {
            ShowTransientStatus(LocalizationService.T("MaxItemsReached"));
            return;
        }

        var item = new MenuItemModel { Label = LocalizationService.T("NewItem"), Type = MenuItemType.LaunchApp };
        item.RefreshDisplayIcon();
        int idx = ItemList.SelectedIndex;
        if (idx >= 0 && idx < _items.Count) _items.Insert(idx + 1, item);
        else _items.Add(item);
        ItemList.SelectedItem = item;
        ClearStatus();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        int idx = ItemList.SelectedIndex;
        if (idx < 0) return;
        _items.RemoveAt(idx);
        if (_items.Count > 0) ItemList.SelectedIndex = Math.Min(idx, _items.Count - 1);
        else LoadDetail();
        ClearStatus();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void DownButton_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        int idx = ItemList.SelectedIndex;
        if (idx < 0) return;
        int target = idx + delta;
        if (target < 0 || target >= _items.Count) return;
        _items.Move(idx, target);
        ItemList.SelectedIndex = target;
        ClearStatus();
    }

    private void ItemList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadDetail();
    }

    private void LoadDetail()
    {
        var item = Selected;
        ItemCard.IsEnabled = item is not null;
        if (item is null) return;

        _loading = true;
        LabelBox.Text = item.Label;
        TypeBox.SelectedItem = _typeItems.FirstOrDefault(t => t.Type == item.Type) ?? _typeItems[0];
        TargetBox.Text = item.Target;
        ArgsBox.Text = item.Arguments;
        AdministratorBox.IsChecked = item.RunAsAdministrator;
        CommandBox.SelectedItem = _commandItems.FirstOrDefault(c => c.Command == item.Command) ?? _commandItems[0];
        IconBox.Text = item.Icon;
        IconBox.FontFamily = new System.Windows.Media.FontFamily(item.DisplayFontFamily);
        EnabledBox.IsChecked = item.Enabled;
        UpdateFieldVisibility(item.Type);
        _loading = false;
    }

    private void UpdateFieldVisibility(MenuItemType type)
    {
        bool showTarget = type != MenuItemType.SystemCommand;
        bool showArgs = type == MenuItemType.LaunchApp;
        bool showCommand = type == MenuItemType.SystemCommand;
        bool showBrowse = type is MenuItemType.LaunchApp or MenuItemType.OpenFile;

        TargetLabel.Visibility = showTarget ? Visibility.Visible : Visibility.Collapsed;
        TargetPanel.Visibility = showTarget ? Visibility.Visible : Visibility.Collapsed;
        BrowseButton.Visibility = showBrowse ? Visibility.Visible : Visibility.Collapsed;
        FolderBrowseButton.Visibility = type == MenuItemType.OpenFile ? Visibility.Visible : Visibility.Collapsed;
        InstalledAppsButton.Visibility = type == MenuItemType.LaunchApp ? Visibility.Visible : Visibility.Collapsed;
        ArgsLabel.Visibility = showArgs ? Visibility.Visible : Visibility.Collapsed;
        ArgsBox.Visibility = showArgs ? Visibility.Visible : Visibility.Collapsed;
        AdministratorBox.Visibility = showArgs ? Visibility.Visible : Visibility.Collapsed;
        CmdLabel.Visibility = showCommand ? Visibility.Visible : Visibility.Collapsed;
        CommandBox.Visibility = showCommand ? Visibility.Visible : Visibility.Collapsed;

        TargetLabel.Text = LocalizationService.T(type switch
        {
            MenuItemType.LaunchApp => "TargetLaunchApp",
            MenuItemType.OpenFile => "TargetOpenFile",
            MenuItemType.OpenUrl => "TargetOpenUrl",
            MenuItemType.RunScript => "TargetRunScript",
            MenuItemType.ClipboardSnippet => "TargetClipboard",
            _ => "TargetField",
        });
    }

    private void LabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is null) return;
        Selected.Label = LabelBox.Text;
        ItemList.Items.Refresh();
    }

    private void TargetBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is null) return;
        Selected.Target = TargetBox.Text;
        RefreshSelectedIcon();
    }

    private void ArgsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is null) return;
        Selected.Arguments = ArgsBox.Text;
    }

    private void AdministratorBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || Selected is null) return;
        Selected.RunAsAdministrator = AdministratorBox.IsChecked == true;
    }

    private void IconBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Selected is null) return;
        Selected.Icon = IconBox.Text;
        RefreshSelectedIcon();
        IconBox.FontFamily = new System.Windows.Media.FontFamily(Selected.DisplayFontFamily);
    }

    private void EnabledBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || Selected is null) return;
        Selected.Enabled = EnabledBox.IsChecked == true;
    }

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected is null) return;
        var t = (TypeBox.SelectedItem as TypeItem)?.Type ?? MenuItemType.LaunchApp;
        Selected.Type = t;
        UpdateFieldVisibility(t);
        RefreshSelectedIcon();
    }

    private void CommandBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || Selected is null) return;
        var c = (CommandBox.SelectedItem as CommandItem)?.Command ?? SystemCommand.Lock;
        Selected.Command = c;
        RefreshSelectedIcon();
    }

    private void RefreshSelectedIcon()
    {
        Selected?.RefreshDisplayIcon();
        ItemList.Items.Refresh();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = LocalizationService.T("SelectFile"),
            Filter = Selected?.Type == MenuItemType.OpenFile
                ? LocalizationService.T("AllFilesFilter")
                : LocalizationService.T("ProgramsFilter"),
        };
        if (dlg.ShowDialog(this) == true)
        {
            TargetBox.Text = dlg.FileName;
            if (Selected is not null) Selected.Target = dlg.FileName;
        }

    }

    private void FolderBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = LocalizationService.T("SelectFolder") };
        if (dialog.ShowDialog(this) == true) TargetBox.Text = dialog.FolderName;
    }

    private void InstalledAppsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new InstalledAppsWindow(InstalledAppService.GetApps()) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedApp is { } app)
            {
                TargetBox.Text = app.Target;
                LabelBox.Text = app.Name;
                ArgsBox.Text = "";
            }
        }
        catch (Exception exception) when (exception is COMException or Win32Exception)
        {
            ErrorService.Report("InstalledAppsError", exception);
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recordingHotkey) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape || (key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None))
        {
            EndShortcutRecording(true);
            e.Handled = key == Key.Escape;
            return;
        }
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        if (parts.Count == 0) return;
        parts.Add(key.ToString());

        string shortcut = string.Join("+", parts);
        if (!HotkeyService.TryParse(shortcut, out _, out _)) return;
        HotkeyBox.Text = shortcut;
        EndShortcutRecording(false);
    }

    private void RecordHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingHotkey) { EndShortcutRecording(true); return; }
        _previousHotkey = HotkeyBox.Text;
        _recordingHotkey = true;
        RecordHotkeyButton.Content = LocalizationService.T("Cancel");
        ShortcutRecordingChanged?.Invoke(true);
        HotkeyBox.Focus();
        ShowTransientStatus(LocalizationService.T("RecordShortcutHint"));
    }

    private void ShortcutControl_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus != RecordHotkeyButton && e.NewFocus != HotkeyBox) EndShortcutRecording(true);
    }

    private void EndShortcutRecording(bool cancel)
    {
        if (!_recordingHotkey) return;
        _recordingHotkey = false;
        if (cancel) HotkeyBox.Text = _previousHotkey;
        RecordHotkeyButton.Content = LocalizationService.T("RecordShortcut");
        ClearStatus();
        ShortcutRecordingChanged?.Invoke(false);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        EndShortcutRecording(true);
        _config.Hotkey = HotkeyBox.Text.Trim();
        _config.Theme = (ThemeBox.SelectedItem as ThemeItem)?.Code ?? "Light";
        _config.Language = (LanguageBox.SelectedItem as LanguageItem)?.Code ?? "en";
        _config.HoverColor = (HoverColorBox.SelectedItem as HoverColorItem)?.Key ?? "windowsBlue";
        _config.ShowAtMouse = ShowAtMouseBox.IsChecked == true;
        _config.ShowCenter = ShowCenterBox.IsChecked == true;
        _config.IconMode = (IconModeBox.SelectedItem as IconModeItem)?.Mode ?? Models.IconMode.IconOutside;
        _config.LaunchAtStartup = StartupBox.IsChecked == true;
        _config.Items = _items.ToList();

        try
        {
            ConfigService.Validate(_config);
            if (SaveRequested is null)
                throw new InvalidOperationException("No configuration save handler is registered.");
            SaveRequested(ConfigService.Clone(_config));
            ShowTransientStatus(LocalizationService.T("SavedStatus"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException)
        {
            ErrorService.Report("ConfigSaveError", exception);
        }
    }

    private DispatcherTimer? _statusTimer;

    private void OnEdited(object sender, RoutedEventArgs e)
    {
        if (!_loading && ReferenceEquals(e.OriginalSource, HoverColorBox)) UpdateSaveAccent();
        if (!_loading) ClearStatus();
    }

    private void UpdateSaveAccent()
    {
        if (SystemParameters.HighContrast)
        {
            SaveButton.Background = SaveButton.BorderBrush = SystemColors.HighlightBrush;
            SaveButton.Foreground = SystemColors.HighlightTextBrush;
            return;
        }
        string key = (HoverColorBox.SelectedItem as HoverColorItem)?.Key ?? _config.HoverColor;
        var color = (Color)ColorConverter.ConvertFromString(HoverPalette.Resolve(key, ThemeService.IsDark));
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        SaveButton.Background = SaveButton.BorderBrush = brush;
        SaveButton.Foreground = HoverPalette.Luminance(color) > 0.179 ? Brushes.Black : Brushes.White;
    }

    private void ClearStatus()
    {
        _statusTimer?.Stop();
        StatusText.Visibility = Visibility.Collapsed;
    }

    private void ShowTransientStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;

        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) =>
        {
            StatusText.Visibility = Visibility.Collapsed;
            _statusTimer.Stop();
        };
        _statusTimer.Start();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
