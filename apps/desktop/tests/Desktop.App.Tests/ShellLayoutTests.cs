// The window's layout (instruction D0004, tasks 8 to 10): panel sizes and open
// states remembered in the settings, the three views of a step file with the
// details panel, adding a step to a chosen group, and the regions on
// Avalonia's headless platform. With DESKTOP_SCREENSHOTS set to a folder, the
// view test also saves what it rendered.

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.Tests.StepFiles;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Steps;
using Desktop.App.Views;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class ShellLayoutTests
{
    private const string Root = "/work/shop-tests";
    private const string Test = "tests/a.test.yaml";

    private static ActionInfo Action(string name, string? shorthand, string schema) => new()
    {
        Name = name,
        Description = $"Does {name}.",
        Shorthand = shorthand,
        ParamsSchema = JsonDocument.Parse(schema).RootElement.Clone(),
        Source = new ActionSource { Kind = ActionSourceKind.Builtin },
    };

    private static ShellViewModel Shell(MemorySettingsStore settings, FakeProjectFiles files) =>
        new(
            new FakeEngineService
            {
                Tests = [Make.Test(Test, "A")],
                Actions =
                [
                    Action("back", null, """{"type":"object","properties":{}}"""),
                    Action("click", "target", """{"type":"object","properties":{"target":{}},"required":["target"]}"""),
                ],
            },
            settings,
            new FakeFolderPicker(Root),
            files,
            new ImmediateDispatcher(),
            p => p == Root,
            new FakeDialogs(),
            new ManualDelay());

    private static async Task<(WorkspaceViewModel Workspace, StepFileViewModel Tab)> OpenAsync(string text, string file = Test)
    {
        var files = new FakeProjectFiles();
        files.Files[file] = text;
        var shell = Shell(new MemorySettingsStore(), files);
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(file);
        return (shell.Workspace, (StepFileViewModel)shell.Workspace.SelectedTab!);
    }

    [Fact]
    public void The_layout_comes_from_the_settings_and_is_saved_when_it_changes()
    {
        var store = new MemorySettingsStore { Settings = new AppSettings { Layout = new LayoutSettings { ProjectWidth = 250, DetailsWidth = 10, IsBottomOpen = false } } };
        var shell = Shell(store, new FakeProjectFiles());

        Assert.Equal(250, shell.Layout.ProjectWidth);
        Assert.Equal(LayoutViewModel.MinimumSize, shell.Layout.DetailsWidth);
        Assert.False(shell.Layout.IsBottomOpen);

        shell.Layout.ShowRunsCommand.Execute(null);
        Assert.True(shell.Layout.IsRunsShown);
        Assert.True(store.Settings.Layout.IsBottomOpen);

        // With the details panel closed, a drag keeps its width for when it opens again.
        shell.Layout.ToggleDetailsCommand.Execute(null);
        shell.Layout.Resized(400, 0, 300);
        var saved = new LayoutSettings { ProjectWidth = 400, DetailsWidth = LayoutViewModel.MinimumSize, BottomHeight = 300, IsDetailsOpen = false, IsBottomOpen = true };
        Assert.Equal(saved, store.Settings.Layout);
        Assert.Equal(saved, Shell(store, new FakeProjectFiles()).Layout.Settings);
    }

    [Theory]
    [InlineData("""{ "recentProjects": [] }""")]
    [InlineData("""{ "recentProjects": [], "layout": null }""")]
    public void A_settings_file_without_a_layout_gets_the_defaults(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            Assert.Equal(new LayoutSettings(), new JsonSettingsStore(path).Load().Layout);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_explorer_says_when_it_shows_no_test()
    {
        var explorer = new TestExplorerViewModel();
        explorer.Load([]);
        Assert.StartsWith("No tests yet.", explorer.EmptyText, StringComparison.Ordinal);

        explorer.Load([Make.Test(Test, "A")]);
        Assert.Null(explorer.EmptyText);

        explorer.SearchText = "nothing like it";
        Assert.Equal("No test matches the search or the tag.", explorer.EmptyText);
    }

    [AvaloniaFact]
    public async Task A_test_opens_in_its_steps_view_and_the_details_show_the_selected_step()
    {
        var (workspace, tab) = await OpenAsync(StepOutlineTests.Sample);
        var raised = new List<string?>();
        workspace.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.Equal(StepFileMode.Steps, tab.View);
        Assert.Null(workspace.Details);
        Assert.Equal("Select a step to edit it here, or the test itself for its name, tags and skip reason.", workspace.DetailsHint);

        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps[3]);
        Assert.Contains(nameof(WorkspaceViewModel.Details), raised);
        Assert.Equal("click", Assert.IsType<StepFormViewModel>(workspace.Details).Action);

        tab.ShowTextCommand.Execute(null);
        Assert.Null(workspace.Details);
        Assert.StartsWith("The text view has no details.", workspace.DetailsHint, StringComparison.Ordinal);

        tab.ShowTargetsCommand.Execute(null);
        Assert.True(tab.IsTargetsShown);
        Assert.Equal("Select a target to edit its candidates here.", workspace.DetailsHint);

        workspace.SelectedTab = workspace.Actions;
        Assert.Null(workspace.Details);
        Assert.StartsWith("Open a test", workspace.DetailsHint, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_problem_opens_the_text_and_a_failed_step_opens_its_card()
    {
        var (workspace, tab) = await OpenAsync(StepOutlineTests.Sample);

        // Line 14 is inside the click step (lines 13 to 15).
        workspace.OpenStep(Test, 14);
        Assert.Equal(StepFileMode.Steps, tab.View);
        Assert.Equal("click", tab.Steps!.Selected!.Action);
        Assert.Equal(14, tab.RevealedLine);

        workspace.OpenFile(Test, 11);
        Assert.Equal(StepFileMode.Text, tab.View);
        Assert.Equal(11, tab.RevealedLine);

        // Line 11 is a comment between steps: no card, so the text.
        tab.ShowStepsCommand.Execute(null);
        workspace.OpenStep(Test, 11);
        Assert.Equal(StepFileMode.Text, tab.View);
        Assert.Equal("click", tab.Steps.Selected!.Action);
    }

    [AvaloniaFact]
    public async Task Views_a_file_does_not_have_are_not_shown()
    {
        var (_, targets) = await OpenAsync(TargetsTests.Shared, "targets/shop.targets.yaml");
        Assert.Equal(StepFileMode.Targets, targets.View);
        targets.ShowStepsCommand.Execute(null);
        Assert.Equal(StepFileMode.Targets, targets.View);
        targets.SidePanelIndex = 2;
        Assert.True(targets.IsTextShown);

        var (_, config) = await OpenAsync("version: 1\n", Product.ConfigFile);
        Assert.False(config.HasViews);
        Assert.Equal(StepFileMode.Text, config.View);
    }

    [AvaloniaFact]
    public async Task Each_group_adds_its_own_steps()
    {
        var (_, tab) = await OpenAsync(StepOutlineTests.Sample);
        var list = tab.Steps!;
        list.SelectCommand.Execute(list.Sections[1].Steps[0]);

        // Another group's "Add step": at the end of that group.
        list.StartAddingCommand.Execute("after");
        Assert.Equal("Add a step to after", list.PickerTitle);
        list.AddCommand.Execute(list.PickerItems.Single(a => a.Name == "back"));
        Assert.Equal(["api", "back"], list.Sections[2].Steps.Select(s => s.Action));
        Assert.Equal(5, list.Sections[1].Steps.Count);

        // The selected step's group: after the selected step.
        list.SelectCommand.Execute(list.Sections[1].Steps[0]);
        list.StartAddingCommand.Execute("steps");
        list.AddCommand.Execute(list.PickerItems.Single(a => a.Name == "back"));
        Assert.Equal(["goto", "back", "back", "fill", "click", "expect.text"], list.Sections[1].Steps.Select(s => s.Action));
        Assert.Null(list.AddingSection);
    }

    [AvaloniaFact]
    public async Task The_regions_collapse_and_a_dragged_splitter_is_remembered()
    {
        var store = new MemorySettingsStore();
        var files = new FakeProjectFiles();
        files.Files[Test] = StepOutlineTests.Sample;
        var shell = Shell(store, files);
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(Test);
        var tab = (StepFileViewModel)shell.Workspace.SelectedTab!;
        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps[3]);
        Render(window, "24-layout");

        Assert.True(Find<Border>(window, "ProjectRegion").IsEffectivelyVisible);
        Assert.True(Find<Border>(window, "DetailsRegion").IsEffectivelyVisible);
        Assert.Equal("click", Find<TextBlock>(window, "FormAction").Text);
        Assert.True(Find<ListBox>(window, "ProblemList").IsEffectivelyVisible);
        Assert.Equal("No problems. Every test file the engine checked is valid.", Find<TextBlock>(window, "NoProblems").Text);

        shell.Layout.ToggleDetailsCommand.Execute(null);
        shell.Layout.ToggleBottomCommand.Execute(null);
        Render(window, "25-layout-collapsed");
        Assert.False(Find<Border>(window, "DetailsRegion").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "ShowDetailsButton").IsEffectivelyVisible);
        Assert.False(Find<ListBox>(window, "ProblemList").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "ProblemsTab").IsEffectivelyVisible);
        Assert.Equal(0, Find<Grid>(window, "Regions").ColumnDefinitions[4].ActualWidth);

        shell.Layout.ShowRunsCommand.Execute(null);
        Render(window);
        Assert.True(Find<ItemsControl>(window, "HistoryList").IsEffectivelyVisible);
        Assert.True(Find<TextBlock>(window, "NoRuns").IsEffectivelyVisible);
        Assert.False(Find<ListBox>(window, "ProblemList").IsEffectivelyVisible);

        var regions = Find<Grid>(window, "Regions");
        regions.ColumnDefinitions[0].Width = new GridLength(222);
        Render(window);
        Find<GridSplitter>(window, "ProjectSplitter").RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent });
        Assert.Equal(222, shell.Layout.ProjectWidth);
        Assert.Equal(222, store.Settings.Layout.ProjectWidth);
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
