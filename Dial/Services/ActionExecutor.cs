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

    public static async void Execute(MenuItemModel item)
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
                    RunSystemCommand(item.Command);
                    break;
                case MenuItemType.RunScript:
                    RunScript(item.Target);
                    break;
                case MenuItemType.ClipboardSnippet:
                    await SetClipboardAsync(item.Target);
                    SendChord(0x11, 0x56);
                    break;
            }
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

    private static void RunScript(string command)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + command,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static async Task SetClipboardAsync(string text)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (COMException) when (i < 4)
            {
                await Task.Delay(40);
            }
        }
    }

    private static void RunSystemCommand(SystemCommand cmd)
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
                PressKey(0xAF);
                break;
            case SystemCommand.VolumeDown:
                PressKey(0xAE);
                break;
            case SystemCommand.VolumeMute:
                PressKey(0xAD);
                break;
            case SystemCommand.Screenshot:
                ComboWinShiftKey(0x53); // Win+Shift+S (VK_S)
                break;
            case SystemCommand.ShowDesktop:
                ComboWinKey(0x44); // Win+D
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
                ComboWinKey(0x56); // Win+V (VK_V)
                break;
            case SystemCommand.EmojiPanel:
                ComboWinKey(0xBE); // Win+. (VK_OEM_PERIOD)
                break;
            case SystemCommand.RunDialog:
                ComboWinKey(0x52); // Win+R (VK_R)
                break;
        }
    }

    private static void PressKey(byte vk)
    {
        SendChord(vk);
    }

    private static void ComboWinKey(byte vk)
    {
        SendChord(0x5B, vk);
    }

    private static void ComboWinShiftKey(byte vk)
    {
        SendChord(0x5B, 0x10, vk);
    }

    private static void SendChord(params ushort[] keys)
    {
        var input = keys.Select(key => new NativeInput
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key } }
        }).Concat(keys.Reverse().Select(key => new NativeInput
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = 2 } }
        })).ToArray();
        if (SendInput((uint)input.Length, input, Marshal.SizeOf<NativeInput>()) != input.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error());
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
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
