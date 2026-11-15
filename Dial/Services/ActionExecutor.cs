using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Dial.Models;

namespace Dial.Services;

public static class ActionExecutor
{
    public static bool SupportsElevation(string target) =>
        !string.IsNullOrWhiteSpace(target) && (InstalledAppService.IsShellTarget(target)
            ? InstalledAppService.SupportsElevation(target)
            : !Uri.TryCreate(Environment.ExpandEnvironmentVariables(target), UriKind.Absolute, out var uri) || uri.IsFile);

    public static async void Execute(MenuItemModel item, IntPtr targetWindow)
    {
        try
        {
            switch (item.Type)
            {
                case MenuItemType.LaunchApp:
                    LaunchApp(item);
                    break;
                case MenuItemType.OpenFile:
                case MenuItemType.OpenUrl:
                    OpenPath(item.Target);
                    break;
                case MenuItemType.SystemCommand:
                    await RunSystemCommandAsync(item.Command, targetWindow);
                    break;
                case MenuItemType.RunScript:
                    Process.Start(CreateScriptStartInfo(item));
                    break;
                case MenuItemType.ClipboardSnippet:
                    await PasteAsync(item.Target, targetWindow);
                    break;
                case MenuItemType.SendShortcut:
                    await SendShortcutAsync(item.Target, targetWindow);
                    break;
            }
        }
        catch (OperationCanceledException exception) when (item.Type == MenuItemType.ClipboardSnippet)
        {
            ErrorService.Report("PasteTargetError", exception);
        }
        catch (OperationCanceledException exception) when (item.Type == MenuItemType.SendShortcut)
        {
            ErrorService.Report("ShortcutTargetError", exception);
        }
        catch (TimeoutException exception) when (item.Type == MenuItemType.SendShortcut)
        {
            ErrorService.Report("ShortcutReleaseKeys", exception);
        }
        catch (TimeoutException exception) when (item.Type == MenuItemType.ClipboardSnippet)
        {
            ErrorService.Report("PasteReleaseKeys", exception);
        }
        catch (OperationCanceledException exception) when (item.Type == MenuItemType.SystemCommand)
        {
            ErrorService.Report("SystemCommandTargetError", exception);
        }
        catch (TimeoutException exception) when (item.Type == MenuItemType.SystemCommand)
        {
            ErrorService.Report("SystemCommandReleaseKeys", exception);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            Trace.TraceInformation("The user cancelled the administrator launch: {0}", exception.Message);
        }
        catch (Exception ex)
        {
            ErrorService.Report("ActionError", ex);
        }
    }

    private static void LaunchApp(MenuItemModel item)
    {
        if (InstalledAppService.IsShellTarget(item.Target))
        {
            InstalledAppService.Activate(item.Target, item.Arguments, item.RunAsAdministrator);
            return;
        }
        Process.Start(CreateAppStartInfo(item));
    }

    public static ProcessStartInfo CreateAppStartInfo(MenuItemModel item)
    {
        if (item.Type != MenuItemType.LaunchApp || InstalledAppService.IsShellTarget(item.Target) ||
            item.RunAsAdministrator && !SupportsElevation(item.Target))
            throw new ArgumentException(LocalizationService.T("AppElevationNotSupported"), nameof(item));
        return new ProcessStartInfo
        {
            FileName = Environment.ExpandEnvironmentVariables(item.Target),
            Arguments = item.Arguments,
            UseShellExecute = true,
            Verb = item.RunAsAdministrator ? "runas" : "",
        };
    }

    private static void OpenPath(string target)
    {
        Process.Start(new ProcessStartInfo { FileName = Environment.ExpandEnvironmentVariables(target), UseShellExecute = true });
    }

