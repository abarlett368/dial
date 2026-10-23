using System.IO;
using System.Text.Json;
using System.Windows;
using Dial.Models;
using Dial.Services;

namespace Dial;

public partial class App : Application
{
    private ConfigService _configService = new();
    private MenuConfig _config = new();
    private HotkeyService? _hotkeys;
    private RadialWindow? _radial;
    private MainWindow? _settings;
    private TrayIconService? _tray;
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
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

    private void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = new MainWindow(_config)
            {
                SaveRequested = updated =>
                {
                    CommitConfiguration(updated, true);
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

    private void ReloadConfig()
    {
        try
        {
            var config = _configService.Load();
            CommitConfiguration(config, false);
            _settings?.Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or System.ComponentModel.Win32Exception or System.Security.SecurityException)
        {
            ErrorService.Report("ConfigLoadError", exception);
            return;
        }
        LocalizationService.SetLanguage(_config.Language);
        ThemeService.SetTheme(_config.Theme);
        _tray?.Notify(LocalizationService.T("AppName"), LocalizationService.T("ReloadNotification"));
    }

    private void CommitConfiguration(MenuConfig updated, bool persist)
    {
        ConfigService.Validate(updated);
        _hotkeys!.Register(updated.Hotkey);
        try
        {
            StartupService.SetEnabled(updated.LaunchAtStartup);
            if (persist) _configService.Save(updated);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            try
            {
                _hotkeys.Register(_config.Hotkey);
                StartupService.SetEnabled(_config.LaunchAtStartup);
            }
            catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException
                or System.Security.SecurityException or System.ComponentModel.Win32Exception)
            {
                throw new IOException(LocalizationService.T("RollbackError"), new AggregateException(exception, rollback));
            }
            throw;
        }
        _config = updated;
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
