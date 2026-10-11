// A test's own settings without YAML (instruction D0004, task 19): the
// top-level key writer, the name, tags and skip reason read and written on
// their own lines with one undo step each, the test itself selected in the
// steps view, and the form in the details panel on Avalonia's headless
// platform. With DESKTOP_SCREENSHOTS set to a folder, the view test also
// saves what it rendered.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using Desktop.App.Services;
using Desktop.App.StepFiles;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Steps;
using Desktop.App.Views;

namespace Desktop.App.Tests.StepFiles;

public sealed class TestSettingsTests
{
    private const string Root = "/work/shop-tests";
    private const string Test = "tests/a.test.yaml";

    private const string Text = """
        # A test.
        version: 1
        name: Guest checks out   # shown in results
        tags:
          - smoke
          - checkout
        steps:
          - goto: /products
        """;

    private static YamlMapping RootOf(string text) => (YamlMapping)YamlTree.Read(text)!;

    [Theory]
    [InlineData("name", "Guest pays", "version: 1\nname: Guest pays\nsteps: []\n")]
    [InlineData("tags", "[smoke]", "version: 1\nname: A\ntags: [smoke]\nsteps: []\n")]
    [InlineData("skip", null, "version: 1\nname: A\nsteps: []\n")]
    public void The_key_writer_sets_adds_and_removes_one_key(string key, string? yaml, string expected)
    {
        const string text = "version: 1\nname: A\nsteps: []\n";

        Assert.Equal(expected, RootKeys.Set(text, RootOf(text), key, yaml, "\n", "name", "version"));
    }

    [Fact]
    public void The_key_writer_replaces_a_block_list_and_keeps_comments_and_line_breaks()
    {
        var text = Text.Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n";

        var tags = RootKeys.Set(text, RootOf(text), "tags", "[smoke]", "\r\n");
        Assert.Equal("# A test.\r\nversion: 1\r\nname: Guest checks out   # shown in results\r\ntags: [smoke]\r\nsteps:\r\n  - goto: /products\r\n", tags);

        var removed = RootKeys.Set(tags, RootOf(tags), "tags", null, "\r\n");
        Assert.Equal("# A test.\r\nversion: 1\r\nname: Guest checks out   # shown in results\r\nsteps:\r\n  - goto: /products\r\n", removed);

        var added = RootKeys.Set(removed, RootOf(removed), "skip", "'Down until SHOP-412'", "\r\n", "tags", "name");
        Assert.Contains("# shown in results\r\nskip: 'Down until SHOP-412'\r\nsteps:", added, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_settings_are_read_and_written_on_their_own_lines_with_one_undo_each()
    {
        var document = new TextDocument(Text + "\n");
        var settings = new TestSettingsViewModel(Test, document);

        Assert.Equal("Guest checks out", settings.Name);
        Assert.Equal("smoke, checkout", settings.Tags);
        Assert.Equal(["smoke", "checkout"], settings.TagList);
        Assert.False(settings.IsSkipped);

        settings.Tags = "smoke, ,smoke, nightly";
        Assert.Contains("\ntags: [smoke, nightly]\nsteps:", document.Text, StringComparison.Ordinal);
        settings.Skip = "Checkout is down: SHOP-412";
        Assert.Contains("\ntags: [smoke, nightly]\nskip: 'Checkout is down: SHOP-412'\nsteps:", document.Text, StringComparison.Ordinal);
        settings.Name = "true";
        Assert.Contains("\nname: 'true'   # shown in results\n", document.Text, StringComparison.Ordinal);

        settings.Name = "  ";
        Assert.StartsWith("A test needs a name.", settings.NameError, StringComparison.Ordinal);
        Assert.Contains("name: 'true'", document.Text, StringComparison.Ordinal);

        document.UndoStack.Undo();
        Assert.Contains("\nname: Guest checks out   # shown in results\n", document.Text, StringComparison.Ordinal);
        Assert.Equal("Guest checks out", settings.Name);
        settings.Skip = string.Empty;
        Assert.DoesNotContain("skip:", document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Selecting_the_test_itself_shows_its_settings_in_the_details_panel()
    {
        var engine = new FakeEngineService { Tests = [Make.Test(Test, "Guest checks out", "smoke")], Actions = StepFormTests.RealActions() };
        var files = new FakeProjectFiles();
        files.Files[Test] = Text + "\n";
        files.Files["flows/log-in.flow.yaml"] = "version: 1\nname: Log in\nsteps:\n  - goto: /login\n";
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root);
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;
        workspace.OpenFile(Test);
        var tab = (StepFileViewModel)workspace.SelectedTab!;

        tab.SelectTestCommand.Execute(null);
        Render(window, "32-test-settings");

        Assert.Same(tab.TestSettings, workspace.Details);
        Assert.Equal("Guest checks out", Find<TextBox>(window, "TestNameField").Text);
        Assert.Contains("selected", Find<Border>(window, "TestCard").Classes);

        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps[0]);
        Assert.False(tab.IsTestSelected);
        Assert.IsType<StepFormViewModel>(workspace.Details);

        workspace.OpenFile("flows/log-in.flow.yaml");
        var flow = (StepFileViewModel)workspace.SelectedTab!;
        Assert.False(flow.HasTestSettings);
        flow.SelectTestCommand.Execute(null);
        Assert.False(flow.IsTestSelected);
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
