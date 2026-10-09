// The exceptions of the engine host. Each message says what happened and what
// the user can do about it.

using System.Text.Json;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.Engine;

/// <summary>Base of every exception the engine host throws.</summary>
public class EngineException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened and what to do.</param>
    /// <param name="inner">The cause, if any.</param>
    public EngineException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>Node or the engine's entry script could not be found.</summary>
public sealed class EngineNotFoundException : EngineException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What was missing and how to fix it.</param>
    /// <param name="searched">Every place that was looked at, in order.</param>
    public EngineNotFoundException(string message, IReadOnlyList<string> searched)
        : base(message)
    {
        Searched = searched;
    }

    /// <summary>Every place that was looked at, in order.</summary>
    public IReadOnlyList<string> Searched { get; }
}

/// <summary>The engine process could not be started.</summary>
public sealed class EngineStartException : EngineException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What happened and what to do.</param>
    /// <param name="inner">The operating system's error.</param>
    public EngineStartException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>The engine process ended while a request was waiting for its answer, or before one was sent.</summary>
public sealed class EngineExitedException : EngineException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="exitCode">The process's exit code, when known.</param>
    /// <param name="stderrTail">The last lines the engine wrote to stderr.</param>
    public EngineExitedException(int? exitCode, string stderrTail)
        : base(BuildMessage(exitCode, stderrTail))
    {
        ExitCode = exitCode;
        StderrTail = stderrTail;
    }

    /// <summary>The process's exit code, when known.</summary>
    public int? ExitCode { get; }

    /// <summary>The last lines the engine wrote to stderr.</summary>
    public string StderrTail { get; }

    private static string BuildMessage(int? exitCode, string stderrTail)
    {
        var code = exitCode is { } c ? $" with exit code {c}" : string.Empty;
        var tail = string.IsNullOrWhiteSpace(stderrTail) ? " It wrote nothing to stderr." : $" Its last output:{Environment.NewLine}{stderrTail}";
        return $"The engine stopped{code}.{tail}{Environment.NewLine}Restart the engine; if it stops again, the engine log has the details.";
    }
}

/// <summary>The engine answered a request with a protocol error.</summary>
public class EngineRequestException : EngineException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="method">The request's method.</param>
    /// <param name="error">The error from the response.</param>
    public EngineRequestException(string method, JsonRpcError error)
        : base(error?.Message ?? string.Empty)
    {
        ArgumentNullException.ThrowIfNull(error);
        Method = method;
        Code = error.Code;
        Name = error.Name;
        ErrorData = error.Data;
    }

    /// <summary>The request's method.</summary>
    public string Method { get; }

    /// <summary>The numeric error code.</summary>
    public int Code { get; }

    /// <summary>The error's name (<see cref="Protocol.ErrorCodes"/>), when known.</summary>
    public string? Name { get; }

    /// <summary>The error's data, if any.</summary>
    public JsonElement? ErrorData { get; }

    /// <summary>Reads the error's data as a protocol type, such as <see cref="StepFilesInvalidData"/>.</summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <returns>The data, or null when there is none or it does not fit.</returns>
    public T? DataAs<T>()
        where T : class
    {
        if (ErrorData is not { ValueKind: JsonValueKind.Object } data)
        {
            return null;
        }

        try
        {
            return ProtocolJson.Read<T>(data);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The engine refused this client in the handshake because their protocol versions differ.</summary>
public sealed class IncompatibleEngineException : EngineException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Which side to update.</param>
    /// <param name="clientProtocolVersion">This client's protocol version.</param>
    /// <param name="engineProtocolVersion">The engine's protocol version.</param>
    public IncompatibleEngineException(string message, string clientProtocolVersion, string engineProtocolVersion)
        : base(message)
    {
        ClientProtocolVersion = clientProtocolVersion;
        EngineProtocolVersion = engineProtocolVersion;
    }

    /// <summary>This client's protocol version.</summary>
    public string ClientProtocolVersion { get; }

    /// <summary>The engine's protocol version.</summary>
    public string EngineProtocolVersion { get; }
}
