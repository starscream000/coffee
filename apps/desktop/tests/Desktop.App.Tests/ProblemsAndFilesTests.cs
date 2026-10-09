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
