// The list of earlier runs in the workspace (instruction D0003, tasks 12 and
// 13): filled when the project opens and when a run starts or ends; opening a
// run shows it in the same run view as a live run; an unfinished record, a
// damaged one and a run deleted meanwhile are explained.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Runs;
using Desktop.App.Views;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Runs;

public sealed class RunHistoryTests : IDisposable
{
    private const string Old = "20261009-090000-000-aaaa";
    private const string New = "20261010-090000-000-bbbb";

    private readonly RunFolders _runs = new();

    public void Dispose() => _runs.Dispose();

    private async Task<(ShellViewModel Shell, FakeEngineService Engine)> OpenAsync()
    {
        var engine = new FakeEngineService { Info = FakeEngineService.ReadyInfo("chromium"), Tests = [Make.Test("tests/a.test.yaml", "A")] };
        var files = new FakeProjectFiles();
        files.Files["tests/a.test.yaml"] = "version: 1\n";
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(_runs.Root), files, new ImmediateDispatcher(), p => p == _runs.Root, new FakeDialogs(), new ManualDelay());
        await shell.InitializeAsync();
        await shell.OpenProjectAsync(_runs.Root);
        await shell.Workspace!.History.RefreshAsync();
        return (shell, engine);
    }

    [AvaloniaFact]
    public async Task Earlier_runs_are_listed_when_the_project_opens()
    {
        _runs.WritePassed(Old);
        var s = new RunScript(New);
        _runs.Write(New, s.Started(RunScript.Planned("t1", "tests/a.test.yaml", "A")), s.TestStarted("t1"), s.Step("t1", "s1", "Open"));

        var (shell, _) = await OpenAsync();
        var history = shell.Workspace!.History;

        Assert.Equal("2 runs", history.Summary);
        Assert.Equal([New, Old], history.Items.Select(i => i.RunId));
        Assert.Equal("Did not finish", history.Items[0].StatusText);
        Assert.Equal("Passed", history.Items[1].StatusText);
        Assert.Equal("1 passed, 0 failed, 0 cancelled, 0 skipped", history.Items[1].TotalsText);
        Assert.Equal("local", history.Items[1].Environment);
    }

    [AvaloniaFact]
    public async Task Opening_a_run_shows_it_like_a_live_run_and_twice_selects_the_same_tab()
    {
        _runs.WritePassed(Old);
        var (shell, _) = await OpenAsync();
        var workspace = shell.Workspace!;

        await workspace.History.OpenCommand.ExecuteAsync(workspace.History.Items[0]);

        var tab = Assert.IsType<RunTabViewModel>(workspace.SelectedTab);
        Assert.False(tab.IsLive);
        Assert.Equal(RunState.Passed, tab.Run.State);
        Assert.Equal(StepRunState.Passed, tab.Run.Test("t1")!.Step("s1")!.State);
        Assert.Equal(_runs.Folder(Old), tab.Run.ResultsDir);

        workspace.SelectedTab = workspace.Actions;
        await workspace.History.OpenCommand.ExecuteAsync(workspace.History.Items[0]);
        Assert.Same(tab, workspace.SelectedTab);
        Assert.Single(workspace.Tabs.OfType<RunTabViewModel>());
    }

    [AvaloniaFact]
    public async Task A_record_that_ends_early_or_has_damaged_lines_says_so()
    {
        var s = new RunScript(New);
        _runs.Write(New, s.Started(RunScript.Planned("t1", "tests/a.test.yaml", "A")), s.TestStarted("t1"), s.Step("t1", "s1", "Open"), "{ broken");
        var (shell, _) = await OpenAsync();
        var history = shell.Workspace!.History;
        Assert.Equal("1 line of its events could not be read.", history.Items[0].Problem);

        await history.OpenCommand.ExecuteAsync(history.Items[0]);

        var run = ((RunTabViewModel)shell.Workspace.SelectedTab!).Run;
        Assert.Equal(RunState.Unfinished, run.State);
        Assert.Equal(StepRunState.Interrupted, run.Test("t1")!.Step("s1")!.State);
        Assert.Contains(run.Messages, m => m.StartsWith("1 line of this run's events could not be read", StringComparison.Ordinal));
        Assert.Contains(run.Messages, m => m.StartsWith("The record of this run ends before the run finished", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task A_run_deleted_while_listed_is_explained_and_dropped()
    {
        var folder = _runs.WritePassed(Old);
        _runs.WritePassed(New);
        var (shell, _) = await OpenAsync();
        var history = shell.Workspace!.History;
        var item = history.Items.Single(i => i.RunId == Old);

        Directory.Delete(folder, recursive: true);
        await history.OpenCommand.ExecuteAsync(item);

        Assert.StartsWith("Run ", history.Notice, StringComparison.Ordinal);
        Assert.Contains("is gone: the engine deletes the oldest runs", history.Notice, StringComparison.Ordinal);
        Assert.Equal([New], history.Items.Select(i => i.RunId));
        Assert.IsNotType<RunTabViewModel>(shell.Workspace.SelectedTab);
    }

    [AvaloniaFact]
    public async Task The_list_follows_a_live_run_and_opening_it_selects_its_tab()
    {
        var (shell, engine) = await OpenAsync();
        var workspace = shell.Workspace!;
        Assert.Equal("No runs yet", workspace.History.Summary);
        engine.StartRun = _ =>
        {
            // The engine creates the run folder before it answers.
            var s0 = new RunScript(New);
            _runs.Write(New, s0.Started(RunScript.Planned("t1", "tests/a.test.yaml", "A")));
            return new StartRunResult { RunId = New, ResultsDir = _runs.Folder(New) };
        };

        await workspace.RunAllCommand.ExecuteAsync(null);
        await WaitForAsync(() => workspace.History.Items.Count == 1);
        Assert.Equal("Running…", workspace.History.Items[0].StatusText);
        var live = workspace.SelectedTab;
        workspace.SelectedTab = workspace.Actions;
        await workspace.History.OpenCommand.ExecuteAsync(workspace.History.Items[0]);
        Assert.Same(live, workspace.SelectedTab);

        _runs.WritePassed(New);
        var s = new RunScript(New);
        engine.Raise(s.Started(RunScript.Planned("t1", "tests/a.test.yaml", "A")));
        engine.Raise(s.Finished(RunOutcome.Passed, passed: 1));
        await WaitForAsync(() => workspace.History.Items is [{ StatusText: "Passed" }]);
    }

    [AvaloniaFact]
    public async Task The_history_panel_lists_the_runs()
    {
        _runs.WritePassed(Old);
        _runs.WritePassed(New);
        var (shell, _) = await OpenAsync();
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var summary = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "HistorySummary");
        Assert.Equal("2 runs", summary.Text);
        var list = window.GetLogicalDescendants().OfType<ItemsControl>().Single(c => c.Name == "HistoryList");
        Assert.Equal(2, list.ItemCount);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
