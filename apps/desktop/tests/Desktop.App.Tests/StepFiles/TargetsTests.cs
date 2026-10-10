// Reading and writing targets for the targets editor (instruction D0003,
// task 18): both forms of a target, each target's lines, and changes that
// leave every other line of the file as it was, comments included.

using Desktop.App.StepFiles;

namespace Desktop.App.Tests.StepFiles;

public sealed class TargetsTests
{
    internal const string Shared = """
        # Shared targets of the shop.
        version: 1
        targets:
          checkout:
            - role: button
              name: Check out
            - testId: checkout   # stable
          # The payment form sits in a frame.
          paymentFrame:
            - css: 'iframe#pay'
          cardNumber:
            frame: paymentFrame
            candidates:
              - label: Card number
              - { css: '#card', nth: 0 }
        """;

    [Fact]
    public void Reads_both_forms_and_each_target_s_lines()
    {
        var outline = TargetsOutline.Read(Shared);

        Assert.True(outline.IsReadable, outline.Problem);
        Assert.Equal(["checkout", "paymentFrame", "cardNumber"], outline.Targets.Select(t => t.Name));
        var checkout = outline.Targets[0];
        Assert.False(checkout.IsLongForm);
        Assert.Equal(2, checkout.Candidates.Count);
        Assert.Equal("  checkout:\n    - role: button\n      name: Check out\n    - testId: checkout   # stable\n", Shared[checkout.Start..checkout.End]);
        var card = outline.Targets[2];
        Assert.True(card.IsLongForm);
        Assert.Equal("paymentFrame", ((YamlScalar)card.Frame!).Value);
        Assert.Null(card.Within);
        Assert.Equal((11, 15), (card.Line, card.EndLine));
        Assert.All(outline.Targets, t => Assert.Null(t.Problem));
    }

    [Theory]
    [InlineData("version: 1\nname: A\nsteps:\n  - back\n", 0)]
    [InlineData("version: 1\ntargets: {}\n", 0)]
    [InlineData("version: 1\ntargets:\n", 0)]
    public void A_file_without_targets_has_none(string text, int count)
    {
        var outline = TargetsOutline.Read(text);

        Assert.True(outline.IsReadable);
        Assert.Equal(count, outline.Targets.Count);
    }

    [Fact]
    public void What_the_editor_cannot_show_says_why()
    {
        Assert.Contains("not written one target per line", TargetsOutline.Read("targets: { a: [ { css: x } ] }\n").Problem, StringComparison.Ordinal);
        Assert.Equal("It has a key the editor does not know: \"candidate\".", TargetsOutline.Read("targets:\n  a:\n    candidate:\n      - css: x\n").Targets[0].Problem);
        Assert.Equal("A candidate is not a mapping such as \"- css: '#buy'\".", TargetsOutline.Read("targets:\n  a:\n    - x\n").Targets[0].Problem);
    }

    [Fact]
    public void Writing_a_target_in_the_short_and_the_long_form()
    {
        CandidateValue[] candidates = [new("role", "button", "Pay now", false, null), new("css", "#pay", null, null, "1")];

        Assert.Equal("pay:\n  - role: button\n    name: Pay now\n    exact: false\n  - css: '#pay'\n    nth: 1", TargetWriter.Write("pay", null, null, candidates));
        Assert.Equal("pay:\n  frame: paymentFrame\n  candidates:\n    - role: button\n      name: Pay now\n      exact: false\n    - css: '#pay'\n      nth: 1", TargetWriter.Write("pay", "paymentFrame", null, candidates));
        Assert.Equal("empty: []", TargetWriter.Write("empty", null, null, []));
    }

    [Fact]
    public void Unchanged_candidates_keep_their_text()
    {
        var outline = TargetsOutline.Read(Shared);
        var card = outline.Targets[2];
        var raw = TargetWriter.CandidateTexts(outline, card).Select(t => new CandidateValue("?", "?", null, null, null, t)).ToList();

        var text = TargetWriter.Replace(outline, card, TargetWriter.Write("cardNumber", "paymentFrame", null, raw));

        Assert.Equal(Shared, text);
    }

    [Fact]
    public void Replacing_one_target_changes_only_its_lines()
    {
        var outline = TargetsOutline.Read(Shared);

        var text = TargetWriter.Replace(outline, outline.Targets[1], TargetWriter.Write("paymentFrame", null, null, [new("css", "iframe#payment", null, null, null)]));

        Assert.Equal(Shared.Replace("    - css: 'iframe#pay'\n", "    - css: iframe#payment\n", StringComparison.Ordinal), text);
    }

    [Fact]
    public void Adding_and_removing_targets()
    {
        var outline = TargetsOutline.Read(Shared);

        var added = TargetWriter.Add(outline, "buy:\n  - testId: buy");
        Assert.EndsWith("      - { css: '#card', nth: 0 }\n  buy:\n    - testId: buy\n", added, StringComparison.Ordinal);

        var removed = TargetWriter.Remove(outline, outline.Targets[0]);
        Assert.StartsWith("# Shared targets of the shop.\nversion: 1\ntargets:\n  # The payment form", removed, StringComparison.Ordinal);

        var single = TargetsOutline.Read("version: 1\ntargets:\n  a:\n    - css: x\n");
        Assert.Equal("version: 1\ntargets: {}\n", TargetWriter.Remove(single, single.Targets[0]));
    }

    [Theory]
    [InlineData("version: 1\ntargets: {}\n", "version: 1\ntargets:\n  buy:\n    - testId: buy\n")]
    [InlineData("version: 1\nname: A\nsteps:\n  - back\n", "version: 1\nname: A\ntargets:\n  buy:\n    - testId: buy\nsteps:\n  - back\n")]
    [InlineData("version: 1\nname: F", "version: 1\nname: F\ntargets:\n  buy:\n    - testId: buy\n")]
    public void Adding_the_first_target(string text, string expected)
    {
        Assert.Equal(expected, TargetWriter.Add(TargetsOutline.Read(text), "buy:\n  - testId: buy"));
    }
}
