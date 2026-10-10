// Reading test and flow files as sections of steps (instruction D0003, task
// 14): the three ways to write a step, each step's lines, and the files the
// step list cannot show, with the reason.

using Desktop.App.StepFiles;

namespace Desktop.App.Tests.StepFiles;

public sealed class StepOutlineTests
{
    internal const string Sample = """
        # A test with every kind of step.
        version: 1
        name: Guest checks out
        before:
          - mock:
              url: '**/api/recommendations'
              json: []
        steps:
          - goto: /products   # the list
          - back
          # a comment between steps
          - fill: { target: checkout.email, value: guest@example.com }
          - click: order.print
            opens: receipt
            name: Print the receipt
          - expect.text:
              target: receipt.total
              equals: '€20.00'
            page: receipt
        after:
          - api:
              method: DELETE
              url: /orders
        """;

    [Fact]
    public void Reads_the_sections_and_the_three_forms_of_a_step()
    {
        var outline = StepOutline.Read("tests/a.test.yaml", Sample);

        Assert.True(outline.IsReadable, outline.Problem);
        Assert.Equal(["before", "steps", "after"], outline.Sections.Select(s => s.Name));
        var steps = outline.Sections[1].Steps;
        Assert.Equal(["goto", "back", "fill", "click", "expect.text"], steps.Select(s => s.Action));
        Assert.Equal([StepForm.Shorthand, StepForm.Bare, StepForm.LongForm, StepForm.Shorthand, StepForm.LongForm], steps.Select(s => s.Form));
        Assert.Equal("/products", ((YamlScalar)steps[0].Value!).Value);
        Assert.Equal(["opens", "name"], steps[3].Settings.Select(s => s.Key.Value));
        Assert.Equal("page", Assert.Single(steps[4].Settings).Key.Value);
        Assert.Equal("mock", Assert.Single(outline.Sections[0].Steps).Action);
    }

    [Fact]
    public void Knows_each_step_s_lines()
    {
        var outline = StepOutline.Read("tests/a.test.yaml", Sample);
        var steps = outline.Sections[1].Steps;

        Assert.Equal("  - goto: /products   # the list\n", Sample[steps[0].Start..steps[0].End]);
        Assert.Equal("  - click: order.print\n    opens: receipt\n    name: Print the receipt\n", Sample[steps[3].Start..steps[3].End]);
        Assert.Equal((13, 15), (steps[3].Line, steps[3].EndLine));
        Assert.Equal(2, steps[3].Indent);
        Assert.Equal(Sample.Length, outline.Sections[2].Steps[0].End);
        var fill = (YamlMapping)steps[2].Value!;
        Assert.Equal("{ target: checkout.email, value: guest@example.com }", Sample[fill.Start..fill.End]);
        Assert.True(fill.IsFlow);
    }

    [Fact]
    public void A_flow_has_only_steps_and_missing_sections_are_empty()
    {
        var flow = StepOutline.Read("flows/login.flow.yaml", "version: 1\nname: Log in\nsteps:\n  - back\n");
        var test = StepOutline.Read("tests/b.test.yaml", "version: 1\nname: B\nsteps: []\nafter:\n");

        Assert.Equal(["steps"], flow.Sections.Select(s => s.Name));
        Assert.All(test.Sections, s => Assert.Empty(s.Steps));
        Assert.Null(test.Sections[0].Key);
        Assert.NotNull(test.Sections[2].Key);
    }

    [Theory]
    [InlineData("tests/a.test.yaml", "version: 1\nsteps: [back, reload]\n", "\"steps\" is not a list of steps written one per line")]
    [InlineData("tests/a.test.yaml", "version: 1\nsteps:\n  - goto: [\n", "Line ")]
    [InlineData("tests/a.test.yaml", "- back\n", "not a mapping of keys")]
    [InlineData("tests/a.test.yaml", "", "The file is empty.")]
    [InlineData("tests/a.test.yaml", "x: &a 1\ny: *a\n", "anchor")]
    [InlineData("targets/shop.targets.yaml", "version: 1\ntargets: {}\n", "Only test and flow files have steps.")]
    [InlineData("tests/a.test.yaml", "a: 1\n---\nb: 2\n", "more than one YAML document")]
    public void A_file_it_cannot_show_says_why(string file, string text, string reason)
    {
        var outline = StepOutline.Read(file, text);

        Assert.False(outline.IsReadable);
        Assert.Contains(reason, outline.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_step_with_two_actions_is_listed_as_unreadable()
    {
        var outline = StepOutline.Read("tests/a.test.yaml", "steps:\n  - click: a\n    fill: b\n  - back\n");

        Assert.True(outline.IsReadable);
        Assert.Equal(StepForm.Unreadable, outline.Sections[1].Steps[0].Form);
        Assert.Equal("back", outline.Sections[1].Steps[1].Action);
    }

    [Fact]
    public void Windows_line_breaks_are_kept_and_offsets_fit_the_text()
    {
        var text = "version: 1\r\nsteps:\r\n  - back\r\n  - goto: /x\r\n";
        var outline = StepOutline.Read("tests/a.test.yaml", text);

        Assert.Equal("\r\n", outline.NewLine);
        var step = outline.Sections[1].Steps[1];
        Assert.Equal("  - goto: /x\r\n", text[step.Start..step.End]);
        Assert.Equal(4, step.Line);
    }
}
