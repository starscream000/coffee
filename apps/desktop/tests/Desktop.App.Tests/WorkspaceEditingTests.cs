// The editor inside the workspace (instruction D0002, tasks 11 to 13): the
// problems panel follows the text being edited, saving validates from disk,
// and a change on disk reaches the tab through the watcher.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;

namespace Desktop.App.Tests;

public sealed class WorkspaceEditingTests
{
    private const string Root = "/work/shop-tests";
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
            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), Files, new ImmediateDispatcher(), p => p == Root, Dialogs, Delay);
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
    public async Task A_change_on_disk_reaches_the_tab_through_the_watcher()
    {
        var setup = new Setup();
        var tab = await setup.OpenAsync();
        Type(tab, "mine\n");
        setup.Files.Files[FileA] = "theirs\n";

        await setup.Workspace.OnFilesChangedAsync([FileA]);

        Assert.True(tab.HasExternalChange);
    }
}
