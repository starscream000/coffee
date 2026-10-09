// Findings 1 to 4 of review D0002: text typed while the project is opened
// again is kept; reopening keeps each tab as it is (the same tab object, with
// undo history, caret and "changed on disk" state); after "keep my version"
// the tab stays unsaved while its text differs from the disk; and if asking
// about unsaved changes fails, the window stays open.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Protocol;

namespace Desktop.App.Tests;

public sealed class ReviewD0002FixesTests
{
    private const string Root = "/work/shop-tests";
    private const string File = "tests/a.test.yaml";

    private sealed class Setup
    {
        public Setup()
        {
            Engine.Tests = [Make.Test(File, "A")];
            Files.Files[File] = "version: 1\n";
            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), Files, new ImmediateDispatcher(), p => p == Root, Dialogs, Delay);
        }

        public FakeEngineService Engine { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public ManualDelay Delay { get; } = new();

        public ShellViewModel Shell { get; }

        public async Task<StepFileViewModel> OpenAsync()
        {
            await Shell.OpenProjectAsync(Root);
            Shell.Workspace!.OpenFile(File);
            return (StepFileViewModel)Shell.Workspace.SelectedTab!;
        }
    }

    private static void Type(StepFileViewModel tab, string text) => tab.Document.Insert(tab.Document.TextLength, text);

    [AvaloniaFact]
    public async Task Text_typed_while_the_project_is_opened_again_is_kept()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "before\n");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Engine.BeforeAnswer = method => method == "openProject" ? gate.Task : Task.CompletedTask;

        var before = setup.Shell.Workspace!;
        await before.OnFilesChangedAsync([Product.ConfigFile]);
        await Task.Yield();
        Type(tab, "during\n");
        setup.Engine.BeforeAnswer = null;
        gate.SetResult();
        await WaitForAsync(() => !ReferenceEquals(before, setup.Shell.Workspace));

        var moved = Assert.Single(setup.Shell.Workspace!.Tabs.OfType<StepFileViewModel>());
        Assert.Equal("version: 1\nbefore\nduring\n", moved.Document.Text);
    }

    [AvaloniaFact]
    public async Task Reopening_keeps_the_same_tab_with_its_undo_history_caret_and_changed_on_disk_state()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "mine\n");
        tab.CaretOffset = 4;
        tab.VerticalScroll = 120;
        setup.Files.Files[File] = "theirs\n";
        tab.OnDiskChanged();
        Assert.True(tab.HasExternalChange);
        var before = setup.Shell.Workspace;

        await before!.OnFilesChangedAsync([Product.ConfigFile]);
        await WaitForAsync(() => !ReferenceEquals(before, setup.Shell.Workspace));

        var workspace = setup.Shell.Workspace!;
        Assert.Same(tab, Assert.Single(workspace.Tabs.OfType<StepFileViewModel>()));
        Assert.Same(tab, workspace.SelectedTab);
        Assert.Empty(before.Tabs.OfType<StepFileViewModel>());
        Assert.True(tab.UndoCommand.CanExecute(null));
        Assert.Equal((4, 120.0), (tab.CaretOffset, tab.VerticalScroll));
        Assert.True(tab.HasExternalChange);
        Assert.True(workspace.HasUnsavedChanges);

        // The baseline is still the text first read, so saving over the other version asks.
        setup.Dialogs.OverwriteAnswer = false;
        Assert.False(await tab.SaveAsync());
        Assert.Equal(["overwrite " + File], setup.Dialogs.Asked);

        // The moved tab now reports to the new workspace, not the old one.
        tab.CloseCommand.Execute(null);
        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Discard;
        tab.CloseCommand.Execute(null);
        await Task.Yield();
        Assert.Empty(workspace.Tabs.OfType<StepFileViewModel>());
    }

    [AvaloniaFact]
    public async Task After_keep_my_version_the_tab_stays_unsaved_while_its_text_differs_from_the_disk()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "mine\n");
        setup.Files.Files[File] = "theirs\n";
        tab.OnDiskChanged();
        tab.KeepMineCommand.Execute(null);

        // Undo back to the text first read: it still differs from the disk.
        tab.UndoCommand.Execute(null);
        Assert.Equal("version: 1\n", tab.Document.Text);
        Assert.True(tab.IsDirty);
        Assert.True(tab.SaveCommand.CanExecute(null));

        // Text equal to the disk is not unsaved.
        tab.Document.Text = "theirs\n";
        Assert.False(tab.IsDirty);
    }

    [AvaloniaFact]
    public async Task If_asking_about_unsaved_changes_fails_the_window_stays_open()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "x\n");
        var shell = new ShellViewModel(setup.Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), setup.Files, new ImmediateDispatcher(), p => p == Root, new ThrowingDialogs(), setup.Delay);
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(File);
        Type((StepFileViewModel)shell.Workspace.SelectedTab!, "y\n");

        Assert.False(await shell.ConfirmCloseWindowAsync());
        Assert.Contains("window stays open", shell.Notice, StringComparison.Ordinal);
        Assert.Contains(shell.Engine.Log, l => l.Text.Contains("dialog broke", StringComparison.Ordinal));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class ThrowingDialogs : IDialogService
    {
        public Task<UnsavedChangesChoice> AskUnsavedChangesAsync(IReadOnlyList<string> files) => throw new InvalidOperationException("dialog broke");

        public Task<bool> AskOverwriteAsync(string file) => throw new InvalidOperationException("dialog broke");

        public Task<UnsavedChangesChoice> AskSaveBeforeRunAsync(IReadOnlyList<string> files) => throw new InvalidOperationException("dialog broke");
    }
}
