// The sections and steps of a test or flow file as the step list shows them,
// read from the text with the lines each step takes. The three ways a step can
// be written (docs/step-format.md, "Steps") are told apart here: an action
// alone, the action with its shorthand value, and the long form.

namespace Desktop.App.StepFiles;

/// <summary>How a step is written.</summary>
public enum StepForm
{
    /// <summary>The action alone: <c>- back</c>.</summary>
    Bare,

    /// <summary>The action with its shorthand parameter: <c>- goto: /products</c>.</summary>
    Shorthand,

    /// <summary>The action with a mapping of named parameters.</summary>
    LongForm,

    /// <summary>Not a step the list can read (no action key, two of them, or not a mapping); it can be moved or removed.</summary>
    Unreadable,
}

/// <summary>One step in a section.</summary>
/// <param name="Section">The section's key: <c>before</c>, <c>steps</c> or <c>after</c>.</param>
/// <param name="Index">Its position in the section, from 0.</param>
/// <param name="Action">The action's name; empty for an unreadable step.</param>
/// <param name="Form">How it is written.</param>
/// <param name="Value">The action key's value: the shorthand value or the parameter mapping; null for a bare step.</param>
/// <param name="Settings">The step's own keys besides the action (<c>name</c>, <c>page</c>, <c>timeout</c>, <c>opens</c>), in order.</param>
/// <param name="Node">The step's node.</param>
/// <param name="Start">Offset of the start of its first line (the line with its dash).</param>
/// <param name="End">Offset just after the line break of its last line, or the end of the text.</param>
/// <param name="Line">1-based line of its dash.</param>
/// <param name="EndLine">1-based line of its last character.</param>
/// <param name="Indent">Column of its dash.</param>
public sealed record StepOutlineItem(
    string Section,
    int Index,
    string Action,
    StepForm Form,
    YamlNode? Value,
    IReadOnlyList<KeyValuePair<YamlScalar, YamlNode>> Settings,
    YamlNode Node,
    int Start,
    int End,
    int Line,
    int EndLine,
    int Indent);

/// <summary>A section of steps.</summary>
/// <param name="Name">Its key: <c>before</c>, <c>steps</c> or <c>after</c>.</param>
/// <param name="Key">The key's node; null when the file does not have the section yet.</param>
/// <param name="Value">The key's value: a sequence, or a null scalar for <c>after:</c> with nothing; null when absent.</param>
/// <param name="Steps">Its steps, in order.</param>
public sealed record StepSectionOutline(string Name, YamlScalar? Key, YamlNode? Value, IReadOnlyList<StepOutlineItem> Steps);

/// <summary>A test or flow file read as sections of steps, or why it could not be.</summary>
/// <param name="Text">The text read.</param>
/// <param name="Root">The root mapping; null when the file could not be read.</param>
/// <param name="Sections">The sections the file kind has, in order; present even when absent from the file.</param>
/// <param name="Problem">Why the file cannot be shown as steps; null when it can.</param>
/// <param name="NewLine">The line break the file uses.</param>
public sealed record StepOutline(string Text, YamlMapping? Root, IReadOnlyList<StepSectionOutline> Sections, string? Problem, string NewLine)
{
    /// <summary>The keys a step may have besides its action.</summary>
    public static readonly IReadOnlySet<string> SettingKeys = new HashSet<string>(StringComparer.Ordinal) { "name", "page", "timeout", "opens" };

    /// <summary>True when the file can be shown as steps.</summary>
    public bool IsReadable => Problem is null;

    /// <summary>Every step, section after section.</summary>
    public IEnumerable<StepOutlineItem> Steps => Sections.SelectMany(s => s.Steps);

    /// <summary>The sections a file has, by its name.</summary>
    /// <param name="file">The file's path or name.</param>
    /// <returns><c>before</c>, <c>steps</c>, <c>after</c> for a test; <c>steps</c> for a flow; none for other files.</returns>
    public static IReadOnlyList<string> SectionsOf(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var name = file.ToLowerInvariant();
        return name.EndsWith(".test.yaml", StringComparison.Ordinal) || name.EndsWith(".test.yml", StringComparison.Ordinal)
            ? ["before", "steps", "after"]
            : name.EndsWith(".flow.yaml", StringComparison.Ordinal) || name.EndsWith(".flow.yml", StringComparison.Ordinal)
                ? ["steps"]
                : [];
    }

