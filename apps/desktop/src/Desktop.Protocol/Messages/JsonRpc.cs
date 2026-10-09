// JSON-RPC 2.0 envelopes (docs/protocol.md, "Transport") and the classification
// of a line received from the engine.

using System.Text.Json;
using System.Text.Json.Serialization;
using Desktop.Protocol.Json;

namespace Desktop.Protocol.Messages;

/// <summary>A request from the client.</summary>
public sealed record JsonRpcRequest
{
    /// <summary>Always <c>2.0</c>.</summary>
    public string Jsonrpc { get; init; } = "2.0";
    /// <summary>Request id, a number or a string; echoed by the response.</summary>
    public required JsonElement Id { get; init; }
    /// <summary>The method.</summary>
    public required string Method { get; init; }
    /// <summary>The parameters, when the method takes any.</summary>
    public JsonElement? Params { get; init; }
}

/// <summary>A notification (an event) from the engine.</summary>
public sealed record JsonRpcNotification
{
    /// <summary>Always <c>2.0</c>.</summary>
    public string Jsonrpc { get; init; } = "2.0";
    /// <summary>The event's name.</summary>
    public required string Method { get; init; }
    /// <summary>The event's fields.</summary>
    public JsonElement? Params { get; init; }
}

/// <summary>A successful response.</summary>
public sealed record JsonRpcSuccessResponse
{
    /// <summary>Always <c>2.0</c>.</summary>
    public string Jsonrpc { get; init; } = "2.0";
    /// <summary>The id of the request answered.</summary>
    public required JsonElement Id { get; init; }
    /// <summary>The result; JSON <c>null</c> for methods without one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required JsonElement Result { get; init; }
}

/// <summary>An error response.</summary>
public sealed record JsonRpcErrorResponse
{
    /// <summary>Always <c>2.0</c>.</summary>
    public string Jsonrpc { get; init; } = "2.0";
    /// <summary>The id of the request answered; JSON <c>null</c> when the request could not be read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required JsonElement Id { get; init; }
    /// <summary>The error.</summary>
    public required JsonRpcError Error { get; init; }
}

/// <summary>The error of an error response.</summary>
public sealed record JsonRpcError
{
    /// <summary>Numeric code (<see cref="ErrorCodes"/>).</summary>
    public required int Code { get; init; }
    /// <summary>What went wrong, for people.</summary>
    public required string Message { get; init; }
    /// <summary>Error data; carries <c>name</c> and, for some errors, more.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>The error's name from <c>data.name</c>, else from the code table, else null.</summary>
    [JsonIgnore]
    public string? Name =>
        Data is { ValueKind: JsonValueKind.Object } data
        && data.TryGetProperty("name", out var name)
        && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : ErrorCodes.NameOf(Code);
}

/// <summary>A line from the engine, classified.</summary>
public abstract record IncomingMessage
{
    /// <summary>
    /// Reads one line received from the engine.
    /// </summary>
    /// <param name="line">The line's UTF-8 bytes, without the newline.</param>
    /// <returns>A response or a notification.</returns>
    /// <exception cref="JsonException">The line is not JSON, or not a response or notification.</exception>
    public static IncomingMessage Parse(ReadOnlySpan<byte> line)
    {
        using var document = JsonDocument.Parse(line.ToArray());
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A protocol message must be a JSON object.");
        }

        var hasId = root.TryGetProperty("id", out _);
        if (hasId && root.TryGetProperty("error", out _))
        {
            return new IncomingErrorResponse(ProtocolJson.Read<JsonRpcErrorResponse>(root.Clone()));
        }

        if (hasId && root.TryGetProperty("result", out _))
        {
            return new IncomingSuccessResponse(ProtocolJson.Read<JsonRpcSuccessResponse>(root.Clone()));
        }

        if (!hasId && root.TryGetProperty("method", out _))
        {
            return new IncomingNotification(ProtocolJson.Read<JsonRpcNotification>(root.Clone()));
        }

        throw new JsonException("The message is neither a response (id with result or error) nor a notification (method without id).");
    }
}

/// <summary>A successful response.</summary>
/// <param name="Response">The response.</param>
public sealed record IncomingSuccessResponse(JsonRpcSuccessResponse Response) : IncomingMessage;

/// <summary>An error response.</summary>
/// <param name="Response">The response.</param>
public sealed record IncomingErrorResponse(JsonRpcErrorResponse Response) : IncomingMessage;

/// <summary>A notification.</summary>
/// <param name="Notification">The notification.</param>
public sealed record IncomingNotification(JsonRpcNotification Notification) : IncomingMessage;
