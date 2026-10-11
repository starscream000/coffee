// A new project created through the real engine (instruction D0004, task 13):
// the New project form creates the folder, the config and the .gitignore, the
// app opens the project, and a folder that is not empty gets the engine's
// message next to the folder field. Skips when the engine is not built,
// unless DESKTOP_TESTS_REQUIRE_ENGINE=1.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Engine;
using Desktop.Protocol;

namespace Desktop.App.Tests;

public sealed class RealEngineNewProjectTests
{
    [AvaloniaFact]
    public async Task A_new_project_is_created_on_disk_and_opened()
    {
        var work = Directory.CreateTempSubdirectory("desktop-new-project-").FullName;
        var settings = new JsonSettingsStore(Path.Combine(work, "settings.json"));
        var engine = new EngineService(settings, new AvaloniaDispatcher());
        var shell = new ShellViewModel(engine, settings, new FakeFolderPicker(work), new DiskProjectFiles(), new AvaloniaDispatcher());
        try
        {
            await shell.InitializeAsync();
            if (engine.State != EngineState.Ready)
            {
                if (Environment.GetEnvironmentVariable("DESKTOP_TESTS_REQUIRE_ENGINE") == "1")
                {
                    Assert.Fail(engine.Failure?.Message);
                }

                Assert.Skip($"No built engine or no Node: {engine.Failure?.Message.Split(Environment.NewLine)[0]}");
            }

            shell.ShowNewProjectCommand.Execute(null);
            var form = shell.NewProject!;
            await form.BrowseCommand.ExecuteAsync(null);
            form.Name = "Shop tests";
            form.BaseUrl = "http://localhost:4310";
            await form.CreateCommand.ExecuteAsync(null);

            var root = Path.Combine(work, "shop-tests");
            Assert.Null(form.Error);
            Assert.Null(form.FolderError);
            var workspace = Assert.IsType<WorkspaceViewModel>(shell.Workspace);
            Assert.True(LocalPaths.Same(root, workspace.Root));
            Assert.Equal(["local"], workspace.Environments);
            Assert.True(workspace.OffersFirstRecording);
            Assert.Empty(workspace.Problems.All);
            Assert.StartsWith("# Shop tests", await File.ReadAllTextAsync(Path.Combine(root, Product.ConfigFile)), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, ".gitignore")));
            Assert.True(Directory.Exists(Path.Combine(root, "tests")));

            // The same folder again: it is not empty any more.
            shell.ShowNewProjectCommand.Execute(null);
            var again = shell.NewProject!;
            again.Location = work;
            again.Name = "Shop tests";
            again.BaseUrl = "http://localhost:4310";
            await again.CreateCommand.ExecuteAsync(null);
            Assert.Contains("is not empty", again.FolderError, StringComparison.Ordinal);
            Assert.Same(again, shell.NewProject);
        }
        finally
        {
            await shell.ShutdownAsync();
            Directory.Delete(work, recursive: true);
        }
    }
}
