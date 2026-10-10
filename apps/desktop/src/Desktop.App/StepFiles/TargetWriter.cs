// Writes the text of one target from the targets editor, and changes the
// "targets" mapping: replace, add or remove one target, touching only that
// target's lines (and, for the first or last one, the "targets" key line).

namespace Desktop.App.StepFiles;

/// <summary>A candidate as the editor holds it.</summary>
/// <param name="Kind">Its kind: <c>role</c>, <c>label</c>, <c>placeholder</c>, <c>text</c>, <c>testId</c> or <c>css</c>.</param>
/// <param name="Value">The kind's value.</param>
/// <param name="Name">For <c>role</c>: the accessible name, if any.</param>
/// <param name="Exact">The <c>exact</c> setting, if written.</param>
/// <param name="Nth">The <c>nth</c> setting as YAML, if written.</param>
/// <param name="Raw">The candidate's original lines (<see cref="TargetWriter.CandidateTexts"/>), used as they are when nothing changed; null for a new or changed one.</param>
public sealed record CandidateValue(string Kind, string Value, string? Name, bool? Exact, string? Nth, string? Raw = null);

/// <summary>Writes targets.</summary>
public static class TargetWriter
{
    /// <summary>The text of a target, without indentation: the short form, or the long form when it has a frame or within.</summary>
    /// <param name="name">The target's name.</param>
    /// <param name="frame">The frame's YAML (a target name), or null.</param>
    /// <param name="within">The within's YAML (a target name), or null.</param>
    /// <param name="candidates">The candidates, in order.</param>
    /// <returns>Such as <c>"buy:\n  - role: button\n    name: Buy"</c>.</returns>
    public static string Write(string name, string? frame, string? within, IReadOnlyList<CandidateValue> candidates)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(candidates);
        var lines = new List<string> { $"{Key(name)}:" };
        var indent = 2;
        if (frame is not null || within is not null)
        {
            if (frame is not null)
            {
                lines.Add($"  frame: {frame}");
            }

            if (within is not null)
            {
                lines.Add($"  within: {within}");
            }

            lines.Add("  candidates:");
            indent = 4;
        }

        var pad = new string(' ', indent);
        if (candidates.Count == 0)
        {
            lines[^1] += " []";
        }

        foreach (var candidate in candidates)
        {
            lines.AddRange(Candidate(candidate).Split('\n').Select(l => pad + l));
        }

        return string.Join('\n', lines);
    }

    /// <summary>The text of one candidate, without indentation: its original text when it has one, else written anew.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>Such as <c>"- role: button\n  name: Save"</c>.</returns>
    public static string Candidate(CandidateValue candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Raw is { } raw)
        {
            return raw;
        }

        var lines = new List<string> { $"- {candidate.Kind}: {StepWriter.Text(candidate.Value)}" };
        if (candidate.Name is { } accessibleName)
        {
            lines.Add($"  name: {StepWriter.Text(accessibleName)}");
        }

        if (candidate.Exact is { } exact)
        {
            lines.Add($"  exact: {(exact ? "true" : "false")}");
        }

        if (candidate.Nth is { } nth)
        {
            lines.Add($"  nth: {nth}");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Each candidate's original lines, without indentation: from the line after
    /// the previous candidate (so comment lines above a candidate go with it) to
    /// the end of its own last line (so a comment at the end of it stays too).
    /// </summary>
    /// <param name="outline">The file's targets.</param>
    /// <param name="target">The target.</param>
    /// <returns>One text per candidate, such as <c>"# stable\n- testId: checkout   # kept"</c>.</returns>
    public static IReadOnlyList<string> CandidateTexts(TargetsOutline outline, TargetOutline target)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(target);
        var text = outline.Text;
        var texts = new List<string>();
        var start = -1;
        foreach (var node in target.Candidates)
        {
            var dash = text.LastIndexOf('-', Math.Max(node.Start - 1, 0));
            var dashLine = StepOutline.LineStart(text, dash);
            var column = dash - dashLine;
            var from = start < 0 ? dashLine : start;
            var end = StepOutline.LineEndAfter(text, Math.Max(node.End - 1, node.Start));
            var lines = text[from..end].Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
            texts.Add(string.Join('\n', lines.Select(l => l[Math.Min(column, l.Length - l.TrimStart(' ').Length)..])));
            start = end;
        }

        return texts;
    }

    /// <summary>Replaces a target's lines.</summary>
    /// <param name="outline">The file's targets.</param>
    /// <param name="target">The target.</param>
    /// <param name="text">Its new text, without indentation.</param>
    /// <returns>The new text of the file.</returns>
    public static string Replace(TargetsOutline outline, TargetOutline target, string text)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(target);
        var file = outline.Text;
        var endsWithBreak = target.End > 0 && file[target.End - 1] == '\n';
        return file[..target.Start] + StepEdits.Indent(text, target.Indent, outline.NewLine) + (endsWithBreak ? outline.NewLine : string.Empty) + file[target.End..];
    }

    /// <summary>Removes a target; the last one leaves <c>targets: {}</c>.</summary>
    /// <param name="outline">The file's targets.</param>
    /// <param name="target">The target.</param>
    /// <returns>The new text of the file.</returns>
    public static string Remove(TargetsOutline outline, TargetOutline target)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(target);
        var file = outline.Text;
        if (outline.Targets.Count == 1 && outline.Key is { } key)
        {
            var colon = file.IndexOf(':', key.End);
            var keepBreak = target.End > 0 && file[target.End - 1] == '\n' ? outline.NewLine : string.Empty;
            return file[..(colon + 1)] + " {}" + keepBreak + file[target.End..];
        }

        return file[..target.Start] + file[target.End..];
    }

    /// <summary>Adds a target after the last one, creating the <c>targets</c> key when the file has none (before its steps).</summary>
    /// <param name="outline">The file's targets.</param>
    /// <param name="text">The target's text, without indentation.</param>
    /// <returns>The new text of the file.</returns>
    public static string Add(TargetsOutline outline, string text)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(text);
        var file = outline.Text;
        var nl = outline.NewLine;
        if (outline.Targets.Count > 0)
        {
            var last = outline.Targets[^1];
            var (with, at) = last.End == file.Length && file.Length > 0 && file[^1] != '\n' ? (file + nl, file.Length + nl.Length) : (file, last.End);
            return with[..at] + StepEdits.Indent(text, last.Indent, nl) + nl + with[at..];
        }

        if (outline.Key is { } key)
        {
            var colon = file.IndexOf(':', key.End);
            var valueEnd = outline.Value is { } value && value.End > colon ? value.End : colon + 1;
            var keyIndent = key.Start - StepOutline.LineStart(file, key.Start);
            return file[..(colon + 1)] + nl + StepEdits.Indent(text, keyIndent + 2, nl) + file[valueEnd..];
        }

        // Before the first section of steps, else at the end.
        var block = "targets:" + nl + StepEdits.Indent(text, 2, nl) + nl;
        var before = outline.Root?.Entries.FirstOrDefault(e => e.Key.Value is "before" or "steps" or "after").Key;
        if (before is not null)
        {
            var at = StepOutline.LineStart(file, before.Start);
            return file[..at] + block + file[at..];
        }

        return (file.Length > 0 && file[^1] != '\n' ? file + nl : file) + block;
    }

    private static string Key(string name) => StepWriter.Text(name);
}
