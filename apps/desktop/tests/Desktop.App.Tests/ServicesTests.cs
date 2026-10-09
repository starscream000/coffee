// Tests of the app services that touch the disk (settings store, project
// files) and of the view models that need no project: engine status, settings.

using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Engine;

namespace Desktop.App.Tests;

public sealed class ServicesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string relative, string text = "version: 1")
    {
        var path = Path.Combine([_dir, .. relative.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Settings_store_round_trips_and_survives_a_broken_file()
    {
        var path = Path.Combine(_dir, "sub", "settings.json");
        var store = new JsonSettingsStore(path);
        Assert.Equal([], store.Load().RecentProjects);

        store.Save(new AppSettings { EnginePath = "/e/main.js", RecentProjects = ["/p"] });
        Assert.Equal(("/e/main.js", (string?)null, "/p"), (store.Load().EnginePath, store.Load().NodePath, store.Load().RecentProjects[0]));

        File.WriteAllText(path, "{ not json");
        Assert.Null(store.Load().EnginePath);
    }

    [Fact]
    public void Project_files_finds_test_files_but_not_in_dot_folders_or_node_modules()
    {
        Write("tests/a.test.yaml");
        Write("tests/deep/b.test.yaml");
        Write("flows/f.flow.yaml");
        Write(".cfe/runs/x.test.yaml");
        Write("node_modules/pkg/c.test.yaml");
        var files = new DiskProjectFiles();

        Assert.Equal(["tests/a.test.yaml", "tests/deep/b.test.yaml"], files.FindTestFiles(_dir));
        Assert.Equal("version: 1", files.ReadText(_dir, "tests/deep/b.test.yaml"));
        Assert.Equal("tests/a.test.yaml", DiskProjectFiles.ToRelative(_dir, Path.Combine(_dir, "tests", "a.test.yaml")));
    }

    [Fact]
    public async Task Project_files_reports_yaml_changes_together()
    {
        var files = new DiskProjectFiles();
        var reported = new TaskCompletionSource<IReadOnlyCollection<string>>();
        using var watch = files.Watch(_dir, batch => reported.TrySetResult(batch));
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Write("root.test.yaml");
        Write("tests/new-folder/new.test.yaml");
        Write("notes.txt", "ignored");

        var batch = await reported.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Contains("root.test.yaml", batch);
        Assert.Contains("tests/new-folder/new.test.yaml", batch);
        Assert.DoesNotContain("notes.txt", batch);
    }

    [Fact]
    public async Task Settings_panel_saves_trimmed_paths_and_asks_for_a_restart()
    {
        var store = new MemorySettingsStore { Settings = new AppSettings { RecentProjects = ["/p"] } };
        var restarted = 0;
        var settings = new SettingsViewModel(store, () =>
        {
            restarted++;
            return Task.CompletedTask;
        });
        var closed = 0;
        settings.Closed += (_, _) => closed++;
        settings.EnginePath = "  /x/main.js ";
        settings.NodePath = " ";

        await settings.SaveCommand.ExecuteAsync(null);

        Assert.Equal("/x/main.js", store.Settings.EnginePath);
        Assert.Null(store.Settings.NodePath);
        Assert.Equal(["/p"], store.Settings.RecentProjects);
        Assert.Equal((1, 1), (restarted, closed));
        Assert.Contains(EngineLocator.EngineVariable, SettingsViewModel.EngineHelp, StringComparison.Ordinal);

        settings.EnginePath = "changed";
        settings.CancelCommand.Execute(null);
        Assert.Equal("/x/main.js", settings.EnginePath);
    }

    [Fact]
    public void Engine_status_reports_capabilities_and_keeps_a_bounded_log()
    {
        var engine = new FakeEngineService();
        var status = new EngineStatusViewModel(engine);
        engine.Info = FakeEngineService.ReadyInfo();
        engine.SetState(EngineState.Ready);
        Assert.Contains("cannot run tests yet", status.Detail, StringComparison.Ordinal);
        Assert.False(status.CanRunTests);

        engine.SetState(EngineState.Starting);
        Assert.True(status.IsBusy);
        engine.Info = FakeEngineService.ReadyInfo("chromium");
        engine.SetState(EngineState.Ready);
        Assert.True(status.CanRunTests);
        Assert.Contains("browsers: chromium", status.Detail, StringComparison.Ordinal);

        for (var i = 0; i < EngineStatusViewModel.MaxLogLines + 5; i++)
        {
            engine.Log($"line {i}");
        }

        Assert.Equal(EngineStatusViewModel.MaxLogLines, status.Log.Count);
        Assert.Equal("line 5", status.Log[0].Text);
    }
    [Fact]
    public void Recent_projects_are_capped_and_deduplicated()
    {
        var settings = new AppSettings();
        for (var i = 0; i < 12; i++)
        {
            settings = settings.WithRecentProject($"/p{i}");
        }

        settings = settings.WithRecentProject("/p5");
        Assert.Equal(AppSettings.MaxRecentProjects, settings.RecentProjects.Count);
        Assert.Equal(["/p5", "/p11", "/p10"], settings.RecentProjects.Take(3));
        Assert.Single(settings.RecentProjects, p => p == "/p5");
    }
}
