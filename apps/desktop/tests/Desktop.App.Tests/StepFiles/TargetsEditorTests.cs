// The targets editor (instruction D0003, task 18): targets by name, their
// candidates in order, adding, removing and reordering candidates, kind,
// value, exact and nth, frame and within picked from other targets, adding
// and removing targets; every change rewrites only that target's lines.

using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Targets;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.StepFiles;

public sealed class TargetsEditorTests
{
    private const string Root = "/work/shop-tests";
    private const string SharedFile = "targets/shop.targets.yaml";

    private static async Task<StepFileViewModel> OpenAsync(string file, string text)
    {
        var engine = new FakeEngineService();
        var files = new FakeProjectFiles();
        files.Files[SharedFile] = TargetsTests.Shared;
        files.Files[file] = text;
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), new ManualDelay());
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(file);
        return (StepFileViewModel)shell.Workspace.SelectedTab!;
    }

    private static TargetFormViewModel Select(StepFileViewModel tab, string name)
    {
        Assert.True(tab.Targets!.Select(name));
        return tab.Targets.Form!;
    }

    [AvaloniaFact]
    public async Task A_targets_file_opens_on_its_targets_and_a_test_on_its_steps()
    {
        var shared = await OpenAsync(SharedFile, TargetsTests.Shared);
        var test = await OpenAsync("tests/a.test.yaml", "version: 1\nname: A\nsteps:\n  - back\n");

        Assert.Equal(["checkout", "paymentFrame", "cardNumber"], shared.Targets!.Names);
        Assert.Null(shared.Steps);
        Assert.Equal(1, shared.SidePanelIndex);
        Assert.Equal(0, test.SidePanelIndex);
        Assert.Equal("No targets yet.", test.Targets!.EmptyText);
    }

    [AvaloniaFact]
    public async Task A_target_s_candidates_frame_and_within()
    {
        var tab = await OpenAsync(SharedFile, TargetsTests.Shared);

        var checkout = Select(tab, "checkout");
        Assert.Equal([("role", "button", "Check out"), ("testId", "checkout", "")], checkout.Candidates.Select(c => (c.Kind, c.Value, c.Name)));
        Assert.True(checkout.Candidates[0].IsRole);
        Assert.Equal(["", "cardNumber", "paymentFrame"], checkout.FrameChoices);

        var card = Select(tab, "cardNumber");
        Assert.Equal("paymentFrame", card.Frame);
        Assert.Equal("0", card.Candidates[1].Nth);
    }

    [AvaloniaFact]
    public async Task Changing_a_candidate_rewrites_only_its_target()
    {
        var tab = await OpenAsync(SharedFile, TargetsTests.Shared);

        Select(tab, "checkout").Candidates[0].Name = "Check out now";

        Assert.Equal(TargetsTests.Shared.Replace("      name: Check out\n", "      name: Check out now\n", StringComparison.Ordinal), tab.Document.Text);
        Assert.Contains("    - testId: checkout   # stable\n", tab.Document.Text, StringComparison.Ordinal);
        Assert.Contains("  # The payment form sits in a frame.\n", tab.Document.Text, StringComparison.Ordinal);
        tab.UndoCommand.Execute(null);
        Assert.Equal(TargetsTests.Shared, tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Adding_removing_and_reordering_candidates()
    {
        var tab = await OpenAsync(SharedFile, TargetsTests.Shared);
        var form = Select(tab, "paymentFrame");

        form.AddCandidateCommand.Execute(null);
        Assert.Contains("  paymentFrame:\n    - css: 'iframe#pay'\n    - testId: ''\n  cardNumber:", tab.Document.Text, StringComparison.Ordinal);

        form = tab.Targets!.Form!;
        var added = form.Candidates[1];
        added.Value = "payment";
        Assert.Contains("  paymentFrame:\n    - css: 'iframe#pay'\n    - testId: payment\n", tab.Document.Text, StringComparison.Ordinal);

        form = tab.Targets.Form!;
        form.MoveCandidateUpCommand.Execute(form.Candidates[1]);
        Assert.Contains("  paymentFrame:\n    - testId: payment\n    - css: 'iframe#pay'\n", tab.Document.Text, StringComparison.Ordinal);

        form = tab.Targets.Form!;
        form.Candidates[0].Kind = "css";
        form = tab.Targets.Form!;
        form.Candidates[0].Exact = "false";
        form = tab.Targets.Form!;
        form.Candidates[0].Nth = "1";
        Assert.Contains("  paymentFrame:\n    - css: payment\n      exact: false\n      nth: 1\n", tab.Document.Text, StringComparison.Ordinal);

        form = tab.Targets.Form!;
        form.RemoveCandidateCommand.Execute(form.Candidates[0]);
        Assert.Contains("  paymentFrame:\n    - css: 'iframe#pay'\n  cardNumber:", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_comment_above_a_candidate_moves_with_it()
    {
        const string Text = "version: 1\ntargets:\n  buy:\n    - testId: buy\n    # the old button, until the redesign ships\n    - css: '.buy'  # fragile\n";
        var tab = await OpenAsync(SharedFile, Text);
        var form = Select(tab, "buy");

        form.MoveCandidateUpCommand.Execute(form.Candidates[1]);

        Assert.Equal("version: 1\ntargets:\n  buy:\n    # the old button, until the redesign ships\n    - css: '.buy'  # fragile\n    - testId: buy\n", tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Frame_and_within_are_picked_from_other_targets()
    {
        var tab = await OpenAsync(SharedFile, TargetsTests.Shared);

        Select(tab, "checkout").Within = "paymentFrame";
        Assert.Contains("  checkout:\n    within: paymentFrame\n    candidates:\n      - role: button\n        name: Check out\n      - testId: checkout   # stable\n", tab.Document.Text, StringComparison.Ordinal);

        var card = Select(tab, "cardNumber");
        card.Within = "checkout";
        Assert.Equal("A target has a frame or a within, not both: put the frame on the outer target.", card.Error);

        Select(tab, "cardNumber").Frame = string.Empty;
        Assert.EndsWith("  cardNumber:\n    - label: Card number\n    - { css: '#card', nth: 0 }", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData("nth", "first", "nth is a number from 0")]
    [InlineData("value", "", "Enter the testId to look for.")]
    public async Task A_candidate_value_that_does_not_fit_is_refused(string field, string value, string error)
    {
        var tab = await OpenAsync(SharedFile, TargetsTests.Shared);
        var candidate = Select(tab, "checkout").Candidates[1];

        if (field == "nth")
        {
            candidate.Nth = value;
        }
        else
        {
            candidate.Value = value;
        }

        Assert.Equal(error, candidate.Error![..error.Length]);
        Assert.Equal(TargetsTests.Shared, tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Adding_and_removing_targets()
    {
        var tab = await OpenAsync("tests/a.test.yaml", "version: 1\nname: A\nsteps:\n  - click: buy\n");
        var editor = tab.Targets!;

        editor.NewName = "checkout";
        editor.AddTargetCommand.Execute(null);
        Assert.Equal("There is already a target named checkout.", editor.Notice);

        editor.NewName = "buy";
        editor.AddTargetCommand.Execute(null);
        Assert.Equal("version: 1\nname: A\ntargets:\n  buy:\n    - testId: ''\nsteps:\n  - click: buy\n", tab.Document.Text);
        Assert.Equal("buy", editor.SelectedName);

        editor.Form!.Candidates[0].Value = "buy-button";
        editor.RemoveTargetCommand.Execute(null);
        Assert.Equal("version: 1\nname: A\ntargets: {}\nsteps:\n  - click: buy\n", tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Showing_a_target_from_a_failure_counts_its_candidates()
    {
        var tab = await OpenAsync(SharedFile, TargetsTests.Shared);
        CandidateMatches[] counts =
        [
            new() { Candidate = new Dictionary<string, System.Text.Json.JsonElement> { ["role"] = Json("\"button\""), ["name"] = Json("\"Check out\"") }, Matches = 0 },
            new() { Candidate = new Dictionary<string, System.Text.Json.JsonElement> { ["testId"] = Json("\"checkout\"") }, Matches = 2 },
        ];

        Assert.True(tab.ShowTarget("checkout", counts));

        Assert.True(tab.IsTargetsShown);
        Assert.Equal(4, tab.RevealedLine);
        Assert.Equal(["matched no element", "matched 2 elements"], tab.Targets!.Form!.Candidates.Select(c => c.MatchText));
        Assert.False(tab.ShowTarget("missing", null));
    }

    private static System.Text.Json.JsonElement Json(string text) => System.Text.Json.JsonDocument.Parse(text).RootElement.Clone();
}
