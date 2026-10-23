using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dial.Models;

namespace Dial.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigService(string? path = null)
    {
        ConfigPath = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dial", "settings.json"));
    }

    public bool Existed { get; private set; }
    public string ConfigPath { get; }

    public MenuConfig Load()
    {
        Existed = File.Exists(ConfigPath);
        if (!Existed) return CreateDefaults();

        return Deserialize(File.ReadAllText(ConfigPath));
    }

    internal static MenuConfig Deserialize(string json)
    {
        var config = JsonSerializer.Deserialize<MenuConfig>(json, Options)
            ?? throw new InvalidDataException(LocalizationService.T("InvalidConfiguration"));
        Validate(config);
        return config;
    }

    public void Save(MenuConfig config)
    {
        Validate(config);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        string temporaryPath = ConfigPath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(config, Options));
            File.Move(temporaryPath, ConfigPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static MenuConfig Clone(MenuConfig config) =>
        JsonSerializer.Deserialize<MenuConfig>(JsonSerializer.Serialize(config, Options), Options)!;

    public static string Serialize(MenuConfig config)
    {
        Validate(config);
        return JsonSerializer.Serialize(config, Options);
    }
    public static void Validate(MenuConfig config)
    {
        if (config is null)
            throw new InvalidDataException(LocalizationService.T("InvalidConfiguration"));
        if (!HotkeyService.TryParse(config.Hotkey, out _, out _))
            throw new InvalidDataException(LocalizationService.T("InvalidShortcut"));
        if (config.Theme is not ("Light" or "Dark") || config.Language is not ("en" or "zh-CN"))
            throw new InvalidDataException(LocalizationService.T("InvalidAppearance"));
        if (!HoverPalette.IsValid(config.HoverColor) || !Enum.IsDefined(config.IconMode))
            throw new InvalidDataException(LocalizationService.T("InvalidAppearance"));
        if (config.Items is null || config.Items.Count > DialGeometry.MaximumItems)
            throw new InvalidDataException(LocalizationService.T("MaxItemsReached"));
        foreach (var item in config.Items)
        {
            if (item is null || item.Label is null || item.Label.Length > DialGeometry.MaximumTextLength ||
                item.Icon is null || item.Target is null || item.Arguments is null ||
                !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Command))
                throw new InvalidDataException(LocalizationService.T("InvalidItem"));
            if (item.Icon.Length > DialGeometry.MaximumTextLength &&
                !File.Exists(Environment.ExpandEnvironmentVariables(item.Icon)))
                throw new InvalidDataException(LocalizationService.T("InvalidIcon"));
            if (!item.Enabled) continue;
            if (item.Type == MenuItemType.LaunchApp && item.RunAsAdministrator &&
                (InstalledAppService.IsShellTarget(item.Target) ||
                 Uri.TryCreate(item.Target, UriKind.Absolute, out var appUri) && !appUri.IsFile))
                throw new InvalidDataException(LocalizationService.T("AppElevationNotSupported"));
            if (string.IsNullOrWhiteSpace(item.Label))
                throw new InvalidDataException(LocalizationService.T("NameRequired"));
            if (item.Type != MenuItemType.SystemCommand && string.IsNullOrWhiteSpace(item.Target))
                throw new InvalidDataException(string.Format(LocalizationService.CurrentCulture,
                    LocalizationService.T("TargetRequired"), item.Label));
            if (item.Type == MenuItemType.OpenUrl &&
                (!Uri.TryCreate(item.Target, UriKind.Absolute, out var uri) || uri.IsFile))
                throw new InvalidDataException(LocalizationService.T("InvalidUrl"));
            if (item.Type == MenuItemType.LaunchApp && InstalledAppService.IsShellTarget(item.Target) &&
                !InstalledAppService.IsValidTarget(item.Target))
                throw new InvalidDataException(LocalizationService.T("InvalidAppTarget"));
            if (item.Type == MenuItemType.LaunchApp && InstalledAppService.IsShellTarget(item.Target) &&
                !item.Target.Contains('!') && !string.IsNullOrWhiteSpace(item.Arguments))
                throw new InvalidDataException(LocalizationService.T("AppArgumentsNotSupported"));
        }
    }

    public static MenuConfig CreateDefaults() => new()
    {
        Items =
        [
            new() { Label = "This PC", Type = MenuItemType.LaunchApp, Target = "explorer.exe",
                Arguments = "shell:MyComputerFolder", Icon = "\uE770" },
            new() { Label = "Microsoft Store", Type = MenuItemType.OpenUrl, Target = "ms-windows-store:", Icon = "\uE719" },
            new() { Label = "Windows PowerShell", Type = MenuItemType.LaunchApp, Target = "powershell.exe" },
            new() { Label = "Screenshot", Type = MenuItemType.SystemCommand, Command = SystemCommand.Screenshot },
            new() { Label = "Bing", Type = MenuItemType.OpenUrl, Target = "https://www.bing.com" },
            new() { Label = "Paste email", Type = MenuItemType.ClipboardSnippet, Target = "you@example.com" },
            new() { Label = "Task Manager", Type = MenuItemType.SystemCommand, Command = SystemCommand.TaskManager },
            new() { Label = "Settings", Type = MenuItemType.SystemCommand, Command = SystemCommand.OpenSettings },
        ]
    };
}
