// The whole path of instruction D0004 through the real engine, against the
// demo server: a new project is created from the app, a test is recorded into
// it (the recording browser headless and driven through its debugging port,
// for the test only), the app checks it by itself, it is run, a step is
// edited in its form, and it is run again. Skips like the other real-engine
// tests.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Recording;
using Desktop.App.ViewModels.Runs;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests.Runs;

public sealed class RealEngineRecordTests
{
    private static readonly string HeadlessVariable = $"{Product.EnvPrefix}TEST_RECORDING_HEADLESS";
    private static readonly string PortVariable = $"{Product.EnvPrefix}TEST_RECORDING_DEBUG_PORT";

    [AvaloniaFact]
    public async Task A_new_project_records_checks_runs_and_edits_a_test()
    {
        var work = Directory.CreateTempSubdirectory("desktop-record-").FullName;
        var settings = new JsonSettingsStore(Path.Combine(work, "settings.json"));
        var port = BrowserInput.FreePort();
        // Read by the engine when it starts the recording browser; set before the engine starts.
        Environment.SetEnvironmentVariable(HeadlessVariable, "1");
        Environment.SetEnvironmentVariable(PortVariable, port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var engine = new EngineService(settings, new AvaloniaDispatcher());
        var shell = new ShellViewModel(engine, settings, new FakeFolderPicker(work), new DiskProjectFiles(), new AvaloniaDispatcher());
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

            // A new project for the demo application.
            shell.ShowNewProjectCommand.Execute(null);
            var form = shell.NewProject!;
            await form.BrowseCommand.ExecuteAsync(null);
            form.Name = "Demo tests";
            form.BaseUrl = demo.Url;
            await form.CreateCommand.ExecuteAsync(null);
            Assert.Null(form.Error);
            var workspace = shell.Workspace!;
            Assert.True(workspace.OffersFirstRecording);

            // Record your first test: add a to-do.
            workspace.RecordFirstTestCommand.Execute(null);
            var start = workspace.RecordStart!;
            start.TestName = "Add a to-do";
            start.StartUrl = "/todos";
            await start.StartCommand.ExecuteAsync(null);
            Assert.Null(start.Error);
            var tab = Assert.IsType<RecordingTabViewModel>(workspace.SelectedTab);
            const string File = "tests/add-a-to-do.test.yaml";
            Assert.Equal(File, tab.File);

            await using (var browser = await BrowserInput.ConnectAsync(port, TimeSpan.FromSeconds(20)))
            {
                await WaitAsync(() => tab.Steps.Count >= 1, "the opening goto");
                await browser.ClickAsync("#new-todo");
                await browser.TypeAsync("Buy milk");
                await browser.PressEnterAsync();
                await browser.WaitForTextAsync("Buy milk");
                await WaitAsync(() => tab.Steps.Any(s => s.Action == "fill" && s.Detail.Contains("Buy milk", StringComparison.Ordinal)), "the recorded fill");
            }

            Assert.Contains(File, workspace.Explorer.Files);
            await tab.StopCommand.ExecuteAsync(null);
            await WaitAsync(() => tab.Phase is RecordingPhase.Passed or RecordingPhase.Failed or RecordingPhase.NotChecked, "the check of the recording", 60);
            Assert.True(tab.IsPassed, $"{tab.StatusText} {tab.FailedStep} {tab.FailedMessage} {tab.Problem}");
            Assert.Null(tab.PasswordHelp);

            // Run it.
            var first = await RunAsync(workspace, File);
            Assert.Equal(RunState.Passed, first.State);

            // Edit the fill in its form, save, and run again.
            tab.OpenTestCommand.Execute(null);
            var editor = Assert.IsType<StepFileViewModel>(workspace.SelectedTab);
            var list = editor.Steps!;
            list.SelectCommand.Execute(list.Sections.SelectMany(s => s.Steps).First(s => s.Action == "fill"));
            list.Form!.Parameters.Single(f => f.Name == "value").Value = "Buy bread";
            Assert.Contains("Buy bread", editor.Document.Text, StringComparison.Ordinal);
            Assert.True(await editor.SaveAsync());
            var second = await RunAsync(workspace, File);
            Assert.Equal(RunState.Passed, second.State);
        }
        finally
        {
            await shell.ShutdownAsync();
            Environment.SetEnvironmentVariable(HeadlessVariable, null);
            Environment.SetEnvironmentVariable(PortVariable, null);
            if (demo is not null)
            {
                await demo.DisposeAsync();
            }

            Directory.Delete(work, recursive: true);
        }
    }

    private static async Task<RunViewModel> RunAsync(WorkspaceViewModel workspace, string file)
    {
        await workspace.RunFileAsync(file);
        Assert.Null(workspace.Runs.Notice);
        var run = workspace.Runs.Current!;
        await WaitAsync(() => !run.IsActive, "the run", 60);
        return run;
    }

    private static async Task WaitAsync(Func<bool> done, string what, int seconds = 20)
    {
        for (var i = 0; i < seconds * 10 && !done(); i++)
        {
            await Task.Delay(100);
        }

        Assert.True(done(), $"Waited {seconds} s for {what}.");
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
