// Tests of the newline framing: split and merged chunks, CRLF, multi-byte
// characters across chunks, empty lines, and the size limit in both directions.

using System.Text;
using System.Text.Json;
using Desktop.Protocol.Framing;
using Desktop.Protocol.Messages;

namespace Desktop.Protocol.Tests;

public sealed class FramingTests
{
    /// <summary>A stream that hands out its bytes in fixed-size pieces, like a pipe.</summary>
    private sealed class ChunkedStream(byte[] data, int chunk) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, chunk));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], cancellationToken);
    }

    private static async Task<List<FramedLine>> ReadAll(string text, int chunk = 3, int max = ProtocolLimits.MaxMessageBytes)
    {
        using var reader = new MessageLineReader(new ChunkedStream(Encoding.UTF8.GetBytes(text), chunk), max);
        var lines = new List<FramedLine>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken) is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static string Text(FramedLine line) => Encoding.UTF8.GetString(line.Bytes!);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4096)]
    public async Task Splits_lines_whatever_the_chunk_size(int chunk)
    {
        var lines = await ReadAll("{\"a\":1}\n{\"b\":\"ä€😀\"}\n", chunk);
        Assert.Equal(["{\"a\":1}", "{\"b\":\"ä€😀\"}"], lines.Select(Text));
    }

    [Fact]
    public async Task Removes_a_trailing_carriage_return_and_skips_empty_lines()
    {
        var lines = await ReadAll("one\r\n\n\r\ntwo\n");
        Assert.Equal(["one", "two"], lines.Select(Text));
    }

    [Fact]
    public async Task Returns_an_unterminated_last_line()
    {
        var lines = await ReadAll("one\ntwo");
        Assert.Equal(["one", "two"], lines.Select(Text));
    }

    [Fact]
    public async Task Discards_an_oversize_line_and_reads_on()
    {
        var lines = await ReadAll($"ok\n{new string('x', 20)}\nnext\n", chunk: 4, max: 10);
        Assert.Equal(3, lines.Count);
        Assert.Equal("ok", Text(lines[0]));
        Assert.True(lines[1].IsOversize);
        Assert.Equal(20, lines[1].DiscardedBytes);
        Assert.Equal("next", Text(lines[2]));
    }

    [Fact]
    public async Task Accepts_a_line_of_exactly_the_limit_even_with_CRLF()
    {
        var lines = await ReadAll($"{new string('x', 10)}\r\n", chunk: 4, max: 10);
        Assert.Equal(new string('x', 10), Text(Assert.Single(lines)));
    }

    [Fact]
    public async Task Writer_writes_one_compact_line_per_message()
    {
        using var stream = new MemoryStream();
        using var writer = new MessageLineWriter(stream);
        await writer.WriteAsync(new CancelRunParams { RunId = "r\n1" }, TestContext.Current.CancellationToken);
        await writer.WriteAsync(EmptyParams.Instance, TestContext.Current.CancellationToken);
        Assert.Equal("{\"runId\":\"r\\n1\"}\n{}\n", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public async Task Writer_refuses_an_oversize_message_and_writes_nothing()
    {
        using var stream = new MemoryStream();
        using var writer = new MessageLineWriter(stream, maxBytes: 10);
        await Assert.ThrowsAsync<MessageTooLargeException>(() =>
            writer.WriteAsync(new CancelRunParams { RunId = "0123456789" }, TestContext.Current.CancellationToken));
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void Parse_classifies_responses_and_notifications()
    {
        Assert.IsType<IncomingSuccessResponse>(IncomingMessage.Parse("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":null}"u8));
        var error = Assert.IsType<IncomingErrorResponse>(
            IncomingMessage.Parse("{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32009,\"message\":\"m\"}}"u8));
        Assert.Equal(JsonValueKind.Null, error.Response.Id.ValueKind);
        Assert.Equal(ErrorCodes.MessageTooLarge, error.Response.Error.Name);
        Assert.IsType<IncomingNotification>(IncomingMessage.Parse("{\"jsonrpc\":\"2.0\",\"method\":\"log\",\"params\":{}}"u8));
        Assert.Throws<JsonException>(() => IncomingMessage.Parse("[1]"u8));
        Assert.Throws<JsonException>(() => IncomingMessage.Parse("{\"jsonrpc\":\"2.0\",\"id\":1}"u8));
        Assert.ThrowsAny<JsonException>(() => IncomingMessage.Parse("not json"u8));
    }
}
