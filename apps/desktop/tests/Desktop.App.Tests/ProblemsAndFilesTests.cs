// Tests of the problems panel, the step file view and the action catalogue.

using System.Text.Json;
using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests;

public sealed class ProblemsAndFilesTests
{
    [Fact]
    public void Problems_merge_both_sources_sorted_without_duplicates()
    {
        var problems = new ProblemsViewModel();
        problems.SetProjectDiagnostics([Make.Error("cfe.config.yaml", 4, "UnknownKey")]);
        problems.SetValidationDiagnostics([Make.Warning("b.test.yaml", 2), Make.Error("a.test.yaml", 9), Make.Error("a.test.yaml", 3), Make.Error("a.test.yaml", 3)]);

        Assert.Equal(["a.test.yaml:3:3", "a.test.yaml:9:3", "b.test.yaml:2:1", "cfe.config.yaml:4:3"], problems.Items.Select(i => i.Location));
        Assert.Equal("3 errors, 1 warning", problems.Summary);
        Assert.Equal((2, 0), problems.CountsByFile()["a.test.yaml"]);
    }

    [Fact]
    public void Problems_can_hide_warnings_but_still_count_them()
    {
        var problems = new ProblemsViewModel();
        problems.SetValidationDiagnostics([Make.Warning("b.test.yaml", 2), Make.Error("a.test.yaml", 9)]);
        problems.ShowWarnings = false;
        Assert.Single(problems.Items);
        Assert.Equal(1, problems.WarningCount);
    }

    [Fact]
    public void Problem_text_includes_the_hint()
    {
        var item = new ProblemItemViewModel(Make.Error("a.test.yaml", 1) with { Hint = "Did you mean \"click\"?" });
        Assert.Equal($"Unknown action.{Environment.NewLine}Did you mean \"click\"?", item.Text);
    }

    [Fact]
    public void Step_file_marks_lines_with_problems_including_ranges()
    {
        var file = new StepFileViewModel("tests/a.test.yaml");
        file.SetText("version: 1\r\nname: A\nsteps:\n  - clik: x\n");
        file.ApplyDiagnostics([
            Make.Error("tests/a.test.yaml", 4),
            Make.Warning("tests/a.test.yaml", 2) with { EndLine = 3 },
            Make.Error("tests/other.test.yaml", 1),
        ]);

        Assert.Equal(4, file.Lines.Count);
        Assert.Equal([LineMark.None, LineMark.Warning, LineMark.Warning, LineMark.Error], file.Lines.Select(l => l.Mark));
        Assert.Equal("UnknownAction: Unknown action.", file.Lines[3].Messages);
        Assert.Equal("1 error, 1 warning", file.ProblemSummary);
        Assert.Equal("a.test.yaml", file.Title);
    }

    [Fact]
    public void Step_file_keeps_marks_when_its_text_is_reloaded_and_reveals_clamped_lines()
    {
        var file = new StepFileViewModel("a.test.yaml");
        file.SetText("a\nb\n");
        file.ApplyDiagnostics([Make.Error("a.test.yaml", 2)]);
        file.SetText("a\nb\nc\n");
        Assert.True(file.Lines[1].IsError);
        file.Reveal(99);
        Assert.Same(file.Lines[2], file.RevealedLine);
    }

    [Fact]
    public void Action_catalogue_reads_parameters_from_the_schema_and_searches()
    {
        var schema = JsonDocument.Parse("""
            {"type":"object","properties":{
              "target":{"anyOf":[{"type":"string"},{"$ref":"#/$defs/Target"}],"description":"What to fill."},
              "value":{"type":"string"},
              "mode":{"enum":["a","b"]}},
             "required":["target","value"]}
            """).RootElement.Clone();
        var catalogue = new ActionCatalogViewModel();
        catalogue.Load([
            new ActionInfo { Name = "fill", Description = "Clears a field and types a value.", Shorthand = null, ParamsSchema = schema, Source = new ActionSource { Kind = ActionSourceKind.Builtin } },
            new ActionInfo { Name = "demo.addTodo", Description = "Adds a to-do.", Shorthand = "title", ParamsSchema = JsonDocument.Parse("{}").RootElement.Clone(), Source = new ActionSource { Kind = ActionSourceKind.File, File = "actions/demo.ts" } },
        ]);

        Assert.Equal("1 built-in, 1 user action", catalogue.Summary);
        var fill = catalogue.Items[0];
        Assert.Same(fill, catalogue.Selected);
        Assert.Equal(
            [("target", "string | target", true), ("value", "string", true), ("mode", "a | b", false)],
            fill.Parameters.Select(p => (p.Name, p.Type, p.IsRequired)));
        Assert.Equal("What to fill.", fill.Parameters[0].Description);
        Assert.Equal("built-in", fill.SourceText);
        Assert.Equal("actions/demo.ts", catalogue.Items[1].SourceText);
        Assert.Contains("\"title\"", catalogue.Items[1].ShorthandText, StringComparison.Ordinal);
        Assert.False(catalogue.CanClose);

        catalogue.SearchText = "to-do";
        Assert.Equal(["demo.addTodo"], catalogue.Items.Select(i => i.Name));
    }
}
