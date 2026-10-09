// The contract tests of ADR D0003: the hand-written C# protocol types must equal
// the JSON Schema files generated from the engine's Zod schemas, and must read
// and write the protocol's example messages.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.Protocol.Tests;

public sealed class ContractTests
{
    public static TheoryData<string> SchemaKeys => [.. SchemaFiles.Keys];

    public static TheoryData<string> ExampleFiles => [.. SchemaFiles.Examples];

    [Fact]
    public void Every_schema_file_has_a_CSharp_type_and_every_type_a_schema_file()
    {
        Assert.Equal(SchemaFiles.Keys, ProtocolTypes.BySchemaKey.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_request_method_has_params_and_result_schemas()
    {
        foreach (var method in Methods.All)
        {
            Assert.Contains($"request.{method}.params", SchemaFiles.Keys);
            Assert.Contains($"request.{method}.result", SchemaFiles.Keys);
        }
    }

    [Theory]
    [MemberData(nameof(SchemaKeys))]
    public void Schema_title_names_this_clients_protocol_version(string key)
    {
        var title = SchemaFiles.Document(key)["title"]!.GetValue<string>();
        Assert.Equal($"{key} (protocol {ProtocolVersion.Current})", title);
    }

    [Theory]
    [MemberData(nameof(SchemaKeys))]
    public void CSharp_type_has_the_schemas_properties_with_matching_optionality(string key)
    {
        var document = SchemaFiles.Document(key);
        var defs = document["$defs"]?.AsObject();
        var problems = new List<string>();
        CompareObject(key, Resolve(document, defs), ProtocolTypes.BySchemaKey[key], defs, problems, depth: 0);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void Example_reads_into_its_CSharp_type_and_writes_back_valid(string fileName)
    {
        var key = Path.GetFileNameWithoutExtension(fileName).Split("--")[0];
        var example = SchemaFiles.Example(fileName);
        var type = ProtocolTypes.BySchemaKey[key];

        if (type == typeof(NullResult) || type == typeof(EmptyParams))
        {
            Assert.Empty(SchemaFiles.Validate(key, example));
            return;
        }

        var value = example.Deserialize(type, ProtocolJson.Options);
        Assert.NotNull(value);
        var written = JsonSerializer.SerializeToElement(value, type, ProtocolJson.Options);
        Assert.Empty(SchemaFiles.Validate(key, written));

        if (value is JsonRpcErrorResponse error)
        {
            var name = error.Error.Name!;
            var data = error.Error.Data!.Value;
            var dataType = ProtocolTypes.BySchemaKey[$"error-data.{name}"];
            var dataWritten = JsonSerializer.SerializeToElement(data.Deserialize(dataType, ProtocolJson.Options), dataType, ProtocolJson.Options);
            Assert.Empty(SchemaFiles.Validate($"error-data.{name}", dataWritten));
        }

        if (value is JsonRpcNotification notification)
        {
            var engineEvent = EngineEvents.Read(notification.Method, notification.Params!.Value);
            Assert.IsNotType<UnknownEngineEvent>(engineEvent);
            var eventWritten = JsonSerializer.SerializeToElement(engineEvent, engineEvent.GetType(), ProtocolJson.Options);
            Assert.Empty(SchemaFiles.Validate($"event.{notification.Method}", eventWritten));
        }
    }

    private static JsonObject Resolve(JsonNode node, JsonObject? defs)
    {
        var obj = node.AsObject();
        if (obj["$ref"]?.GetValue<string>() is { } reference)
        {
            var name = reference.Replace("#/$defs/", string.Empty, StringComparison.Ordinal);
            return defs![name]!.AsObject();
        }

        return obj;
    }

    private sealed record SchemaProperty(bool Required, bool AllowsNull, JsonObject Schema, HashSet<string>? Values);

    /// <summary>The string values a property allows: its <c>enum</c>, or its <c>const</c> (unioned across union variants).</summary>
    private static HashSet<string>? ValuesOf(JsonObject property) =>
        property["enum"]?.AsArray().Select(v => v!.GetValue<string>()).ToHashSet()
        ?? (property["const"] is JsonValue constant && constant.TryGetValue<string>(out var text) ? [text] : null);

    /// <summary>The properties of an object schema; for oneOf/anyOf of objects, their union (required only when required in every variant).</summary>
    private static Dictionary<string, SchemaProperty>? PropertiesOf(JsonObject schema, JsonObject? defs)
    {
        var variants = (schema["oneOf"] ?? schema["anyOf"])?.AsArray()
            .Select(v => Resolve(v!, defs))
            .Where(v => v["properties"] is not null)
            .ToList();
        if (variants is null || variants.Count == 0)
        {
            variants = schema["properties"] is null ? [] : [schema];
        }

        if (variants.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, SchemaProperty>(StringComparer.Ordinal);
        foreach (var variant in variants)
        {
            var required = variant["required"]?.AsArray().Select(r => r!.GetValue<string>()).ToHashSet() ?? [];
            foreach (var (name, propertyNode) in variant["properties"]!.AsObject())
            {
                var property = propertyNode!.AsObject();
                var isRequired = required.Contains(name);
                var values = ValuesOf(property);
                result[name] = result.TryGetValue(name, out var seen)
                    ? seen with
                    {
                        Required = seen.Required && isRequired,
                        Values = seen.Values is null || values is null ? seen.Values ?? values : [.. seen.Values, .. values],
                    }
                    : new SchemaProperty(isRequired, AllowsNull(property), property, values);
            }
        }

        foreach (var name in result.Keys.ToList())
        {
            if (variants.Any(v => v["properties"]![name] is null))
            {
                result[name] = result[name] with { Required = false };
            }
        }

        return result;
    }

    private static bool AllowsNull(JsonObject property)
    {
        if (property["type"] is JsonArray types)
        {
            return types.Any(t => t!.GetValue<string>() == "null");
        }

        return property["anyOf"]?.AsArray().Any(v => v!["type"]?.GetValue<string>() == "null") ?? false;
    }

    private static void CompareObject(string path, JsonObject schema, Type type, JsonObject? defs, List<string> problems, int depth)
    {
        var schemaProperties = PropertiesOf(schema, defs);
        if (schemaProperties is null || depth > 6)
        {
            return;
        }

        var nullability = new NullabilityInfoContext();
        var csharp = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
            .ToDictionary(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        foreach (var name in schemaProperties.Keys.Except(csharp.Keys))
        {
            problems.Add($"{path}: schema property '{name}' is missing from {type.Name}");
        }

        foreach (var name in csharp.Keys.Except(schemaProperties.Keys))
        {
            problems.Add($"{path}: {type.Name}.{csharp[name].Name} is not in the schema");
        }

        foreach (var (name, property) in schemaProperties)
        {
            if (!csharp.TryGetValue(name, out var member))
            {
                continue;
            }

            var isNullable = Nullable.GetUnderlyingType(member.PropertyType) is not null
                || nullability.Create(member).ReadState == NullabilityState.Nullable;
            var mustBeNullable = !property.Required || property.AllowsNull;
            // A JsonElement holds JSON null itself, so a required field that may be null needs no C# nullability.
            var jsonNullCarrier = member.PropertyType == typeof(JsonElement) && property.Required && property.AllowsNull;
            if (isNullable != mustBeNullable && !jsonNullCarrier)
            {
                problems.Add($"{path}.{name}: schema says {(property.Required ? "required" : "optional")}{(property.AllowsNull ? ", null allowed" : string.Empty)}, but {type.Name}.{member.Name} is {(isNullable ? "nullable" : "not nullable")}");
            }

            CompareEnum($"{path}.{name}", property.Values, member.PropertyType, problems);
            CompareNested($"{path}.{name}", property.Schema, member.PropertyType, defs, problems, depth);
        }
    }

    private static void CompareEnum(string path, HashSet<string>? values, Type type, List<string> problems)
    {
        var enumType = Nullable.GetUnderlyingType(type) ?? type;
        if (!enumType.IsEnum)
        {
            // A single const (such as jsonrpc "2.0" or an error-data name) is a fixed string, not an enum.
            if (values is { Count: > 1 })
            {
                problems.Add($"{path}: the schema is an enum, the C# type {type.Name} is not");
            }

            return;
        }

        var names = Enum.GetValues(enumType).Cast<object>()
            .Where(v => Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture) != 0)
            .Select(v => JsonNamingPolicy.CamelCase.ConvertName(v.ToString()!))
            .ToHashSet();
        if (values is null || !values.SetEquals(names))
        {
            problems.Add($"{path}: schema values [{string.Join(", ", values ?? [])}] differ from {enumType.Name} [{string.Join(", ", names)}]");
        }
    }

    private static void CompareNested(string path, JsonObject schema, Type type, JsonObject? defs, List<string> problems, int depth)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        var nodeSchema = schema;
        if (schema["anyOf"] is JsonArray anyOf && anyOf.Count == 2 && AllowsNull(schema))
        {
            nodeSchema = Resolve(anyOf.First(v => v!["type"]?.GetValue<string>() != "null")!, defs);
        }

        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            target = target.GetGenericArguments()[0];
            nodeSchema = nodeSchema["items"] is JsonObject items ? Resolve(items, defs) : nodeSchema;
        }
        else
        {
            nodeSchema = Resolve(nodeSchema, defs);
        }

        if (target.Namespace == typeof(Location).Namespace && !target.IsEnum)
        {
            CompareObject(path, nodeSchema, target, defs, problems, depth + 1);
        }
    }
}
