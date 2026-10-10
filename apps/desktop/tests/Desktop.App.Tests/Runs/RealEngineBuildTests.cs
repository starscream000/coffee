// The real engine in a real browser against the demo server, in a temporary
// copy of the demo project: a test built in the step list and its forms runs
// and passes (instruction D0003, "Done when"), and a target that fell back
// opens in the targets editor (task 19). Skips like the other real-engine tests.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Runs;
using Desktop.App.ViewModels.Steps;
using Desktop.Engine;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests.Runs;

public sealed class RealEngineBuildTests
{
    private const string File = "tests/built.test.yaml";

    private static void Add(StepListViewModel list, string action)
    {
        list.StartAddingCommand.Execute(null);
        list.AddCommand.Execute(list.PickerItems.Single(a => a.Name == action));
    }

    private static void Set(StepListViewModel list, string field, string value) =>
        list.Form!.Parameters.Single(f => f.Name == field).Value = value;

    [AvaloniaFact]
    public async Task A_test_built_in_the_step_list_runs_and_passes()
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
            await System.IO.File.WriteAllTextAsync(Path.Combine(demo.Root, "tests", "built.test.yaml"), "version: 1\nname: Built in the step list\nsteps: []\n");
            await shell.OpenProjectAsync(demo.Root);
            var workspace = shell.Workspace!;
            workspace.OpenFile(File);
            var tab = (StepFileViewModel)workspace.SelectedTab!;
            var list = tab.Steps!;

            Add(list, "goto");
            Set(list, "url", "/todos");
            Add(list, "fill");
            Set(list, "target", "todos.new");
            Set(list, "value", "Buy milk");
            Add(list, "click");
            Set(list, "target", "todos.add");
            Add(list, "expect.text");
            Set(list, "target", "todos.count");
            Set(list, "equals", "1");
            Add(list, "click");
            Set(list, "target", "todos.clearAll");
            list.MoveToSectionCommand.Execute("after");

            Assert.Equal(
                "version: 1\nname: Built in the step list\nsteps:\n  - goto: /todos\n  - fill:\n      target: todos.new\n      value: Buy milk\n"
                + "  - click: todos.add\n  - expect.text:\n      target: todos.count\n      equals: '1'\nafter:\n  - click: todos.clearAll\n",
                tab.Document.Text);
            Assert.Contains("todos.count", list.Sections[1].Steps[3].Detail, StringComparison.Ordinal);
            Assert.True(await tab.SaveAsync());

            await workspace.RunFileAsync(File);
            Assert.Null(workspace.Runs.Notice);
            var run = workspace.Runs.Current!;
            for (var i = 0; i < 600 && run.IsActive; i++)
            {
                await Task.Delay(100);
            }

            Assert.Equal(RunState.Passed, run.State);
            var test = Assert.Single(run.Tests);
            Assert.Equal(TestRunState.Passed, test.State);
            Assert.Equal(["steps", "after"], test.Sections.Select(s => s.Name));
            Assert.Equal(5, test.AllSteps.Count());

            // The README's walk-through, step 10: a failure shows expected and actual.
            workspace.OpenFile(File);
            list.SelectCommand.Execute(list.Sections[1].Steps[3]);
            Set(list, "equals", "2");
            Assert.True(await tab.SaveAsync());
            await workspace.RunFileAsync(File);
            var failing = workspace.Runs.Current!;
            for (var i = 0; i < 600 && failing.IsActive; i++)
            {
                await Task.Delay(100);
            }

            Assert.Equal(RunState.Failed, failing.State);
            var failed = failing.Tests.Single().SelectedStep!;
            Assert.Equal("expect.text", failed.Action);
            Assert.Equal("\"2\"", failed.ExpectedText);
            Assert.Equal("\"1\"", failed.ActualText);
            Assert.All(failing.Tests.Single().Sections.Single(s => s.Name == "after").Steps, s => Assert.Equal(StepRunState.Passed, s.State));

            // Step 11: both runs are in the history.
            for (var i = 0; i < 200 && workspace.History.Items.Count < 2; i++)
            {
                await Task.Delay(50);
            }

            Assert.Equal(["Failed", "Passed"], workspace.History.Items.Select(h => h.StatusText));
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

    [AvaloniaFact]
    public async Task A_target_that_fell_back_in_a_real_run_opens_in_the_targets_editor()
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
            var workspace = shell.Workspace!;
            await workspace.RunFileAsync("tests/locator-fallback.test.yaml");
            var run = workspace.Runs.Current!;
            for (var i = 0; i < 600 && run.IsActive; i++)
            {
                await Task.Delay(100);
            }

            Assert.Equal(RunState.Passed, run.State);
            var click = run.Tests.Single().AllSteps.Single(s => s.Action == "click");
            var row = Assert.Single(click.Locators);
            Assert.True(row.IsFallback);
            Assert.Equal("order.placeOrder", row.Target);
            var tab = (RunTabViewModel)workspace.SelectedTab!;
            run.Tests.Single().SelectedStep = click;

            tab.OpenTargetCommand.Execute(row);

            var editor = Assert.IsType<StepFileViewModel>(workspace.SelectedTab);
            Assert.Equal("targets/demo.targets.yaml", editor.File);
            Assert.Equal("order.placeOrder", editor.Targets!.SelectedName);
            Assert.Equal(["role", "testId"], editor.Targets.Form!.Candidates.Select(c => c.Kind));
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
