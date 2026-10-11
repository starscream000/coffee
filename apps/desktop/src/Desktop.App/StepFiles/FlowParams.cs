// Reads the parameters a flow declares (its params key: a name, a type of
// string, number or boolean, and an optional default), for the fields a
// call step's form shows for its "with" (instruction D0004, task 20).

namespace Desktop.App.StepFiles;

/// <summary>The project's flows and how to read one's parameters, for a <c>call</c> step's form.</summary>
/// <param name="Flows">The flows, relative to the project root.</param>
/// <param name="ParamsOf">A flow's parameters by its path; null when it cannot be read.</param>
public sealed record FlowSource(IReadOnlyList<string> Flows, Func<string, IReadOnlyList<ParamField>?> ParamsOf);

/// <summary>Reads a flow's parameters.</summary>
public static class FlowParams
{
    /// <summary>The fields of a flow's declared parameters, in their order.</summary>
    /// <param name="text">The flow file's text; null when there is no such file.</param>
    /// <returns>The fields (none for a flow without params); null when the text is missing or not a readable mapping.</returns>
    /// <example><c>params: { email: { type: string }, express: { type: boolean, default: false } }</c> gives a required text field
    /// <c>email</c> and an optional yes/no field <c>express</c>.</example>
    public static IReadOnlyList<ParamField>? Read(string? text)
    {
        if (text is null)
        {
            return null;
        }

        YamlNode? root;
        try
        {
            root = YamlTree.Read(text);
        }
        catch (YamlTreeException)
        {
            return null;
        }

        if (root is not YamlMapping mapping)
        {
            return null;
        }

        if (mapping.Get("params") is not YamlMapping parameters)
        {
            return [];
        }

        return [.. parameters.Entries.Select(e => Field(e.Key.Value, e.Value))];
    }

    private static ParamField Field(string name, YamlNode declaration)
    {
        var spec = declaration as YamlMapping;
        var type = (spec?.Get("type") as YamlScalar)?.Value ?? (declaration as YamlScalar)?.Value ?? "string";
        var fallback = spec?.Get("default") as YamlScalar;
        var kind = type switch
        {
            "number" => FieldKind.Number,
            "boolean" => FieldKind.Boolean,
            _ => FieldKind.Text,
        };
        var description = fallback is null ? $"The flow's {type} parameter." : $"The flow's {type} parameter; {fallback.Value} when not given.";
        return new ParamField(name, kind, fallback is null, kind == FieldKind.Boolean ? ["true", "false"] : [], description);
    }
}
