// Something wrong in what the engine sent that the connection survived: a line
// over the size limit, a line that is not a protocol message, an event that
// does not fit its type, or a response to no request.

namespace Desktop.Engine;

/// <summary>A problem in the engine's output that did not end the session.</summary>
/// <param name="Message">What was wrong, for the engine log.</param>
/// <param name="Excerpt">The start of the offending line, when there was one.</param>
public sealed record ProtocolProblem(string Message, string? Excerpt = null);
