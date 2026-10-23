using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Dial.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint NimAdd = 0x0;
    private const uint NimModify = 0x1;
    private const uint NimDelete = 0x2;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;
    private const uint NifInfo = 0x10;

    private const int WmTrayCallback = 0x8001;
    private const int WmCommand = 0x0111;
    private const int WmRButtonUp = 0x0205;
    private const int WmLButtonDblClk = 0x0203;

    private const int MenuOpen = 1;
    private const int MenuReload = 2;
    private const int MenuExit = 3;

    private HwndSource? _source;
    private IntPtr _icon;
    private bool _added;
    private bool _disposed;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");

    private readonly Action _onOpen;
    private readonly Action _onReload;
    private readonly Action _onExit;

    public TrayIconService(Action onOpen, Action onReload, Action onExit)
    {
        _onOpen = onOpen;
        _onReload = onReload;
        _onExit = onExit;

        var parameters = new HwndSourceParameters("DialTrayWindow")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        _icon = ExtractIcon();
        AddOrModify(NimAdd);
        _added = true;
    }

    public void ApplyLanguage()
    {
        if (_added)
        {
            AddOrModify(NimModify);
        }
    }

    public void Notify(string title, string message)
    {
        if (!_added) return;

        var data = CreateData();
        data.Flags = NifInfo;
        data.Info = message;
        data.InfoTitle = title;
        data.InfoFlags = 1; // NIIF_INFO
        data.TimeoutOrVersion = 2500;
        if (!Shell_NotifyIcon(NimModify, ref data))
            Trace.TraceWarning("The notification could not be displayed: {0}.", Marshal.GetLastWin32Error());
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == _taskbarCreated)
        {
            AddOrModify(NimAdd);
            _added = true;
        }
        else if (msg == WmTrayCallback)
        {
            int mouseMsg = lParam.ToInt32();
            if (mouseMsg == WmRButtonUp)
            {
                ShowContextMenu();
                handled = true;
            }
            else if (mouseMsg == WmLButtonDblClk)
            {
                _onOpen();
                handled = true;
            }
        }
        else if (msg == WmCommand)
        {
            int id = wParam.ToInt32() & 0xFFFF;
            switch (id)
            {
                case MenuOpen:
                    _onOpen();
                    handled = true;
                    break;
                case MenuReload:
                    _onReload();
                    handled = true;
                    break;
                case MenuExit:
                    _onExit();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private void ShowContextMenu()
    {
        IntPtr menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, 0, MenuOpen, LocalizationService.T("TrayOpenSettings"));
            AppendMenu(menu, 0, MenuReload, LocalizationService.T("TrayReload"));
            AppendMenu(menu, 0x800, 0, null); // MF_SEPARATOR
            AppendMenu(menu, 0, MenuExit, LocalizationService.T("TrayExit"));

            GetCursorPos(out NativePoint p);
            SetForegroundWindow(_source!.Handle);
            TrackPopupMenuEx(menu, 0x0002, p.X, p.Y, _source.Handle, IntPtr.Zero);
            PostMessage(_source.Handle, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void AddOrModify(uint message)
    {
        var data = CreateData();
        if (!Shell_NotifyIcon(message, ref data))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private NotifyIconData CreateData()
    {
        return new NotifyIconData
        {
            Size = Marshal.SizeOf<NotifyIconData>(),
            WindowHandle = _source!.Handle,
            Id = 1,
            Flags = NifMessage | NifIcon | NifTip,
            CallbackMessage = WmTrayCallback,
            IconHandle = _icon,
            Tip = LocalizationService.T("AppName"),
        };
    }

    private static IntPtr ExtractIcon()
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("The executable path is unavailable.");
        var large = new IntPtr[1];
        var small = new IntPtr[1];
        if (ExtractIconEx(exe, 0, large, small, 1) == 0)
            throw new Win32Exception("The application icon could not be extracted.");
        if (small[0] == IntPtr.Zero) return large[0];
        if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
        return small[0];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_added)
        {
            var data = CreateData();
            if (!Shell_NotifyIcon(NimDelete, ref data))
                Trace.TraceWarning("The notification icon could not be removed.");
            _added = false;
        }

        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }

        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemId;
        public IntPtr BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, int itemId, string? text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr windowHandle, IntPtr parameters);
}
