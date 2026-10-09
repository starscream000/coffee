// Entry point of the desktop app: builds and starts the Avalonia application.

using Avalonia;

namespace Desktop.App;

/// <summary>Starts the desktop application.</summary>
internal static class Program
{
    /// <summary>
    /// Runs the application until its main window closes. Avalonia is not
    /// usable before this is called.
    /// </summary>
    /// <param name="args">Command-line arguments, passed on to the application lifetime.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Configures Avalonia for this app. Also used by the XAML previewer and by
    /// the headless UI tests.
    /// </summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
