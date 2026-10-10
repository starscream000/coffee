// Running tests from the app with a fake engine playing scripted runs
// (instruction D0003, tasks 8 to 11): what is asked of the engine, the run
// built from its events (pass, failure with every detail, cancel, the engine
// stopping), one run at a time, the question about unsaved files, refusals,
// and page states.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Runs;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Runs;

public sealed class RunControlTests
{
    private const string Root = "/work/shop-tests";
    private const string FileA = "tests/checkout/a.test.yaml";
    private const string FileB = "tests/checkout/b.test.yaml";
    private const string FileC = "tests/login.test.yaml";

    private sealed class Setup
    {
        public Setup(params string[] browsers)
        {
            Engine.Info = FakeEngineService.ReadyInfo(browsers.Length == 0 ? ["chromium"] : browsers);
            Engine.Tests = [Make.Test(FileA, "A", "smoke"), Make.Test(FileB, "B"), Make.Test(FileC, "C", "smoke")];
            foreach (var t in Engine.Tests)
            {
                Files.Files[t.File] = "version: 1\n";
            }

            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), Files, new ImmediateDispatcher(), p => p == Root, Dialogs, new ManualDelay());
        }

        public FakeEngineService Engine { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public ShellViewModel Shell { get; }

        public WorkspaceViewModel Workspace => Shell.Workspace!;

        public RunControlViewModel Runs => Workspace.Runs;

        public async Task OpenAsync()
        {
            await Shell.InitializeAsync();
            await Shell.OpenProjectAsync(Root);
        }

        public void Play(params EngineEvent[] events)
        {
            foreach (var e in events)
            {
                Engine.Raise(e);
            }
        }
    }

    [AvaloniaFact]
    public async Task Run_all_sends_the_environment_and_the_browser_choice_and_opens_the_run()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.SelectedEnvironment = "staging";
        setup.Runs.Headed = true;

        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        var sent = Assert.Single(setup.Engine.Started);
        Assert.Null(sent.Files);
        Assert.Null(sent.Tags);
        Assert.Equal("staging", sent.Env);
        Assert.True(sent.Options?.Headed);
        var tab = Assert.IsType<RunTabViewModel>(setup.Workspace.SelectedTab);
        Assert.Equal("run-1", tab.Run.RunId);
        Assert.True(setup.Runs.IsRunning);
        Assert.False(setup.Runs.CanRun);
    }

    [AvaloniaFact]
    public async Task Cancelling_asks_the_engine_and_the_run_ends_cancelled()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        var s = new RunScript("run-1");
        setup.Play(s.Started(RunScript.Planned("t1", FileA, "A")), s.TestStarted("t1"), s.Step("t1", "s1", "Wait for the order"));

        var tab = (RunTabViewModel)setup.Workspace.SelectedTab!;
        Assert.True(tab.IsLive);
        await tab.Cancel!.ExecuteAsync(null);

        Assert.Contains("cancelRun run-1", setup.Engine.Calls);
        Assert.Equal("Cancelling…", tab.Run.StatusText);
        setup.Play(
            s.Skipped("t1", "s1", StepSkipReason.Cancelled, "cancelled"),
            s.TestFinished("t1", RunOutcome.Cancelled),
            s.Finished(RunOutcome.Cancelled, cancelled: 1));
        Assert.Equal(RunState.Cancelled, tab.Run.State);
        Assert.Equal(TestRunState.Cancelled, tab.Run.Test("t1")!.State);
        Assert.False(setup.Runs.IsRunning);
    }

    [AvaloniaFact]
    public async Task When_the_engine_stops_mid_run_what_it_reported_is_kept()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        var s = new RunScript("run-1");
        setup.Play(
            s.Started(RunScript.Planned("t1", FileA, "A")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Open"),
            s.Passed("t1", "s1"),
            s.Step("t1", "s2", "Click"));

        setup.Engine.SetState(EngineState.Failed, new EngineExitedException(1, "boom"));

        var run = setup.Runs.Current!;
        Assert.Equal(RunState.Unfinished, run.State);
        Assert.Equal(StepRunState.Passed, run.Test("t1")!.Step("s1")!.State);
        Assert.Equal(StepRunState.Interrupted, run.Test("t1")!.Step("s2")!.State);
        Assert.Equal(TestRunState.Interrupted, run.Test("t1")!.State);
        Assert.Contains(run.Messages, m => m.StartsWith("The engine stopped during the run.", StringComparison.Ordinal));
        Assert.False(setup.Runs.CanRun);
        Assert.Equal("Running is off because the engine is not running.", setup.Runs.UnavailableReason);
    }

    [AvaloniaFact]
    public async Task Events_that_arrive_before_the_answer_to_startRun_are_kept()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var s = new RunScript("run-1");
        setup.Engine.BeforeAnswer = method =>
        {
            if (method == "startRun")
            {
                setup.Play(s.Started(RunScript.Planned("t1", FileA, "A")), s.TestStarted("t1"));
            }

            return Task.CompletedTask;
        };

        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        var run = setup.Runs.Current!;
        Assert.Equal(RunState.Running, run.State);
        Assert.Equal(TestRunState.Running, Assert.Single(run.Tests).State);
    }

    [AvaloniaFact]
    public async Task Without_a_browser_running_is_off_and_says_how_to_install_one()
    {
        var setup = new Setup();
        setup.Engine.Info = FakeEngineService.ReadyInfo();
        setup.Engine.OnStart = e =>
        {
            e.State = EngineState.Ready;
            e.Info = FakeEngineService.ReadyInfo();
            e.SetState(EngineState.Ready);
            return Task.CompletedTask;
        };
        await setup.OpenAsync();

        Assert.False(setup.Runs.CanRun);
        Assert.Contains(RunControlViewModel.InstallCommand, setup.Runs.UnavailableReason, StringComparison.Ordinal);
        Assert.Contains(Product.NpmScope + "/engine", RunControlViewModel.InstallCommand, StringComparison.Ordinal);
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        Assert.Empty(setup.Engine.Started);
        Assert.Equal(setup.Runs.UnavailableReason, setup.Runs.Notice);
    }

    [AvaloniaFact]
    public async Task One_run_at_a_time()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        Assert.Single(setup.Engine.Started);
    }

    [AvaloniaFact]
    public async Task The_engine_refusing_because_a_run_is_going_is_explained()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Engine.StartRun = _ => throw FakeEngineService.Refusal("startRun", ErrorCodes.RunInProgress, "A run is in progress.");

        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        Assert.Equal("The engine is already running tests. Wait for that run to end, or cancel it.", setup.Runs.Notice);
        Assert.True(setup.Runs.CanRun);
        Assert.IsNotType<RunTabViewModel>(setup.Workspace.SelectedTab);
    }

    [AvaloniaFact]
    public async Task Invalid_files_stop_a_run_and_their_problems_are_shown()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var problem = Make.Error(FileA, 4, "UnknownAction", "Unknown action \"clik\".");
        setup.Engine.StartRun = _ => throw FakeEngineService.Refusal(
            "startRun",
            ErrorCodes.StepFilesInvalid,
            "The step files have errors.",
            new StepFilesInvalidData { Name = ErrorCodes.StepFilesInvalid, Diagnostics = [problem] });

        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        Assert.Equal("UnknownAction", Assert.Single(setup.Workspace.Problems.All).Code);
        Assert.Contains("1 problem to fix first", setup.Runs.Notice, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Run_selected_runs_a_test_or_the_tests_shown_in_a_folder()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tests = setup.Workspace.Explorer.Roots.Single(n => n.Name == "tests");
        var checkout = tests.Children.Single(n => n.Name == "checkout");

        setup.Workspace.Explorer.SelectedNode = checkout;
        await setup.Workspace.RunSelectedCommand.ExecuteAsync(null);
        Assert.Equal([FileA, FileB], setup.Engine.Started[^1].Files);

        var s = new RunScript("run-1");
        setup.Play(s.Started(), s.Finished(RunOutcome.Passed));
        setup.Workspace.Explorer.SelectedNode = checkout.Children[1];
        await setup.Workspace.RunSelectedCommand.ExecuteAsync(null);
        Assert.Equal([FileB], setup.Engine.Started[^1].Files);
    }

    [AvaloniaFact]
    public async Task Run_tag_runs_the_tests_with_the_chosen_tag()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.Explorer.SelectedTag = "smoke";

        await setup.Workspace.RunTagCommand.ExecuteAsync(null);

        Assert.Equal(["smoke"], setup.Engine.Started[^1].Tags);
        Assert.Null(setup.Engine.Started[^1].Files);
    }

    [AvaloniaFact]
    public async Task A_tab_runs_its_test_and_a_flow_says_it_cannot_be_run_alone()
    {
        var setup = new Setup();
        setup.Files.Files["flows/login.flow.yaml"] = "version: 1\n";
        await setup.OpenAsync();
        setup.Workspace.OpenFile("flows/login.flow.yaml");
        var flow = (StepFileViewModel)setup.Workspace.SelectedTab!;

        await flow.RunCommand.ExecuteAsync(null);
        Assert.Empty(setup.Engine.Started);
        Assert.StartsWith("flows/login.flow.yaml is not a test", setup.Runs.Notice, StringComparison.Ordinal);

        setup.Workspace.OpenFile(FileC);
        await ((StepFileViewModel)setup.Workspace.SelectedTab!).RunCommand.ExecuteAsync(null);
        Assert.Equal([FileC], Assert.Single(setup.Engine.Started).Files);
    }

    [AvaloniaTheory]
    [InlineData(UnsavedChangesChoice.Cancel, false, true)]
    [InlineData(UnsavedChangesChoice.Save, true, false)]
    [InlineData(UnsavedChangesChoice.Discard, true, true)]
    public async Task Unsaved_files_are_asked_about_before_a_run(UnsavedChangesChoice answer, bool runs, bool stillDirty)
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.OpenFile(FileA);
        var tab = (StepFileViewModel)setup.Workspace.SelectedTab!;
        tab.Document.Insert(tab.Document.TextLength, "name: A\n");
        setup.Dialogs.RunAnswer = answer;

        await setup.Workspace.RunAllCommand.ExecuteAsync(null);

        Assert.Equal($"run {FileA}", Assert.Single(setup.Dialogs.Asked));
        Assert.Equal(runs, setup.Engine.Started.Count == 1);
        Assert.Equal(stillDirty, tab.IsDirty);
        Assert.Equal(answer == UnsavedChangesChoice.Save, setup.Files.Files[FileA].Contains("name: A", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task A_run_tab_cannot_close_while_its_run_goes_on()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        var tab = (RunTabViewModel)setup.Workspace.SelectedTab!;

        tab.CloseCommand.Execute(null);
        Assert.Contains(tab, setup.Workspace.Tabs);
        Assert.StartsWith("A run's tab stays open", setup.Runs.Notice, StringComparison.Ordinal);

        var s = new RunScript("run-1");
        setup.Play(s.Started(), s.Finished(RunOutcome.Passed));
        tab.CloseCommand.Execute(null);
        Assert.DoesNotContain(tab, setup.Workspace.Tabs);
    }

    [AvaloniaFact]
    public async Task Go_to_step_opens_the_file_at_its_line()
    {
        var setup = new Setup();
        setup.Files.Files[FileA] = string.Concat(Enumerable.Range(1, 10).Select(i => $"# line {i}\n"));
        await setup.OpenAsync();
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        var s = new RunScript("run-1");
        setup.Play(s.Started(RunScript.Planned("t1", FileA, "A")), s.TestStarted("t1"), s.Step("t1", "s1", "Click", file: FileA, line: 7));
        var tab = (RunTabViewModel)setup.Workspace.SelectedTab!;

        tab.GoToStepCommand.Execute(tab.Run.Test("t1")!.Step("s1"));

        var editor = Assert.IsType<StepFileViewModel>(setup.Workspace.SelectedTab);
        Assert.Equal(FileA, editor.File);
        Assert.Equal(7, editor.RevealedLine);
    }

    [AvaloniaFact]
    public async Task Open_page_state_asks_the_engine_and_falls_back_to_the_screenshot()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        var s = new RunScript("run-1");
        setup.Play(s.Started(RunScript.Planned("t1", FileA, "A")), s.TestStarted("t1"), s.Step("t1", "s1", "Click"), s.Passed("t1", "s1"), s.Snapshot("t1", "s1"));
        var tab = (RunTabViewModel)setup.Workspace.SelectedTab!;
        var step = tab.Run.Test("t1")!.Step("s1")!;

        await tab.OpenPageStateCommand.ExecuteAsync(step);
        Assert.Contains("openSnapshot t1 s1", setup.Engine.Calls);
        Assert.Null(tab.PageStateMessage);

        setup.Engine.OpenSnapshot = _ => throw FakeEngineService.Refusal(
            "openSnapshot",
            ErrorCodes.SnapshotUnavailable,
            "The page state cannot be opened without a browser.",
            new SnapshotUnavailableData { Name = ErrorCodes.SnapshotUnavailable, Screenshot = "/shots/s1.png" });
        await tab.OpenPageStateCommand.ExecuteAsync(step);
        Assert.Equal("/shots/s1.png", step.ScreenshotPath);
        Assert.Contains("The screenshot is shown instead.", tab.PageStateMessage, StringComparison.Ordinal);
    }
}
