// The run views on Avalonia's headless platform: the run controls follow
// whether the engine can start a browser, and a run tab shows its tests, a
// failed step's details and its screenshot or the note that none was recorded.
// With DESKTOP_SCREENSHOTS set to a folder, each test also saves what it rendered.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Runs;
using Desktop.App.Views;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Runs;

public sealed class RunViewTests : IDisposable
{
    private const string Root = "/work/shop-tests";
    private const string File = "tests/checkout/guest-checkout.test.yaml";

    // A 1×1 PNG, standing in for a screenshot the engine wrote.
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _folder = Directory.CreateTempSubdirectory("desktop-run-view-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static (MainWindow Window, ShellViewModel Shell, FakeEngineService Engine) Create(params string[] browsers)
    {
        var engine = new FakeEngineService { Info = FakeEngineService.ReadyInfo(browsers), Tests = [Make.Test(File, "Guest checks out", "smoke")] };
        var files = new FakeProjectFiles();
        files.Files[File] = "version: 1\nname: Guest checks out\nsteps:\n  - goto: /products\n  - expect.text: { target: order.total, equals: '£10.00' }\n";
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root);
        return (new MainWindow { DataContext = shell, Width = 1280, Height = 800 }, shell, engine);
    }

    private static async Task OpenAsync(MainWindow window, ShellViewModel shell)
    {
        window.Show();
        await shell.InitializeAsync();
        await shell.OpenProjectAsync(Root);
        Render(window);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Snapshot(Window window, string name)
    {
        Render(window);
        var folder = Environment.GetEnvironmentVariable("DESKTOP_SCREENSHOTS");
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, name + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private static T Find<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name && c.IsEffectivelyVisible)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named {name} is shown.");

    [AvaloniaFact]
    public async Task Without_a_browser_the_run_buttons_are_off_and_the_install_command_is_shown()
    {
        var (window, shell, _) = Create();
        await OpenAsync(window, shell);
        Snapshot(window, "10-runs-no-browser");

        Assert.False(Find<Button>(window, "RunAllButton").IsEffectivelyEnabled);
        Assert.False(Find<Button>(window, "RunSelectedButton").IsEffectivelyEnabled);
        Assert.Contains(RunControlViewModel.InstallCommand, Find<SelectableTextBlock>(window, "RunUnavailable").Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_failed_run_shows_the_failure_and_the_screenshot()
    {
        var (window, shell, engine) = Create("chromium");
        await OpenAsync(window, shell);
        Assert.True(Find<Button>(window, "RunAllButton").IsEffectivelyEnabled);
        var screenshot = Path.Combine(_folder, "s2.png");
        await System.IO.File.WriteAllBytesAsync(screenshot, Png);

        await shell.Workspace!.RunAllCommand.ExecuteAsync(null);
        var s = new RunScript("run-1");
        foreach (var e in new EngineEvent[]
        {
            s.Started(RunScript.Planned("t1", File, "Guest checks out")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Go to /products", file: File, line: 4),
            s.Passed("t1", "s1", SnapshotState.Skipped),
            s.Step("t1", "s2", "Expect order.total to be £10.00", file: File, line: 5),
            s.Failed("t1", "s2"),
            s.Screenshot("t1", "s2", screenshot),
            s.TestFinished("t1", RunOutcome.Failed),
            s.Finished(RunOutcome.Failed, failed: 1),
        })
        {
            engine.Raise(e);
        }

        Snapshot(window, "11-run-failed");
        Assert.Equal("Failed", Find<TextBlock>(window, "RunStatus").Text);
        Assert.Equal("Expect order.total to be £10.00", Find<TextBlock>(window, "StepTitle").Text);
        Assert.StartsWith("The text of \"order total\"", Find<SelectableTextBlock>(window, "ErrorMessage").Text, StringComparison.Ordinal);
        Assert.NotNull(Find<Image>(window, "Screenshot").Source);
        Assert.True(Find<Button>(window, "OpenPageStateButton").IsVisible);

        var run = ((RunTabViewModel)shell.Workspace.SelectedTab!).Run;
        run.SelectedTest!.SelectStepCommand.Execute(run.SelectedTest.Step("s1"));
        Render(window);
        Assert.Equal("No screenshot was recorded.", Find<TextBlock>(window, "NoScreenshot").Text);
        Assert.Throws<InvalidOperationException>(() => Find<Button>(window, "OpenPageStateButton"));
    }
}
