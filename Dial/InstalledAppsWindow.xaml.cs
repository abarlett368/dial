using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dial.Services;

namespace Dial;

public partial class InstalledAppsWindow : Window
{
    private readonly IReadOnlyList<InstalledApp> _apps;

    public InstalledAppsWindow(IReadOnlyList<InstalledApp> apps)
    {
        _apps = apps;
        InitializeComponent();
        AppList.ItemsSource = apps;
        SourceInitialized += (_, _) => ApplyWindowAppearance();
        ThemeService.WindowAppearanceChanged += ApplyWindowAppearance;
        Closed += (_, _) => ThemeService.WindowAppearanceChanged -= ApplyWindowAppearance;
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
        };
    }

    public InstalledApp? SelectedApp => AppList.SelectedItem as InstalledApp;

    private void ApplyWindowAppearance() => ThemeService.ApplyWindowAppearance(this);

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AppList is not null)
            AppList.ItemsSource = _apps.Where(app =>
                app.Name.Contains(SearchBox.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = SelectedApp is not null;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedApp is not null) DialogResult = true;
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedApp is not null && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(AppList, source) is ListBoxItem)
            DialogResult = true;
    }
}
