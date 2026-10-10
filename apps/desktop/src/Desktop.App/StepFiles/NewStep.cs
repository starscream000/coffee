// The text of a new step for an action picked from the catalogue: the action
// alone when it needs nothing, its shorthand with an empty value when it has
// one, else the long form with its required parameters empty. The engine then
// reports what is still missing, so the step shows as needing values.

namespace Desktop.App.StepFiles;

/// <summary>Writes new steps.</summary>
public static class NewStep
{
    /// <summary>The text of a new step, without indentation.</summary>
    /// <param name="action">The action's name.</param>
    /// <param name="shorthand">The action's shorthand parameter, if it has one.</param>
    /// <param name="required">The parameters its long form requires.</param>
    /// <returns>Such as <c>"- back"</c>, <c>"- goto: ''"</c> or <c>"- fill:\n    target: ''\n    value: ''"</c>.</returns>
    /// <example><c>NewStep.Text("fill", null, ["target", "value"])</c> is <c>"- fill:\n    target: ''\n    value: ''"</c>.</example>
    public static string Text(string action, string? shorthand, IReadOnlyList<string> required)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(required);
        if (shorthand is not null)
        {
            return $"- {action}: ''";
        }

        return required.Count == 0
            ? $"- {action}"
            : $"- {action}:\n" + string.Join('\n', required.Select(p => $"    {p}: ''"));
    }
}
