// The JSON settings every protocol message is read and written with.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desktop.Protocol.Json;

/// <summary>
/// JSON settings for protocol messages: camelCase names, unknown fields
/// ignored (clients must ignore them, docs/protocol.md "Versioning"), absent
/// optional fields not written.
/// </summary>
/// <example>
/// <code>
/// var result = JsonSerializer.Deserialize&lt;OpenProjectResult&gt;(json, ProtocolJson.Options);
/// </code>
/// </example>
public static class ProtocolJson
{
    /// <summary>The shared, read-only serializer options.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Reads a value of type <typeparamref name="T"/> from a JSON element.</summary>
    /// <typeparam name="T">The protocol type to read.</typeparam>
    /// <param name="element">The JSON value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="JsonException">The JSON does not fit <typeparamref name="T"/>, for example a required field is missing.</exception>
    public static T Read<T>(JsonElement element) =>
        element.Deserialize<T>(Options) ?? throw new JsonException($"Expected {typeof(T).Name}, got null.");

    /// <summary>Writes a value as a JSON element.</summary>
    /// <typeparam name="T">The protocol type to write.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The JSON value.</returns>
    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
