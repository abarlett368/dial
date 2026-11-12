using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Dial.Services;

namespace Dial;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        AppIcon.Source = BitmapDecoder.Create(new Uri("pack://application:,,,/dial;component/Assets/dial.ico"),
            BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames.MaxBy(frame => frame.PixelWidth);
        VersionText.Text = App.DisplayVersion;
        SourceInitialized += (_, _) => ApplyWindowAppearance();
        ThemeService.WindowAppearanceChanged += ApplyWindowAppearance;
        Closed += (_, _) => ThemeService.WindowAppearanceChanged -= ApplyWindowAppearance;
    }

    private void ApplyWindowAppearance() => ThemeService.ApplyWindowAppearance(this);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ProjectLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or SecurityException)
        {
            ErrorService.Report("ProjectLinkError", exception);
        }
    }
}
