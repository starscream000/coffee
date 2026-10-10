// Changes to a test or flow file made from the step list: add, remove,
// duplicate, move a step and move it to another section. Each returns the
// file's new text, in which only the lines of the steps concerned (and, for a
// section that appears or empties, its key line) differ; comments and layout
// elsewhere are left as they were. Step texts are written without indentation,
// with "\n" between lines, and indented here to fit where they go.

namespace Desktop.App.StepFiles;

/// <summary>Text changes for the step list.</summary>
public static class StepEdits
{
    /// <summary>Removes a step. A section left without steps becomes <c>key: []</c>, so it stays valid.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="item">The step.</param>
    /// <returns>The new text.</returns>
    public static string Remove(StepOutline outline, StepOutlineItem item)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(item);
        var text = outline.Text;
        var section = outline.Sections.Single(s => s.Name == item.Section);
        if (section.Steps.Count == 1 && section.Key is { } key)
        {
            var colon = text.IndexOf(':', key.End);
            var keepBreak = item.End > 0 && text[item.End - 1] == '\n' ? outline.NewLine : string.Empty;
            return text[..(colon + 1)] + " []" + keepBreak + text[item.End..];
        }

        return text[..item.Start] + text[item.End..];
    }

    /// <summary>Inserts a copy of a step right after it.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="item">The step.</param>
    /// <returns>The new text.</returns>
    public static string Duplicate(StepOutline outline, StepOutlineItem item)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(item);
        var (text, end) = EnsureBreakAfter(outline, item.End);
        return text[..end] + WithBreak(outline.Text[item.Start..item.End], outline.NewLine) + text[end..];
    }

    /// <summary>Swaps a step with the one before (<paramref name="delta"/> −1) or after (+1) it in its section.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="item">The step.</param>
    /// <param name="delta">−1 or +1.</param>
    /// <returns>The new text; the same text when there is no step to swap with.</returns>
    public static string Move(StepOutline outline, StepOutlineItem item, int delta)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(item);
        var steps = outline.Sections.Single(s => s.Name == item.Section).Steps;
        var other = item.Index + delta;
        if (delta is not (-1 or 1) || other < 0 || other >= steps.Count)
        {
            return outline.Text;
        }

        var (first, second) = delta < 0 ? (steps[other], item) : (item, steps[other]);
        var text = outline.Text;
        var endsWithBreak = second.End > 0 && text[second.End - 1] == '\n';
        var swapped = WithBreak(text[second.Start..second.End], outline.NewLine)
            + text[first.End..second.Start]
            + WithBreak(text[first.Start..first.End], outline.NewLine);
        if (!endsWithBreak)
        {
            swapped = swapped[..^outline.NewLine.Length];
        }

        return text[..first.Start] + swapped + text[second.End..];
    }

    /// <summary>Moves a step to the end of another section, creating the section when the file has none.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="item">The step.</param>
    /// <param name="section">The other section's key.</param>
    /// <returns>The new text.</returns>
    /// <exception cref="InvalidOperationException">The text without the step can no longer be read as steps.</exception>
    public static string MoveToSection(StepOutline outline, StepOutlineItem item, string section)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(item);
        if (section == item.Section)
        {
            return outline.Text;
        }

        var step = TextOf(outline, item);
        var file = SectionsFile(outline);
        var without = StepOutline.Read(file, Remove(outline, item));
        return without.IsReadable
            ? Insert(without, section, null, step)
            : throw new InvalidOperationException(without.Problem);
    }

    /// <summary>Inserts a step into a section: after the step at <paramref name="afterIndex"/>, or at the end.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="section">The section's key; created when absent.</param>
    /// <param name="afterIndex">The step to insert after; null for the end of the section; −1 for the start.</param>
    /// <param name="step">The step's text without indentation, such as <c>"- goto: /products"</c>.</param>
    /// <returns>The new text.</returns>
    public static string Insert(StepOutline outline, string section, int? afterIndex, string step)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(step);
        var target = outline.Sections.Single(s => s.Name == section);
        var text = outline.Text;
        var nl = outline.NewLine;
        if (target.Steps.Count > 0)
        {
            var indent = target.Steps[0].Indent;
            if (afterIndex == -1)
            {
                var at = target.Steps[0].Start;
                return text[..at] + Indent(step, indent, nl) + nl + text[at..];
            }

            var anchor = target.Steps[Math.Clamp(afterIndex ?? target.Steps.Count - 1, 0, target.Steps.Count - 1)];
            var (with, end) = EnsureBreakAfter(outline, anchor.End);
            return with[..end] + Indent(step, indent, nl) + nl + with[end..];
        }

        if (target.Key is { } key)
        {
            // "after:" or "after: []": the steps go on the lines below the key.
            var colon = text.IndexOf(':', key.End);
            var valueEnd = target.Value is { } value && value.End > colon ? value.End : colon + 1;
            var keyIndent = key.Start - StepOutline.LineStart(text, key.Start);
            return text[..(colon + 1)] + nl + Indent(step, keyIndent + 2, nl) + text[valueEnd..];
        }

        return InsertSection(outline, section, step);
    }

    /// <summary>Replaces a step's lines with new text for it.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="item">The step.</param>
    /// <param name="step">The step's new text without indentation, such as <c>"- goto: /cart"</c>.</param>
    /// <returns>The new text.</returns>
    public static string Replace(StepOutline outline, StepOutlineItem item, string step)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(step);
        var text = outline.Text;
        var endsWithBreak = item.End > 0 && text[item.End - 1] == '\n';
        return text[..item.Start] + Indent(step, item.Indent, outline.NewLine) + (endsWithBreak ? outline.NewLine : string.Empty) + text[item.End..];
    }

    /// <summary>A step's text without its indentation, with "\n" between lines and no final line break.</summary>
    /// <param name="outline">The file.</param>
    /// <param name="item">The step.</param>
    /// <returns>Such as <c>"- fill:\n    target: email\n    value: x"</c>.</returns>
    public static string TextOf(StepOutline outline, StepOutlineItem item)
    {
        ArgumentNullException.ThrowIfNull(outline);
        ArgumentNullException.ThrowIfNull(item);
        var lines = outline.Text[item.Start..item.End].Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        return string.Join('\n', lines.Select(l => l[Math.Min(item.Indent, l.Length - l.TrimStart(' ').Length)..]));
    }

    /// <summary>Indents every line of a step's text and joins the lines with the file's line break, without a final one.</summary>
    /// <param name="step">The step's text without indentation.</param>
    /// <param name="indent">The column of its dash.</param>
    /// <param name="newLine">The file's line break.</param>
    /// <returns>The indented text.</returns>
    public static string Indent(string step, int indent, string newLine)
    {
        ArgumentNullException.ThrowIfNull(step);
        var pad = new string(' ', indent);
        return string.Join(newLine, step.Split('\n').Select(l => l.Length == 0 ? l : pad + l));
    }

    private static string InsertSection(StepOutline outline, string section, string step)
    {
        var text = outline.Text;
        var nl = outline.NewLine;
        var root = outline.Root!;
        var keyIndent = root.Entries.Count == 0 ? 0 : root.Entries[0].Key.Start - StepOutline.LineStart(text, root.Entries[0].Key.Start);
        var block = new string(' ', keyIndent) + section + ":" + nl + Indent(step, keyIndent + 2, nl) + nl;
        var order = outline.Sections.Select(s => s.Name).ToList();
        var position = order.IndexOf(section);

        // After the nearest section before it that the file has, else before the nearest one after it, else at the end.
        var before = outline.Sections.Take(position).LastOrDefault(s => s.Key is not null);
        if (before is not null)
        {
            var end = Math.Max(before.Key!.End, before.Value?.End ?? 0);
            var (with, at) = EnsureBreakAfter(outline, StepOutline.LineEndAfter(text, Math.Max(end - 1, 0)));
            return with[..at] + block + with[at..];
        }

        var next = outline.Sections.Skip(position + 1).FirstOrDefault(s => s.Key is not null);
        if (next is not null)
        {
            var at = StepOutline.LineStart(text, next.Key!.Start);
            return text[..at] + block + text[at..];
        }

        var (all, endOfText) = EnsureBreakAfter(outline, text.Length);
        return all[..endOfText] + block;
    }

    /// <summary>When a position is the end of a text that has no final line break, adds one.</summary>
    private static (string Text, int Position) EnsureBreakAfter(StepOutline outline, int position)
    {
        var text = outline.Text;
        return position == text.Length && text.Length > 0 && text[^1] != '\n'
            ? (text + outline.NewLine, text.Length + outline.NewLine.Length)
            : (text, position);
    }

    private static string WithBreak(string span, string newLine) => span.EndsWith('\n') ? span : span + newLine;

    /// <summary>A file name with the sections of the outline, for reading the text again.</summary>
    private static string SectionsFile(StepOutline outline) =>
        outline.Sections.Count == 3 ? "x.test.yaml" : "x.flow.yaml";
}
