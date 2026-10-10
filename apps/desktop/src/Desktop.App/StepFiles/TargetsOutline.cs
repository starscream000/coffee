// The targets a file declares under its "targets" key, as the targets editor
// shows them: each target's name, its frame and within (when written as a
// name), its candidates in order, and the lines it takes. Both forms of the
// step format are read: the short form (a list of candidates) and the long
// form (frame, within, candidates).

namespace Desktop.App.StepFiles;

/// <summary>One target.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Key">The name's node.</param>
/// <param name="Value">The target's value.</param>
/// <param name="IsLongForm">True when written with <c>candidates:</c>.</param>
/// <param name="Frame">The <c>frame</c> value, if any.</param>
/// <param name="Within">The <c>within</c> value, if any.</param>
/// <param name="Candidates">The candidates, in order; each normally a mapping.</param>
/// <param name="Problem">Why the editor cannot show it as a form; null when it can.</param>
/// <param name="Start">Offset of the start of its first line.</param>
/// <param name="End">Offset just after the line break of its last line, or the end of the text.</param>
/// <param name="Line">1-based line of its name.</param>
/// <param name="EndLine">1-based line of its last character.</param>
/// <param name="Indent">Column of its name.</param>
public sealed record TargetOutline(
    string Name,
    YamlScalar Key,
    YamlNode Value,
    bool IsLongForm,
    YamlNode? Frame,
    YamlNode? Within,
    IReadOnlyList<YamlNode> Candidates,
    string? Problem,
    int Start,
    int End,
    int Line,
    int EndLine,
    int Indent);

/// <summary>The targets of a test, flow or targets file, or why they cannot be read.</summary>
/// <param name="Text">The text read.</param>
/// <param name="Root">The root mapping; null when the file could not be read.</param>
/// <param name="Key">The <c>targets</c> key; null when the file has none.</param>
/// <param name="Value">Its value: a mapping, or a null scalar for <c>targets:</c> with nothing.</param>
/// <param name="Targets">The targets, in order.</param>
/// <param name="Problem">Why the file's targets cannot be shown; null when they can.</param>
/// <param name="NewLine">The line break the file uses.</param>
public sealed record TargetsOutline(string Text, YamlMapping? Root, YamlScalar? Key, YamlNode? Value, IReadOnlyList<TargetOutline> Targets, string? Problem, string NewLine)
{
    /// <summary>The candidate kinds, in the order of reliability the step format gives.</summary>
    public static readonly IReadOnlyList<string> CandidateKinds = ["role", "label", "placeholder", "text", "testId", "css"];

    /// <summary>True when the targets can be shown.</summary>
    public bool IsReadable => Problem is null;

    /// <summary>True for a file that can have targets: a test, a flow or a targets file.</summary>
    /// <param name="file">The file's path.</param>
    /// <returns>True when it can.</returns>
    public static bool CanHaveTargets(string file) => NewFiles.KindOf(file) is not null || file.EndsWith(".test.yml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".flow.yml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a file's targets.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The outline; when it cannot be read, <see cref="Problem"/> says why. Never throws for bad input.</returns>
    public static TargetsOutline Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        YamlNode? root;
        try
        {
            root = YamlTree.Read(text);
        }
        catch (YamlTreeException ex)
        {
            return new(text, null, null, null, [], ex.LineNumber is { } line ? $"Line {line}: {ex.Message}" : ex.Message, newLine);
        }

        if (root is not YamlMapping { IsFlow: false } mapping)
        {
            return new(text, null, null, null, [], root is null ? "The file is empty." : "The file is not a mapping of keys.", newLine);
        }

        var key = mapping.KeyOf("targets");
        var value = mapping.Get("targets");
        switch (value)
        {
            case null:
            case YamlScalar { IsNull: true }:
            case YamlMapping { IsFlow: true, Entries.Count: 0 }:
                return new(text, mapping, key, value, [], null, newLine);
            case YamlMapping { IsFlow: false } targets:
                return new(text, mapping, key, value, [.. targets.Entries.Select(e => Target(text, e.Key, e.Value))], null, newLine);
            default:
                return new(text, mapping, key, value, [], $"Line {value.Line}: \"targets\" is not written one target per line, so the targets editor cannot show it.", newLine);
        }
    }

    private static TargetOutline Target(string text, YamlScalar key, YamlNode value)
    {
        var start = StepOutline.LineStart(text, key.Start);
        var end = StepOutline.LineEndAfter(text, Math.Max(value.End, key.End) - 1);
        var endLine = key.Line + text[key.Start..Math.Max(Math.Max(value.End, key.End) - 1, key.Start)].Count(c => c == '\n');
        TargetOutline Make(bool longForm, YamlNode? frame, YamlNode? within, IReadOnlyList<YamlNode> candidates, string? problem) =>
            new(key.Value, key, value, longForm, frame, within, candidates, problem, start, end, key.Line, endLine, key.Start - start);

        switch (value)
        {
            case YamlSequence sequence:
                return Make(false, null, null, sequence.Items, Check(sequence.Items));
            case YamlMapping mapping:
                var unknown = mapping.Entries.Select(e => e.Key.Value).FirstOrDefault(k => k is not ("candidates" or "frame" or "within"));
                var items = mapping.Get("candidates") is YamlSequence candidates ? candidates.Items : [];
                var problem = unknown is not null ? $"It has a key the editor does not know: \"{unknown}\"."
                    : mapping.Get("candidates") is not YamlSequence ? "Its \"candidates\" is not a list of candidates."
                    : Check(items);
                return Make(true, mapping.Get("frame"), mapping.Get("within"), items, problem);
            default:
                return Make(false, null, null, [], "It is neither a list of candidates nor a mapping with \"candidates\".");
        }
    }

    private static string? Check(IReadOnlyList<YamlNode> candidates) =>
        candidates.All(c => c is YamlMapping { IsFlow: false } or YamlMapping { IsFlow: true })
            ? null
            : "A candidate is not a mapping such as \"- css: '#buy'\".";
}