    internal static ProcessStartInfo CreateScriptStartInfo(MenuItemModel item)
    {
        if (item.Type != MenuItemType.RunScript)
            throw new ArgumentException(nameof(item));

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        switch (item.Interpreter)
        {
            case ScriptInterpreter.CommandPrompt:
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/c " + item.Target;
                break;
            case ScriptInterpreter.PowerShell:
                startInfo.FileName = "powershell.exe";
                startInfo.ArgumentList.Add("-NoLogo");
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add(item.Target);
                break;
            case ScriptInterpreter.Custom:
                if (string.IsNullOrWhiteSpace(item.InterpreterPath))
                    throw new ArgumentException(LocalizationService.T("ScriptInterpreterRequired"), nameof(item));
                startInfo.FileName = Environment.ExpandEnvironmentVariables(item.InterpreterPath);
                startInfo.Arguments = item.InterpreterArguments.Contains("{command}", StringComparison.Ordinal)
                    ? item.InterpreterArguments.Replace("{command}", item.Target, StringComparison.Ordinal)
                    : string.IsNullOrWhiteSpace(item.InterpreterArguments)
                        ? item.Target
                        : $"{item.InterpreterArguments.TrimEnd()} {item.Target}";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
        return startInfo;
    }

    private static async Task PasteAsync(string text, IntPtr targetWindow)
    {
        for (int i = 0; i < 5; i++)
        {
            await WaitForPhysicalKeyReleaseAsync(() => EnsurePasteTarget(targetWindow), "PasteReleaseKeys");
            try
            {
                Clipboard.SetText(text);
                break;
            }
            catch (COMException) when (i < 4)
            {
                await Task.Delay(40);
            }
        }
        EnsurePasteTarget(targetWindow);
        SendChord(0x11, 0x56);
    }

    private static void EnsurePasteTarget(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero || GetForegroundWindow() != targetWindow)
            throw new OperationCanceledException(LocalizationService.T("PasteTargetError"));
    }

    private static async Task SendShortcutAsync(string shortcut, IntPtr targetWindow)
    {
        if (!HotkeyService.TryParseShortcut(shortcut, out uint modifiers, out uint key))
            throw new ArgumentException(LocalizationService.T("InvalidActionShortcut"), nameof(shortcut));
        await WaitForPhysicalKeyReleaseAsync(() => EnsureShortcutTarget(targetWindow), "ShortcutReleaseKeys");
        var keys = new List<ushort>();
        if ((modifiers & 2) != 0) keys.Add(0x11);
        if ((modifiers & 1) != 0) keys.Add(0x12);
        if ((modifiers & 4) != 0) keys.Add(0x10);
        if ((modifiers & 8) != 0) keys.Add(0x5B);
        keys.Add((ushort)key);
        SendChord(keys.ToArray());
    }

