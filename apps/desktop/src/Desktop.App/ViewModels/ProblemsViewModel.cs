// The problems panel: diagnostics from openProject (config and user actions)
// and from validating the test files, sorted by file and position.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>One diagnostic in the problems panel.</summary>
/// <param name="Diagnostic">The diagnostic.</param>
public sealed record ProblemItemViewModel(Diagnostic Diagnostic)
{
    /// <summary>True for an error, false for a warning.</summary>
    public bool IsError => Diagnostic.Severity != DiagnosticSeverity.Warning;

    /// <summary>"file:line:column".</summary>
    public string Location => $"{Diagnostic.File}:{Diagnostic.Line}:{Diagnostic.Column}";

    /// <summary>The message, with the hint on a new line when there is one.</summary>
    public string Text => Diagnostic.Hint is { } hint ? $"{Diagnostic.Message}{Environment.NewLine}{hint}" : Diagnostic.Message;
}

/// <summary>The problems panel.</summary>
public sealed partial class ProblemsViewModel : ObservableObject
{
    private IReadOnlyList<Diagnostic> _projectDiagnostics = [];
    private IReadOnlyList<Diagnostic> _validationDiagnostics = [];
    private readonly Dictionary<string, IReadOnlyList<Diagnostic>> _overrides = new(StringComparer.Ordinal);

    /// <summary>Raised when the user picks a problem: open its file at its line.</summary>
    public event EventHandler<Diagnostic>? ProblemActivated;

    /// <summary>Raised when the set of problems changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The problems shown, after the severity filter.</summary>
    public ObservableCollection<ProblemItemViewModel> Items { get; } = [];

    /// <summary>Every problem, unfiltered.</summary>
    public IReadOnlyList<Diagnostic> All { get; private set; } = [];

    /// <summary>Number of errors.</summary>
    [ObservableProperty]
    private int _errorCount;

    /// <summary>Number of warnings.</summary>
    [ObservableProperty]
    private int _warningCount;

    /// <summary>Show warnings as well as errors.</summary>
    [ObservableProperty]
    private bool _showWarnings = true;

    /// <summary>The selected problem.</summary>
    [ObservableProperty]
    private ProblemItemViewModel? _selectedItem;

    /// <summary>"No problems", or "2 errors, 1 warning".</summary>
    public string Summary => (ErrorCount, WarningCount) switch
    {
        (0, 0) => "No problems",
        (_, 0) => Count(ErrorCount, "error"),
        (0, _) => Count(WarningCount, "warning"),
        _ => $"{Count(ErrorCount, "error")}, {Count(WarningCount, "warning")}",
    };

    /// <summary>Replaces the problems of the config and user actions.</summary>
    /// <param name="diagnostics">From <c>openProject</c>.</param>
    public void SetProjectDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        _projectDiagnostics = diagnostics;
        Rebuild();
    }

    /// <summary>Replaces the problems found by validating the test files.</summary>
    /// <param name="diagnostics">From <c>validate</c>.</param>
    public void SetValidationDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        _validationDiagnostics = diagnostics;
        Rebuild();
    }

    /// <summary>
    /// Shows <paramref name="diagnostics"/> for <paramref name="file"/> instead of
    /// what the project and the validation of files on disk say about it: the
    /// problems of text being edited. Null removes the override.
    /// </summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <param name="diagnostics">Its problems, or null.</param>
    public void SetFileOverride(string file, IReadOnlyList<Diagnostic>? diagnostics)
    {
        if (diagnostics is not null)
        {
            _overrides[file] = diagnostics;
            Rebuild();
        }
        else if (_overrides.Remove(file))
        {
            Rebuild();
        }
    }

    /// <summary>Errors and warnings per file.</summary>
    /// <returns>Counts by relative path.</returns>
    public IReadOnlyDictionary<string, (int Errors, int Warnings)> CountsByFile() =>
        All.GroupBy(d => d.File, StringComparer.Ordinal).ToDictionary(
            g => g.Key,
            g => (g.Count(d => d.Severity != DiagnosticSeverity.Warning), g.Count(d => d.Severity == DiagnosticSeverity.Warning)),
            StringComparer.Ordinal);

    partial void OnShowWarningsChanged(bool value) => Rebuild();

    partial void OnSelectedItemChanged(ProblemItemViewModel? value)
    {
        if (value is not null)
        {
            ProblemActivated?.Invoke(this, value.Diagnostic);
        }
    }

    private void Rebuild()
    {
        All = [.. _projectDiagnostics.Concat(_validationDiagnostics)
            .Where(d => !_overrides.ContainsKey(d.File))
            .Concat(_overrides.Values.SelectMany(o => o))
            .Distinct()
            .OrderBy(d => d.File, StringComparer.Ordinal)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Column)
            .ThenBy(d => d.Code, StringComparer.Ordinal)];
        ErrorCount = All.Count(d => d.Severity != DiagnosticSeverity.Warning);
        WarningCount = All.Count(d => d.Severity == DiagnosticSeverity.Warning);
        Items.Clear();
        foreach (var diagnostic in All.Where(d => ShowWarnings || d.Severity != DiagnosticSeverity.Warning))
        {
            Items.Add(new ProblemItemViewModel(diagnostic));
        }

        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Count(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";
}
