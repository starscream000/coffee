// Writes protocol messages to a stream, one per line, refusing any message over
// the size limit (docs/protocol.md, "Transport").

using System.Text.Json;
using Desktop.Protocol.Json;

namespace Desktop.Protocol.Framing;

/// <summary>Thrown when a message to send is larger than the protocol allows.</summary>
public sealed class MessageTooLargeException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="size">The message's size in bytes.</param>
    public MessageTooLargeException(long size)
        : base($"The message is {size:N0} bytes; the protocol allows at most {ProtocolLimits.MaxMessageBytes:N0}. Send less at once, for example fewer files.")
    {
        Size = size;
    }

    /// <summary>The message's size in bytes.</summary>
    public long Size { get; }
}

/// <summary>
/// Serialises protocol messages and writes each as one line. Safe to call from
/// several threads; lines never interleave.
/// </summary>
public sealed class MessageLineWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly int _maxBytes;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a writer.</summary>
    /// <param name="stream">The stream to write, usually the engine's stdin.</param>
    /// <param name="maxBytes">Largest message in bytes, without the newline.</param>
    public MessageLineWriter(Stream stream, int maxBytes = ProtocolLimits.MaxMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _maxBytes = maxBytes;
    }

    /// <summary>Serialises <paramref name="message"/> and writes it as one line, then flushes.</summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the line is flushed.</returns>
    /// <exception cref="MessageTooLargeException">The message is over the limit; nothing was written.</exception>
    public async Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolJson.Options);
        if (bytes.Length > _maxBytes)
        {
            throw new MessageTooLargeException(bytes.Length);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
