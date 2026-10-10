// Writing one step from its form's values (instruction D0003, task 15): the
// form the step was written in is kept where it can be, values not changed
// keep their text, and strings are quoted only when they must be.

using Desktop.App.StepFiles;

namespace Desktop.App.Tests.StepFiles;

public sealed class StepWriterTests
{
    private static StepOutlineItem Step(string text, int index = 0) => StepOutline.Read("tests/a.test.yaml", text).Sections[1].Steps[index];

    [Fact]
    public void Shorthand_stays_shorthand_and_more_parameters_make_the_long_form()
    {
        var was = Step("steps:\n  - click: buy\n");

        Assert.Equal("- click: pay", StepWriter.Write("click", was, "target", [new("target", "pay")], []));
        Assert.Equal("- click:\n    target: pay\n    button: right", StepWriter.Write("click", was, "target", [new("target", "pay"), new("button", "right")], []));
    }

    [Fact]
    public void A_shorthand_list_stays_shorthand()
    {
        var was = Step("steps:\n  - click:\n      - role: button\n        name: Buy\n");

        Assert.Equal("- click:\n    - role: button\n      name: Pay", StepWriter.Write("click", was, "target", [new("target", "- role: button\n  name: Pay")], []));
    }

    [Fact]
    public void A_long_form_stays_long_and_a_flow_mapping_stays_a_flow_mapping()
    {
        var block = Step("steps:\n  - click:\n      target: buy\n");
        var flow = Step("steps:\n  - fill: { target: email, value: a }\n");

        Assert.Equal("- click:\n    target: pay", StepWriter.Write("click", block, "target", [new("target", "pay")], []));
        Assert.Equal("- fill: { target: email, value: b }", StepWriter.Write("fill", flow, null, [new("target", "email"), new("value", "b")], []));
    }

    [Fact]
    public void No_parameters_is_the_action_alone_and_unset_ones_are_left_out()
    {
        Assert.Equal("- back", StepWriter.Write("back", null, null, [], []));
        Assert.Equal("- goto: /x", StepWriter.Write("goto", null, "url", [new("url", "/x"), new("waitUntil", null)], []));
    }

    [Fact]
    public void Settings_follow_the_parameters()
    {
        var text = StepWriter.Write("click", Step("steps:\n  - click: buy\n"), "target", [new("target", "buy")], [new("name", "Buy it"), new("page", null), new("timeout", "20s")]);

        Assert.Equal("- click: buy\n  name: Buy it\n  timeout: 20s", text);
    }

    [Fact]
    public void Collections_and_block_scalars_go_below_their_key()
    {
        var text = StepWriter.Write("api", null, null, [new("headers", "Authorization: x\nAccept: y"), new("body", "|\nline 1\nline 2")], []);

        Assert.Equal("- api:\n    headers:\n      Authorization: x\n      Accept: y\n    body: |\n      line 1\n      line 2", text);
    }

    [Fact]
    public void Raw_gives_a_value_s_original_text_without_its_indentation()
    {
        const string File = "steps:\n  - click:\n      target:\n        - role: button\n          name: Save\n      modifiers: [Shift]\n      clickCount: '${vars.n}'\n";
        var step = Step(File);
        var mapping = (YamlMapping)step.Value!;

        Assert.Equal("- role: button\n  name: Save", StepWriter.Raw(File, mapping.Get("target")!));
        Assert.Equal("[Shift]", StepWriter.Raw(File, mapping.Get("modifiers")!));
        Assert.Equal("'${vars.n}'", StepWriter.Raw(File, mapping.Get("clickCount")!));

        const string Scalar = "steps:\n  - api:\n      body: |\n        line 1\n          line 2\n";
        Assert.Equal("|\nline 1\n  line 2", StepWriter.Raw(Scalar, ((YamlMapping)Step(Scalar).Value!).Get("body")!));
    }

    [Theory]
    [InlineData("/products", "/products")]
    [InlineData("Buy milk", "Buy milk")]
    [InlineData("€20.00", "€20.00")]
    [InlineData("${vars.email}", "${vars.email}")]
    [InlineData("2", "'2'")]
    [InlineData("true", "'true'")]
    [InlineData("no", "'no'")]
    [InlineData("null", "'null'")]
    [InlineData("", "''")]
    [InlineData("a: b", "'a: b'")]
    [InlineData("#tag", "'#tag'")]
    [InlineData("**/api/*", "'**/api/*'")]
    [InlineData("it's", "it's")]
    [InlineData("'quoted'", "'''quoted'''")]
    [InlineData(" padded", "' padded'")]
    public void Strings_are_quoted_only_when_they_must_be(string value, string expected)
    {
        Assert.Equal(expected, StepWriter.Text(value));
    }

    [Theory]
    [InlineData("3", "3")]
    [InlineData(" 2.5 ", "2.5")]
    [InlineData("${vars.count}", "${vars.count}")]
    [InlineData("three", null)]
    [InlineData("", null)]
    public void Numbers_are_written_plain_and_anything_else_is_refused(string value, string? expected)
    {
        Assert.Equal(expected, StepWriter.Number(value));
    }

    [Fact]
    public void Replacing_a_step_changes_only_its_lines()
    {
        var outline = StepOutline.Read("tests/a.test.yaml", StepOutlineTests.Sample);
        var step = outline.Sections[1].Steps[3];

        var text = StepEdits.Replace(outline, step, "- click: order.pay");

        Assert.Equal(StepOutlineTests.Sample.Replace("  - click: order.print\n    opens: receipt\n    name: Print the receipt\n", "  - click: order.pay\n", StringComparison.Ordinal), text);
    }
}
