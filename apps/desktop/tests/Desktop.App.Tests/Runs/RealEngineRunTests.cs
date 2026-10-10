// A real run: the engine built in this checkout runs demo tests in a real
// browser against the demo server, in a temporary copy of the demo project,
// and the app builds the run from the engine's events. Skips when the engine
// is not built (unless DESKTOP_TESTS_REQUIRE_ENGINE=1) or cannot start a
// browser (unless DESKTOP_TESTS_REQUIRE_BROWSER=1).

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Runs;
using Desktop.Engine;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests.Runs;

public sealed class RealEngineRunTests
{
    private const string Passing = "tests/user-action.test.yaml";
    private const string Failing = "fixtures/failing/assertion.test.yaml";
    private const string Skipped = "tests/skipped.test.yaml";

    [AvaloniaFact]
    public async Task Runs_demo_tests_in_a_real_browser()
    {
        var settingsFolder = Directory.CreateTempSubdirectory("desktop-settings-").FullName;
        var settings = new JsonSettingsStore(Path.Combine(settingsFolder, "settings.json"));
        var engine = new EngineService(settings, new AvaloniaDispatcher());
        var shell = new ShellViewModel(engine, settings, new FakeFolderPicker(null), new DiskProjectFiles(), new AvaloniaDispatcher());
        DemoCopy? demo = null;
        try
        {
            DemoEnvironment.Ensure();
            await shell.InitializeAsync();
            if (engine.State != EngineState.Ready)
            {
                SkipUnless("DESKTOP_TESTS_REQUIRE_ENGINE", $"No built engine or no Node: {engine.Failure?.Message.Split(Environment.NewLine)[0]}");
            }

            if (engine.Info?.Capabilities.Browsers is not { Count: > 0 })
            {
                SkipUnless("DESKTOP_TESTS_REQUIRE_BROWSER", "The engine cannot start a browser here.");
            }

            demo = await DemoCopy.StartAsync(settings.Load().NodePath ?? "node");
            await shell.OpenProjectAsync(demo.Root);
            var workspace = Assert.IsType<WorkspaceViewModel>(shell.Workspace);
            Assert.True(workspace.Runs.CanRun, workspace.Runs.UnavailableReason);

            await workspace.Runs.RunFilesAsync([Passing, Failing, Skipped]);
            Assert.Null(workspace.Runs.Notice);
            var run = workspace.Runs.Current!;
            for (var i = 0; i < 1200 && run.IsActive; i++)
            {
                await Task.Delay(100);
            }

            Assert.Equal(RunState.Failed, run.State);
            Assert.Empty(run.Messages);
            Assert.StartsWith(demo.Root, run.ResultsDir, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(run.ResultsDir!, "events.ndjson")));

            var passed = Assert.Single(run.Tests, t => t.File == Passing);
            Assert.Equal(TestRunState.Passed, passed.State);
            Assert.Equal(["steps", "after"], passed.Sections.Select(s => s.Name));
            Assert.Contains(passed.AllSteps, s => s.Action == "demo.addTodo");

            var failed = Assert.Single(run.Tests, t => t.File == Failing);
            Assert.Equal(TestRunState.Failed, failed.State);
            var step = Assert.Single(failed.AllSteps, s => s.IsFailed);
            Assert.Equal(step, failed.SelectedStep);
            Assert.NotNull(step.Error);
            Assert.NotNull(step.ExpectedText);
            Assert.NotNull(step.ActualText);
            // The engine of v0.1.0 sends no screenshotReady yet and saves no page states
            // (every step's snapshot is "skipped"); when it does, the paths must exist.
            Assert.True(step.ScreenshotPath is null || File.Exists(step.ScreenshotPath), $"No screenshot at {step.ScreenshotPath}");
            Assert.Equal(step.HasPageState, failed.AllSteps.Any(s => s.HasPageState));
            // Steps after the failure never start; they are reported in the test's messages.
            Assert.Contains(failed.Messages, m => m.Contains("skipped", StringComparison.OrdinalIgnoreCase));
            Assert.All(failed.Sections.Single(s => s.Name == "after").Steps, s => Assert.Equal(StepRunState.Passed, s.State));

            Assert.Equal(2, run.Tests.Count(t => t.File == Skipped && t.State == TestRunState.Skipped));
            Assert.Equal("1 passed, 1 failed, 0 cancelled, 2 skipped", run.TotalsText);
            Assert.True(workspace.Runs.CanRun);

            // The same run, read back from its folder, shows the same (instruction D0003, task 12).
            for (var i = 0; i < 200 && !workspace.History.Items.Any(h => h.RunId == run.RunId && h.StatusText == "Failed"); i++)
            {
                await Task.Delay(50);
            }

            Assert.Equal("1 passed, 1 failed, 0 cancelled, 2 skipped", Assert.Single(workspace.History.Items, h => h.RunId == run.RunId).TotalsText);
            var record = new DiskRunRecords().Read(demo.Root, run.RunId)!;
            Assert.Equal(0, record.DamagedLines);
            var replay = RunHistoryViewModel.Build(record);
            Assert.Equal(RunState.Failed, replay.State);
            Assert.Equal(run.TotalsText, replay.TotalsText);
            Assert.Equal(run.Tests.Select(t => (t.TestId, t.State)), replay.Tests.Select(t => (t.TestId, t.State)));
            Assert.Equal(run.Tests.SelectMany(t => t.AllSteps).Select(st => (st.StepId, st.State)), replay.Tests.SelectMany(t => t.AllSteps).Select(st => (st.StepId, st.State)));
        }
        finally
        {
            await shell.ShutdownAsync();
            if (demo is not null)
            {
                await demo.DisposeAsync();
            }

            Directory.Delete(settingsFolder, recursive: true);
        }
    }

    private static void SkipUnless(string variable, string reason)
    {
        if (Environment.GetEnvironmentVariable(variable) == "1")
        {
            Assert.Fail(reason);
        }

        Assert.Skip(reason);
    }
}
