// Reads newline-delimited protocol messages from a stream (docs/protocol.md,
// "Transport"): one message per line, at most 4 MiB of UTF-8; longer lines are
// discarded unparsed and reported.

namespace Desktop.Protocol.Framing;

/// <summary>A line read from the engine: its bytes, or the size of a discarded over-long line.</summary>
public readonly record struct FramedLine
{
    /// <summary>The line's UTF-8 bytes without the newline (and without a trailing <c>\r</c>); null when it was too long.</summary>
    public byte[]? Bytes { get; init; }

    /// <summary>For a discarded line, how many bytes it had; 0 otherwise.</summary>
    public long DiscardedBytes { get; init; }

    /// <summary>True when the line was over the size limit and was discarded.</summary>
    public bool IsOversize => Bytes is null;
}

/// <summary>
/// Splits a byte stream into protocol lines. Not thread-safe: one reader at a time.
/// </summary>
/// <example>
/// <code>
/// var reader = new MessageLineReader(process.StandardOutput.BaseStream);
/// while (await reader.ReadAsync(ct) is { } line) { … }
/// </code>
/// </example>
public sealed class MessageLineReader : IDisposable
{
    private const int ChunkSize = 64 * 1024;
    private readonly Stream _stream;
    private readonly int _maxBytes;
    private readonly byte[] _chunk = new byte[ChunkSize];
    private readonly MemoryStream _line = new();
    private int _chunkStart;
    private int _chunkEnd;
    private bool _discarding;
    private long _discarded;

    /// <summary>Creates a reader.</summary>
    /// <param name="stream">The stream to read, usually the engine's stdout.</param>
    /// <param name="maxBytes">Largest line in bytes, without the newline.</param>
    public MessageLineReader(Stream stream, int maxBytes = ProtocolLimits.MaxMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        _stream = stream;
        _maxBytes = maxBytes;
    }

    /// <summary>
    /// Reads the next line. Empty lines are skipped. At the end of the stream an
    /// unterminated last line is returned as a line.
    /// </summary>
    /// <param name="cancellationToken">Stops waiting for data.</param>
    /// <returns>The next line, or null at the end of the stream.</returns>
    public async ValueTask<FramedLine?> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_chunkStart == _chunkEnd)
            {
                _chunkStart = 0;
                _chunkEnd = await _stream.ReadAsync(_chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (_chunkEnd == 0)
                {
                    return TakeLine();
                }
            }

            var available = _chunk.AsSpan(_chunkStart, _chunkEnd - _chunkStart);
            var newline = available.IndexOf((byte)'\n');
            var part = newline < 0 ? available : available[..newline];
            Append(part);
            _chunkStart += newline < 0 ? available.Length : newline + 1;

            if (newline >= 0 && TakeLine() is { } line)
            {
                return line;
            }
        }
    }

    private void Append(ReadOnlySpan<byte> part)
    {
        if (_discarding)
        {
            _discarded += part.Length;
            return;
        }

        // One byte of slack for a trailing \r that is removed later.
        if (_line.Length + part.Length > _maxBytes + 1L)
        {
            _discarding = true;
            _discarded = _line.Length + part.Length;
            _line.SetLength(0);
            return;
        }

        _line.Write(part);
    }

    private FramedLine? TakeLine()
    {
        if (_discarding)
        {
            var size = _discarded;
            _discarding = false;
            _discarded = 0;
            return new FramedLine { DiscardedBytes = size };
        }

        var length = (int)_line.Length;
        var buffer = _line.GetBuffer();
        if (length > 0 && buffer[length - 1] == (byte)'\r')
        {
            length--;
        }

        if (length > _maxBytes)
        {
            _line.SetLength(0);
            return new FramedLine { DiscardedBytes = length };
        }

        var bytes = buffer.AsSpan(0, length).ToArray();
        _line.SetLength(0);
        // An empty line carries no message: the caller reads on, or ends at the end of the stream.
        return bytes.Length == 0 ? null : new FramedLine { Bytes = bytes };
    }

    /// <inheritdoc />
    public void Dispose() => _line.Dispose();
}
