using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Dial.Services;

public static class ErrorService
{
    private const int MaximumLogBytes = 1024 * 1024;
    private static readonly object LogLock = new();
    private static string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dial", "logs", "errors.log");

    public static void InitializeLog(string directory)
    {
        lock (LogLock) _logPath = Path.Combine(Path.GetFullPath(directory), "errors.log");
    }

    public static void LogError(string resourceKey, Exception exception)
    {
        Trace.TraceError("{0}: {1}", resourceKey, exception);
        try
        {
            var record = new StringBuilder()
                .Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                .Append(' ').AppendLine(resourceKey);
            int remaining = 16;
            AppendException(record, exception, ref remaining);
            string text = record.Length > 16000 ? record.ToString(0, 16000) + "\n[truncated]\n" : record.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
            lock (LogLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length + bytes.Length > MaximumLogBytes)
                    File.Move(_logPath, _logPath + ".previous", true);
                using var stream = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
                stream.Flush(true);
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or SecurityException)
        {
            Trace.TraceWarning("The diagnostic log could not be written: {0}", failure.GetType().Name);
        }
    }

    private static void AppendException(StringBuilder record, Exception exception, ref int remaining)
    {
        if (remaining-- <= 0) return;
        // Exception messages can contain clipboard text or launch arguments; do not persist them.
        record.Append(exception.GetType().FullName).Append(" HRESULT=0x")
            .Append(exception.HResult.ToString("X8", CultureInfo.InvariantCulture));
        if (exception is Win32Exception native) record.Append(" Win32=").Append(native.NativeErrorCode);
        record.AppendLine();
        if (exception.StackTrace is { } stack)
            record.AppendLine(stack.Length > 8192 ? stack[..8192] : stack);
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (remaining <= 0) break;
                AppendException(record, inner, ref remaining);
            }
        }
        else if (exception.InnerException is { } inner)
            AppendException(record, inner, ref remaining);
    }

    public static string GetMessage(string resourceKey, Exception exception)
    {
        int code = exception is Win32Exception native ? native.NativeErrorCode
            : (exception.HResult & unchecked((int)0xffff0000)) == unchecked((int)0x80070000)
                ? exception.HResult & 0xffff : 0;
        string? reasonKey = exception switch
        {
            StartupChangeException startup => startup.ResourceKey,
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
        LogError(resourceKey, exception);
        MessageBox.Show(GetMessage(resourceKey, exception),
            LocalizationService.T("AppName"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
