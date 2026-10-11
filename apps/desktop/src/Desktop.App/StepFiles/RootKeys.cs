// Changes one top-level key of a step file (such as a test's name, tags or
// skip reason) on the text: only that key's lines change, so comments and the
// rest of the file stay as they were.

namespace Desktop.App.StepFiles;

/// <summary>Writes a top-level key of a YAML file.</summary>
public static class RootKeys
{
    /// <summary>Sets, adds or removes a top-level key.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="root">The file's top-level mapping, as read from <paramref name="text"/>.</param>
    /// <param name="key">The key.</param>
    /// <param name="yaml">The value as YAML on one line, such as <c>[smoke, checkout]</c>; null to remove the key.</param>
    /// <param name="newLine">The file's line break.</param>
    /// <param name="after">Keys after which a new key goes, in order of preference; it goes first when none is there.</param>
    /// <returns>The new text.</returns>
    /// <example>Setting <c>tags</c> to <c>[smoke]</c> in <c>"version: 1\nname: A\nsteps: []\n"</c>, after <c>name</c>, gives <c>"version: 1\nname: A\ntags: [smoke]\nsteps: []\n"</c>.</example>
    public static string Set(string text, YamlMapping root, string key, string? yaml, string newLine, params string[] after)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(after);
        if (root.KeyOf(key) is { } existing)
        {
            var end = ValueEnd(text, existing, root.Get(key));
            if (yaml is null)
            {
                return text[..LineStart(text, existing.Start)] + text[LineEnd(text, end)..];
            }

            return text[..existing.Start] + $"{key}: {yaml}" + text[end..];
        }

        if (yaml is null)
        {
            return text;
        }

        var indent = root.Entries.Count > 0 ? root.Entries[0].Key.Start - LineStart(text, root.Entries[0].Key.Start) : 0;
        var line = new string(' ', indent) + $"{key}: {yaml}" + newLine;
        var anchor = after.Select(a => root.KeyOf(a)).FirstOrDefault(k => k is not null);
        var at = anchor is not null
            ? LineEnd(text, ValueEnd(text, anchor, root.Get(anchor.Value)))
            : root.Entries.Count > 0 ? LineStart(text, root.Entries[0].Key.Start) : text.Length;
        var gap = at == text.Length && text.Length > 0 && !text.EndsWith('\n') ? newLine : string.Empty;
        return text[..at] + gap + line + text[at..];
    }

    /// <summary>The end of a key's value, before any line break or space that follows it.</summary>
    private static int ValueEnd(string text, YamlScalar key, YamlNode? value)
    {
        var end = Math.Max(key.End, value?.End ?? key.End);
        while (end > key.End && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end;
    }

    private static int LineStart(string text, int at) => at <= 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;

    private static int LineEnd(string text, int at) => text.IndexOf('\n', at) is var n and >= 0 ? n + 1 : text.Length;
}
