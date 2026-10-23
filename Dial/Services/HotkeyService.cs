using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Dial.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? _source;
    private readonly Dictionary<int, uint> _registered = new();
    private int _nextId = 1;
    private bool _disposed;
    private string? _currentHotkey;
    private uint _currentModifiers;

    public event Action? HotkeyPressed;

    public HotkeyService()
    {
        var p = new HwndSourceParameters("DialHotkeyWindow")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _registered.ContainsKey(wParam.ToInt32()))
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Register(string hotkey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (hotkey == _currentHotkey) return;
        if (!TryParse(hotkey, out uint mods, out uint vk))
            throw new ArgumentException("The keyboard shortcut is invalid.", nameof(hotkey));
        if (_registered.Values.Contains(vk) && _currentModifiers == mods) return;

        int id = _nextId++;
        if (!RegisterHotKey(_source!.Handle, id, mods | ModNoRepeat, vk))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        UnregisterAll();
        _registered[id] = vk;
        _currentHotkey = hotkey;
        _currentModifiers = mods;
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.Keys.ToList())
        {
            if (!UnregisterHotKey(_source!.Handle, id))
                Trace.TraceWarning("Could not unregister keyboard shortcut {0}: {1}", id, Marshal.GetLastWin32Error());
            _registered.Remove(id);
        }
        _currentHotkey = null;
        _currentModifiers = 0;
    }

    public static bool TryParse(string? hotkey, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;
        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts.Any(string.IsNullOrEmpty)) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= ModControl; break;
                case "alt": mods |= ModAlt; break;
                case "shift": mods |= ModShift; break;
                case "win":
                case "windows": mods |= ModWin; break;
                default: return false;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], true, out var key) || !Enum.IsDefined(key) ||
            !Enum.GetNames<Key>().Contains(parts[^1], StringComparer.OrdinalIgnoreCase) ||
            key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
            return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        return vk != 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
