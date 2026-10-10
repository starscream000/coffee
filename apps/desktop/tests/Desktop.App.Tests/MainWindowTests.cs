// Tests of the views on Avalonia's headless platform: the start page, an open
// project, and the settings panel bind to their view models. With
// DESKTOP_SCREENSHOTS set to a folder, each test also saves what it rendered.

using System.Text.Json;
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
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class MainWindowTests
{
    private const string Root = "/work/shop-tests";

    private static (MainWindow Window, ShellViewModel Shell, FakeEngineService Engine, FakeProjectFiles Files) Create()
    {
        var engine = new FakeEngineService();
        var files = new FakeProjectFiles();
        var settings = new MemorySettingsStore { Settings = new AppSettings { RecentProjects = [Root, "/work/old-project"] } };
        var shell = new ShellViewModel(engine, settings, new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root);
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        return (window, shell, engine, files);
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Snapshot(Window window, string name)
    {
        Render(window);
        var folder = Environment.GetEnvironmentVariable("DESKTOP_SCREENSHOTS");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        window.CaptureRenderedFrame()?.Save(Path.Combine(folder, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static T Find<T>(Window window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named {name} is shown.");

    private static async Task OpenDemoProject(ShellViewModel shell, FakeEngineService engine, FakeProjectFiles files)
    {
        engine.Info = FakeEngineService.ReadyInfo();
        engine.Tests =
        [
            Make.Test("tests/checkout/guest-checkout.test.yaml", "Guest checks out", "smoke", "checkout"),
            Make.Test("tests/checkout/member-checkout.test.yaml", "Member checks out with a saved card", "checkout"),
            Make.Test("tests/login.test.yaml", "Customer logs in", "smoke"),
            Make.Test("tests/search/search-by-name.test.yaml", "Search finds a product by name"),
        ];
        engine.Validate = _ =>
        [
            Make.Error("tests/checkout/guest-checkout.test.yaml", 7, "UnknownAction", "Unknown action \"clik\".") with { Hint = "Did you mean \"click\"?" },
            Make.Warning("tests/login.test.yaml", 3, "UnusedTarget", "Target \"login.remember\" is never used."),
        ];
        engine.Actions =
        [
            new ActionInfo
            {
                Name = "click",
                Description = "Clicks an element.",
                Shorthand = "target",
                ParamsSchema = JsonDocument.Parse("""{"type":"object","properties":{"target":{"type":"string"},"button":{"enum":["left","right"]}},"required":["target"]}""").RootElement.Clone(),
                Source = new ActionSource { Kind = ActionSourceKind.Builtin },
            },
            new ActionInfo
            {
                Name = "shop.addToCart",
                Description = "Adds a product to the cart.",
                Shorthand = "product",
                ParamsSchema = JsonDocument.Parse("{}").RootElement.Clone(),
                Source = new ActionSource { Kind = ActionSourceKind.File, File = "actions/shop/add-to-cart.ts" },
            },
        ];
        files.Files["tests/checkout/guest-checkout.test.yaml"] =
            "version: 1\nname: Guest checks out\ntags: [smoke, checkout]\nsteps:\n  - goto: /products\n  - click: product.addToCart\n  - clik: cart.open\n  - expect.text: { target: cart.count, equals: '1' }\n";
        await shell.InitializeAsync();
        await shell.OpenProjectAsync(Root);
    }

    [AvaloniaFact]
    public void Start_page_shows_recent_projects_and_the_engine_state()
    {
        var (window, shell, engine, _) = Create();
        window.Show();
        engine.Info = FakeEngineService.ReadyInfo();
        engine.SetState(Engine.EngineState.Ready);
        Snapshot(window, "01-start-page");

        Assert.Equal(Product.DisplayName, window.Title);
        Assert.True(Find<StartPageView>(window, "StartPage").IsVisible);
        Assert.False(Find<WorkspaceView>(window, "WorkspacePanel").IsVisible);
        Assert.Equal("Engine ready", Find<TextBlock>(window, "EngineStateText").Text);
        Assert.Equal(2, shell.RecentProjects.Count);
    }

    [AvaloniaFact]
    public async Task Open_project_shows_tree_tabs_and_problems()
    {
        var (window, shell, engine, files) = Create();
        window.Show();
        await OpenDemoProject(shell, engine, files);
        Render(window);

        Assert.Equal($"shop-tests – {Product.DisplayName}", window.Title);
        Assert.False(Find<StartPageView>(window, "StartPage").IsVisible);
        Assert.True(Find<WorkspaceView>(window, "WorkspacePanel").IsVisible);
        Assert.Equal("1 error, 1 warning", Find<TextBlock>(window, "ProblemSummary").Text);
        Assert.Equal(2, Find<ListBox>(window, "ProblemList").ItemCount);
        Snapshot(window, "02-project-actions");

        shell.Workspace!.Problems.SelectedItem = shell.Workspace.Problems.Items[0];
        Render(window);
        var editor = Find<AvaloniaEdit.TextEditor>(window, "Editor");
        Assert.Equal(9, editor.Document.LineCount);
        Assert.Equal(7, editor.TextArea.Caret.Line);
        Assert.Equal([7], window.GetVisualDescendants().OfType<StepFileView>().Single().Marks.Marks.Keys);
        Snapshot(window, "03-project-step-file");
    }

    [AvaloniaFact]
    public async Task Typing_in_the_editor_marks_a_problem_line()
    {
        var engine = new FakeEngineService { Info = FakeEngineService.ReadyInfo(), Tests = [Make.Test("tests/a.test.yaml", "A")] };
        var files = new FakeProjectFiles();
        files.Files["tests/a.test.yaml"] = "version: 1\nname: A\nsteps:\n";
        var delay = new ManualDelay();
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), delay);
        engine.ValidateContent = (file, text) =>
            [.. text.Split('\n').Select((line, i) => (line, i)).Where(x => x.line.Contains("clik", StringComparison.Ordinal))
                .Select(x => Make.Error(file, x.i + 1, "UnknownAction", "Unknown action \"clik\"."))];
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile("tests/a.test.yaml");
        Render(window);
        var editor = Find<AvaloniaEdit.TextEditor>(window, "Editor");
        editor.TextArea.Caret.Offset = editor.Document.TextLength;
        editor.TextArea.Focus();

        window.KeyTextInput("  - clik: cart.open");
        delay.Elapse();
        await Task.Yield();
        Render(window);

        Assert.Equal("version: 1\nname: A\nsteps:\n  - clik: cart.open", editor.Document.Text);
        var view = window.GetVisualDescendants().OfType<StepFileView>().Single();
        Assert.Equal(LineMark.Error, view.Marks.Marks[4]);
        Assert.Equal("1 error", window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "ProblemSummary").Text);
        Assert.StartsWith("● ", ((StepFileViewModel)shell.Workspace.SelectedTab!).Title, StringComparison.Ordinal);
        Snapshot(window, "07-editor-typing");
    }

    [AvaloniaFact]
    public async Task A_new_view_of_a_tab_puts_its_caret_back()
    {
        var (window, shell, engine, files) = Create();
        window.Show();
        await OpenDemoProject(shell, engine, files);
        shell.Workspace!.OpenFile("tests/checkout/guest-checkout.test.yaml");
        Render(window);
        var editor = Find<AvaloniaEdit.TextEditor>(window, "Editor");
        editor.CaretOffset = 42;
        Render(window);

        shell.Workspace.SelectedTab = shell.Workspace.Actions;
        Render(window);
        shell.Workspace.OpenFile("tests/checkout/guest-checkout.test.yaml");
        Render(window);

        Assert.Equal(42, Find<AvaloniaEdit.TextEditor>(window, "Editor").CaretOffset);
        Assert.Equal(42, ((StepFileViewModel)shell.Workspace.SelectedTab!).CaretOffset);
    }

    [AvaloniaFact]
    public void Settings_panel_and_failure_notice_render()
    {
        var (window, shell, engine, _) = Create();
        window.Show();
        engine.SetState(Engine.EngineState.Failed, new Engine.EngineNotFoundException(
            "The engine was not found. Build it with \"pnpm build\" in the repository, or set its path (packages/engine/dist/main.js) in Settings.", []));
        Snapshot(window, "04-engine-not-found");

        shell.ToggleSettingsCommand.Execute(null);
        Snapshot(window, "05-settings");
        Assert.True(shell.IsSettingsOpen);
        Assert.Equal("Engine not running", Find<TextBlock>(window, "EngineStateText").Text);
    }
}
