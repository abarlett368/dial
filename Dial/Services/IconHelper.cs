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
            if (item.IconIndex is int iconIndex && iconIndex >= 0)
            {
                try
                {
                    if (iconIndex < GetResourceIconCount(iconPath))
                    {
                        var resourceIcon = GetCachedResourceIcon(iconPath, iconIndex);
                        if (resourceIcon is not null) return new IconSpec(null, resourceIcon);
                    }
                }
                catch (IOException)
                {
                    // An optional resource must not prevent the normal automatic icon fallback.
                }
            }
            else
            {
                if (File.Exists(iconPath))
                {
                    var img = LoadImage(iconPath);
                    if (img is not null) return new IconSpec(null, img);
                }

                string text = item.Icon.Length > 64 ? item.Icon[..64] : item.Icon;
                bool isGlyph = text.Length == 1 && IsFluentGlyph(text[0]);
                return new IconSpec(text, null, IsText: !isGlyph);
            }
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

    private static ImageSource? GetCachedResourceIcon(string path, int index)
    {
        string key = $"{path.ToLowerInvariant()}|{index}";
        if (IconCache.TryGetValue(key, out var cached)) return cached;

        var image = LoadResourceIcon(path, index);
        if (image is not null) IconCache[key] = image;
        return image;
    }

    public static int GetResourceIconCount(string path)
    {
        if (!File.Exists(path)) return 0;
        return ValidateResourceIconCount(ExtractIconEx(path, -1, null, null, 0));
    }

    internal static int ValidateResourceIconCount(uint count)
    {
        if (count > int.MaxValue)
        {
            var exception = new IOException(count == uint.MaxValue
                ? "ExtractIconEx failed (UINT_MAX)."
                : "ExtractIconEx returned an unsupported icon count.");
            ErrorService.LogError("IconLoadFailed", exception);
            throw exception;
        }
        return (int)count;
    }

    public static ImageSource? LoadResourceIcon(string path, int index)
    {
        if (index < 0 || !File.Exists(path)) return null;

        var largeIcons = new IntPtr[1];
        var smallIcons = new IntPtr[1];
        uint extracted = ExtractIconEx(path, index, largeIcons, smallIcons, 1);
        IntPtr iconHandle = largeIcons[0] != IntPtr.Zero ? largeIcons[0] : smallIcons[0];

        try
        {
            ValidateResourceIconCount(extracted);
            if (extracted == 0 || iconHandle == IntPtr.Zero) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(iconHandle, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception exception) when (exception is ExternalException or ArgumentException)
        {
            Trace.TraceWarning("Could not load icon {0} from resource {1}: {2}", index, path, exception.Message);
            return null;
        }
        finally
        {
            if (largeIcons[0] != IntPtr.Zero) DestroyIcon(largeIcons[0]);
            if (smallIcons[0] != IntPtr.Zero) DestroyIcon(smallIcons[0]);
        }
    }

    public static ImageSource? LoadImageFile(string path) => LoadImage(path);

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
            bmp.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException or FileFormatException)
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

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(
        string file, int index, [Out] IntPtr[]? largeIcons, [Out] IntPtr[]? smallIcons, uint iconCount);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr IconHandle);

}
