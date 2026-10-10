// The recorder's place in the app: a tab that says that recording arrives with
// a later engine version (v0.2.0 of the plan) and what it will do. Nothing is
// recorded, and nothing pretends to be.

namespace Desktop.App.ViewModels;

/// <summary>The tab Record opens until the engine can record.</summary>
public sealed class RecorderTabViewModel : WorkspaceTabViewModel
{
    /// <inheritdoc />
    public override string Title => "Record";

    /// <summary>The heading.</summary>
    public static string Heading => "Recording is not here yet";

    /// <summary>What is true today.</summary>
    public static string Now =>
        "Recording arrives with a later version of the engine. This version of the engine cannot record, so this app records nothing yet.";

    /// <summary>What recording will do, in plain words.</summary>
    public static IReadOnlyList<string> WillDo { get; } =
    [
        "Record will open the test browser on a page of your app, for the environment you chose.",
        "As you click, type, choose and check things, each action becomes a step of a test file, written in the same YAML as the steps you build in the step list.",
        "Each element you use becomes a target with several ways to find it, most reliable first: its role and name, its test ID, and CSS last. They are added to the file's targets, where the targets editor shows them.",
        "You stop recording when you are done, then review the steps and targets, add checks (expect steps) and save the file like any other.",
    ];

    /// <summary>What to do meanwhile.</summary>
    public static string Meanwhile =>
        "Until then, create a test with New test and build it in the step list: pick an action, fill in its form, and pick targets from the file's and the shared targets.";
}
