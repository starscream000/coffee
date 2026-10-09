// Findings 3 (app side), 5, 6 and 7 of review D0001: no start after shutdown;
// no command or discarded task loses an exception; one refresh at a time with
// the newest answer winning; a changed action file reopens the project.

using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class ReviewD0001FixesTests
{
    private const string Root = "/work/shop-tests";

    private static (ShellViewModel Shell, FakeEngineService Engine, FakeProjectFiles Files) Create()
    {
        var engine = new FakeEngineService { Tests = [Make.Test("tests/a.test.yaml", "A")] };
        var files = new FakeProjectFiles();
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root);
        return (shell, engine, files);
    }

    private static bool Logged(ShellViewModel shell, string text) =>
        shell.Engine.Log.Any(l => l.Text.Contains(text, StringComparison.Ordinal));

    // Finding 5

    [Fact]
    public async Task The_engine_stopping_between_openProject_and_the_next_request_ends_in_the_notice_bar()
    {
        var (shell, engine, _) = Create();
        engine.BeforeAnswer = method => method == "listTests" ? Task.FromException(new EngineNotReadyException(EngineState.Failed)) : Task.CompletedTask;

        await shell.OpenProjectAsync(Root);

        Assert.Null(shell.Workspace);
        Assert.Contains("The engine is not ready", shell.Notice, StringComparison.Ordinal);
        Assert.True(Logged(shell, nameof(EngineNotReadyException)));
    }

    [Fact]
    public async Task A_result_that_does_not_fit_ends_in_the_notice_bar()
    {
        var (shell, engine, _) = Create();
        engine.BeforeAnswer = method => method == "listActions"
            ? Task.FromException(new ProtocolViolationException("The engine's answer to \"listActions\" does not fit this app's protocol."))
            : Task.CompletedTask;

        await shell.OpenProjectAsync(Root);

        Assert.Null(shell.Workspace);
        Assert.Contains("does not fit", shell.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unexpected_error_while_opening_is_reported_not_thrown()
    {
        var (shell, engine, _) = Create();
        engine.OpenProject = _ => throw new InvalidOperationException("bug in the app");

        await shell.OpenProjectAsync(Root);

        Assert.Contains("unexpected error (InvalidOperationException: bug in the app)", shell.Notice, StringComparison.Ordinal);
        Assert.True(Logged(shell, "failed unexpectedly"));
    }

    [Fact]
    public async Task Validation_and_file_change_failures_end_in_the_status_line_and_the_log()
    {
        var (shell, engine, files) = Create();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;

        engine.Validate = _ => throw new InvalidOperationException("validate broke");
        await workspace.ValidateCommand.ExecuteAsync(null);
        Assert.StartsWith("Validation failed: validate broke", workspace.Status, StringComparison.Ordinal);

        engine.Validate = _ => [];
        engine.BeforeAnswer = method => method == "listTests" ? Task.FromException(new EngineExitedException(1, "gone")) : Task.CompletedTask;
        var refresh = workspace.OnFilesChangedAsync(["tests/b.test.yaml"]);
        await refresh;

        Assert.True(refresh.IsCompletedSuccessfully);
        Assert.StartsWith("Could not refresh after a file change: The engine stopped", workspace.Status, StringComparison.Ordinal);
        Assert.True(Logged(shell, "Could not refresh after a file change: " + typeof(EngineExitedException).FullName));
    }

    // Finding 6

    [Fact]
    public async Task Batches_during_a_refresh_are_joined_into_one_more_refresh()
    {
        var (shell, engine, _) = Create();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;
        engine.Calls.Clear();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.BeforeAnswer = method => method == "validate" ? gate.Task : Task.CompletedTask;

        var first = workspace.OnFilesChangedAsync(["flows/a.flow.yaml"]);
        var second = workspace.OnFilesChangedAsync(["flows/b.flow.yaml"]);
        var third = workspace.OnFilesChangedAsync(["tests/new.test.yaml"]);
        Assert.Equal(["validate tests/a.test.yaml"], engine.Calls);

        engine.BeforeAnswer = null;
        gate.SetResult();
        await Task.WhenAll(first, second, third);

        // One refresh for the first batch, then one for the two that waited (with a re-list, because a test file came).
        Assert.Equal(["validate tests/a.test.yaml", "listTests", "validate tests/a.test.yaml"], engine.Calls);
    }

    [Fact]
    public async Task An_older_validation_answer_never_replaces_a_newer_one()
    {
        var (shell, engine, _) = Create();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answers = new Queue<IReadOnlyList<Diagnostic>>([[Make.Error("tests/a.test.yaml", 1, "Old")], [Make.Error("tests/a.test.yaml", 2, "New")]]);
        var calls = 0;
        engine.Validate = _ => answers.Dequeue();
        engine.BeforeAnswer = method => method == "validate" && ++calls == 1 ? slow.Task : Task.CompletedTask;

        var older = workspace.ValidateAsync(TestContext.Current.CancellationToken);
        await workspace.ValidateAsync(TestContext.Current.CancellationToken);
        slow.SetResult();
        await older;

        Assert.Equal("New", Assert.Single(workspace.Problems.All).Code);
        Assert.False(workspace.IsBusy);
    }

    // Finding 7

    [Theory]
    [InlineData("actions/shop/add-to-cart.ts", true)]
    [InlineData("actions/x.mts", true)]
    [InlineData("helpers/y.cjs", true)]
    [InlineData("actions/types.d.ts", false)]
    [InlineData("tests/a.test.yaml", false)]
    public async Task A_changed_action_source_reopens_the_project(string file, bool reopens)
    {
        var (shell, engine, _) = Create();
        await shell.OpenProjectAsync(Root);
        var first = shell.Workspace;
        engine.Calls.Clear();

        await first!.OnFilesChangedAsync([file]);

        Assert.Equal(reopens, engine.Calls.Contains($"openProject {Root}"));
        Assert.Equal(reopens, !ReferenceEquals(first, shell.Workspace));
    }

    [Theory]
    [InlineData("actions/a.ts", true)]
    [InlineData("tests/a.test.yaml", true)]
    [InlineData(Product.DataDir + "/cache/actions/x.mjs", false)]
    [InlineData("node_modules/pkg/index.js", false)]
    [InlineData("notes.txt", false)]
    public void The_watcher_reports_yaml_and_action_sources_outside_ignored_folders(string file, bool watched)
    {
        Assert.Equal(watched, ProjectFileKinds.IsWatched(file));
    }

    // Finding 3, app side

    [Fact]
    public async Task After_shutdown_starting_the_engine_does_nothing()
    {
        var store = new MemorySettingsStore();
        var started = 0;
        var service = new EngineService(
            store,
            new ImmediateDispatcher(),
            (_, _) =>
            {
                started++;
                throw new InvalidOperationException("no engine in this test");
            },
            (engine, node) => new Desktop.Engine.EngineSearchInput
            {
                AppBaseDirectory = "/app",
                GetEnvironmentVariable = _ => null,
                FileExists = _ => true,
                ConfiguredEngine = "/e/main.js",
                ConfiguredNode = "/n/node",
            });

        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, started);
        Assert.Equal(EngineState.Failed, service.State);

        await service.ShutdownAsync();
        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, started);
        Assert.Equal(EngineState.Stopped, service.State);
    }
}
