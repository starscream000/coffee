// Tests of the shell (open, close, recent projects, engine failures, settings)
// and of the workspace (loading, opening files, file changes).

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class ShellAndWorkspaceTests
{
    private const string Root = "/work/shop-tests";

    private sealed class Setup
    {
        public FakeEngineService Engine { get; } = new();

        public MemorySettingsStore Settings { get; } = new();

        public FakeProjectFiles Files { get; } = new();

        public HashSet<string> Folders { get; } = [Root];

        public string? Picked { get; set; } = Root;

        public ShellViewModel Shell() =>
            new(Engine, Settings, new FakeFolderPicker(Picked), Files, new ImmediateDispatcher(), Folders.Contains);
    }

    [AvaloniaFact]
    public async Task Opening_a_folder_loads_tests_actions_and_problems_and_remembers_it()
    {
        var setup = new Setup();
        setup.Engine.Tests = [Make.Test("tests/a.test.yaml", "A", "smoke")];
        setup.Engine.Validate = files => [Make.Error("tests/a.test.yaml", 4)];
        setup.Engine.OpenProject = root => FakeEngineService.Project(root, Make.Warning(Product.ConfigFile, 2, "UnknownKey"));
        var shell = setup.Shell();
        await shell.InitializeAsync();

        await shell.OpenFolderCommand.ExecuteAsync(null);

        var workspace = Assert.IsType<WorkspaceViewModel>(shell.Workspace);
        Assert.Equal(["start", $"openProject {Root}", "listTests", "listActions", "validate tests/a.test.yaml"], setup.Engine.Calls);
        Assert.Equal("shop-tests", workspace.Name);
        Assert.Equal($"shop-tests – {Product.DisplayName}", shell.Title);
        Assert.Equal("local", workspace.SelectedEnvironment);
        Assert.Equal("1 error, 1 warning", workspace.Problems.Summary);
        Assert.Equal(1, workspace.Explorer.Roots[0].ErrorCount);
        Assert.Equal([Root], setup.Settings.Settings.RecentProjects);
        Assert.Equal(Root, Assert.Single(shell.RecentProjects).Path);
        Assert.Null(shell.Notice);
        Assert.NotNull(setup.Files.Changed);
    }

    [AvaloniaFact]
    public async Task A_folder_that_is_not_a_project_shows_the_engines_reason()
    {
        var setup = new Setup();
        setup.Engine.OpenProject = _ => throw new EngineRequestException(Methods.OpenProject, new JsonRpcError
        {
            Code = -32004,
            Message = "No config file in /work/shop-tests.",
            Data = Protocol.Json.ProtocolJson.ToElement(new { name = ErrorCodes.ProjectInvalid }),
        });
        var shell = setup.Shell();

        await shell.OpenProjectAsync(Root);

        Assert.Null(shell.Workspace);
        Assert.True(shell.NoticeIsError);
        Assert.Equal($"{Root} is not a project: No config file in /work/shop-tests.", shell.Notice);
        Assert.Empty(setup.Settings.Settings.RecentProjects);
    }

    [AvaloniaFact]
    public async Task Opening_starts_the_engine_first_and_explains_when_it_cannot()
    {
        var setup = new Setup();
        setup.Engine.OnStart = e =>
        {
            e.SetState(EngineState.Failed, new EngineNotFoundException("Node was not found. Install Node 24.", []));
            return Task.CompletedTask;
        };
        var shell = setup.Shell();

        await shell.OpenProjectAsync(Root);

        Assert.Null(shell.Workspace);
        Assert.Contains("engine is not running. Node was not found", shell.Notice, StringComparison.Ordinal);
        Assert.True(shell.Engine.IsFailed);
        Assert.Equal("Node was not found. Install Node 24.", shell.Engine.Detail);
    }

    [AvaloniaFact]
    public async Task A_missing_folder_is_reported_and_greyed_out_in_recent_projects()
    {
        var setup = new Setup();
        setup.Settings.Settings = new AppSettings { RecentProjects = ["/gone", Root] };
        var shell = setup.Shell();
        Assert.Equal([false, true], shell.RecentProjects.Select(r => r.Exists));

        await shell.OpenProjectAsync("/gone");
        Assert.Contains("does not exist", shell.Notice, StringComparison.Ordinal);

        shell.ForgetRecentCommand.Execute(shell.RecentProjects[0]);
        Assert.Equal([Root], setup.Settings.Settings.RecentProjects);
    }

    [AvaloniaFact]
    public async Task An_engine_crash_with_a_project_open_shows_a_notice_and_restart_reopens_the_project()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        await shell.InitializeAsync();
        await shell.OpenProjectAsync(Root);

        setup.Engine.SetState(EngineState.Failed, new EngineExitedException(1, "boom"));
        Assert.Contains("The engine stopped", shell.Notice, StringComparison.Ordinal);

        setup.Engine.Calls.Clear();
        await shell.RestartEngineCommand.ExecuteAsync(null);
        Assert.Equal(["start", $"openProject {Root}", "listTests", "listActions", "validate "], setup.Engine.Calls);
        Assert.Null(shell.Notice);
    }

    [AvaloniaFact]
    public async Task Closing_the_project_returns_to_the_start_page_and_stops_watching()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        await shell.OpenProjectAsync(Root);
        shell.CloseProjectCommand.Execute(null);
        Assert.False(shell.IsProjectOpen);
        Assert.Equal(Product.DisplayName, shell.Title);
        Assert.Null(setup.Files.Changed);
    }

    [AvaloniaFact]
    public async Task Shutdown_stops_the_engine()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        await shell.InitializeAsync();
        await shell.ShutdownAsync();
        Assert.Equal(["start", "shutdown"], setup.Engine.Calls);
    }

    [AvaloniaFact]
    public async Task Saving_settings_closes_the_panel_and_restarts_the_engine()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        shell.ToggleSettingsCommand.Execute(null);
        Assert.True(shell.IsSettingsOpen);

        await shell.Settings.SaveCommand.ExecuteAsync(null);

        Assert.False(shell.IsSettingsOpen);
        Assert.Equal(["start"], setup.Engine.Calls);
    }

    [AvaloniaFact]
    public async Task Workspace_uses_the_fallback_when_the_engine_cannot_list_tests()
    {
        var setup = new Setup();
        setup.Engine.Tests = null;
        setup.Files.Files["tests/a.test.yaml"] = "version: 1";
        setup.Files.Files["flows/f.flow.yaml"] = "version: 1";
        var shell = setup.Shell();

        await shell.OpenProjectAsync(Root);

        Assert.Equal(["tests/a.test.yaml"], shell.Workspace!.Explorer.Files);
        Assert.NotNull(shell.Workspace.Explorer.Notice);
        Assert.Contains("validate tests/a.test.yaml", setup.Engine.Calls);
    }

    [AvaloniaFact]
    public async Task Workspace_opens_files_in_tabs_and_problems_reveal_their_line()
    {
        var setup = new Setup();
        setup.Engine.Tests = [Make.Test("tests/a.test.yaml", "A")];
        setup.Engine.Validate = _ => [Make.Error("tests/a.test.yaml", 2)];
        setup.Files.Files["tests/a.test.yaml"] = "version: 1\nsteps: [x]\n";
        var shell = setup.Shell();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;

        workspace.Problems.SelectedItem = workspace.Problems.Items[0];

        var tab = Assert.IsType<StepFileViewModel>(workspace.SelectedTab);
        Assert.Equal(2, tab.RevealedLine);
        Assert.Equal(LineMark.Error, tab.LineMarks[2]);
        workspace.Explorer.SelectedNode = workspace.Explorer.Roots[0].Children[0];
        Assert.Equal(2, workspace.Tabs.Count);

        tab.CloseCommand.Execute(null);
        Assert.Same(workspace.Actions, workspace.SelectedTab);
        Assert.Single(workspace.Tabs);
    }

    [AvaloniaFact]
    public async Task Workspace_shows_a_load_error_for_a_missing_file()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile("nope.test.yaml");
        Assert.Contains("cannot be read", ((StepFileViewModel)shell.Workspace.SelectedTab!).LoadError, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task File_changes_reload_open_tabs_relist_tests_and_revalidate()
    {
        var setup = new Setup();
        setup.Engine.Tests = [Make.Test("tests/a.test.yaml", "A")];
        setup.Files.Files["tests/a.test.yaml"] = "one\n";
        var shell = setup.Shell();
        await shell.OpenProjectAsync(Root);
        var workspace = shell.Workspace!;
        workspace.OpenFile("tests/a.test.yaml");
        setup.Engine.Calls.Clear();

        setup.Files.Files["tests/a.test.yaml"] = "one\ntwo\n";
        setup.Engine.Tests = [Make.Test("tests/a.test.yaml", "A"), Make.Test("tests/b.test.yaml", "B")];
        setup.Files.Changed!(["tests/a.test.yaml", "tests/b.test.yaml"]);
        await Task.Yield();

        Assert.Equal("one\ntwo\n", ((StepFileViewModel)workspace.SelectedTab!).Document.Text);
        Assert.Equal(["listTests", "validate tests/a.test.yaml,tests/b.test.yaml"], setup.Engine.Calls);

        setup.Engine.Calls.Clear();
        setup.Files.Changed!(["flows/x.flow.yaml"]);
        await Task.Yield();
        Assert.Equal(["validate tests/a.test.yaml,tests/b.test.yaml"], setup.Engine.Calls);
    }

    [AvaloniaFact]
    public async Task A_config_change_reopens_the_project()
    {
        var setup = new Setup();
        var shell = setup.Shell();
        await shell.OpenProjectAsync(Root);
        var first = shell.Workspace;
        setup.Engine.Calls.Clear();

        setup.Files.Changed!([Product.ConfigFile]);
        await Task.Yield();

        Assert.Equal($"openProject {Root}", setup.Engine.Calls[0]);
        Assert.NotSame(first, shell.Workspace);
    }
}
