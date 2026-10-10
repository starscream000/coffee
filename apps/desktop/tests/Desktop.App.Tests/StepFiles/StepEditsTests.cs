// Changes made from the step list (instruction D0003, tasks 14 and 16):
// every other line of the file stays as it was, comments included.

using Desktop.App.StepFiles;

namespace Desktop.App.Tests.StepFiles;

public sealed class StepEditsTests
{
    private const string File = "tests/a.test.yaml";

    private static StepOutline Read(string text) => StepOutline.Read(File, text);

    private static StepOutlineItem Step(StepOutline outline, string section, int index) =>
        outline.Sections.Single(s => s.Name == section).Steps[index];

    /// <summary>Lines of <paramref name="after"/> that are not in <paramref name="before"/> and the other way round.</summary>
    private static (string[] Removed, string[] Added) Diff(string before, string after)
    {
        var a = before.Split('\n');
        var b = after.Split('\n');
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[^(suffix + 1)] == b[^(suffix + 1)])
        {
            suffix++;
        }

        return (a[prefix..(a.Length - suffix)], b[prefix..(b.Length - suffix)]);
    }

    [Fact]
    public void Removing_a_step_removes_only_its_lines()
    {
        var outline = Read(StepOutlineTests.Sample);

        var text = StepEdits.Remove(outline, Step(outline, "steps", 3));

        var (removed, added) = Diff(StepOutlineTests.Sample, text);
        Assert.Equal(["  - click: order.print", "    opens: receipt", "    name: Print the receipt"], removed);
        Assert.Empty(added);
        Assert.Contains("  # a comment between steps", text, StringComparison.Ordinal);
        Assert.Equal(4, Read(text).Sections[1].Steps.Count);
    }

    [Fact]
    public void Removing_the_last_step_of_a_section_leaves_an_empty_list()
    {
        var outline = Read(StepOutlineTests.Sample);

        var text = StepEdits.Remove(outline, Step(outline, "after", 0));

        Assert.EndsWith("after: []", text, StringComparison.Ordinal);
        Assert.Empty(Read(text).Sections[2].Steps);
    }

    [Fact]
    public void Duplicating_inserts_a_copy_after_the_step()
    {
        var outline = Read(StepOutlineTests.Sample);

        var text = StepEdits.Duplicate(outline, Step(outline, "steps", 0));

        var (removed, added) = Diff(StepOutlineTests.Sample, text);
        Assert.Empty(removed);
        Assert.Equal(["  - goto: /products   # the list"], added);
        Assert.Equal(["goto", "goto", "back"], Read(text).Sections[1].Steps.Take(3).Select(s => s.Action));
    }

    [Fact]
    public void Moving_swaps_two_steps_and_keeps_what_is_between_them()
    {
        var outline = Read(StepOutlineTests.Sample);

        var down = StepEdits.Move(outline, Step(outline, "steps", 1), +1);
        var up = StepEdits.Move(Read(down), Step(Read(down), "steps", 2), -1);

        Assert.Equal(["goto", "fill", "back", "click", "expect.text"], Read(down).Sections[1].Steps.Select(s => s.Action));
        Assert.Contains("  # a comment between steps\n  - back\n", down, StringComparison.Ordinal);
        Assert.Equal(StepOutlineTests.Sample, up);
        Assert.Equal(StepOutlineTests.Sample, StepEdits.Move(outline, Step(outline, "steps", 0), -1));
    }

    [Fact]
    public void Moving_the_last_step_of_a_file_without_a_final_line_break()
    {
        var text = "steps:\n  - back\n  - reload";
        var outline = Read(text);

        var moved = StepEdits.Move(outline, Step(outline, "steps", 1), -1);

        Assert.Equal("steps:\n  - reload\n  - back", moved);
    }

    [Fact]
    public void Moving_to_another_section_reindents_the_step()
    {
        var text = "version: 1\nsteps:\n    - goto: /x\n    - fill:\n        target: email\n        value: a\nafter:\n  - back\n";
        var outline = Read(text);

        var moved = StepEdits.MoveToSection(outline, Step(outline, "steps", 1), "after");

        Assert.Equal("version: 1\nsteps:\n    - goto: /x\nafter:\n  - back\n  - fill:\n      target: email\n      value: a\n", moved);
    }

    [Fact]
    public void Moving_to_a_section_the_file_lacks_creates_it_in_order()
    {
        var text = "version: 1\nname: A\nsteps:\n  - goto: /x\n  - back\n# end\n";
        var outline = Read(text);

        var before = StepEdits.MoveToSection(outline, Step(outline, "steps", 0), "before");
        var after = StepEdits.MoveToSection(outline, Step(outline, "steps", 1), "after");

        Assert.Equal("version: 1\nname: A\nbefore:\n  - goto: /x\nsteps:\n  - back\n# end\n", before);
        Assert.Equal("version: 1\nname: A\nsteps:\n  - goto: /x\nafter:\n  - back\n# end\n", after);
    }

    [Theory]
    [InlineData("version: 1\nsteps: []\nafter:\n", "steps", "version: 1\nsteps:\n  - back\nafter:\n")]
    [InlineData("version: 1\nsteps:\nafter: []\n", "after", "version: 1\nsteps:\nafter:\n  - back\n")]
    [InlineData("version: 1\nname: A", "steps", "version: 1\nname: A\nsteps:\n  - back\n")]
    public void Inserting_into_an_empty_or_missing_section(string text, string section, string expected)
    {
        Assert.Equal(expected, StepEdits.Insert(Read(text), section, null, "- back"));
    }

    [Fact]
    public void Inserting_after_a_step_at_the_start_or_at_the_end()
    {
        var text = "steps:\n  - goto: /x\n  - back\n";
        var outline = Read(text);

        Assert.Equal("steps:\n  - goto: /x\n  - fill:\n      target: e\n  - back\n", StepEdits.Insert(outline, "steps", 0, "- fill:\n    target: e"));
        Assert.Equal("steps:\n  - reload\n  - goto: /x\n  - back\n", StepEdits.Insert(outline, "steps", -1, "- reload"));
        Assert.Equal("steps:\n  - goto: /x\n  - back\n  - reload\n", StepEdits.Insert(outline, "steps", null, "- reload"));
    }

    [Fact]
    public void Windows_line_breaks_stay_windows_line_breaks()
    {
        var text = "version: 1\r\nsteps:\r\n  - back\r\n";
        var outline = Read(text);

        Assert.Equal("version: 1\r\nsteps:\r\n  - back\r\n  - fill:\r\n      target: e\r\n", StepEdits.Insert(outline, "steps", null, "- fill:\n    target: e"));
        Assert.Equal("version: 1\r\nsteps:\r\n  - back\r\n  - back\r\n", StepEdits.Duplicate(outline, Step(outline, "steps", 0)));
        Assert.Equal("version: 1\r\nsteps: []\r\n", StepEdits.Remove(outline, Step(outline, "steps", 0)));
    }

    [Fact]
    public void Text_of_a_step_has_no_indentation()
    {
        var outline = Read(StepOutlineTests.Sample);

        Assert.Equal("- expect.text:\n    target: receipt.total\n    equals: '€20.00'\n  page: receipt", StepEdits.TextOf(outline, Step(outline, "steps", 4)));
    }
}
