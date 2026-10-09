// Sets up Avalonia's headless platform for the UI tests of this assembly. It
// renders with Skia, so tests can capture frames (MainWindowTests saves them
// when DESKTOP_SCREENSHOTS is set to a folder).

using Avalonia;
using Avalonia.Headless;
using Desktop.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Desktop.App.Tests;

/// <summary>Builds the app on the headless platform, without a window system.</summary>
public static class TestAppBuilder
{
    /// <summary>Called by Avalonia.Headless.XUnit once per test run.</summary>
    /// <returns>The app builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
