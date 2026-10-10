// The form of a step (instruction D0003, task 15): fields from the action's
// parameter schema (the engine's own, from list-actions.json), the step's
// name, page and timeout, a target picker, the engine's problems next to their
// field, and changes that rewrite only the step's lines.

using System.Text.Json;
using Avalonia.Headless.XUnit;
using Desktop.App.Services;
using Desktop.App.StepFiles;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.App.ViewModels.Steps;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests.StepFiles;

public sealed class StepFormTests
{
    private const string Root = "/work/shop-tests";
    private const string Test = "tests/a.test.yaml";

    private const string Text = """
        version: 1
        name: A
        targets:
          buyButton:
            - testId: buy
        steps:
          - goto: /products
          # keep me
          - click: buyButton
            name: Buy it
          - expect.text: { target: cart.count, equals: '1' }
          - click:
              - role: button
                name: Pay
          - shop.addToCart: Desk lamp
        """;

    internal static IReadOnlyList<ActionInfo> RealActions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoPaths.Of("apps/desktop/tests/Desktop.App.Tests/StepFiles/list-actions.json")));
        return document.RootElement.GetProperty("actions").Deserialize<List<ActionInfo>>(ProtocolJson.Options)!;
    }

    private static async Task<(WorkspaceViewModel Workspace, StepFileViewModel Tab, FakeEngineService Engine)> OpenAsync(string text = Text)
    {
        var engine = new FakeEngineService { Tests = [Make.Test(Test, "A")], Actions = RealActions() };
        var files = new FakeProjectFiles();
        files.Files[Test] = text;
        files.Files["targets/shop.targets.yaml"] = "version: 1\ntargets:\n  cart.count:\n    - testId: cart-count\n  checkout.submit:\n    - css: '#go'\n";
        var shell = new ShellViewModel(engine, new MemorySettingsStore(), new FakeFolderPicker(Root), files, new ImmediateDispatcher(), p => p == Root, new FakeDialogs(), new ManualDelay());
        await shell.OpenProjectAsync(Root);
        shell.Workspace!.OpenFile(Test);
        return (shell.Workspace, (StepFileViewModel)shell.Workspace.SelectedTab!, engine);
    }

    private static StepFormViewModel Select(StepFileViewModel tab, int index)
    {
        tab.Steps!.SelectCommand.Execute(tab.Steps.Sections[1].Steps[index]);
        return tab.Steps.Form!;
    }

    private static StepFieldViewModel Field(StepFormViewModel form, string name) =>
        form.Parameters.Concat(form.Settings).Single(f => f.Name == name);

    [AvaloniaFact]
    public async Task The_form_has_the_action_s_parameters_and_the_step_s_settings()
    {
        var (_, tab, _) = await OpenAsync();

        var form = Select(tab, 1);

        Assert.Equal("click", form.Action);
        Assert.Equal(["target", "button", "clickCount", "modifiers"], form.Parameters.Select(f => f.Name));
        Assert.Equal((FieldKind.Target, "buyButton", "target *"), (Field(form, "target").Kind, Field(form, "target").Value, Field(form, "target").Label));
        Assert.Equal(["", "left", "right", "middle"], Field(form, "button").Choices);
        Assert.Equal(["name", "page", "timeout"], form.Settings.Select(f => f.Name));
        Assert.Equal("Buy it", Field(form, "name").Value);
        Assert.Equal(["buyButton", "cart.count", "checkout.submit"], Field(form, "target").TargetNames);
    }

    [AvaloniaFact]
    public async Task Changing_a_field_rewrites_only_the_step_and_keeps_its_form()
    {
        var (_, tab, _) = await OpenAsync();

        Field(Select(tab, 1), "target").Value = "checkout.submit";
        Assert.Equal(Text.Replace("  - click: buyButton\n", "  - click: checkout.submit\n", StringComparison.Ordinal), tab.Document.Text);

        Field(Select(tab, 2), "equals").Value = "2";
        Assert.Contains("  - expect.text: { target: cart.count, equals: '2' }\n", tab.Document.Text, StringComparison.Ordinal);
        Assert.Contains("  # keep me\n", tab.Document.Text, StringComparison.Ordinal);

        tab.UndoCommand.Execute(null);
        tab.UndoCommand.Execute(null);
        Assert.Equal(Text, tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task A_second_parameter_makes_the_long_form_and_clearing_it_removes_the_key()
    {
        var (_, tab, _) = await OpenAsync();

        Field(Select(tab, 1), "button").Value = "right";
        Assert.Contains("  - click:\n      target: buyButton\n      button: right\n    name: Buy it\n", tab.Document.Text, StringComparison.Ordinal);

        Field(Select(tab, 1), "button").Value = string.Empty;
        Assert.Contains("  - click:\n      target: buyButton\n    name: Buy it\n", tab.Document.Text, StringComparison.Ordinal);

        Field(Select(tab, 1), "name").Value = string.Empty;
        Field(Select(tab, 1), "timeout").Value = "20s";
        Assert.Contains("  - click:\n      target: buyButton\n    timeout: 20s\n", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [InlineData("clickCount", "two", "Enter a number")]
    [InlineData("target", "not a name", "A target name starts with a letter")]
    [InlineData("timeout", "soon", "A timeout is a number with ms, s or m")]
    [InlineData("modifiers", "[Shift", "not valid YAML")]
    public async Task A_value_that_does_not_fit_is_refused_and_nothing_is_written(string name, string value, string error)
    {
        var (_, tab, _) = await OpenAsync();
        var field = Field(Select(tab, 1), name);

        field.Value = value;

        Assert.Contains(error, field.Error, StringComparison.Ordinal);
        Assert.Equal(Text, tab.Document.Text);
    }

    [AvaloniaFact]
    public async Task An_inline_target_is_edited_as_yaml()
    {
        var (_, tab, _) = await OpenAsync();
        var form = Select(tab, 3);
        var target = Field(form, "target");

        Assert.Equal(FieldKind.Yaml, target.Kind);
        Assert.Equal("An inline target, edited as YAML.", target.Note);
        Assert.Equal("- role: button\n  name: Pay", target.Value);

        target.Value = "- role: button\n  name: Pay now";

        Assert.Contains("  - click:\n      - role: button\n        name: Pay now\n  - shop.addToCart", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task An_action_the_engine_does_not_know_keeps_its_value_as_yaml()
    {
        var (_, tab, _) = await OpenAsync();
        var form = Select(tab, 4);

        Assert.StartsWith("The engine does not know the action \"shop.addToCart\"", form.Notice, StringComparison.Ordinal);
        var value = Assert.Single(form.Parameters);
        value.Value = "Chair";

        Assert.EndsWith("  - shop.addToCart: Chair", tab.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_engine_s_problems_show_next_to_their_field()
    {
        var (workspace, tab, engine) = await OpenAsync();
        // Line 11 is "  - expect.text: { target: cart.count, equals: '1' }": column 30 is in "cart.count".
        engine.Validate = _ =>
        [
            Make.Error(Test, 11, "UnknownTarget", "Unknown target \"cart.count\".") with { Column = 30 },
            Make.Error(Test, 10, "InvalidName", "The name is too long.") with { Column = 5 },
            Make.Error(Test, 9, "MissingParameter", "click needs a target.") with { Column = 5 },
        ];
        await workspace.ValidateAsync();

        var expect = Select(tab, 2);
        Assert.Equal("Unknown target \"cart.count\".", Field(expect, "target").Problem);
        Assert.Null(Field(expect, "equals").Problem);

        var click = Select(tab, 1);
        Assert.Equal("The name is too long.", Field(click, "name").Problem);
        Assert.Equal("click needs a target.", Assert.Single(click.Problems));
    }
}
