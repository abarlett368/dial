using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Dial.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "dial";
    private const string StartupTaskId = "DialStartup";

    public static bool IsPackaged { get; } = HasPackageIdentity();

    public static async Task<bool> IsEnabledAsync()
    {
        if (IsPackaged)
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) is not null;
    }

    public static async Task SetEnabledAsync(bool enabled, string? executablePath = null)
    {
        if (IsPackaged)
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
            {
                var state = task.State;
                if (state == StartupTaskState.Disabled) state = await task.RequestEnableAsync();
                ValidatePackagedState(state, true);
            }
            else
            {
                if (task.State == StartupTaskState.Enabled) task.Disable();
                ValidatePackagedState(task.State, false);
            }
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);

        if (enabled)
        {
            string? exe = executablePath ?? Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                throw new InvalidOperationException("The executable path is unavailable.");
            key.SetValue(AppName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    private static void ValidatePackagedState(StartupTaskState state, bool enabled)
    {
        if (enabled && state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy ||
            !enabled && state is StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy)
            return;
        throw new StartupChangeException(state switch
        {
            StartupTaskState.DisabledByUser => "StartupDisabledByUserError",
            StartupTaskState.DisabledByPolicy or StartupTaskState.EnabledByPolicy => "StartupPolicyError",
            _ => "StartupChangeError"
        });
    }

    private static bool HasPackageIdentity()
    {
        int length = 0;
        int result = GetCurrentPackageFullName(ref length, IntPtr.Zero);
        return result switch
        {
            122 => true,
            15700 => false,
            _ => throw new Win32Exception(result)
        };
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);
}

internal sealed class StartupChangeException(string resourceKey) : IOException(LocalizationService.T(resourceKey))
{
    public string ResourceKey { get; } = resourceKey;
}
