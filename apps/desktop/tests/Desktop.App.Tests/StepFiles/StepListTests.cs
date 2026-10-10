// The step list in a step file's tab (instruction D0003, tasks 14 and 16):
// it shows the same text as the text editor, both ways and at once; its
// changes are undone by the editor's undo in one step; adding a step from the
// action picker; problems marked on steps; files it cannot show say why.

using System.Text.Json;
using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.StepFiles;

public sealed class StepListTests
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

    private static async Task<(WorkspaceViewModel Workspace, StepFileViewModel Tab, FakeEngineService Engine)> OpenAsync(string text, string file = Test)
    {
        var engine = new FakeEngineService
        {
            Tests = [Make.Test(Test, "A")],
            Actions =
            [
                Action("goto", "url", """{"type":"object","properties":{"url":{"type":"string"}},"required":["url"]}"""),
                Action("back", null, """{"type":"object","properties":{}}"""),
                Action("fill", null, """{"type":"object","properties":{"target":{},"value":{"type":"string"}},"required":["target","value"]}"""),
                Action("click", "target", """{"type":"object","properties":{"target":{}},"required":["target"]}"""),
            ],
        };
        var files = new FakeProjectFiles();
        files.Files[file] = text;
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), new ManualDelay());
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(file);
        return (shell.Workspace, (StepFileViewModel)shell.Workspace.SelectedTab!, engine);
    }

    [AvaloniaFact]
    public async Task Shows_the_steps_and_selecting_one_shows_its_line()
    {
        var (_, tab, _) = await OpenAsync(StepOutlineTests.Sample);
        var list = tab.Steps!;

        Assert.True(list.IsAvailable);
        Assert.Equal(["before", "steps", "after"], list.Sections.Select(s => s.Name));
        var click = list.Sections[1].Steps[3];
        Assert.Equal("click", click.Action);
        Assert.Equal("order.print", click.Detail);
        Assert.Equal("Print the receipt", click.Name);
        Assert.Equal("target: receipt.total, equals: €20.00", list.Sections[1].Steps[4].Detail);

        list.SelectCommand.Execute(click);

        Assert.Equal(13, tab.RevealedLine);
        Assert.True(click.IsSelected);
    }

    [AvaloniaFact]
    public async Task Typing_in_the_text_changes_the_list_at_once()
    {
        var (_, tab, _) = await OpenAsync("version: 1\nsteps:\n  - back\n");

        tab.Document.Insert(tab.Document.TextLength, "  - goto: /cart\n");

        Assert.Equal(["back", "goto"], tab.Steps!.Sections[1].Steps.Select(s => s.Action));
        Assert.Equal("/cart", tab.Steps.Sections[1].Steps[1].Detail);
    }

    [AvaloniaFact]
    public async Task Adding_a_step_from_the_picker_and_undoing_it_in_one_step()
    {
        var text = "version: 1\nname: A\nsteps:\n  - goto: /x  # first\n  - back\n";
        var (_, tab, _) = await OpenAsync(text);
        var list = tab.Steps!;
        list.SelectCommand.Execute(list.Sections[1].Steps[0]);

        list.StartAddingCommand.Execute(null);
        list.PickerSearch = "FIL";
        var fill = Assert.Single(list.PickerItems);
        list.AddCommand.Execute(fill);

        Assert.False(list.IsPicking);
        Assert.Equal("version: 1\nname: A\nsteps:\n  - goto: /x  # first\n  - fill:\n      target: ''\n      value: ''\n  - back\n", tab.Document.Text);
        Assert.Equal("fill", list.Selected!.Action);
        Assert.True(tab.IsDirty);

        tab.UndoCommand.Execute(null);
        Assert.Equal(text, tab.Document.Text);
        Assert.Equal(["goto", "back"], list.Sections[1].Steps.Select(s => s.Action));
        tab.RedoCommand.Execute(null);
        Assert.Equal(3, list.Sections[1].Steps.Count);
    }

    [AvaloniaFact]
    public async Task Adding_without_a_selection_goes_to_the_end_of_the_steps()
    {
        var (_, tab, _) = await OpenAsync("version: 1\nsteps: []\n");
        var list = tab.Steps!;
        list.StartAddingCommand.Execute(null);

        list.AddCommand.Execute(list.PickerItems.Single(a => a.Name == "goto"));
        list.Selected = null;
        list.AddCommand.Execute(list.PickerItems.Single(a => a.Name == "back"));

        Assert.Equal("version: 1\nsteps:\n  - goto: ''\n  - back\n", tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Remove_duplicate_and_move_change_the_text_and_follow_the_step()
    {
        var (_, tab, _) = await OpenAsync("version: 1\nsteps:\n  - goto: /x\n  - back\n");
        var list = tab.Steps!;
        list.SelectCommand.Execute(list.Sections[1].Steps[0]);
        Assert.Equal(["before", "after"], list.MoveTargets);

        list.MoveDownCommand.Execute(null);
        Assert.Equal("version: 1\nsteps:\n  - back\n  - goto: /x\n", tab.Document.Text);
        Assert.Equal(("goto", 1), (list.Selected!.Action, list.Selected.Outline.Index));
        Assert.False(list.MoveDownCommand.CanExecute(null));

        list.DuplicateCommand.Execute(null);
        Assert.Equal(2, list.Selected!.Outline.Index);

        list.MoveToSectionCommand.Execute("after");
        Assert.Equal("version: 1\nsteps:\n  - back\n  - goto: /x\nafter:\n  - goto: /x\n", tab.Document.Text);
        Assert.Equal("after", list.Selected!.Section);

        list.RemoveCommand.Execute(null);
        Assert.Equal("version: 1\nsteps:\n  - back\n  - goto: /x\nafter: []\n", tab.Document.Text);
        Assert.Null(list.Selected);

        for (var i = 0; i < 4; i++)
        {
            tab.UndoCommand.Execute(null);
        }

        Assert.Equal("version: 1\nsteps:\n  - goto: /x\n  - back\n", tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task Problems_are_marked_on_their_steps()
    {
        var (workspace, tab, engine) = await OpenAsync("version: 1\nsteps:\n  - goto: /x\n  - clik: a\n");
        engine.Validate = _ => [Make.Error(Test, 4, "UnknownAction")];

        await workspace.ValidateAsync();

        var steps = tab.Steps!.Sections[1].Steps;
        Assert.False(steps[0].HasError);
        Assert.True(steps[1].HasError);
    }

    [AvaloniaFact]
    public async Task A_file_it_cannot_show_is_text_only_with_the_reason()
    {
        var (_, tab, _) = await OpenAsync("version: 1\nsteps: [back]\n");

        Assert.False(tab.Steps!.IsAvailable);
        Assert.StartsWith("The step list cannot show this file. Line 2: \"steps\" is not a list of steps", tab.Steps.Problem, StringComparison.Ordinal);
        Assert.EndsWith("Edit it as text.", tab.Steps.Problem, StringComparison.Ordinal);

        tab.Document.Replace(0, tab.Document.TextLength, "version: 1\nsteps:\n  - back\n");
        Assert.True(tab.Steps.IsAvailable);
    }

    [AvaloniaFact]
    public async Task A_targets_file_has_no_step_list()
    {
        var (_, tab, _) = await OpenAsync("version: 1\ntargets: {}\n", "targets/shop.targets.yaml");

        Assert.Null(tab.Steps);
        Assert.False(tab.HasSteps);
    }
}