    /// <summary>Reads a test or flow file.</summary>
    /// <param name="file">The file's path, for its kind.</param>
    /// <param name="text">Its text.</param>
    /// <returns>The outline; when it cannot be read, <see cref="Problem"/> says why. Never throws for bad input.</returns>
    public static StepOutline Read(string file, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var names = SectionsOf(file);
        if (names.Count == 0)
        {
            return Unreadable(text, "Only test and flow files have steps.", newLine);
        }

        YamlNode? root;
        try
        {
            root = YamlTree.Read(text);
        }
        catch (YamlTreeException ex)
        {
            return Unreadable(text, ex.LineNumber is { } line ? $"Line {line}: {ex.Message}" : ex.Message, newLine);
        }

        if (root is not YamlMapping { IsFlow: false } mapping)
        {
            return Unreadable(text, root is null ? "The file is empty." : "The file is not a mapping of keys (version, name, steps, …).", newLine);
        }

        var sections = new List<StepSectionOutline>();
        foreach (var name in names)
        {
            var key = mapping.KeyOf(name);
            var value = mapping.Get(name);
            switch (value)
            {
                case null:
                case YamlScalar { IsNull: true }:
                    sections.Add(new StepSectionOutline(name, key, value, []));
                    break;
                case YamlSequence { IsFlow: true, Items.Count: 0 }:
                    sections.Add(new StepSectionOutline(name, key, value, []));
                    break;
                case YamlSequence { IsFlow: false } sequence:
                    sections.Add(new StepSectionOutline(name, key, value, [.. sequence.Items.Select((item, i) => Item(text, name, i, item))]));
                    break;
                default:
                    return Unreadable(text, $"Line {value.Line}: \"{name}\" is not a list of steps written one per line (\"- …\"), so the step list cannot show it.", newLine);
            }
        }

        return new StepOutline(text, mapping, sections, null, newLine);
    }

    private static StepOutline Unreadable(string text, string problem, string newLine) => new(text, null, [], problem, newLine);

    private static StepOutlineItem Item(string text, string section, int index, YamlNode node)
    {
        var dash = text.LastIndexOf('-', Math.Max(node.Start - 1, 0));
        var start = LineStart(text, dash < 0 ? node.Start : dash);
        var indent = (dash < 0 ? node.Start : dash) - start;
        var end = LineEndAfter(text, Math.Max(node.End - 1, node.Start));
        var endLine = node.Line + Count(text, node.Start, Math.Max(node.End - 1, node.Start), '\n');
        var line = node.Line - Count(text, start, node.Start, '\n');
        StepOutlineItem Make(string action, StepForm form, YamlNode? value, IReadOnlyList<KeyValuePair<YamlScalar, YamlNode>> settings) =>
            new(section, index, action, form, value, settings, node, start, end, line, endLine, indent);

        switch (node)
        {
            case YamlScalar { IsPlain: true, IsNull: false } scalar:
                return Make(scalar.Value, StepForm.Bare, null, []);
            case YamlMapping mapping:
                var actions = mapping.Entries.Where(e => !SettingKeys.Contains(e.Key.Value)).ToList();
                var settings = mapping.Entries.Where(e => SettingKeys.Contains(e.Key.Value)).ToList();
                if (actions.Count != 1)
                {
                    return Make(string.Empty, StepForm.Unreadable, null, settings);
                }

                var (key, value) = (actions[0].Key, actions[0].Value);
                var form = value switch
                {
                    YamlScalar { IsNull: true } => StepForm.Bare,
                    YamlMapping => StepForm.LongForm,
                    _ => StepForm.Shorthand,
                };
                return Make(key.Value, form, form == StepForm.Bare ? null : value, settings);
            default:
                return Make(string.Empty, StepForm.Unreadable, null, []);
        }
    }

    /// <summary>The offset of the start of the line holding an offset.</summary>
    internal static int LineStart(string text, int offset) => offset <= 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;

    /// <summary>The offset just after the line break of the line holding an offset, or the end of the text.</summary>
    internal static int LineEndAfter(string text, int offset)
    {
        var lineBreak = text.IndexOf('\n', Math.Min(offset, text.Length));
        return lineBreak < 0 ? text.Length : lineBreak + 1;
    }

    private static int Count(string text, int from, int to, char c)
    {
        var n = 0;
        for (var i = Math.Max(from, 0); i < Math.Min(to, text.Length); i++)
        {
            if (text[i] == c)
            {
                n++;
            }
        }

        return n;
    }
}