    private static void EnsureShortcutTarget(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero || GetForegroundWindow() != targetWindow)
            throw new OperationCanceledException(LocalizationService.T("ShortcutTargetError"));
    }

    internal static async Task WaitForPhysicalKeyReleaseAsync(Action ensureTarget, string releaseErrorKey,
        Func<bool>? anyKeyDown = null, TimeSpan? timeout = null)
    {
        anyKeyDown ??= () => Enumerable.Range(1, 254).Any(vk => (GetAsyncKeyState(vk) & 0x8000) != 0);
        var timer = Stopwatch.StartNew();
        while (true)
        {
            ensureTarget();
            if (!anyKeyDown()) break;
            if (timer.Elapsed >= (timeout ?? TimeSpan.FromSeconds(2)))
                throw new TimeoutException(LocalizationService.T(releaseErrorKey));
            await Task.Delay(20);
        }
        ensureTarget();
    }

    private static async Task SendSystemChordAsync(IntPtr targetWindow, params ushort[] keys)
    {
        void EnsureTarget()
        {
            if (targetWindow == IntPtr.Zero || GetForegroundWindow() != targetWindow)
                throw new OperationCanceledException(LocalizationService.T("SystemCommandTargetError"));
        }
        await WaitForPhysicalKeyReleaseAsync(EnsureTarget, "SystemCommandReleaseKeys");
        SendChord(keys);
    }

    private static async Task RunSystemCommandAsync(SystemCommand cmd, IntPtr targetWindow)
    {
        switch (cmd)
        {
            case SystemCommand.Lock:
                if (!LockWorkStation()) throw new Win32Exception(Marshal.GetLastWin32Error());
                break;
            case SystemCommand.Sleep:
                if (!SetSuspendState(false, false, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
                break;
            case SystemCommand.Restart:
                Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { UseShellExecute = true, CreateNoWindow = true });
                break;
            case SystemCommand.Shutdown:
                Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 0") { UseShellExecute = true, CreateNoWindow = true });
                break;
            case SystemCommand.SignOut:
                if (!ExitWindowsEx(0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
                break;
            case SystemCommand.VolumeUp:
                await SendSystemChordAsync(targetWindow, 0xAF);
                break;
            case SystemCommand.VolumeDown:
                await SendSystemChordAsync(targetWindow, 0xAE);
                break;
            case SystemCommand.VolumeMute:
                await SendSystemChordAsync(targetWindow, 0xAD);
                break;
            case SystemCommand.MediaPlayPause:
            case SystemCommand.MediaPrevious:
            case SystemCommand.MediaNext:
            case SystemCommand.MediaStop:
                await SendSystemChordAsync(targetWindow, MediaKeyForCommand(cmd));
                break;
            case SystemCommand.Screenshot:
                await SendSystemChordAsync(targetWindow, 0x5B, 0x10, 0x53);
                break;
            case SystemCommand.ShowDesktop:
                await SendSystemChordAsync(targetWindow, 0x5B, 0x44);
                break;
            case SystemCommand.TaskManager:
                Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
                break;
            case SystemCommand.EmptyRecycleBin:
                Marshal.ThrowExceptionForHR(SHEmptyRecycleBin(IntPtr.Zero, null, 0));
                break;
            case SystemCommand.OpenSettings:
                Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
                break;
            case SystemCommand.ControlPanel:
                Process.Start(new ProcessStartInfo("control.exe") { UseShellExecute = true });
                break;
            case SystemCommand.ClipboardHistory:
                await SendSystemChordAsync(targetWindow, 0x5B, 0x56);
                break;
            case SystemCommand.EmojiPanel:
                await SendSystemChordAsync(targetWindow, 0x5B, 0xBE);
                break;
            case SystemCommand.RunDialog:
                await SendSystemChordAsync(targetWindow, 0x5B, 0x52);
                break;
        }
    }

    private static byte MediaKeyForCommand(SystemCommand command) => command switch
    {
        SystemCommand.MediaPlayPause => 0xB3,
        SystemCommand.MediaPrevious => 0xB1,
        SystemCommand.MediaNext => 0xB0,
        SystemCommand.MediaStop => 0xB2,
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    private static void SendChord(params ushort[] keys)
    {
        var input = CreateChordInput(keys);
        uint sent = SendInput((uint)input.Length, input, Marshal.SizeOf<NativeInput>());
        if (sent != input.Length)
        {
            int error = Marshal.GetLastWin32Error();
            var releases = input.Skip(keys.Length).ToArray();
            if (SendInput((uint)releases.Length, releases, Marshal.SizeOf<NativeInput>()) != releases.Length)
                Trace.TraceWarning("Could not release all keys after incomplete input delivery.");
            throw new Win32Exception(error, LocalizationService.T("ShortcutSendFailed"));
        }
    }

    private static NativeInput[] CreateChordInput(ushort[] keys)
    {
        uint Flags(ushort key) => key is >= 0x21 and <= 0x28 or 0x2D or 0x2E
            or 0x5B or 0x5C or 0x6F or 0x90 or 0xA3 or 0xA5 or >= 0xA6 and <= 0xB7 ? 1u : 0u;
        return keys.Select(key => new NativeInput
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = Flags(key) } }
        }).Concat(keys.Reverse().Select(key => new NativeInput
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = Flags(key) | 2 } }
        })).ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] input, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
