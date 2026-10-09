// Reads and writes the protocol's string enums. An unknown value from a newer
// engine reads as the enum's Unknown member instead of failing the message.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desktop.Protocol.Json;

/// <summary>
/// JSON converter for a protocol enum. Each member is written as its camelCase
/// name; a value this client does not know reads as the member with value 0,
/// which every protocol enum names <c>Unknown</c>.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
public sealed class ProtocolEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private static readonly Dictionary<string, T> ByName = BuildByName();
    private static readonly Dictionary<T, string> ByValue = ByName.ToDictionary(p => p.Value, p => p.Key);

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string for {typeof(T).Name}, got {reader.TokenType}.");
        }

        return ByName.TryGetValue(reader.GetString()!, out var value) ? value : default;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!ByValue.TryGetValue(value, out var name))
        {
            throw new JsonException($"{typeof(T).Name}.{value} has no protocol name and cannot be sent.");
        }

        writer.WriteStringValue(name);
    }

    private static Dictionary<string, T> BuildByName()
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (T)field.GetValue(null)!;
            if (Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) == 0)
            {
                continue;
            }

            map[JsonNamingPolicy.CamelCase.ConvertName(field.Name)] = value;
        }

        return map;
    }
}
