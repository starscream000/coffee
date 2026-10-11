// The screenshots of the desktop README (instruction D0004, task 22): the real
// main window, rendered on Avalonia's headless platform with a fake engine
// and demo-like data. With DESKTOP_DOC_SCREENS set to a folder (such as
// apps/desktop/docs/screens), each test saves its screen there; without it
// the tests only check that the screen shows what it should.

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.Tests.StepFiles;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Recording;
using Desktop.App.ViewModels.Runs;
using Desktop.App.Views;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class DocScreensTests : IDisposable
{
    private const string AddTodo = "tests/add-todo.test.yaml";
    private const string Login = "tests/login.test.yaml";
    private const string Recorded = "tests/clear-the-list.test.yaml";

    private const string AddTodoText = """
        version: 1
        name: Add a to-do
        tags: [smoke]
        steps:
          - goto: /todos
          - fill: { target: todos.newTodo, value: Buy milk }
          - press: { target: todos.newTodo, key: Enter }
          - expect.text: { target: todos.count, equals: '1' }
        after:
          - click: todos.clearAll
        targets:
          todos.newTodo:
            - role: textbox
              name: New to-do
            - label: New to-do
            - css: '#new-todo'
          todos.count:
            - testId: todo-count

        """;

    private readonly string _root = Path.Combine(Directory.CreateTempSubdirectory("desktop-screens-").FullName, "demo-tests");

    public DocScreensTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);

    private (ShellViewModel Shell, FakeEngineService Engine, MainWindow Window) Create(MemorySettingsStore? settings = null)
    {
        var engine = new FakeEngineService
        {
            Info = FakeEngineService.ReadyInfo("chromium"),
            Tests = [Make.Test(AddTodo, "Add a to-do", "smoke"), Make.Test(Login, "Customer logs in", "smoke", "account")],
            Actions = StepFormTests.RealActions(),
        };
        var files = new FakeProjectFiles();
        files.Files[AddTodo] = AddTodoText;
        files.Files[Login] = "version: 1\nname: Customer logs in\ntags: [smoke, account]\nsteps:\n  - call:\n      flow: flows/log-in.flow.yaml\n      with: { user: alice@example.com }\n  - expect.visible: account.menu\n";
        files.Files["flows/log-in.flow.yaml"] = "version: 1\nname: Log in\nparams:\n  user: { type: string }\nsteps:\n  - goto: /login\n";
        files.Files["targets/todos.targets.yaml"] = "version: 1\ntargets:\n  todos.clearAll:\n    - role: button\n      name: Clear all\n";
        files.Files[Product.ConfigFile] = "version: 1\n";
        var shell = new ShellViewModel(engine, settings ?? new MemorySettingsStore(), new FakeFolderPicker(_root), files, new ImmediateDispatcher(), _ => true, new FakeDialogs(), new ManualDelay());
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        return (shell, engine, window);
    }

    [AvaloniaFact]
    public async Task Start_page()
    {
        var settings = new MemorySettingsStore { Settings = new AppSettings { RecentProjects = ["/work/demo-tests", "/work/shop-tests"] } };
        var (shell, _, window) = Create(settings);
        await shell.InitializeAsync();

        Save(window, "start-page");
        Assert.Equal(2, shell.RecentProjects.Count);
    }

    [AvaloniaFact]
    public async Task New_project()
    {
        var (shell, _, window) = Create();
        await shell.InitializeAsync();
        shell.ShowNewProjectCommand.Execute(null);
        var form = shell.NewProject!;
        form.Name = "Demo tests";
        form.Location = Path.GetDirectoryName(_root)!;
        form.BaseUrl = "http://localhost:4310";

        Save(window, "new-project");
        Assert.True(shell.IsNewProjectOpen);
    }

    [AvaloniaFact]
    public async Task Steps_view()
    {
        var (shell, _, window) = Create();
        await shell.OpenProjectAsync(_root);
        shell.Workspace!.OpenFile(AddTodo);
        var tab = (StepFileViewModel)shell.Workspace.SelectedTab!;
        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps[1]);

        Save(window, "steps-view");
        Assert.Equal("fill", tab.Steps.Form!.Action);
    }

    [AvaloniaFact]
    public async Task Targets_view()
    {
        var (shell, _, window) = Create();
        await shell.OpenProjectAsync(_root);
        shell.Workspace!.OpenFile(AddTodo);
        var tab = (StepFileViewModel)shell.Workspace.SelectedTab!;
        tab.ShowTargetsCommand.Execute(null);
        tab.Targets!.Select("todos.newTodo");

        Save(window, "targets-view");
        Assert.Equal(3, tab.Targets.Form!.Candidates.Count);
    }

    [AvaloniaFact]
    public async Task Recording_in_progress_and_verified()
    {
        var (shell, engine, window) = Create();
        await shell.OpenProjectAsync(_root);
        var workspace = shell.Workspace!;
        workspace.RecordCommand.Execute(null);
        workspace.RecordStart!.TestName = "Clear the list";
        workspace.RecordStart.StartUrl = "/todos";
        await workspace.RecordStart.StartCommand.ExecuteAsync(null);
        var tab = (RecordingTabViewModel)workspace.SelectedTab!;
        foreach (var e in new RecordingEvent[]
        {
            Step(0, "goto", new { url = "/todos" }),
            Step(1, "fill", new { target = "todos.newTodo", value = "Walk the dog" }),
            Step(2, "press", new { target = "todos.newTodo", key = "Enter" }),
            Step(3, "click", new { target = "todos.item" }, "only CSS identifies this element"),
            Step(4, "click", new { target = "todos.clearAll" }),
            new RecordingNoticeEvent { RecordingId = "rec-1", Kind = "drag", Message = "A drag is not recorded yet: the to-do was moved by hand.", Page = "main", Url = "http://localhost:4310/todos" },
        })
        {
            engine.Raise(e);
        }

        Save(window, "recording");
        Assert.True(tab.IsRecording);

        engine.Raise(new RecordingStoppedEvent { RecordingId = "rec-1", File = Recorded, Reason = RecordingStopReason.Stopped, Steps = 5 });
        var s = new RunScript("verify-rec-1");
        foreach (var e in new EngineEvent[]
        {
            s.Started(RunScript.Planned("t1", Recorded, "Clear the list")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Go to /todos", file: Recorded, line: 4),
            s.Passed("t1", "s1"),
            s.TestFinished("t1", RunOutcome.Passed),
            s.Finished(RunOutcome.Passed, passed: 1),
        })
        {
            engine.Raise(e);
        }

        engine.Raise(new RecordingVerifiedEvent { RecordingId = "rec-1", RunId = "verify-rec-1", Status = VerifyOutcome.Passed });
        Save(window, "recording-verified");
        Assert.True(tab.IsPassed);

        static StepRecordedEvent Step(int index, string action, object parameters, string? review = null) => new()
        {
            RecordingId = "rec-1",
            Index = index,
            Step = new RecordedStep { Action = action, Params = JsonSerializer.SerializeToElement(parameters) },
            Targets = new Dictionary<string, JsonElement>(),
            Review = review,
        };
    }

    [AvaloniaFact]
    public async Task A_failed_run_with_its_error()
    {
        var (shell, engine, window) = Create();
        await shell.OpenProjectAsync(_root);
        var workspace = shell.Workspace!;
        engine.StartRun = _ => new StartRunResult { RunId = "run-1", ResultsDir = Path.Combine(_root, ".runs", "run-1") };
        await workspace.RunFileAsync(AddTodo);
        foreach (var e in FailedRun("run-1"))
        {
            engine.Raise(e);
        }

        // The run needs the room: the details panel has nothing for a run.
        shell.Layout.ToggleDetailsCommand.Execute(null);
        Save(window, "run-failed");
        Assert.Equal(RunState.Failed, workspace.Runs.Current!.State);
    }

    [AvaloniaFact]
    public async Task The_run_history()
    {
        var runs = DiskRunRecords.RunsFolder(_root);
        Write(Path.Combine(runs, "20261011-091500-000-a1b2"), FailedRun("20261011-091500-000-a1b2"));
        var s = new RunScript("20261011-093000-000-c3d4");
        Write(Path.Combine(runs, "20261011-093000-000-c3d4"),
        [
            s.Started(RunScript.Planned("t1", AddTodo, "Add a to-do"), RunScript.Planned("t2", Login, "Customer logs in")) with { StartedAt = "2026-10-11T09:30:00Z" },
            s.TestStarted("t1"), s.TestFinished("t1", RunOutcome.Passed),
            s.TestStarted("t2"), s.TestFinished("t2", RunOutcome.Passed),
            s.Finished(RunOutcome.Passed, passed: 2),
        ]);
        var (shell, _, window) = Create();
        await shell.OpenProjectAsync(_root);
        for (var i = 0; i < 100 && shell.Workspace!.History.Items.Count < 2; i++)
        {
            await Task.Delay(20);
        }

        shell.Layout.ShowRunsCommand.Execute(null);
        Save(window, "run-history");
        Assert.Equal(2, shell.Workspace!.History.Items.Count);
    }

    private static EngineEvent[] FailedRun(string runId)
    {
        var s = new RunScript(runId);
        return
        [
            s.Started(RunScript.Planned("t1", AddTodo, "Add a to-do")) with { StartedAt = "2026-10-11T09:15:00Z" },
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Go to /todos", file: AddTodo, line: 5) with { Action = "goto" },
            s.Passed("t1", "s1"),
            s.Step("t1", "s2", "Fill todos.newTodo with \"Buy milk\"", file: AddTodo, line: 6) with { Action = "fill" },
            s.Passed("t1", "s2"),
            s.Step("t1", "s3", "Press Enter in todos.newTodo", file: AddTodo, line: 7) with { Action = "press" },
            s.Passed("t1", "s3"),
            s.Step("t1", "s4", "Expect the text of todos.count to be \"1\"", file: AddTodo, line: 8) with { Action = "expect.text" },
            s.Failed("t1", "s4") with
            {
                Error = new ErrorInfo
                {
                    Code = "TextMismatch",
                    Message = "The text of \"todos.count\" is \"2\", not \"1\".",
                    Hint = "The list still held a to-do from an earlier run; clear it in a before step.",
                    Expected = JsonSerializer.SerializeToElement("1"),
                    Actual = JsonSerializer.SerializeToElement("2"),
                },
            },
            s.Step("t1", "s5", "Click todos.clearAll", StepSection.After, file: AddTodo, line: 10),
            s.Passed("t1", "s5"),
            s.TestFinished("t1", RunOutcome.Failed),
            s.Finished(RunOutcome.Failed, failed: 1),
        ];
    }

    private static void Write(string folder, EngineEvent[] events)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, DiskRunRecords.EventsFile), string.Concat(events.Select(e => RunFolders.Line(e) + "\n")));
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        if (Environment.GetEnvironmentVariable("DESKTOP_DOC_SCREENS") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, name + ".png"), new PngBitmapEncoderOptions());
        }

        Assert.NotEmpty(window.GetVisualDescendants().OfType<Control>());
    }
}
