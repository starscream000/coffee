// One character per state of a test or step in a run, so that the run view
// reads at a glance without relying on colour alone.

namespace Desktop.App.ViewModels.Runs;

/// <summary>Characters for run states.</summary>
public static class RunGlyphs
{
    /// <summary>The character for a state name of <see cref="StepRunState"/> or <see cref="TestRunState"/>.</summary>
    /// <param name="state">The state's name.</param>
    /// <returns>✓ passed, ✗ failed, – skipped, ⊘ cancelled, ! interrupted, … running, ○ pending.</returns>
    /// <example><c>RunGlyphs.Of(nameof(StepRunState.Passed))</c> is "✓".</example>
    public static string Of(string state) => state switch
    {
        "Passed" => "✓",
        "Failed" => "✗",
        "Skipped" => "–",
        "Cancelled" => "⊘",
        "Interrupted" => "!",
        "Running" => "…",
        _ => "○",
    };
}
