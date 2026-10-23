using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dial.Models;

namespace Dial.Services;

public sealed record IconSpec(string? Glyph, ImageSource? Image, bool IsText = false)
{
    public bool IsImage => Image is not null;
}

public static class IconHelper
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiLargeIcon = 0x0;

    private static readonly ConcurrentDictionary<string, ImageSource> IconCache = new();

    public static IconSpec Resolve(MenuItemModel item)
    {
        if (!string.IsNullOrWhiteSpace(item.Icon))
        {
            string iconPath = Environment.ExpandEnvironmentVariables(item.Icon);
            if (File.Exists(iconPath))
            {
                var img = LoadImage(iconPath);
                if (img is not null) return new IconSpec(null, img);
            }

            string text = item.Icon.Length > 64 ? item.Icon[..64] : item.Icon;
            bool isGlyph = text.Length == 1 && IsFluentGlyph(text[0]);
            return new IconSpec(text, null, IsText: !isGlyph);
        }

        switch (item.Type)
        {
            case MenuItemType.LaunchApp:
                {
                    if (InstalledAppService.IsShellTarget(item.Target))
                    {
                        if (!IconCache.TryGetValue(item.Target, out var appImage))
                        {
                            appImage = InstalledAppService.GetIcon(item.Target);
                            if (appImage is not null) IconCache[item.Target] = appImage;
                        }
                        return appImage is not null ? new IconSpec(null, appImage) : new IconSpec(MenuGlyphs.ForType(item.Type), null);
                    }
                    string? exe = ResolveExecutable(item.Target);
                    var img = exe is not null ? GetCachedIcon(exe) : null;
                    return img is not null ? new IconSpec(null, img) : new IconSpec(MenuGlyphs.ForType(item.Type), null);
                }
            case MenuItemType.OpenFile:
                {
                    string expanded = Environment.ExpandEnvironmentVariables(item.Target);
                    var img = File.Exists(expanded) || Directory.Exists(expanded) ? GetCachedIcon(expanded) : null;
                    return img is not null ? new IconSpec(null, img) : new IconSpec("\uE8A5", null);
                }
            case MenuItemType.SystemCommand:
                return new IconSpec(MenuGlyphs.ForCommand(item.Command), null);
            default:
                return new IconSpec(MenuGlyphs.ForType(item.Type), null);
        }
    }

    private static ImageSource? GetCachedIcon(string path)
    {
        string key = path.ToLowerInvariant();
        if (IconCache.TryGetValue(key, out var cached)) return cached;

        var img = ExtractFileIcon(path);
        if (img is not null) IconCache[key] = img;
        return img;
    }

    private static bool IsFluentGlyph(char c) => c is >= '\uE000' and <= '\uF8FF';

    private static string? ResolveExecutable(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;

        string expanded = Environment.ExpandEnvironmentVariables(target.Trim().Trim('"'));
        if (File.Exists(expanded)) return expanded;

        if (expanded.Contains(Path.DirectorySeparatorChar) || expanded.Contains('/')) return null;

        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVar))
        {
            foreach (string dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir, expanded);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException exception)
                {
                    Trace.TraceWarning("Invalid PATH entry: {0}", exception.Message);
                }
            }
        }

        string system32 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", expanded);
        if (File.Exists(system32)) return system32;

        return null;
    }

    private static ImageSource? ExtractFileIcon(string path)
    {
        var info = new ShellFileInfo();
        IntPtr result = SHGetFileInfo(
            path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), ShgfiIcon | ShgfiLargeIcon);

        if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
        {
            Trace.TraceWarning("Windows could not resolve the icon for {0}.", path);
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.IconHandle, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.IconHandle);
        }
    }

    private static ImageSource? LoadImage(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            Trace.TraceWarning("Could not load icon {0}: {1}", path, exception.Message);
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref ShellFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr IconHandle);

}
