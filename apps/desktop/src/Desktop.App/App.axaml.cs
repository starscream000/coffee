// Code-behind of the application: builds the app's objects and the main window.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Desktop.App.Views;

namespace Desktop.App;

/// <summary>The Avalonia application object.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            window.DataContext = AppComposition.CreateShell(() => window);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
