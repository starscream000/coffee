// Puts a new text into an editor document as one replacement of the part that
// differs, so the text editor shows the change at once, keeps the caret and
// the rest of the text, and its undo takes the change back in one step. Used
// by the step list and the targets editor, which compute changes on the text.

using AvaloniaEdit.Document;

namespace Desktop.App.ViewModels;

/// <summary>Replaces a document's text by its difference.</summary>
public static class DocumentText
{
    /// <summary>Replaces the part of the document's text that differs from <paramref name="text"/>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="text">The new text.</param>
    /// <returns>False when the text was already the same, and nothing changed.</returns>
    /// <example>Changing <c>"a: 1\nb: 2\n"</c> to <c>"a: 1\nb: 3\n"</c> replaces one character.</example>
    public static bool Replace(TextDocument document, string text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);
        var old = document.Text;
        if (text == old)
        {
            return false;
        }

        var prefix = 0;
        var max = Math.Min(old.Length, text.Length);
        while (prefix < max && old[prefix] == text[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < max - prefix && old[old.Length - 1 - suffix] == text[text.Length - 1 - suffix])
        {
            suffix++;
        }

        document.Replace(prefix, old.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        return true;
    }
}
