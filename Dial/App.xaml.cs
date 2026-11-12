using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using Dial.Models;
using Dial.Services;

namespace Dial;

public partial class App : Application
{
    public static string DisplayVersion => $"v{typeof(App).Assembly.GetName().Version!.ToString(3)}";

    private ConfigService _configService = new();
    private MenuConfig _config = new();
    private HotkeyService? _hotkeys;
    private RadialWindow? _radial;
    private MainWindow? _settings;
    private TrayIconService? _tray;
    private SingleInstanceService? _singleInstance;
    private readonly SemaphoreSlim _configurationGate = new(1, 1);
    private bool _openingSettings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ErrorService.InitializeLog(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dial", "logs"));
        int configArgument = Array.IndexOf(e.Args, "--config");
        if (configArgument >= 0)
        {
            if (configArgument + 1 >= e.Args.Length)
            {
                ErrorService.Report("ConfigLoadError", new ArgumentException("Specify a settings path after --config."));
                Shutdown(1);
                return;
            }
            _configService = new ConfigService(e.Args[configArgument + 1]);
        }

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsOwner)
        {
            _singleInstance.ActivateOwner();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            ErrorService.Report("UnexpectedError", args.Exception);
            args.Handled = true;
            Shutdown(1);
        };

        try
        {
            _config = _configService.Load();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
        {
            ErrorService.Report("ConfigLoadError", exception);
            Shutdown(1);
            return;
        }

        LocalizationService.SetLanguage(_config.Language);
        LocalizationService.LanguageChanged += OnLanguageChanged;

        ThemeService.SetTheme(_config.Theme);
        ThemeService.ThemeChanged += OnThemeChanged;

        _radial = new RadialWindow();

        _hotkeys = new HotkeyService();
        _hotkeys.HotkeyPressed += ShowRadialMenu;
        TryRegisterHotkey();

        _tray = new TrayIconService(ShowSettings, ReloadConfig, ExitApplication);
        _singleInstance.Listen(() => Dispatcher.BeginInvoke(ShowSettings));

        if (e.Args.Contains("--settings") || !_configService.Existed)
        {
            ShowSettings();
        }
    }

    private void TryRegisterHotkey()
    {
        if (_hotkeys is not null && !string.IsNullOrWhiteSpace(_config.Hotkey))
        {
            try
            {
                _hotkeys.Register(_config.Hotkey);
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                ErrorService.Report("HotkeyError", exception);
            }
        }
    }

    private void ShowRadialMenu()
    {
        _radial?.ShowMenu(_config.Items, _config.ShowAtMouse, _config.HoverColor, _config.IconMode, _config.ShowCenter);
    }

    private async void ShowSettings()
    {
        if (_openingSettings) return;
        _openingSettings = true;
        try
        {
            if (_settings is null)
            {
                if (StartupService.IsPackaged)
                    _config.LaunchAtStartup = await StartupService.IsEnabledAsync();
                _settings = new MainWindow(_config)
                {
                    SaveRequested = async updated =>
                    {
                        await CommitConfigurationAsync(() => updated, true);
                        LocalizationService.SetLanguage(updated.Language);
                        ThemeService.SetTheme(updated.Theme);
                    }
                };
                _settings.ShortcutRecordingChanged += recording =>
                {
                    if (recording) _hotkeys!.UnregisterAll();
                    else TryRegisterHotkey();
                };
                _settings.Closed += (_, _) => _settings = null;
            }

            _settings.Show();
            _settings.Activate();
            if (_settings.WindowState == WindowState.Minimized)
                _settings.WindowState = WindowState.Normal;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or COMException or System.Security.SecurityException)
        {
            ErrorService.Report("ConfigLoadError", exception);
        }
        finally { _openingSettings = false; }
    }

    private void OnLanguageChanged()
    {
        _tray?.ApplyLanguage();
        _settings?.ApplyLanguage();
        _radial?.RefreshAppearance();
    }

    private void OnThemeChanged()
    {
        _settings?.ApplyTheme();
        _radial?.RefreshAppearance();
    }

    private async void ReloadConfig()
    {
        try
        {
            await CommitConfigurationAsync(_configService.Load, false);
            _settings?.Close();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
            or System.ComponentModel.Win32Exception or COMException or System.Security.SecurityException)
        {
            ErrorService.Report("ConfigLoadError", exception);
            return;
        }
        LocalizationService.SetLanguage(_config.Language);
        ThemeService.SetTheme(_config.Theme);
        _tray?.Notify(LocalizationService.T("AppName"), LocalizationService.T("ReloadNotification"));
    }

    private async Task CommitConfigurationAsync(Func<MenuConfig> readConfiguration, bool persist)
    {
        await _configurationGate.WaitAsync();
        try
        {
            var updated = readConfiguration();
            ConfigService.Validate(updated);
            bool startupWasEnabled = await StartupService.IsEnabledAsync();
            _hotkeys!.Register(updated.Hotkey);
            try
            {
                await StartupService.SetEnabledAsync(updated.LaunchAtStartup);
                if (persist) _configService.Save(updated);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or System.ComponentModel.Win32Exception or COMException or System.Security.SecurityException)
            {
                try
                {
                    _hotkeys.Register(_config.Hotkey);
                    await StartupService.SetEnabledAsync(startupWasEnabled);
                }
                catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException
                    or System.Security.SecurityException or System.ComponentModel.Win32Exception or COMException)
                {
                    throw new IOException(LocalizationService.T("RollbackError"), new AggregateException(exception, rollback));
                }
                throw;
            }
            _config = updated;
        }
        finally { _configurationGate.Release(); }
    }

    private void ExitApplication()
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _radial?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _radial?.Close();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        ThemeService.ThemeChanged -= OnThemeChanged;
        _singleInstance?.Dispose();
        NativeBackdrop.ReleaseDispatcherQueue();
        base.OnExit(e);
    }
}
