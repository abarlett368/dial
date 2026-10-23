using System.Diagnostics;
using System.Windows;

namespace Dial.Services;

public static class ErrorService
{
    public static void Report(string resourceKey, Exception exception)
    {
        Trace.TraceError("{0}: {1}", resourceKey, exception);
        MessageBox.Show($"{LocalizationService.T(resourceKey)}\n\n{exception.Message}",
            LocalizationService.T("AppName"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
