// Reads the protocol's committed JSON Schema files and example messages for
// the contract tests.

using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Desktop.Protocol.Tests;

/// <summary>The JSON Schema files of packages/protocol/schema/ and the protocol examples.</summary>
internal static class SchemaFiles
{
    private static readonly string SchemaDir = RepoPaths.Of("packages/protocol/schema");
    private static readonly string ExampleDir = RepoPaths.Of("packages/protocol/test/fixtures/protocol-examples");

    /// <summary>Every schema key (file name without <c>.json</c>), sorted.</summary>
    public static IReadOnlyList<string> Keys { get; } =
        [.. Directory.GetFiles(SchemaDir, "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).Cast<string>()];

    /// <summary>Every example file name, sorted.</summary>
    public static IReadOnlyList<string> Examples { get; } =
        [.. Directory.GetFiles(ExampleDir, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal).Cast<string>()];

    /// <summary>The schema document of a key, as a JSON node.</summary>
    public static JsonObject Document(string key) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(SchemaDir, key + ".json")))!.AsObject();

    /// <summary>The schema of a key, built for validation.</summary>
    public static JsonSchema Schema(string key) =>
        JsonSchema.FromText(
            File.ReadAllText(Path.Combine(SchemaDir, key + ".json")),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });

    /// <summary>An example message.</summary>
    public static JsonElement Example(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(ExampleDir, fileName))).RootElement.Clone();

    /// <summary>Validates a value against a schema and returns the errors, empty when valid.</summary>
    public static IReadOnlyList<string> Validate(string key, JsonElement value)
    {
        var results = Schema(key).Evaluate(value, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid)
        {
            return [];
        }

        return [.. (results.Details ?? []).Where(d => d.Errors is not null)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))];
    }
}
