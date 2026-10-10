// From a failure to its target (instruction D0003, task 19): a target that was
// not found or fell back links to the target in the targets editor of the file
// that declares it, with the failure's match counts beside its candidates.

using System.Text.Json;
using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.Tests.StepFiles;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Runs;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Runs;

public sealed class FailureToTargetTests
{
    private const string Root = "/work/shop-tests";
    private const string Test = "tests/a.test.yaml";

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static LocatorUse Use(string? target, int? index, LocatorUse? frame = null, LocatorUse? within = null) => new()
    {
        Param = "target",
        Target = target,
        CandidateIndex = index,
        Candidate = index is null ? null : new Dictionary<string, JsonElement> { ["testId"] = Json("x") },
        Frame = frame,
        Within = within,
    };

    private static StepRunViewModel Failed(params LocatorUse[] uses)
    {
        var s = new RunScript("r1");
        var step = new StepRunViewModel(s.Step("t1", "s1", "Click"), 0);
        step.Apply(s.Failed("t1", "s1") with { Locators = uses });
        return step;
    }

    [Fact]
    public void A_row_points_at_the_level_to_look_at()
    {
        Assert.Equal("checkout", Failed(Use("checkout", null)).Locators[0].Target);
        Assert.Equal("paymentFrame", Failed(Use("cardNumber", null, frame: Use("paymentFrame", null))).Locators[0].Target);
        Assert.Equal("row", Failed(Use("delete", 0, within: Use("row", 1))).Locators[0].Target);
        Assert.Null(Failed(Use(null, null)).Locators[0].Target);
        Assert.Null(Failed(Use("ok", 0)).Locators[0].Target);
        Assert.True(Failed(Use("cardNumber", 0, frame: Use("paymentFrame", null))).Locators[0].NotFound);
        Assert.Equal("Open target checkout", Failed(Use("checkout", null)).Locators[0].OpenText);
    }

    private static async Task<(WorkspaceViewModel Workspace, RunTabViewModel Tab)> RunWithFailureAsync(string target)
    {
        var engine = new FakeEngineService { Info = FakeEngineService.ReadyInfo("chromium"), Tests = [Make.Test(Test, "A")] };
        var files = new FakeProjectFiles();
        files.Files[Test] = "version: 1\nname: A\ntargets:\n  own:\n    - css: '#own'\nsteps:\n  - click: checkout\n";
        files.Files["targets/shop.targets.yaml"] = TargetsTests.Shared;
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), new ManualDelay());
        await shell.InitializeAsync();
        await shell.OpenProjectAsync(Root);
        await shell.Workspace!.RunAllCommand.ExecuteAsync(null);
        var s = new RunScript("run-1");
        CandidateMatches[] counts =
        [
            new() { Candidate = new Dictionary<string, JsonElement> { ["role"] = Json("button"), ["name"] = Json("Check out") }, Matches = 0 },
            new() { Candidate = new Dictionary<string, JsonElement> { ["testId"] = Json("checkout") }, Matches = 3 },
        ];
        var failed = s.Failed("t1", "s1");
        foreach (var e in new EngineEvent[]
        {
            s.Started(RunScript.Planned("t1", Test, "A")),
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Click", file: Test, line: 7),
            failed with { Error = failed.Error with { Code = "TargetNotFound", Candidates = counts }, Locators = [Use(target, null)] },
            s.TestFinished("t1", RunOutcome.Failed),
            s.Finished(RunOutcome.Failed, failed: 1),
        })
        {
            engine.Raise(e);
        }

        return (shell.Workspace, (RunTabViewModel)shell.Workspace.SelectedTab!);
    }

    [AvaloniaFact]
    public async Task A_shared_target_opens_in_its_file_with_the_counts()
    {
        var (workspace, tab) = await RunWithFailureAsync("checkout");
        var row = tab.Run.SelectedTest!.SelectedStep!.Locators.Single();

        tab.OpenTargetCommand.Execute(row);

        var editor = Assert.IsType<StepFileViewModel>(workspace.SelectedTab);
        Assert.Equal("targets/shop.targets.yaml", editor.File);
        Assert.True(editor.IsTargetsShown);
        Assert.Equal("checkout", editor.Targets!.SelectedName);
        Assert.Equal(["matched no element", "matched 3 elements"], editor.Targets.Form!.Candidates.Select(c => c.MatchText));
    }

    [AvaloniaFact]
    public async Task A_target_of_the_test_itself_opens_in_the_test()
    {
        var (workspace, tab) = await RunWithFailureAsync("own");

        tab.OpenTargetCommand.Execute(tab.Run.SelectedTest!.SelectedStep!.Locators.Single());

        var editor = Assert.IsType<StepFileViewModel>(workspace.SelectedTab);
        Assert.Equal(Test, editor.File);
        Assert.Equal(1, editor.SidePanelIndex);
        Assert.Equal("own", editor.Targets!.SelectedName);
    }

    [AvaloniaFact]
    public async Task A_target_declared_nowhere_says_so()
    {
        var (workspace, tab) = await RunWithFailureAsync("gone");

        tab.OpenTargetCommand.Execute(tab.Run.SelectedTest!.SelectedStep!.Locators.Single());

        Assert.Equal($"The target gone is declared neither in {Test} nor in a shared targets file.", workspace.Runs.Notice);
        Assert.Same(tab, workspace.SelectedTab);
    }
}
