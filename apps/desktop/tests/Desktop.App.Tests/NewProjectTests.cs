// New projects (instruction D0004, tasks 12 and 13): the start page's two
// actions, the New project form's own checks, createProject with the form's
// values, the engine's refusals next to their field, and the offer to record
// the first test. With DESKTOP_SCREENSHOTS set to a folder, the view test
// also saves what it rendered.

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
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class NewProjectTests
{
    private const string Work = "/work";
    private const string Root = "/work/shop-tests";

    private sealed class Setup
    {
        public FakeEngineService Engine { get; } = new() { Info = FakeEngineService.ReadyInfo() };

        public MemorySettingsStore Settings { get; } = new();

        public FakeDialogs Dialogs { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public ShellViewModel Shell(string? picked = Work) =>
            new(Engine, Settings, new FakeFolderPicker(picked), Files, new ImmediateDispatcher(), p => p == Root, Dialogs, new ManualDelay());
    }

    private static EngineRequestException Refusal(string name, string message) =>
        new(Methods.CreateProject, new JsonRpcError { Code = -32014, Message = message, Data = ProtocolJson.ToElement(new { name }) });

    private static void Fill(NewProjectViewModel form)
    {
        form.Name = "Shop tests";
        form.Location = Work;
        form.BaseUrl = "http://localhost:4310";
    }

    [Fact]
    public void The_folder_name_follows_the_project_name_until_it_is_changed()
    {
        var form = new NewProjectViewModel(new FakeFolderPicker(null), _ => Task.FromResult(true), Work);

        form.Name = "Shop tests: checkout!";
        Assert.Equal("shop-tests-checkout", form.FolderName);
        Assert.Equal(Path.Combine(Work, "shop-tests-checkout"), form.Root);
        Assert.Equal($"The project goes into {form.Root}", form.RootText);

        form.FolderName = "checkout";
        form.Name = "Something else";
        Assert.Equal("checkout", form.FolderName);
        Assert.Equal(NewProjectViewModel.DefaultEnvironment, form.Environment);
    }

    [AvaloniaFact]
    public async Task The_form_checks_what_it_can_before_asking_the_engine()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        shell.ShowNewProjectCommand.Execute(null);
        var form = shell.NewProject!;
        form.Location = string.Empty;
        form.BaseUrl = "localhost:4310";
        form.Environment = "1st";

        await form.CreateCommand.ExecuteAsync(null);

        Assert.Equal("Give the project a name.", form.NameError);
        Assert.Equal("Choose a folder to create the project in.", form.FolderError);
        Assert.StartsWith("Give the full address of the application under test", form.BaseUrlError, StringComparison.Ordinal);
        Assert.StartsWith("The environment's name must start with a letter", form.EnvironmentError, StringComparison.Ordinal);
        Assert.Empty(setup.Engine.Created);

        form.Name = "x";
        Assert.Null(form.NameError);
    }

    [AvaloniaFact]
    public async Task A_new_project_is_created_opened_and_offers_its_first_recording()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        await shell.InitializeAsync();
        shell.ShowNewProjectCommand.Execute(null);
        var form = shell.NewProject!;
        await form.BrowseCommand.ExecuteAsync(null);
        Fill(form);
        form.Environment = "dev";

        await form.CreateCommand.ExecuteAsync(null);

        var sent = Assert.Single(setup.Engine.Created);
        Assert.Equal(new CreateProjectParams { Root = Path.Combine(Work, "shop-tests"), Name = "Shop tests", BaseUrl = "http://localhost:4310", Environment = "dev" }, sent);
        Assert.Null(shell.NewProject);
        Assert.False(shell.IsNewProjectOpen);
        var workspace = shell.Workspace!;
        Assert.Equal(["dev"], workspace.Environments);
        Assert.True(workspace.OffersFirstRecording);
        Assert.Equal(Path.Combine(Work, "shop-tests"), setup.Settings.Settings.RecentProjects[0]);

        workspace.RecordFirstTestCommand.Execute(null);
        Assert.False(workspace.OffersFirstRecording);
        Assert.True(workspace.IsRecordStartOpen);

        // The next new project is offered beside this one.
        shell.ShowNewProjectCommand.Execute(null);
        Assert.True(LocalPaths.Same(Work, shell.NewProject!.Location));
    }

    [AvaloniaTheory]
    [InlineData(ErrorCodes.FolderNotEmpty, "\"/work/shop-tests\" is not empty: it holds notes.txt. Choose an empty folder, or a new one.", "folder")]
    [InlineData(ErrorCodes.InvalidParams, "\"/work/shop-tests\" is a file, not a folder.", "folder")]
    [InlineData(ErrorCodes.InternalError, "EACCES: permission denied, mkdir '/work/shop-tests'", "folder")]
    [InlineData(ErrorCodes.InvalidParams, "Give the project a name.", "name")]
    [InlineData(ErrorCodes.InvalidParams, "The project cannot be created with these values: environments.local.baseUrl: Invalid URL.", "baseUrl")]
    [InlineData(ErrorCodes.InvalidParams, "The environment name \"x y\" must start with a letter and hold only letters, digits, - and _.", "environment")]
    [InlineData(ErrorCodes.RunInProgress, "A run is in progress; create a project once it has finished.", "form")]
    public async Task The_engines_refusal_is_shown_next_to_its_field(string name, string message, string field)
    {
        var setup = new Setup();
        setup.Engine.CreateProject = _ => throw Refusal(name, message);
        var shell = setup.Shell();
        await shell.InitializeAsync();
        shell.ShowNewProjectCommand.Execute(null);
        var form = shell.NewProject!;
        Fill(form);

        await form.CreateCommand.ExecuteAsync(null);

        var shown = new Dictionary<string, string?>
        {
            ["folder"] = form.FolderError,
            ["name"] = form.NameError,
            ["baseUrl"] = form.BaseUrlError,
            ["environment"] = form.EnvironmentError,
            ["form"] = form.Error,
        };
        Assert.Contains(message, shown[field], StringComparison.Ordinal);
        Assert.All(shown.Where(s => s.Key != field), s => Assert.Null(s.Value));
        Assert.Same(form, shell.NewProject);
        Assert.Null(shell.Workspace);
        Assert.False(form.IsCreating);
    }

    [AvaloniaFact]
    public async Task Unsaved_changes_in_the_open_project_are_asked_about_first()
    {
        var setup = new Setup { };
        setup.Engine.Tests = [Make.Test("tests/a.test.yaml", "A")];
        setup.Files.Files["tests/a.test.yaml"] = "version: 1\n";
        var shell = setup.Shell();
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile("tests/a.test.yaml");
        ((StepFileViewModel)shell.Workspace.SelectedTab!).Document.Insert(0, "# changed\n");
        setup.Dialogs.UnsavedAnswer = UnsavedChangesChoice.Cancel;
        shell.ShowNewProjectCommand.Execute(null);
        var form = shell.NewProject!;
        Fill(form);

        await form.CreateCommand.ExecuteAsync(null);

        Assert.Single(setup.Dialogs.Asked);
        Assert.Empty(setup.Engine.Created);
        Assert.Same(form, shell.NewProject);
        Assert.Equal(Root, shell.Workspace!.Root);
    }

    [AvaloniaFact]
    public async Task Without_the_engine_the_form_says_so()
    {
        var setup = new Setup();
        setup.Engine.OnStart = e =>
        {
            e.SetState(Engine.EngineState.Failed, new EngineNotFoundException("The engine was not found.", []));
            return Task.CompletedTask;
        };
        var shell = setup.Shell();
        shell.ShowNewProjectCommand.Execute(null);
        var form = shell.NewProject!;
        Fill(form);

        await form.CreateCommand.ExecuteAsync(null);

        Assert.Equal("The project could not be created: The engine is not running. The engine was not found.", form.Error);
        Assert.Empty(setup.Engine.Created);
    }

    [AvaloniaFact]
    public async Task The_start_page_offers_new_and_open_and_the_form_shows_problems_by_their_field()
    {
        var setup = new Setup();
        setup.Engine.CreateProject = _ => throw Refusal(ErrorCodes.FolderNotEmpty, "\"/work/shop-tests\" is not empty: it holds notes.txt. Choose an empty folder, or a new one.");
        var shell = setup.Shell();
        var window = new MainWindow { DataContext = shell, Width = 1280, Height = 800 };
        window.Show();
        await shell.InitializeAsync();
        Render(window, "01-start-page");
        Assert.True(Find<Button>(window, "NewProjectButton").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "OpenFolderButton").IsEffectivelyVisible);

        Find<Button>(window, "NewProjectButton").Command!.Execute(null);
        Render(window);
        Assert.True(Find<NewProjectView>(window, "NewProjectForm").IsEffectivelyVisible);
        Fill(shell.NewProject!);
        await shell.NewProject!.CreateCommand.ExecuteAsync(null);
        Render(window, "26-new-project");
        Assert.True(Find<TextBlock>(window, "FolderError").IsEffectivelyVisible);
        Assert.False(Find<TextBlock>(window, "NameError").IsEffectivelyVisible);

        setup.Engine.CreateProject = p => FakeEngineService.Project(p.Root);
        await shell.NewProject.CreateCommand.ExecuteAsync(null);
        Render(window, "27-new-project-created");
        Assert.False(Find<NewProjectView>(window, "NewProjectForm").IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "RecordFirstTestButton").IsEffectivelyVisible);
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
