// The editor inside the workspace and the shell (instruction D0002, tasks 11
// to 14): the problems panel follows the text being edited, saving validates
// from disk, Save all, and the questions before closing the project, opening
// another one or closing the window. Unsaved text survives a reopen.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Protocol;

namespace Desktop.App.Tests;

public sealed class WorkspaceEditingTests
{
    private const string Root = "/work/shop-tests";
    private const string Other = "/work/other";
    private const string FileA = "tests/a.test.yaml";
    private const string FileB = "tests/b.test.yaml";

    private sealed class Setup
    {
        public Setup()
        {
            Engine.Tests = [Make.Test(FileA, "A"), Make.Test(FileB, "B")];
            Engine.Validate = _ => [Make.Error(FileA, 1, "OnDisk")];
            Files.Files[FileA] = "version: 1\n";
            Files.Files[FileB] = "version: 1\n";
            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Other), Files, new ImmediateDispatcher(), p => p is Root or Other, Dialogs, Delay);
        }

        public FakeEngineService Engine { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public ManualDelay Delay { get; } = new();

        public ShellViewModel Shell { get; }

        public WorkspaceViewModel Workspace => Shell.Workspace!;

        public async Task<StepFileViewModel> OpenAsync(string file = FileA)
        {
            if (Shell.Workspace is null)
            {
                await Shell.OpenProjectAsync(Root);
            }

            Workspace.OpenFile(file);
            return (StepFileViewModel)Workspace.SelectedTab!;
        }
    }

    private static void Type(StepFileViewModel tab, string text) => tab.Document.Insert(tab.Document.TextLength, text);

    [AvaloniaFact]
    public async Task While_a_tab_has_unsaved_changes_its_problems_are_those_of_the_text()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Assert.Equal("OnDisk", Assert.Single(setup.Workspace.Problems.All).Code);
        setup.Engine.ValidateContent = (_, _) => [Make.Error(FileA, 2, "Typed"), Make.Error("flows/other.flow.yaml", 1, "Elsewhere")];

        Type(tab, "nme: A\n");
        setup.Delay.Elapse();
        await Task.Yield();

        Assert.Equal("Typed", Assert.Single(setup.Workspace.Problems.All).Code);
        Assert.Equal(LineMark.Error, tab.LineMarks[2]);
        Assert.Equal(1, setup.Workspace.Explorer.Roots[0].ErrorCount);

        tab.RevertCommand.Execute(null);
        Assert.Equal("OnDisk", Assert.Single(setup.Workspace.Problems.All).Code);
    }

    [AvaloniaFact]
    public async Task Saving_validates_the_file_from_disk()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "name: A\n");
        setup.Engine.Calls.Clear();
        setup.Engine.Validate = _ => [];

        await tab.SaveCommand.ExecuteAsync(null);
        await Task.Yield();

        Assert.Contains($"validate {FileA},{FileB}", setup.Engine.Calls);
        Assert.Empty(setup.Workspace.Problems.All);
    }

    [AvaloniaFact]
    public async Task Save_all_saves_every_changed_tab()
    {
        var setup = new Setup();
        var a = await setup.OpenAsync(FileA);
        var b = await setup.OpenAsync(FileB);
        Assert.False(setup.Workspace.SaveAllCommand.CanExecute(null));
        Type(a, "a\n");
        Type(b, "b\n");
        Assert.True(setup.Workspace.HasUnsavedChanges);

        await setup.Workspace.SaveAllCommand.ExecuteAsync(null);

        Assert.Equal([FileA, FileB], setup.Files.Writes.Select(w => w.File));
        Assert.False(setup.Workspace.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task A_change_on_disk_reaches_the_tab_through_the_watcher()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "mine\n");
        setup.Files.Files[FileA] = "theirs\n";

        await setup.Workspace.OnFilesChangedAsync([FileA]);

        Assert.True(tab.HasExternalChange);
    }

    [AvaloniaTheory]
    [InlineData(UnsavedChangesChoice.Save, false)]
    [InlineData(UnsavedChangesChoice.Discard, false)]
    [InlineData(UnsavedChangesChoice.Cancel, true)]
    public async Task Closing_the_project_with_unsaved_changes_asks(UnsavedChangesChoice choice, bool staysOpen)
    {
        var setup = new Setup();
        Type(await setup.OpenAsync(), "x\n");
        setup.Dialogs.UnsavedAnswer = choice;

        await setup.Shell.CloseProjectCommand.ExecuteAsync(null);

        Assert.Equal(["unsaved " + FileA], setup.Dialogs.Asked);
        Assert.Equal(staysOpen, setup.Shell.IsProjectOpen);
        Assert.Equal(choice == UnsavedChangesChoice.Save, setup.Files.Writes.Count == 1);
    }

    [AvaloniaFact]
    public async Task Closing_the_project_while_a_tab_closes_its_tab_asks_once_for_all()
    {
        var setup = new Setup();
        Type(await setup.OpenAsync(FileA), "x\n");
        Type(await setup.OpenAsync(FileB), "y\n");
        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Discard;

        Assert.True(await setup.Shell.ConfirmCloseWindowAsync());

        Assert.Equal([$"unsaved {FileA},{FileB}"], setup.Dialogs.Asked);
    }

    [AvaloniaFact]
    public async Task Closing_the_window_can_be_cancelled_and_needs_no_question_without_changes()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Assert.True(await setup.Shell.ConfirmCloseWindowAsync());
        Assert.Empty(setup.Dialogs.Asked);

        Type(tab, "x\n");
        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Cancel;
        Assert.False(await setup.Shell.ConfirmCloseWindowAsync());
    }

    [AvaloniaFact]
    public async Task Opening_another_project_asks_and_cancel_keeps_this_one()
    {
        var setup = new Setup();
        Type(await setup.OpenAsync(), "x\n");
        var before = setup.Workspace;
        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Cancel;

        await setup.Shell.OpenFolderCommand.ExecuteAsync(null);

        Assert.Same(before, setup.Shell.Workspace);
        Assert.DoesNotContain($"openProject {Other}", setup.Engine.Calls);
    }

    [AvaloniaFact]
    public async Task Closing_a_tab_with_unsaved_changes_asks_and_cancel_keeps_it()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "x\n");
        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Cancel;

        tab.CloseCommand.Execute(null);
        await Task.Yield();
        Assert.Contains(tab, setup.Workspace.Tabs);

        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Discard;
        tab.CloseCommand.Execute(null);
        await Task.Yield();
        Assert.DoesNotContain(tab, setup.Workspace.Tabs);
    }

    [AvaloniaFact]
    public async Task Reopening_after_a_config_change_keeps_tabs_and_unsaved_text()
    {
        var setup = new Setup();
        await setup.OpenAsync(FileB);
        var a = await setup.OpenAsync(FileA);
        Type(a, "unsaved\n");
        var before = setup.Workspace;

        await before.OnFilesChangedAsync([Product.ConfigFile]);
        await Task.Yield();

        Assert.NotSame(before, setup.Workspace);
        var tabs = setup.Workspace.Tabs.OfType<StepFileViewModel>().ToList();
        Assert.Equal([FileB, FileA], tabs.Select(t => t.File));
        Assert.Equal("version: 1\nunsaved\n", tabs[1].Document.Text);
        Assert.True(tabs[1].IsDirty);
        Assert.Same(tabs[1], setup.Workspace.SelectedTab);
        Assert.Empty(setup.Dialogs.Asked);
    }
}
