// The step file editor's view model, without a window (instruction D0002,
// tasks 10 to 14): changed state, save, revert, line endings, a failed save,
// validation while typing with time under the test's control, changes on disk,
// and the question before overwriting. The tests run on Avalonia's headless UI
// thread, because the editor's document may only be used from the thread that
// created it, as in the app.

using Avalonia.Headless.XUnit;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class StepFileEditorTests
{
    private const string File = "tests/a.test.yaml";

    private sealed class Setup
    {
        public FakeProjectFiles Files { get; } = new();

        public FakeEngineService Engine { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public ManualDelay Delay { get; } = new();

        public List<string> Log { get; } = [];

        public List<IReadOnlyList<Diagnostic>?> ContentProblems { get; } = [];

        public StepFileViewModel Open(string text)
        {
            Files.Files[File] = text;
            var tab = new StepFileViewModel(File, new StepFileServices("/root", Files, Engine, Dialogs, Delay, Log.Add));
            tab.ContentDiagnosticsChanged += (_, d) => ContentProblems.Add(d);
            tab.LoadFromDisk();
            return tab;
        }
    }

    private static void Type(StepFileViewModel tab, string text) => tab.Document.Insert(tab.Document.TextLength, text);

    [AvaloniaFact]
    public void A_change_marks_the_tab_and_undoing_it_clears_the_mark()
    {
        var setup = new Setup();
        var tab = setup.Open("version: 1\n");
        Assert.False(tab.IsDirty);
        Assert.Equal("a.test.yaml", tab.Title);
        Assert.False(tab.SaveCommand.CanExecute(null));

        Type(tab, "name: A\n");
        Assert.True(tab.IsDirty);
        Assert.Equal("● a.test.yaml", tab.Title);
        Assert.True(tab.SaveCommand.CanExecute(null));
        Assert.True(tab.UndoCommand.CanExecute(null));

        tab.UndoCommand.Execute(null);
        Assert.False(tab.IsDirty);
        Assert.True(tab.RedoCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Save_writes_the_text_keeping_the_files_line_endings()
    {
        var setup = new Setup();
        var tab = setup.Open("version: 1\r\nname: A\r\n");
        var saved = 0;
        tab.Saved += (_, _) => saved++;

        Type(tab, "steps:\n  - goto: /\n");
        Assert.True(await tab.SaveAsync());

        Assert.Equal((File, "version: 1\r\nname: A\r\nsteps:\r\n  - goto: /\r\n"), Assert.Single(setup.Files.Writes));
        Assert.False(tab.IsDirty);
        Assert.Equal(1, saved);
        Assert.Null(setup.ContentProblems[^1]);
    }

    [AvaloniaFact]
    public async Task A_file_with_unix_line_endings_keeps_them()
    {
        var setup = new Setup();
        var tab = setup.Open("a\nb\n");
        Type(tab, "c\r\n");
        await tab.SaveAsync();
        Assert.Equal("a\nb\nc\n", setup.Files.Files[File]);
    }

    [AvaloniaFact]
    public async Task A_failed_save_keeps_the_text_and_says_why()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        setup.Files.WriteFailures[File] = new IOException("disk full");
        Type(tab, "b\n");

        Assert.False(await tab.SaveAsync());

        Assert.True(tab.IsDirty);
        Assert.Equal("a\nb\n", tab.Document.Text);
        Assert.Contains("could not be saved: disk full", tab.SaveError, StringComparison.Ordinal);
        Assert.Contains(setup.Log, l => l.Contains("disk full", StringComparison.Ordinal));
        Assert.Equal("a\n", setup.Files.Files[File]);
    }

    [AvaloniaFact]
    public void Revert_puts_back_the_saved_text_and_can_be_undone()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        Type(tab, "b\n");

        tab.RevertCommand.Execute(null);
        Assert.Equal("a\n", tab.Document.Text);
        Assert.False(tab.IsDirty);
        Assert.Null(setup.ContentProblems[^1]);

        tab.UndoCommand.Execute(null);
        Assert.Equal("a\nb\n", tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Validates_the_text_only_after_the_delay_and_only_the_newest()
    {
        var setup = new Setup();
        var tab = setup.Open("version: 1\n");
        setup.Engine.ValidateContent = (_, text) => [Make.Error(File, text.Split('\n').Length - 1, "Line" + text.Length)];

        Type(tab, "x");
        Type(tab, "y");
        Assert.DoesNotContain(setup.Engine.Calls, c => c.StartsWith("validateContent", StringComparison.Ordinal));
        Assert.Equal(1, setup.Delay.Pending);

        setup.Delay.Elapse();
        await Task.Yield();

        Assert.Equal(["validateContent " + File], setup.Engine.Calls);
        Assert.Equal("Line13", Assert.Single(setup.ContentProblems[^1]!).Code);
    }

    [AvaloniaFact]
    public async Task An_answer_for_older_text_is_dropped()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Engine.ValidateContent = (_, text) => [Make.Error(File, 1, text.Trim().Replace('\n', '-'))];
        setup.Engine.BeforeAnswer = _ => slow.Task;

        Type(tab, "b\n");
        setup.Delay.Elapse();
        await Task.Yield();
        Type(tab, "c\n");
        setup.Engine.BeforeAnswer = null;
        setup.Delay.Elapse();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        slow.SetResult();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("a-b-c", Assert.Single(setup.ContentProblems.Where(p => p is not null).Last()!).Code);
        Assert.DoesNotContain(setup.ContentProblems, p => p is not null && p[0].Code == "a-b");
    }

    [AvaloniaFact]
    public void A_change_on_disk_reloads_a_tab_without_unsaved_changes()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        setup.Files.Files[File] = "a\nfrom git\n";

        tab.OnDiskChanged();

        Assert.Equal("a\nfrom git\n", tab.Document.Text);
        Assert.False(tab.IsDirty);
        Assert.False(tab.HasExternalChange);
    }

    [AvaloniaFact]
    public async Task The_apps_own_save_is_not_a_change_from_outside()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        Type(tab, "b\n");
        await tab.SaveAsync();
        Type(tab, "c\n");

        tab.OnDiskChanged();

        Assert.False(tab.HasExternalChange);
        Assert.Equal("a\nb\nc\n", tab.Document.Text);
    }

    [AvaloniaFact]
    public void A_change_on_disk_during_editing_offers_reload_or_keep()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        Type(tab, "mine\n");
        setup.Files.Files[File] = "theirs\n";

        tab.OnDiskChanged();
        Assert.True(tab.HasExternalChange);
        Assert.Equal("a\nmine\n", tab.Document.Text);

        tab.ReloadFromDiskCommand.Execute(null);
        Assert.Equal("theirs\n", tab.Document.Text);
        Assert.False(tab.HasExternalChange);
        Assert.False(tab.IsDirty);
    }

    [AvaloniaFact]
    public async Task Keeping_my_version_lets_a_later_save_overwrite_without_asking()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        Type(tab, "mine\n");
        setup.Files.Files[File] = "theirs\n";
        tab.OnDiskChanged();

        tab.KeepMineCommand.Execute(null);
        Assert.False(tab.HasExternalChange);
        Assert.True(await tab.SaveAsync());

        Assert.Empty(setup.Dialogs.Asked);
        Assert.Equal("a\nmine\n", setup.Files.Files[File]);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Saving_over_a_file_that_changed_on_disk_asks_first(bool overwrite)
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        Type(tab, "mine\n");
        setup.Files.Files[File] = "theirs\n";
        setup.Dialogs.OverwriteAnswer = overwrite;

        Assert.Equal(overwrite, await tab.SaveAsync());

        Assert.Equal(["overwrite " + File], setup.Dialogs.Asked);
        Assert.Equal(overwrite ? "a\nmine\n" : "theirs\n", setup.Files.Files[File]);
    }

    [AvaloniaFact]
    public async Task A_file_deleted_on_disk_is_said_to_be_deleted_and_saving_creates_it_again()
    {
        var setup = new Setup();
        var tab = setup.Open("a\n");
        setup.Files.Files.Remove(File);

        tab.OnDiskChanged();
        Assert.True(tab.IsDeletedOnDisk);
        Assert.True(tab.SaveCommand.CanExecute(null));

        Assert.True(await tab.SaveAsync());
        Assert.Equal("a\n", setup.Files.Files[File]);
        Assert.False(tab.IsDeletedOnDisk);
        Assert.Empty(setup.Dialogs.Asked);
    }

    [AvaloniaFact]
    public void Marks_lines_with_problems_including_ranges_and_reveals_clamped_lines()
    {
        var setup = new Setup();
        var tab = setup.Open("version: 1\nname: A\nsteps:\n  - clik: x\n");
        tab.ApplyDiagnostics([
            Make.Error(File, 4),
            Make.Warning(File, 2) with { EndLine = 3 },
            Make.Error("tests/other.test.yaml", 1),
        ]);

        Assert.Equal(new Dictionary<int, LineMark> { [2] = LineMark.Warning, [3] = LineMark.Warning, [4] = LineMark.Error }, tab.LineMarks);
        Assert.Equal("UnknownAction: Unknown action.", tab.MessagesAt(4));
        Assert.Null(tab.MessagesAt(1));
        Assert.Equal("1 error, 1 warning", tab.ProblemSummary);

        tab.Reveal(99);
        Assert.Equal(tab.Document.LineCount, tab.RevealedLine);
    }

    [AvaloniaFact]
    public void A_file_that_cannot_be_read_says_so()
    {
        var setup = new Setup();
        var tab = new StepFileViewModel("nope.test.yaml", new StepFileServices("/root", setup.Files, setup.Engine, setup.Dialogs, setup.Delay, setup.Log.Add));
        tab.LoadFromDisk();
        Assert.Contains("does not exist", tab.LoadError, StringComparison.Ordinal);
    }
}
