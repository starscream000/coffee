// Creating, renaming and deleting files from the app (instruction D0003,
// task 17): the questions asked, what is written, the tabs, and the test list
// and problems read again.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;

namespace Desktop.App.Tests.StepFiles;

public sealed class FileOperationsTests
{
    private const string Root = "/work/shop-tests";
    private const string Existing = "tests/checkout/a.test.yaml";

    private sealed class Setup
    {
        public Setup()
        {
            Files.Files[Existing] = "version: 1\nname: A\nsteps:\n  - goto: /\n";
            Engine.Tests = [Make.Test(Existing, "A")];
            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), Files, new ImmediateDispatcher(), p => p == Root, Dialogs, new ManualDelay());
        }

        public FakeEngineService Engine { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public ShellViewModel Shell { get; }

        public WorkspaceViewModel Workspace => Shell.Workspace!;

        public async Task OpenAsync() => await Shell.OpenProjectAsync(Root);
    }

    [AvaloniaFact]
    public async Task A_new_test_goes_in_the_selected_folder_is_written_and_opened()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.Explorer.SelectedNode = setup.Workspace.Explorer.Roots[0].Children[0].Children[0];
        setup.Dialogs.NewFileAnswer = ("tests/checkout", "guest pays");
        setup.Engine.Tests = [Make.Test(Existing, "A"), Make.Test("tests/checkout/guest pays.test.yaml", "Guest pays")];

        await setup.Workspace.NewTestCommand.ExecuteAsync(null);

        Assert.Equal("new test in tests/checkout", Assert.Single(setup.Dialogs.Asked));
        Assert.Equal("version: 1\nname: Guest pays\nsteps:\n  - goto: /\n", setup.Files.Files["tests/checkout/guest pays.test.yaml"]);
        var tab = Assert.IsType<StepFileViewModel>(setup.Workspace.SelectedTab);
        Assert.Equal("tests/checkout/guest pays.test.yaml", tab.File);
        Assert.Contains("tests/checkout/guest pays.test.yaml", setup.Workspace.Explorer.Files);
    }

    [AvaloniaFact]
    public async Task A_flow_and_a_targets_file_go_to_their_folders_by_default()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Dialogs.NewFileAnswer = ("flows", "log-in");

        await setup.Workspace.NewFlowCommand.ExecuteAsync(null);
        setup.Dialogs.NewFileAnswer = ("targets", "shop");
        await setup.Workspace.NewTargetsCommand.ExecuteAsync(null);

        Assert.Equal(["new flow in flows", "new targets file in targets"], setup.Dialogs.Asked);
        Assert.True(setup.Files.Files.ContainsKey("flows/log-in.flow.yaml"));
        Assert.Equal("version: 1\ntargets: {}\n", setup.Files.Files["targets/shop.targets.yaml"]);
    }

    [AvaloniaFact]
    public async Task An_existing_file_is_never_overwritten_and_a_bad_name_says_why()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        var before = setup.Files.Files[Existing];
        setup.Dialogs.NewFileAnswer = ("tests/checkout", "a");

        await setup.Workspace.NewTestCommand.ExecuteAsync(null);
        Assert.Equal("tests/checkout/a.test.yaml already exists. Choose another name.", setup.Workspace.FileNotice);
        Assert.Equal(before, setup.Files.Files[Existing]);

        setup.Dialogs.NewFileAnswer = ("../elsewhere", "b");
        await setup.Workspace.NewTestCommand.ExecuteAsync(null);
        Assert.StartsWith("The test was not created: \"..\" cannot be part", setup.Workspace.FileNotice, StringComparison.Ordinal);

        setup.Dialogs.NewFileAnswer = null;
        await setup.Workspace.NewTestCommand.ExecuteAsync(null);
        Assert.Null(setup.Workspace.FileNotice);
        Assert.Single(setup.Files.Files);
    }

    [AvaloniaFact]
    public async Task Renaming_moves_the_file_and_reopens_it_under_its_new_name()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.OpenFile(Existing);
        var tab = (StepFileViewModel)setup.Workspace.SelectedTab!;
        setup.Dialogs.RenameAnswer = "b";

        await tab.RenameCommand.ExecuteAsync(null);

        Assert.False(setup.Files.Files.ContainsKey(Existing));
        Assert.True(setup.Files.Files.ContainsKey("tests/checkout/b.test.yaml"));
        Assert.DoesNotContain(tab, setup.Workspace.Tabs);
        Assert.Equal("tests/checkout/b.test.yaml", ((StepFileViewModel)setup.Workspace.SelectedTab!).File);
        Assert.Contains("listTests", setup.Engine.Calls);
    }

    [AvaloniaFact]
    public async Task A_tab_with_unsaved_changes_is_not_renamed()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.OpenFile(Existing);
        var tab = (StepFileViewModel)setup.Workspace.SelectedTab!;
        tab.Document.Insert(0, "# draft\n");

        await tab.RenameCommand.ExecuteAsync(null);

        Assert.Empty(setup.Dialogs.Asked);
        Assert.Equal($"Save or revert {Existing} before renaming it.", setup.Workspace.FileNotice);
    }

    [AvaloniaFact]
    public async Task Deleting_asks_first_then_deletes_and_closes_the_tab()
    {
        var setup = new Setup();
        await setup.OpenAsync();
        setup.Workspace.OpenFile(Existing);
        var tab = (StepFileViewModel)setup.Workspace.SelectedTab!;

        await tab.DeleteCommand.ExecuteAsync(null);
        Assert.Equal($"delete {Existing}", Assert.Single(setup.Dialogs.Asked));
        Assert.True(setup.Files.Files.ContainsKey(Existing));

        setup.Dialogs.DeleteAnswer = true;
        tab.Document.Insert(0, "# unsaved, but the file is going\n");
        await tab.DeleteCommand.ExecuteAsync(null);

        Assert.False(setup.Files.Files.ContainsKey(Existing));
        Assert.DoesNotContain(tab, setup.Workspace.Tabs);
        Assert.Equal(2, setup.Dialogs.Asked.Count);
    }
}
