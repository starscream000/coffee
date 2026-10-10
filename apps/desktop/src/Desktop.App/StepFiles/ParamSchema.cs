// Reads an action's parameter schema (listActions' paramsSchema, JSON Schema of
// the canonical long form) into the fields of a step's form. The protocol does
// not say which parameters are targets; a parameter whose schema allows an
// object with "candidates" is taken as one (request R0004 asks for a mark).

using System.Text.Json;

namespace Desktop.App.StepFiles;

/// <summary>How a parameter is edited in a step's form.</summary>
public enum FieldKind
{
    /// <summary>A line of text, written as a YAML string.</summary>
    Text,

    /// <summary>A number, or a <c>${…}</c>.</summary>
    Number,

    /// <summary>Yes, no, or not set.</summary>
    Boolean,

    /// <summary>One of a list of values.</summary>
    Choice,

    /// <summary>A target: a name picked from the file's and the shared targets.</summary>
    Target,

    /// <summary>Anything else, edited as YAML text.</summary>
    Yaml,
}

/// <summary>One parameter of an action, as a form shows it.</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="Kind">How it is edited.</param>
/// <param name="IsRequired">True when the long form must give it.</param>
/// <param name="Choices">The values a <see cref="FieldKind.Choice"/> offers.</param>
/// <param name="Description">The schema's description, if any.</param>
public sealed record ParamField(string Name, FieldKind Kind, bool IsRequired, IReadOnlyList<string> Choices, string? Description);

/// <summary>Reads parameter schemas.</summary>
public static class ParamSchema
{
    /// <summary>The fields of an action's parameters, in the schema's order.</summary>
    /// <param name="schema">The action's <c>paramsSchema</c>.</param>
    /// <returns>The fields; empty for a schema without properties (such as <c>{}</c>).</returns>
    public static IReadOnlyList<ParamField> Read(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var required = schema.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];
        return [.. properties.EnumerateObject().Select(p =>
        {
            var resolved = Resolve(schema, p.Value);
            var (kind, choices) = KindOf(schema, resolved);
            var description = resolved.ValueKind == JsonValueKind.Object && resolved.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            return new ParamField(p.Name, kind, required.Contains(p.Name), choices, description);
        })];
    }

    private static (FieldKind Kind, IReadOnlyList<string> Choices) KindOf(JsonElement root, JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return (FieldKind.Yaml, []);
        }

        if (schema.TryGetProperty("enum", out var values) && values.ValueKind == JsonValueKind.Array && values.EnumerateArray().All(v => v.ValueKind == JsonValueKind.String))
        {
            return (FieldKind.Choice, [.. values.EnumerateArray().Select(v => v.GetString()!)]);
        }

        if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
        {
            var branches = anyOf.EnumerateArray().Select(b => Resolve(root, b)).ToList();
            if (branches.Any(IsTargetObject))
            {
                return (FieldKind.Target, []);
            }

            var types = branches.Select(TypeOf).ToList();
            if (types.Contains("boolean") && types.All(t => t is "boolean" or "string"))
            {
                // true, false, or a ${…} that gives one.
                return (FieldKind.Boolean, ["true", "false"]);
            }

            if (types.Contains("integer") || types.Contains("number"))
            {
                return types.All(t => t is "integer" or "number" or "string") ? (FieldKind.Number, []) : (FieldKind.Yaml, []);
            }

            return types.All(t => t is "string" or "boolean") ? (FieldKind.Text, []) : (FieldKind.Yaml, []);
        }

        if (schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Array)
        {
            var list = type.EnumerateArray().Select(t => t.GetString()).ToList();
            return list.All(t => t is "string" or "number" or "integer" or "boolean") ? (FieldKind.Text, []) : (FieldKind.Yaml, []);
        }

        return TypeOf(schema) switch
        {
            "string" => (FieldKind.Text, []),
            "integer" or "number" => (FieldKind.Number, []),
            "boolean" => (FieldKind.Boolean, ["true", "false"]),
            _ => (FieldKind.Yaml, []),
        };
    }

    private static string? TypeOf(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

    private static bool IsTargetObject(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object
        && schema.TryGetProperty("properties", out var p)
        && p.ValueKind == JsonValueKind.Object
        && p.TryGetProperty("candidates", out _);

    /// <summary>Follows a local <c>$ref</c> such as <c>#/$defs/__schema0</c>.</summary>
    private static JsonElement Resolve(JsonElement root, JsonElement schema)
    {
        for (var depth = 0; depth < 8 && schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$ref", out var reference) && reference.GetString() is { } path && path.StartsWith("#/", StringComparison.Ordinal); depth++)
        {
            var target = root;
            foreach (var part in path[2..].Split('/'))
            {
                if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty(part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal), out target))
                {
                    return default;
                }
            }

            schema = target;
        }

        return schema;
    }
}
