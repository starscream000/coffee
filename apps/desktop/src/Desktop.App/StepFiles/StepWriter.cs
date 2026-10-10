// Writes the text of one step from the values of its form. A step keeps the
// form it was written in where it can (the step format's rule: shorthand stays
// shorthand, a flow mapping stays a flow mapping), and a value the form did
// not change keeps its original text. Only the step's own lines are replaced
// (StepEdits.Replace), so the rest of the file stays as it was.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Desktop.App.StepFiles;

/// <summary>A key of a step and its value as YAML text, or null to leave the key out.</summary>
/// <param name="Name">The key.</param>
/// <param name="Yaml">The value as YAML, without indentation, lines joined with "\n"; null to leave the key out.</param>
public sealed record StepValue(string Name, string? Yaml);

/// <summary>Writes steps and YAML values.</summary>
public static partial class StepWriter
{
    /// <summary>The text of a step, without indentation.</summary>
    /// <param name="action">The action's name.</param>
    /// <param name="was">The step as it was, for its form; null for a new step.</param>
    /// <param name="shorthand">The action's shorthand parameter, if it has one.</param>
    /// <param name="parameters">The parameters, in the order to write them.</param>
    /// <param name="settings">The step's own keys (<c>name</c>, <c>page</c>, …), in order.</param>
    /// <returns>Such as <c>"- goto: /products"</c> or <c>"- fill:\n    target: email\n    value: x\n  name: Fill in"</c>.</returns>
    public static string Write(string action, StepOutlineItem? was, string? shorthand, IReadOnlyList<StepValue> parameters, IReadOnlyList<StepValue> settings)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(settings);
        var set = parameters.Where(p => p.Yaml is not null).ToList();
        var lines = new List<string>();
        if (set.Count == 0)
        {
            lines.Add($"- {action}");
        }
        else if (shorthand is not null && set is [{ } only] && only.Name == shorthand && was?.Form != StepForm.LongForm
            && (!only.Yaml!.Contains('\n') || was?.Form == StepForm.Shorthand))
        {
            // The action's key takes the shorthand value as a key takes its value: "- click: buy",
            // or a list or block scalar on the lines below it.
            var entry = Entry(action, only.Yaml!, 2).ToList();
            entry[0] = "- " + entry[0][2..];
            lines.AddRange(entry);
        }
        else if (was?.Value is YamlMapping { IsFlow: true } && set.All(p => !p.Yaml!.Contains('\n')))
        {
            lines.Add($"- {action}: {{ {string.Join(", ", set.Select(p => $"{p.Name}: {p.Yaml}"))} }}");
        }
        else
        {
            lines.Add($"- {action}:");
            foreach (var parameter in set)
            {
                lines.AddRange(Entry(parameter.Name, parameter.Yaml!, 4));
            }
        }

        foreach (var setting in settings.Where(s => s.Yaml is not null))
        {
            lines.AddRange(Entry(setting.Name, setting.Yaml!, 2));
        }

        return string.Join('\n', lines);
    }

    /// <summary>The original YAML text of a value, without its indentation, lines joined with "\n".</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="node">The value.</param>
    /// <returns>Such as <c>"'€20.00'"</c>, <c>"[Shift]"</c> or <c>"- role: button\n  name: Save"</c>.</returns>
    public static string Raw(string text, YamlNode node)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(node);
        var lines = text[node.Start..node.End].Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

        // A collection's lines line up with its first line; a block scalar's content lines are indented on their own.
        var rest = lines.Skip(1).Where(l => l.Trim().Length > 0).ToList();
        var isBlockScalar = node is YamlScalar && (lines[0].StartsWith('|') || lines[0].StartsWith('>'));
        var indent = isBlockScalar
            ? rest.Count == 0 ? 0 : rest.Min(l => l.Length - l.TrimStart(' ').Length)
            : node.Start - StepOutline.LineStart(text, node.Start);
        return string.Join('\n', lines.Take(1).Concat(lines.Skip(1).Select(l => l.Length >= indent ? l[indent..] : l.TrimStart(' '))));
    }

    /// <summary>A string as a YAML scalar: plain when that reads back as the same string, else in single quotes.</summary>
    /// <param name="value">The string.</param>
    /// <returns>Such as <c>/products</c>, <c>'€20.00'</c> written plain, <c>'2'</c> or <c>'a: b'</c>.</returns>
    /// <example><c>StepWriter.Text("true")</c> is <c>'true'</c>, so it stays a string.</example>
    public static string Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsSafePlain(value) ? value : "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    /// <summary>A number or a <c>${…}</c> as YAML; null when the text is neither.</summary>
    /// <param name="value">What was typed.</param>
    /// <returns>Such as <c>3</c>, <c>2.5</c> or <c>'${vars.count}'</c>.</returns>
    public static string? Number(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var trimmed = value.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _) && NumberLike().IsMatch(trimmed)
            ? trimmed
            : Interpolation().IsMatch(trimmed) ? Text(trimmed) : null;
    }

    private static IEnumerable<string> Entry(string key, string yaml, int indent)
    {
        var pad = new string(' ', indent);
        var lines = yaml.Split('\n');
        if (lines.Length == 1)
        {
            return [$"{pad}{key}: {yaml}"];
        }

        // A block scalar keeps its indicator on the key's line; a collection starts on the next line.
        var first = lines[0].StartsWith('|') || lines[0].StartsWith('>') ? [$"{pad}{key}: {lines[0]}"] : new[] { $"{pad}{key}:", $"{pad}  {lines[0]}" };
        return first.Concat(lines.Skip(1).Select(l => l.Length == 0 ? l : $"{pad}  {l}"));
    }

    private static bool IsSafePlain(string value) =>
        value.Length > 0
        && value.Trim() == value
        && value.IndexOfAny(['\n', '\r', '\t']) < 0
        && "-?:,[]{}#&*!|>'\"%@`".IndexOf(value[0], StringComparison.Ordinal) < 0
        && !value.Contains(": ", StringComparison.Ordinal)
        && !value.Contains(" #", StringComparison.Ordinal)
        && !value.EndsWith(':')
        && !OtherType().IsMatch(value);

    /// <summary>Plain scalars YAML would read as something other than a string: null, booleans (1.1 and 1.2), numbers.</summary>
    [GeneratedRegex(@"^(~|null|true|false|yes|no|on|off|y|n|[-+]?(\d[\d_]*(\.[\d_]*)?|\.\d[\d_]*)([eE][-+]?\d+)?|[-+]?\.(inf|nan)|0x[0-9a-f]+|0o[0-7]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OtherType();

    [GeneratedRegex(@"^[-+]?(\d+(\.\d+)?|\.\d+)([eE][-+]?\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberLike();

    [GeneratedRegex(@"^\$\{[^{}]+\}$", RegexOptions.CultureInvariant)]
    private static partial Regex Interpolation();
}
