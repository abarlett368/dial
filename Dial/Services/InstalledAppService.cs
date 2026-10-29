using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Dial.Services;

public sealed record InstalledApp(string Name, string Target);

public static class InstalledAppService
{
    public const string TargetPrefix = @"shell:AppsFolder\";
    private static readonly Guid ShellItemId = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    public static bool IsShellTarget(string target) =>
        target.StartsWith(TargetPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsValidTarget(string target) =>
        IsShellTarget(target) && target.Length > TargetPrefix.Length &&
        target[TargetPrefix.Length..].All(character => !char.IsControl(character) &&
            character is not ('\\' or '/' or '"' or '<' or '>' or '|'));

    public static bool SupportsElevation(string target)
    {
        if (!IsValidTarget(target)) return false;
        IShellItem? item = null;
        object? handler = null;
        IntPtr menu = IntPtr.Zero;
        try
        {
            Guid itemId = ShellItemId;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(target, IntPtr.Zero, ref itemId, out item));
            Guid handlerId = new("3981e225-f559-11d3-8e3a-00c04f6837d5");
            Guid contextMenuId = typeof(IContextMenu).GUID;
            item.BindToHandler(IntPtr.Zero, ref handlerId, ref contextMenuId, out handler);
            var contextMenu = (IContextMenu)handler;
            menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            int result = contextMenu.QueryContextMenu(menu, 0, 1, 0x7fff, 0);
            Marshal.ThrowExceptionForHR(result);
            var verb = new StringBuilder(256);
            for (uint index = 0; index < (result & 0xffff); index++)
            {
                verb.Clear();
                if (contextMenu.GetCommandString(new UIntPtr(index), 4, IntPtr.Zero, verb, (uint)verb.Capacity) >= 0 &&
                    string.Equals(verb.ToString(), "runas", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("The installed app's elevation support could not be read for {0}: {1}",
                target, exception.Message);
            return false;
        }
        finally
        {
            if (menu != IntPtr.Zero) DestroyMenu(menu);
            if (handler is not null) Marshal.ReleaseComObject(handler);
            if (item is not null) Marshal.ReleaseComObject(item);
        }
    }

    public static void Activate(string target, string arguments, bool runAsAdministrator = false)
    {
        if (!IsValidTarget(target)) throw new ArgumentException(LocalizationService.T("InvalidAppTarget"));
        if (runAsAdministrator)
        {
            if (!SupportsElevation(target))
                throw new InvalidOperationException(LocalizationService.T("AppElevationNotSupported"));
            ActivateElevated(target, arguments);
            return;
        }
        string id = target[TargetPrefix.Length..];
        if (!id.Contains('!'))
        {
            if (!string.IsNullOrWhiteSpace(arguments))
                throw new ArgumentException(LocalizationService.T("AppArgumentsNotSupported"));
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add(target);
            Process.Start(start);
            return;
        }
        Type type = Type.GetTypeFromCLSID(new Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c"), true)!;
        var manager = (IApplicationActivationManager)Activator.CreateInstance(type)!;
        try { manager.ActivateApplication(id, arguments, 0, out _); }
        finally { Marshal.ReleaseComObject(manager); }
    }

    private static void ActivateElevated(string target, string arguments)
    {
        Marshal.ThrowExceptionForHR(SHParseDisplayName(target, IntPtr.Zero, out IntPtr itemId, 0, out _));
        try
        {
            var info = new ShellExecuteInfo
            {
                Size = Marshal.SizeOf<ShellExecuteInfo>(),
                Mask = 0x0000000c | 0x00000100 | 0x00000400, // Invoke the item's verb synchronously; report errors ourselves.
                Verb = "runas",
                Parameters = arguments,
                Show = 1,
                ItemIdList = itemId,
            };
            if (!ShellExecuteEx(ref info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeCoTaskMem(itemId); }
    }

    public static IReadOnlyList<InstalledApp> GetApps()
    {
        Guid itemId = ShellItemId;
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, ref itemId, out var folder));
        try
        {
            Guid handler = new("94f60519-2850-4924-aa5a-d15e84868039");
            Guid enumId = typeof(IEnumShellItems).GUID;
            folder.BindToHandler(IntPtr.Zero, ref handler, ref enumId, out var bound);
            var enumerator = (IEnumShellItems)bound;
            try
            {
                var apps = new List<InstalledApp>();
                int result;
                while ((result = enumerator.Next(1, out var item, out uint fetched)) == 0 && fetched == 1)
                {
                    try
                    {
                        string name = GetName(item, 0);
                        string id = GetName(item, 0x80018001);
                        string target = TargetPrefix + id;
                        if (!string.IsNullOrWhiteSpace(name) && IsValidTarget(target))
                            apps.Add(new InstalledApp(name, target));
                    }
                    catch (COMException exception)
                    {
                        Trace.TraceWarning("An installed app could not be read: {0}", exception.Message);
                    }
                    finally { Marshal.ReleaseComObject(item); }
                }
                Marshal.ThrowExceptionForHR(result);
                return apps.DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
            finally { Marshal.ReleaseComObject(enumerator); }
        }
        finally { Marshal.ReleaseComObject(folder); }
    }

    public static BitmapSource? GetIcon(string target)
    {
        if (!IsValidTarget(target)) return null;
        Guid itemId = ShellItemId;
        IShellItem? item = null;
        IntPtr bitmap = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(target, IntPtr.Zero, ref itemId, out item));
            var factory = (IShellItemImageFactory)item;
            factory.GetImage(new NativeSize { Width = 48, Height = 48 }, 0x4, out bitmap);
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        catch (COMException exception)
        {
            Trace.TraceWarning("The installed app icon could not be loaded for {0}: {1}", target, exception.Message);
            return null;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (item is not null) Marshal.ReleaseComObject(item);
        }
    }

    private static string GetName(IShellItem item, uint format)
    {
        item.GetDisplayName(format, out IntPtr name);
        try { return Marshal.PtrToStringUni(name) ?? throw new COMException("The app name is unavailable."); }
        finally { Marshal.FreeCoTaskMem(name); }
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid id,
            [MarshalAs(UnmanagedType.Interface)] out object value);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint format, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem item, uint hint, out int order);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig] int Next(uint count, out IShellItem item, out uint fetched);
        void Skip(uint count);
        void Reset();
        void Clone(out IEnumShellItems items);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, uint flags, out IntPtr bitmap);
    }

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
        void ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items,
            [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
        void ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr items, out uint processId);
    }

    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint firstId, uint lastId, uint flags);
        void InvokeCommand(IntPtr info);
        [PreserveSig] int GetCommandString(UIntPtr command, uint flags, IntPtr reserved,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, uint capacity);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellExecuteInfo
    {
        public int Size;
        public uint Mask;
        public IntPtr Window;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Verb;
        [MarshalAs(UnmanagedType.LPWStr)] public string? File;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Parameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Directory;
        public int Show;
        public IntPtr Instance;
        public IntPtr ItemIdList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Class;
        public IntPtr ClassKey;
        public uint HotKey;
        public IntPtr IconOrMonitor;
        public IntPtr Process;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width, Height; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid id, out IShellItem item);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr itemId, uint attributes, out uint found);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellExecuteEx(ref ShellExecuteInfo info);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
