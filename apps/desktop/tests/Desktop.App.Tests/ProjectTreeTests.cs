// The project panel's groups (instruction D0004, task 18): tests, flows,
// shared targets files and the config file; selecting a file in any group
// opens it; new, rename and delete from each group; the config file keeps its
// name. With DESKTOP_SCREENSHOTS set to a folder, the view test also saves
// what it rendered.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.Views;
using Desktop.Protocol;

namespace Desktop.App.Tests;

public sealed class ProjectTreeTests
{
    private const string Root = "/work/shop-tests";
    private const string Test = "tests/checkout/a.test.yaml";
    private const string Flow = "flows/log-in.flow.yaml";
    private const string Targets = "targets/shop.targets.yaml";

    private sealed class Setup
    {
        public Setup()
        {
            Files.Files[Test] = "version: 1\nname: A\nsteps:\n  - goto: /\n";
            Files.Files[Flow] = "version: 1\nname: Log in\nsteps:\n  - goto: /login\n";
            Files.Files["flows/search.flow.yaml"] = "version: 1\nname: Search\nsteps:\n  - goto: /\n";
            Files.Files[Targets] = "version: 1\ntargets: {}\n";
            Files.Files[Product.ConfigFile] = "version: 1\n";
            Engine.Tests = [Make.Test(Test, "A")];
            Shell = new ShellViewModel(Engine, new MemorySettingsStore(), new FakeFolderPicker(Root), Files, new ImmediateDispatcher(), p => p == Root, Dialogs, new ManualDelay());
        }

        public FakeEngineService Engine { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public ShellViewModel Shell { get; }

        public WorkspaceViewModel Workspace => Shell.Workspace!;

        public ProjectFilesViewModel Groups => Workspace.ProjectFiles;

        public async Task OpenAsync() => await Shell.OpenProjectAsync(Root);
    }

    [AvaloniaFact]
    public async Task The_panel_lists_flows_targets_files_and_the_config()
    {
        var setup = new Setup();
        await setup.OpenAsync();

        Assert.Equal([Flow, "flows/search.flow.yaml"], setup.Groups.Flows.Select(f => f.Path));
        Assert.Equal("log-in.flow.yaml", setup.Groups.Flows[0].Name);
        Assert.Equal("flows", setup.Groups.Flows[0].Folder);
        Assert.Equal("2 flows", setup.Groups.FlowsSummary);
        Assert.Equal([Targets], setup.Groups.TargetsFiles.Select(f => f.Path));
        Assert.Equal("1 file", setup.Groups.TargetsSummary);
        Assert.False(setup.Groups.HasNoFlows);
        Assert.Equal(Product.ConfigFile, setup.Groups.Config.Path);
    }

    [AvaloniaFact]
    public async Task Selecting_a_file_in_any_group_opens_it_and_is_the_one_selection()
    {
        var setup = new Setup();
        await setup.OpenAsync();

        setup.Groups.SelectedFlow = setup.Groups.Flows[0];
        Assert.Equal(Flow, Assert.IsType<StepFileViewModel>(setup.Workspace.SelectedTab).File);
        Assert.Equal(Flow, setup.Workspace.SelectedFile);
        Assert.Equal("log-in.flow.yaml", setup.Workspace.SelectedFileName);

        setup.Groups.SelectedTargetsFile = setup.Groups.TargetsFiles[0];
        Assert.Null(setup.Groups.SelectedFlow);
        Assert.Equal(Targets, setup.Workspace.SelectedFile);

        setup.Workspace.Explorer.SelectedNode = setup.Workspace.Explorer.Roots[0].Children[0].Children[0];
        Assert.Null(setup.Groups.SelectedTargetsFile);
        Assert.Equal(Test, setup.Workspace.SelectedFile);
        Assert.True(setup.Workspace.RenameSelectedFileCommand.CanExecute(null));

        // The config file opens as text, and keeps its name.
        setup.Groups.OpenConfigCommand.Execute(null);
        Assert.Equal(Product.ConfigFile, Assert.IsType<StepFileViewModel>(setup.Workspace.SelectedTab).File);
        Assert.Null(setup.Workspace.Explorer.SelectedNode);
        Assert.False(setup.Workspace.CanChangeSelectedFile);
        Assert.False(setup.Workspace.RenameSelectedFileCommand.CanExecute(null));
        Assert.False(setup.Workspace.DeleteSelectedFileCommand.CanExecute(null));

        // A folder is not a file to rename.
        setup.Workspace.Explorer.SelectedNode = setup.Workspace.Explorer.Roots[0];
        Assert.Null(setup.Workspace.SelectedFile);
    }

    [AvaloniaFact]
    public async Task New_rename_and_delete_work_from_each_group()
    {
        var setup = new Setup();
        await setup.OpenAsync();

        // New, from the flows group.
        setup.Dialogs.NewFileAnswer = ("flows", "pay");
        await setup.Workspace.NewFlowCommand.ExecuteAsync(null);
        Assert.Contains("flows/pay.flow.yaml", setup.Groups.Flows.Select(f => f.Path));

        // Rename the selected flow.
        setup.Groups.SelectedFlow = setup.Groups.Flows.Single(f => f.Path == Flow);
        setup.Dialogs.RenameAnswer = "sign-in";
        await setup.Workspace.RenameSelectedFileCommand.ExecuteAsync(null);
        Assert.Contains("flows/sign-in.flow.yaml", setup.Groups.Flows.Select(f => f.Path));
        Assert.DoesNotContain(Flow, setup.Groups.Flows.Select(f => f.Path));
        Assert.False(setup.Files.Files.ContainsKey(Flow));

        // Delete the selected targets file, after asking.
        setup.Groups.SelectedTargetsFile = setup.Groups.TargetsFiles[0];
        setup.Dialogs.DeleteAnswer = true;
        await setup.Workspace.DeleteSelectedFileCommand.ExecuteAsync(null);
        Assert.Empty(setup.Groups.TargetsFiles);
        Assert.True(setup.Groups.HasNoTargetsFiles);
        Assert.False(setup.Files.Files.ContainsKey(Targets));
        Assert.Null(setup.Workspace.SelectedFile);
        Assert.Contains(setup.Dialogs.Asked, q => q.StartsWith("delete", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task The_groups_render_in_the_project_panel()
    {
        var setup = new Setup();
        var window = new MainWindow { DataContext = setup.Shell, Width = 1280, Height = 800 };
        window.Show();
        await setup.OpenAsync();
        setup.Groups.SelectedFlow = setup.Groups.Flows[0];
        Render(window, "31-project-panel");

        Assert.Equal(2, Find<ListBox>(window, "FlowList").ItemCount);
        Assert.Equal(1, Find<ListBox>(window, "TargetsFileList").ItemCount);
        Assert.True(Find<Button>(window, "ConfigButton").IsEffectivelyVisible);
        Assert.True(Find<Border>(window, "SelectionBar").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "DeleteSelectedButton").IsEffectivelyEnabled);
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
