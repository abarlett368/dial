using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Windows;

namespace Dial.Services;

public static class ErrorService
{
    public static string GetMessage(string resourceKey, Exception exception)
    {
        int code = exception is Win32Exception native ? native.NativeErrorCode
            : (exception.HResult & unchecked((int)0xffff0000)) == unchecked((int)0x80070000)
                ? exception.HResult & 0xffff : 0;
        string? reasonKey = exception switch
        {
            IOException { InnerException: AggregateException } when resourceKey == "ConfigSaveError" => "RollbackError",
            UnauthorizedAccessException or SecurityException => "AccessDeniedError",
            FileNotFoundException or DirectoryNotFoundException => "FileNotFoundError",
            JsonException or InvalidDataException => "InvalidConfiguration",
            IOException or Win32Exception or COMException => code switch
            {
                2 or 3 => "FileNotFoundError",
                5 => "AccessDeniedError",
                32 or 33 => "FileInUseError",
                39 or 112 => "DiskFullError",
                1223 => "OperationCancelledError",
                1409 => "HotkeyError",
                _ => null
            },
            _ => null
        };
        string message = LocalizationService.T(resourceKey);
        return reasonKey is null || reasonKey == resourceKey
            ? message : $"{message} {LocalizationService.T(reasonKey)}";
    }

    public static void Report(string resourceKey, Exception exception)
    {
        Trace.TraceError("{0}: {1}", resourceKey, exception);
        MessageBox.Show(GetMessage(resourceKey, exception),
            LocalizationService.T("AppName"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
