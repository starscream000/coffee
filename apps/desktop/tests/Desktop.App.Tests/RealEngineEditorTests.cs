// The editor against the engine built in this checkout, in a temporary copy of
// examples/demo-app (no test writes inside examples/): typing an invalid step
// shows the engine's diagnostic, and saving writes the file. Skips when the
// engine is not built, unless DESKTOP_TESTS_REQUIRE_ENGINE=1.

using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests;

public sealed class RealEngineEditorTests
{
    private const string File = "tests/user-action.test.yaml";

    private static string CopyDemoApp()
    {
        var source = RepoPaths.Of("examples/demo-app");
        var target = Path.Combine(Directory.CreateTempSubdirectory().FullName, "demo-app");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.Split(Path.DirectorySeparatorChar)[0] == Product.DataDir)
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            System.IO.File.Copy(file, destination);
        }

        return target;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(50);
        }

        Assert.True(condition(), $"Timed out waiting for {what}.");
    }

    [AvaloniaFact]
    public async Task Typing_an_invalid_step_shows_the_engines_diagnostic_and_saving_writes_the_file()
    {
        var original = System.IO.File.ReadAllText(RepoPaths.Of("examples/demo-app/" + File));
        var project = CopyDemoApp();
        var settings = new JsonSettingsStore(Path.Combine(Path.GetDirectoryName(project)!, "settings.json"));
        var engine = new EngineService(settings, new AvaloniaDispatcher());
        var shell = new ShellViewModel(engine, settings, new FakeFolderPicker(null), new DiskProjectFiles(), new AvaloniaDispatcher(), dialogs: new FakeDialogs(), delay: new RealDelay());
        try
        {
            DemoEnvironment.Ensure();
            await shell.InitializeAsync();
            if (engine.State != EngineState.Ready)
            {
                if (Environment.GetEnvironmentVariable("DESKTOP_TESTS_REQUIRE_ENGINE") == "1")
                {
                    Assert.Fail(engine.Failure?.Message);
                }

                Assert.Skip($"No built engine or no Node: {engine.Failure?.Message.Split(Environment.NewLine)[0]}");
            }

            await shell.OpenProjectAsync(project);
            Assert.Null(shell.Notice);
            var workspace = shell.Workspace!;
            workspace.OpenFile(File);
            var tab = (StepFileViewModel)workspace.SelectedTab!;
            var invalidLine = tab.Document.LineCount;
            Assert.Equal(string.Empty, tab.Document.GetText(tab.Document.GetLineByNumber(invalidLine)));

            tab.Document.Insert(tab.Document.TextLength, "  - nosuchaction: x\n");

            await WaitUntilAsync(() => workspace.Problems.All.Any(d => d.File == File && d.Line == invalidLine), "the engine's diagnostic for the typed step");
            var diagnostic = workspace.Problems.All.First(d => d.File == File && d.Line == invalidLine);
            Assert.Equal(Protocol.Messages.DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(LineMark.Error, tab.LineMarks[invalidLine]);
            Assert.True(tab.IsDirty);

            Assert.True(await tab.SaveAsync());

            var saved = System.IO.File.ReadAllText(Path.Combine(project, "tests", "user-action.test.yaml"));
            Assert.EndsWith("  - nosuchaction: x\n", saved, StringComparison.Ordinal);
            Assert.False(tab.IsDirty);
            await WaitUntilAsync(() => workspace.Problems.All.Any(d => d.File == File && d.Line == invalidLine && d == diagnostic), "the file's problems from disk");
        }
        finally
        {
            await shell.ShutdownAsync();
            Directory.Delete(Path.GetDirectoryName(project)!, recursive: true);
        }

        Assert.Equal(original, System.IO.File.ReadAllText(RepoPaths.Of("examples/demo-app/" + File)));
    }
}
