// The protocol version this client speaks and the rule that decides whether it
// can talk to an engine (docs/protocol.md, "Versioning"; repository ADR 0011).

using System.Globalization;
using System.Text.RegularExpressions;

namespace Desktop.Protocol;

/// <summary>
/// The protocol version spoken by this client, and the compatibility rule.
/// </summary>
/// <example>
/// <code>
/// ProtocolVersion.IsCompatible("0.1.3", "0.1.0"); // true
/// ProtocolVersion.IsCompatible("0.2.0", "0.1.0"); // false
/// </code>
/// </example>
public static partial class ProtocolVersion
{
    /// <summary>
    /// The version of <c>docs/protocol.md</c> this client implements. Sent in
    /// <c>initialize</c>; must equal the version in the titles of the JSON
    /// Schema files (a contract test checks it).
    /// </summary>
    public const string Current = "0.1.0";

    /// <summary>
    /// Splits a version of the form <c>major.minor.patch</c>.
    /// </summary>
    /// <param name="version">A version such as <c>"0.1.0"</c>. Pre-release and build suffixes are rejected.</param>
    /// <param name="parts">The numeric parts, when the version is well formed.</param>
    /// <returns><see langword="true"/> when <paramref name="version"/> is of that form.</returns>
    public static bool TryParse(string? version, out (int Major, int Minor, int Patch) parts)
    {
        parts = default;
        if (version is null)
        {
            return false;
        }

        var match = SemVer().Match(version);
        if (!match.Success
            || !int.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups[2].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(match.Groups[3].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        parts = (major, minor, patch);
        return true;
    }

    /// <summary>
    /// Says whether a client speaking <paramref name="clientVersion"/> and an
    /// engine speaking <paramref name="engineVersion"/> may talk to each other.
    /// From <c>1.0.0</c> on the major versions must be equal; while the version
    /// is <c>0.x</c>, major and minor must be equal. A malformed version is never
    /// compatible.
    /// </summary>
    /// <param name="clientVersion">The client's protocol version.</param>
    /// <param name="engineVersion">The engine's protocol version.</param>
    /// <returns><see langword="true"/> when the two are compatible.</returns>
    public static bool IsCompatible(string clientVersion, string engineVersion)
    {
        if (!TryParse(clientVersion, out var client) || !TryParse(engineVersion, out var engine))
        {
            return false;
        }

        if (client.Major != engine.Major)
        {
            return false;
        }

        return engine.Major != 0 || client.Minor == engine.Minor;
    }

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex SemVer();
}
