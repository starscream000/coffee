// Size limits of the protocol (docs/protocol.md, "Transport").

namespace Desktop.Protocol;

/// <summary>Size limits that both sides of the protocol obey.</summary>
public static class ProtocolLimits
{
    /// <summary>
    /// Largest message, in bytes of UTF-8, excluding the newline: 4 MiB. A
    /// longer line is discarded unparsed by whoever receives it.
    /// </summary>
    public const int MaxMessageBytes = 4 * 1024 * 1024;
}
