// A step file shown read-only, line by line, with its problems marked. Editing
// comes with desktop milestone D4.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>How serious the worst problem on a line is.</summary>
public enum LineMark
{
    /// <summary>No problem.</summary>
    None,
    /// <summary>At least one warning, no error.</summary>
    Warning,
    /// <summary>At least one error.</summary>
    Error,
}

/// <summary>One line of a step file.</summary>
/// <param name="Number">The line number, from 1.</param>
/// <param name="Text">The line's text.</param>
public sealed partial class StepFileLineViewModel(int Number, string Text) : ObservableObject
{
    /// <summary>The line number, from 1.</summary>
    public int Number { get; } = Number;

    /// <summary>The line's text.</summary>
    public string Text { get; } = Text;

    /// <summary>The worst problem on the line.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsError), nameof(IsWarning))]
    private LineMark _mark;

    /// <summary>The problems' messages, one per line; null when none.</summary>
    [ObservableProperty]
    private string? _messages;

    /// <summary>True when the line has an error.</summary>
    public bool IsError => Mark == LineMark.Error;

    /// <summary>True when the line has a warning and no error.</summary>
    public bool IsWarning => Mark == LineMark.Warning;
}

/// <summary>A step file in a tab.</summary>
public sealed partial class StepFileViewModel : WorkspaceTabViewModel
{
    /// <summary>Creates the tab for a file.</summary>
    /// <param name="file">The file, relative to the project root.</param>
    public StepFileViewModel(string file)
    {
        File = file;
    }

    /// <summary>The file, relative to the project root.</summary>
    public string File { get; }

    /// <inheritdoc />
    public override string Title => File[(File.LastIndexOf('/') + 1)..];

    /// <inheritdoc />
    public override string ToolTip => File;

    /// <summary>The file's lines.</summary>
    public ObservableCollection<StepFileLineViewModel> Lines { get; } = [];

    /// <summary>Why the file could not be read, if it could not.</summary>
    [ObservableProperty]
    private string? _loadError;

    /// <summary>The line to bring into view and select; the view scrolls when it changes.</summary>
    [ObservableProperty]
    private StepFileLineViewModel? _revealedLine;

    /// <summary>"2 errors, 1 warning" for this file, or empty.</summary>
    [ObservableProperty]
    private string _problemSummary = string.Empty;

    private IReadOnlyList<Diagnostic> _diagnostics = [];

    /// <summary>Shows new text, keeping the problems marked.</summary>
    /// <param name="text">The file's text.</param>
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        LoadError = null;
        Lines.Clear();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var count = lines.Length > 1 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        for (var i = 0; i < count; i++)
        {
            Lines.Add(new StepFileLineViewModel(i + 1, lines[i]));
        }

        ApplyDiagnostics(_diagnostics);
    }

    /// <summary>Shows that the file cannot be read.</summary>
    /// <param name="message">Why.</param>
    public void SetLoadError(string message)
    {
        Lines.Clear();
        LoadError = message;
    }

    /// <summary>Marks the lines that have problems.</summary>
    /// <param name="diagnostics">Problems of any file; only this file's are used.</param>
    public void ApplyDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = [.. diagnostics.Where(d => d.File == File)];
        foreach (var line in Lines)
        {
            var here = _diagnostics.Where(d => d.Line <= line.Number && line.Number <= (d.EndLine ?? d.Line)).ToList();
            line.Mark = here.Count == 0 ? LineMark.None
                : here.Any(d => d.Severity != DiagnosticSeverity.Warning) ? LineMark.Error : LineMark.Warning;
            line.Messages = here.Count == 0 ? null : string.Join(Environment.NewLine, here.Select(d => $"{d.Code}: {d.Message}"));
        }

        var errors = _diagnostics.Count(d => d.Severity != DiagnosticSeverity.Warning);
        var warnings = _diagnostics.Count - errors;
        ProblemSummary = (errors, warnings) switch
        {
            (0, 0) => string.Empty,
            (_, 0) => errors == 1 ? "1 error" : $"{errors} errors",
            (0, _) => warnings == 1 ? "1 warning" : $"{warnings} warnings",
            _ => $"{errors} error{(errors == 1 ? string.Empty : "s")}, {warnings} warning{(warnings == 1 ? string.Empty : "s")}",
        };
    }

    /// <summary>Brings a line into view.</summary>
    /// <param name="number">The line number, from 1; clamped to the file.</param>
    public void Reveal(int number)
    {
        if (Lines.Count == 0)
        {
            return;
        }

        RevealedLine = null;
        RevealedLine = Lines[Math.Clamp(number, 1, Lines.Count) - 1];
    }
}
