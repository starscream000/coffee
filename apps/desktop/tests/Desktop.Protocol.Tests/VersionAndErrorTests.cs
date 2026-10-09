// Tests of the version rule and the error-code table, against the engine's
// TypeScript sources where they define the same thing.

using System.Text.RegularExpressions;

namespace Desktop.Protocol.Tests;

public sealed class VersionAndErrorTests
{
    [Theory]
    [InlineData("0.1.0", "0.1.0", true)]
    [InlineData("0.1.3", "0.1.0", true)]
    [InlineData("0.1.0", "0.1.9", true)]
    [InlineData("0.2.0", "0.1.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("1.0.0", "0.1.0", false)]
    [InlineData("1.4.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.9.2", true)]
    [InlineData("2.0.0", "1.0.0", false)]
    [InlineData("0.1", "0.1.0", false)]
    [InlineData("0.1.0-beta", "0.1.0", false)]
    [InlineData("01.1.0", "1.1.0", false)]
    [InlineData("", "0.1.0", false)]
    public void IsCompatible_follows_the_protocol_rule(string client, string engine, bool expected)
    {
        Assert.Equal(expected, ProtocolVersion.IsCompatible(client, engine));
    }

    [Fact]
    public void Current_version_equals_the_TypeScript_PROTOCOL_VERSION()
    {
        var source = File.ReadAllText(RepoPaths.Of("packages/protocol/src/version.ts"));
        var match = Regex.Match(source, @"PROTOCOL_VERSION = '([^']+)'");
        Assert.Equal(ProtocolVersion.Current, match.Groups[1].Value);
    }

    [Fact]
    public void Error_table_equals_the_TypeScript_ERROR_CODES()
    {
        var source = File.ReadAllText(RepoPaths.Of("packages/protocol/src/errors.ts"));
        var block = source[source.IndexOf("ERROR_CODES = {", StringComparison.Ordinal)..];
        block = block[..block.IndexOf('}', StringComparison.Ordinal)];
        var typescript = Regex.Matches(block, @"(\w+): (-\d+),")
            .Select(m => new KeyValuePair<string, int>(m.Groups[1].Value, int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        Assert.Equal(typescript, ErrorCodes.All);
    }

    [Fact]
    public void NameOf_finds_known_codes_and_returns_null_for_unknown_ones()
    {
        Assert.Equal(ErrorCodes.IncompatibleProtocol, ErrorCodes.NameOf(-32002));
        Assert.Null(ErrorCodes.NameOf(-32099));
    }

    [Fact]
    public void MaxMessageBytes_equals_the_TypeScript_limit()
    {
        var source = File.ReadAllText(RepoPaths.Of("packages/protocol/src/limits.ts"));
        Assert.Contains("MAX_MESSAGE_BYTES = 4 * 1024 * 1024;", source, StringComparison.Ordinal);
        Assert.Equal(4 * 1024 * 1024, ProtocolLimits.MaxMessageBytes);
    }
}
