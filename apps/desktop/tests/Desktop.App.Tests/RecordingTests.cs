// Recording (instruction D0004, tasks 14 to 17): the form Record opens, the
// Record tab as steps arrive and change, review marks and notices, Stop, the
// check that follows (passed and failed), a password that matched no secret,
// and the rules: one recording at a time, none during a run, runs wait while
// recording, and what the engine's stopping keeps. With DESKTOP_SCREENSHOTS
// set to a folder, the view test also saves what it rendered.

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Recording;
using Desktop.App.Views;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class RecordingTests
{
    private const string Root = "/work/shop-tests";
    private const string File = "tests/add-a-to-do.test.yaml";
    private const string Id = "rec-1";

    private const string RecordedText = """
        version: 1
        name: Add a to-do
        steps:
          - goto: /todos
          - fill: { target: todos.newTodo, value: Buy milk }
          - click: todos.add
        targets:
          todos.newTodo: { label: New to-do }
          todos.add: { role: button, name: Add }
        """;

    private sealed class Setup
    {
        public Setup(params string[] browsers)
        {
            Engine.Info = FakeEngineService.ReadyInfo(browsers.Length == 0 ? ["chromium"] : browsers);
            Engine.Tests = [Make.Test("tests/other.test.yaml", "Other")];
            Files.Files["tests/other.test.yaml"] = "version: 1\nsteps:\n  - goto: /\n";
            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), Files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), new ManualDelay());
        }

        public FakeEngineService Engine { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public ShellViewModel Shell { get; }

        public WorkspaceViewModel Workspace => Shell.Workspace!;

        public async Task OpenAsync()
        {
            await Shell.InitializeAsync();
            await Shell.OpenProjectAsync(Root);
        }

        /// <summary>Records "Add a to-do" from /todos and returns its tab.</summary>
        public async Task<RecordingTabViewModel> RecordAsync()
        {
            Workspace.RecordCommand.Execute(null);
            var form = Workspace.RecordStart!;
            form.TestName = "Add a to-do";
            form.StartUrl = "/todos";
            await form.StartCommand.ExecuteAsync(null);
            return Assert.IsType<RecordingTabViewModel>(Workspace.SelectedTab);
        }

        public void Raise(params RecordingEvent[] events)
        {
            foreach (var e in events)
            {
                Engine.Raise(e);
            }
        }

        public void Play(params EngineEvent[] events)
        {
            foreach (var e in events)
            {
                Engine.Raise(e);
            }
        }
    }

    private static RecordedStep Step(string action, object parameters) =>
        new() { Action = action, Params = JsonSerializer.SerializeToElement(parameters) };

    private static StepRecordedEvent Recorded(int index, RecordedStep step, string? review = null) =>
        new() { RecordingId = Id, Index = index, Step = step, Targets = new Dictionary<string, JsonElement>(), Review = review };

    private static RecordingStoppedEvent Stopped(RecordingStopReason reason = RecordingStopReason.Stopped) =>
        new() { RecordingId = Id, File = File, Reason = reason, Steps = 3 };

    private static RecordingVerifiedEvent Verified(VerifyOutcome status) => new() { RecordingId = Id, RunId = $"verify-{Id}", Status = status };

    private static EngineRequestException Refusal(string method, string name, string message) =>
        new(method, new JsonRpcError { Code = -32000, Message = message, Data = ProtocolJson.ToElement(new { name }) });

    [AvaloniaFact]
    public async Task Record_asks_for_a_name_and_starts_in_the_chosen_environment()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.SelectedEnvironment = "staging";

        setup.Workspace.RecordCommand.Execute(null);
        var form = setup.Workspace.RecordStart!;
        Assert.True(form.HasBrowser);
        Assert.Equal("staging", form.Environment);
        Assert.Equal([RecordStartViewModel.NoLogin, "customer"], form.Logins);
        form.TestName = "Add a to-do!";
        Assert.Equal(File, form.File);
        Assert.Equal($"Saved as {File}", form.FileText);
        form.Login = "customer";
        await form.StartCommand.ExecuteAsync(null);

        var sent = Assert.Single(setup.Engine.Recordings);
        Assert.Equal(new StartRecordingParams { File = File, StartUrl = "/", Environment = "staging", Login = "customer", Name = "Add a to-do!" }, sent);
        Assert.False(setup.Workspace.IsRecordStartOpen);
        var tab = Assert.IsType<RecordingTabViewModel>(setup.Workspace.SelectedTab);
        Assert.True(tab.IsRecording);
        Assert.False(tab.CanClose);
        Assert.Equal("● Recording", tab.Title);
    }

    [AvaloniaFact]
    public async Task The_form_refuses_what_it_can_and_shows_the_engines_refusals()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.RecordCommand.Execute(null);
        var form = setup.Workspace.RecordStart!;

        await form.StartCommand.ExecuteAsync(null);
        Assert.StartsWith("Give the test a name", form.NameError, StringComparison.Ordinal);

        form.TestName = "Other";
        await form.StartCommand.ExecuteAsync(null);
        Assert.Equal("A test is already saved as tests/other.test.yaml. Choose another name.", form.NameError);

        form.TestName = "Add a to-do";
        form.StartUrl = "todos";
        await form.StartCommand.ExecuteAsync(null);
        Assert.StartsWith("Start with /", form.StartUrlError, StringComparison.Ordinal);
        Assert.Empty(setup.Engine.Recordings);

        form.StartUrl = "/todos";
        setup.Engine.StartRecording = _ => throw Refusal(Methods.StartRecording, ErrorCodes.FileExists, "tests/add-a-to-do.test.yaml exists.");
        await form.StartCommand.ExecuteAsync(null);
        Assert.Equal("tests/add-a-to-do.test.yaml exists.", form.NameError);

        setup.Engine.StartRecording = _ => throw Refusal(Methods.StartRecording, ErrorCodes.RunInProgress, "A run is in progress.");
        await form.StartCommand.ExecuteAsync(null);
        Assert.StartsWith("Tests are running. Recording can start once the run has ended.", form.Error, StringComparison.Ordinal);
        Assert.True(setup.Workspace.IsRecordStartOpen);
        Assert.False(setup.Workspace.IsRecording);
    }

    [AvaloniaFact]
    public async Task Without_a_browser_the_form_shows_the_install_command()
    {
        var setup = new Setup([]);
        setup.Engine.Info = FakeEngineService.ReadyInfo();
        await setup.OpenAsync();

        setup.Workspace.RecordCommand.Execute(null);

        var form = setup.Workspace.RecordStart!;
        Assert.False(form.HasBrowser);
        Assert.Contains(FakeEngineService.EngineInstallCommand, form.NoBrowser, StringComparison.Ordinal);
        Assert.False(form.StartCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Steps_arrive_as_cards_change_in_place_and_carry_their_review()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        // The first events can overtake the answer to startRecording.
        setup.Engine.BeforeAnswer = method =>
        {
            if (method == "startRecording")
            {
                setup.Raise(new RecordingStartedEvent { RecordingId = Id, File = File, StartUrl = "http://localhost:4310/todos" }, Recorded(0, Step("goto", new { url = "/todos" })));
            }

            return Task.CompletedTask;
        };
        var tab = await setup.RecordAsync();
        Assert.Equal("goto", Assert.Single(tab.Steps).Action);
        Assert.Contains($"listTests", setup.Engine.Calls);

        setup.Raise(
            Recorded(1, Step("fill", new { target = "todos.newTodo", value = "Bu" })),
            new StepChangedEvent { RecordingId = Id, Index = 1, Step = Step("fill", new { target = "todos.newTodo", value = "Buy milk" }) },
            Recorded(2, Step("click", new { target = "todos.thing" }), "only CSS identifies this element"),
            new RecordingNoticeEvent { RecordingId = Id, Kind = "drag", Message = "A drag is not recorded yet.", Page = "main", Url = "http://localhost:4310/todos" },
            new StepRecordedEvent { RecordingId = "rec-other", Index = 3, Step = Step("back", new { }), Targets = new Dictionary<string, JsonElement>() });

        Assert.Equal(["goto", "fill", "click"], tab.Steps.Select(s => s.Action));
        Assert.Equal("target: todos.newTodo, value: Buy milk", tab.Steps[1].Detail);
        Assert.Equal(2, tab.Steps[1].Number);
        Assert.True(tab.Steps[2].HasReview);
        Assert.Equal("only CSS identifies this element", tab.Steps[2].Review);
        Assert.Equal("A drag is not recorded yet.", Assert.Single(tab.Notices).Message);
        Assert.True(tab.HasNotices);
        Assert.Null(tab.PasswordHelp);
    }

    [AvaloniaFact]
    public async Task Stop_then_the_check_says_this_test_runs()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tab = await setup.RecordAsync();
        setup.Files.Files[File] = RecordedText;
        setup.Raise(Recorded(0, Step("goto", new { url = "/todos" })));

        await tab.StopCommand.ExecuteAsync(null);
        Assert.Contains($"stopRecording {Id}", setup.Engine.Calls);
        setup.Raise(Stopped());

        Assert.Equal(RecordingPhase.Checking, tab.Phase);
        Assert.Contains($"verifyRecording {Id}", setup.Engine.Calls);
        Assert.False(setup.Workspace.IsRecording);
        Assert.True(tab.CanClose);
        var s = new RunScript($"verify-{Id}");
        setup.Play(
            s.Started(RunScript.Planned("t1", File, "Add a to-do")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Go to /todos", file: File, line: 4),
            s.Passed("t1", "s1"),
            s.TestFinished("t1", RunOutcome.Passed),
            s.Finished(RunOutcome.Passed, passed: 1));
        setup.Raise(Verified(VerifyOutcome.Passed));

        Assert.True(tab.IsPassed);
        Assert.Equal("This test runs: it passed when it was checked.", tab.StatusText);
        Assert.Equal($"verify-{Id}", tab.Run!.RunId);
        Assert.Equal("Recorded: add-a-to-do.test.yaml", tab.Title);

        tab.OpenTestCommand.Execute(null);
        Assert.Equal(File, Assert.IsType<StepFileViewModel>(setup.Workspace.SelectedTab).File);
        tab.OpenRunCommand.Execute(null);
        Assert.IsType<ViewModels.Runs.RunTabViewModel>(setup.Workspace.SelectedTab);
    }

    [AvaloniaFact]
    public async Task A_failed_stop_says_so_and_the_recording_goes_on()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tab = await setup.RecordAsync();
        setup.Engine.StopRecording = _ => throw Refusal(Methods.StopRecording, ErrorCodes.RecordingNotFound, "No recording rec-1.");

        await tab.StopCommand.ExecuteAsync(null);

        Assert.Equal(RecordingPhase.Recording, tab.Phase);
        Assert.Equal("The recording did not stop: No recording rec-1. Close the recording browser to end it.", tab.Problem);
        Assert.True(tab.StopCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task A_failed_check_says_which_step_and_opens_the_test_there()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tab = await setup.RecordAsync();
        setup.Files.Files[File] = RecordedText;

        // Closing the recording browser ends the recording too.
        setup.Raise(Stopped(RecordingStopReason.BrowserClosed));
        var s = new RunScript($"verify-{Id}");
        setup.Play(
            s.Started(RunScript.Planned("t1", File, "Add a to-do")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Click todos.add", file: File, line: 6),
            s.Failed("t1", "s1"),
            s.TestFinished("t1", RunOutcome.Failed),
            s.Finished(RunOutcome.Failed, failed: 1));
        setup.Raise(Verified(VerifyOutcome.Failed));

        Assert.True(tab.IsFailed);
        Assert.Equal("Click todos.add", tab.FailedStep);
        Assert.StartsWith("The text of \"order total\"", tab.FailedMessage, StringComparison.Ordinal);
        Assert.Equal(6, tab.FailedLine);

        tab.OpenFailedStepCommand.Execute(null);
        var editor = Assert.IsType<StepFileViewModel>(setup.Workspace.SelectedTab);
        Assert.Equal(StepFileMode.Steps, editor.View);
        Assert.Equal("click", editor.Steps!.Selected!.Action);
    }

    [AvaloniaFact]
    public async Task A_password_that_matched_no_secret_is_explained()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tab = await setup.RecordAsync();

        setup.Raise(Recorded(1, Step("fill", new { target = "login.password", value = "${vars.loginPassword}" }),
            "typed into a password field, and no declared secret has this value; declare a secret and use ${secrets.NAME}"));

        Assert.Equal("loginPassword", tab.Steps[0].PasswordVariable);
        Assert.Contains("login.password", tab.PasswordHelp, StringComparison.Ordinal);
        Assert.Contains("an empty variable, loginPassword, in its place", tab.PasswordHelp, StringComparison.Ordinal);
        Assert.Contains("${secrets.NAME}", tab.PasswordHelp, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task One_recording_at_a_time_none_during_a_run_and_runs_wait()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tab = await setup.RecordAsync();

        // Runs wait while recording, with the reason shown.
        Assert.False(setup.Workspace.Runs.CanRun);
        Assert.Equal("Runs wait until the recording stops.", setup.Workspace.Runs.UnavailableReason);
        await setup.Workspace.RunAllCommand.ExecuteAsync(null);
        Assert.Empty(setup.Engine.Started);

        // A second Record shows the recording going on.
        setup.Workspace.SelectedTab = setup.Workspace.Actions;
        setup.Workspace.RecordCommand.Execute(null);
        Assert.Same(tab, setup.Workspace.SelectedTab);
        Assert.False(setup.Workspace.IsRecordStartOpen);
        Assert.StartsWith("A recording is already going on", setup.Workspace.Runs.Notice, StringComparison.Ordinal);

        // Once it stops (and its check ends), runs can start again; Record waits for a run.
        setup.Raise(Stopped());
        var s = new RunScript($"verify-{Id}");
        setup.Play(s.Started(), s.Finished(RunOutcome.Passed));
        Assert.True(setup.Workspace.Runs.CanRun);
        setup.Engine.BeforeAnswer = _ => Task.Delay(Timeout.Infinite);
        _ = setup.Workspace.RunAllCommand.ExecuteAsync(null);
        setup.Workspace.RecordCommand.Execute(null);
        Assert.False(setup.Workspace.IsRecordStartOpen);
        Assert.StartsWith("Tests are running. Record once the run has ended", setup.Workspace.Runs.Notice, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_engine_stopping_mid_recording_keeps_what_was_recorded_and_says_so()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var tab = await setup.RecordAsync();
        setup.Raise(Recorded(0, Step("goto", new { url = "/todos" })), Recorded(1, Step("click", new { target = "todos.add" })));

        setup.Engine.SetState(EngineState.Failed, new EngineException("The engine exited with code 1."));

        Assert.Equal(RecordingPhase.EngineStopped, tab.Phase);
        Assert.Equal($"The engine stopped during the recording. What was recorded so far is kept in {File}.", tab.StatusText);
        Assert.StartsWith("2 recorded steps are in the file.", tab.Problem, StringComparison.Ordinal);
        Assert.True(tab.CanClose);
        Assert.False(setup.Workspace.IsRecording);
    }

    [AvaloniaFact]
    public async Task The_record_tab_renders_while_recording_and_after_the_check()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var window = new MainWindow { DataContext = setup.Shell, Width = 1280, Height = 800 };
        window.Show();
        setup.Workspace.RecordCommand.Execute(null);
        setup.Workspace.RecordStart!.TestName = "Add a to-do";
        Render(window, "28-record-form");
        Assert.True(Find<RecordStartView>(window, "RecordStartForm").IsEffectivelyVisible);
        await setup.Workspace.RecordStart.StartCommand.ExecuteAsync(null);
        var tab = (RecordingTabViewModel)setup.Workspace.SelectedTab!;
        setup.Files.Files[File] = RecordedText;
        setup.Raise(
            Recorded(0, Step("goto", new { url = "/todos" })),
            Recorded(1, Step("fill", new { target = "todos.newTodo", value = "Buy milk" })),
            Recorded(2, Step("click", new { target = "todos.thing" }), "only CSS identifies this element"),
            new RecordingNoticeEvent { RecordingId = Id, Kind = "drag", Message = "A drag is not recorded yet.", Page = "main", Url = "http://localhost:4310/todos" });
        Render(window, "29-recording");
        Assert.Equal(3, Find<ItemsControl>(window, "RecordedSteps").ItemCount);
        Assert.True(Find<Button>(window, "StopRecordingButton").IsEffectivelyVisible);

        setup.Raise(Stopped());
        var s = new RunScript($"verify-{Id}");
        setup.Play(s.Started(RunScript.Planned("t1", File, "Add a to-do")), s.TestStarted("t1"), s.Step("t1", "s1", "Go to /todos", file: File, line: 4), s.Passed("t1", "s1"),
            s.TestFinished("t1", RunOutcome.Passed), s.Finished(RunOutcome.Passed, passed: 1));
        setup.Raise(Verified(VerifyOutcome.Passed));
        Render(window, "30-recording-verified");
        Assert.True(Find<TextBlock>(window, "VerdictPassed").IsEffectivelyVisible);
        Assert.False(Find<Button>(window, "StopRecordingButton").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "OpenRecordedTestButton").IsEffectivelyVisible);
        Assert.False(tab.IsRecording);
    }

    private static void Render(Window window, string? snapshot = null)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        if (snapshot is not null && Environment.GetEnvironmentVariable("DESKTOP_SCREENSHOTS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, snapshot + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private static T Find<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named {name} is in the window.");
}
