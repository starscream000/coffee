// The app's real services (EngineService, DiskProjectFiles, JsonSettingsStore)
// against the engine built in this checkout, opening examples/demo-app in the
// main window. Skips when the engine is not built, unless
// DESKTOP_TESTS_REQUIRE_ENGINE=1.

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.Views;
using Desktop.Engine;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests;

public sealed class RealEngineAppTests
{
    [AvaloniaFact]
    public async Task Opens_the_demo_project_through_the_real_engine()
    {
        var settingsFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, "settings.json");
        var settings = new JsonSettingsStore(settingsFile);
        var engine = new EngineService(settings, new AvaloniaDispatcher());
        var shell = new ShellViewModel(engine, settings, new FakeFolderPicker(null), new DiskProjectFiles(), new AvaloniaDispatcher());
        // Shown before it gets its view model, so its Opened handler finds no shell
        // to start: this test starts the engine once, itself (finding 2 of review
        // D0001 was a second start from Opened racing the shutdown at the end).
        var window = new MainWindow { Width = 1280, Height = 800 };
        window.Show();
        window.DataContext = shell;
        try
        {
            await shell.InitializeAsync();
            if (engine.State != EngineState.Ready)
            {
                if (Environment.GetEnvironmentVariable("DESKTOP_TESTS_REQUIRE_ENGINE") == "1")
                {
                    Assert.Fail(engine.Failure?.Message);
                }

                Assert.Skip($"No built engine or no Node: {engine.Failure?.Message.Split(Environment.NewLine)[0]}");
            }

            await shell.OpenProjectAsync(RepoPaths.Of("examples/demo-app"));

            var workspace = Assert.IsType<WorkspaceViewModel>(shell.Workspace);
            Assert.Null(shell.Notice);
            Assert.Contains("tests/user-action.test.yaml", workspace.Explorer.Files);
            Assert.Contains(workspace.Actions.Items, a => a.Name == "demo.addTodo" && a.IsUserAction);
            Assert.Contains(workspace.Actions.Items, a => a.Name == "goto");
            Assert.Equal(["local"], workspace.Environments);
            Assert.Contains(shell.Engine.Log, l => l.Text.Contains("ready", StringComparison.Ordinal));
            Assert.Contains(RepoPaths.Of("examples/demo-app"), new JsonSettingsStore(settingsFile).Load().RecentProjects);

            workspace.OpenFile("tests/user-action.test.yaml");
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            if (Environment.GetEnvironmentVariable("DESKTOP_SCREENSHOTS") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                window.CaptureRenderedFrame()?.Save(Path.Combine(folder, "06-demo-app-real-engine.png"), new PngBitmapEncoderOptions());
            }
        }
        finally
        {
            await shell.ShutdownAsync();
            Directory.Delete(Path.GetDirectoryName(settingsFile)!, recursive: true);
        }

        Assert.Equal(EngineState.Stopped, engine.State);
        Assert.Single(shell.Engine.Log, l => l.Text.StartsWith("Starting ", StringComparison.Ordinal));
    }
}
