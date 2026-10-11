// The window's layout (instruction D0004, tasks 8 and 10): panel sizes and
// open states remembered in the settings, and the regions on Avalonia's
// headless platform. With DESKTOP_SCREENSHOTS set to a folder, the view test
// also saves what it rendered.

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
using Desktop.App.Views;
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

    [Fact]
    public void The_layout_comes_from_the_settings_and_is_saved_when_it_changes()
    {
        var store = new MemorySettingsStore { Settings = new AppSettings { Layout = new LayoutSettings { ProjectWidth = 10, IsBottomOpen = false } } };
        var shell = Shell(store, new FakeProjectFiles());

        Assert.Equal(LayoutViewModel.MinimumSize, shell.Layout.ProjectWidth);
        Assert.False(shell.Layout.IsBottomOpen);

        shell.Layout.ShowRunsCommand.Execute(null);
        Assert.True(shell.Layout.IsRunsShown);
        Assert.True(store.Settings.Layout.IsBottomOpen);

        shell.Layout.Resized(400, 300);
        var saved = new LayoutSettings { ProjectWidth = 400, BottomHeight = 300, IsBottomOpen = true };
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
        Render(window, "24-layout");

        Assert.True(Find<Border>(window, "ProjectRegion").IsEffectivelyVisible);
        Assert.True(Find<Border>(window, "TabsRegion").IsEffectivelyVisible);
        Assert.True(Find<ListBox>(window, "ProblemList").IsEffectivelyVisible);
        Assert.Equal("No problems. Every test file the engine checked is valid.", Find<TextBlock>(window, "NoProblems").Text);

        shell.Layout.ToggleBottomCommand.Execute(null);
        Render(window, "25-layout-collapsed");
        Assert.False(Find<ListBox>(window, "ProblemList").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "ProblemsTab").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "ShowBottomButton").IsEffectivelyVisible);

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
