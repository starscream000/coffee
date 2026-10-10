// Reading the engine's parameter schemas into form fields (instruction D0003,
// task 15), against a snapshot of what the engine's listActions returned
// (list-actions.json, from the engine of protocol 0.1.0).

using System.Text.Json;
using Desktop.App.StepFiles;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests.StepFiles;

public sealed class ParamSchemaTests
{
    private static readonly Dictionary<string, JsonElement> Schemas = Load();

    private static Dictionary<string, JsonElement> Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoPaths.Of("apps/desktop/tests/Desktop.App.Tests/StepFiles/list-actions.json")));
        return document.RootElement.GetProperty("actions").EnumerateArray()
            .ToDictionary(a => a.GetProperty("name").GetString()!, a => a.GetProperty("paramsSchema").Clone(), StringComparer.Ordinal);
    }

    private static Dictionary<string, ParamField> Fields(string action) => ParamSchema.Read(Schemas[action]).ToDictionary(f => f.Name, StringComparer.Ordinal);

    [Fact]
    public void Click_has_a_required_target_a_choice_a_number_and_a_list()
    {
        var click = Fields("click");

        Assert.Equal(["target", "button", "clickCount", "modifiers"], click.Keys);
        Assert.Equal((FieldKind.Target, true), (click["target"].Kind, click["target"].IsRequired));
        Assert.Equal(FieldKind.Choice, click["button"].Kind);
        Assert.Equal(["left", "right", "middle"], click["button"].Choices);
        Assert.Equal((FieldKind.Number, false), (click["clickCount"].Kind, click["clickCount"].IsRequired));
        Assert.Equal(FieldKind.Yaml, click["modifiers"].Kind);
    }

    [Fact]
    public void Text_numbers_yes_no_and_objects()
    {
        Assert.Equal(FieldKind.Text, Fields("goto")["url"].Kind);
        Assert.Equal(FieldKind.Text, Fields("expect.text")["equals"].Kind);
        Assert.Equal(FieldKind.Boolean, Fields("expect.text")["ignoreCase"].Kind);
        Assert.Equal(FieldKind.Boolean, Fields("check")["checked"].Kind);
        Assert.Equal(FieldKind.Number, Fields("wait.response")["status"].Kind);
        Assert.Equal(FieldKind.Choice, Fields("wait.element")["state"].Kind);
        Assert.Equal(FieldKind.Text, Fields("call")["flow"].Kind);
        Assert.Equal(FieldKind.Yaml, Fields("call")["with"].Kind);
        Assert.Equal(FieldKind.Yaml, Fields("select")["option"].Kind);
    }

    [Fact]
    public void Every_target_parameter_of_the_built_in_actions_is_found()
    {
        var targets = Schemas.Keys.SelectMany(a => ParamSchema.Read(Schemas[a]).Where(f => f.Kind == FieldKind.Target).Select(f => $"{a}.{f.Name}")).ToList();

        Assert.Contains("fill.target", targets);
        Assert.Contains("expect.visible.target", targets);
        Assert.Contains("extract.target", targets);
        Assert.DoesNotContain(targets, t => t.EndsWith(".url", StringComparison.Ordinal));
    }

    [Fact]
    public void A_schema_without_properties_has_no_fields()
    {
        Assert.Empty(ParamSchema.Read(JsonDocument.Parse("{}").RootElement));
        Assert.Empty(ParamSchema.Read(Schemas["back"]));
    }
}
