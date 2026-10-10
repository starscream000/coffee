// A step file in an editable tab: the text being edited, saving and reverting,
// validation of the text while typing, changes made on disk by others, and
// the question before closing with unsaved changes. The view only binds.

using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.App.StepFiles;
using Desktop.App.ViewModels.Steps;
using Desktop.App.ViewModels.Targets;
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

/// <summary>What a step file tab needs from the app.</summary>
/// <param name="Root">The project root.</param>
/// <param name="Files">Reads and writes the file.</param>
/// <param name="Engine">Validates the text being edited.</param>
/// <param name="Dialogs">Asks before closing or overwriting.</param>
/// <param name="Delay">Waits before validating while typing.</param>
/// <param name="Report">Writes a line into the engine log.</param>
/// <param name="Run">Runs a test file; null when the tab cannot start runs.</param>
/// <param name="Actions">The actions the engine knows, for the step list; null for none.</param>
/// <param name="SharedTargets">The names of the project's shared targets, for the target picker; null for none.</param>
/// <param name="Rename">Renames the tab's file; null when the tab cannot.</param>
/// <param name="Delete">Deletes the tab's file; null when the tab cannot.</param>
public sealed record StepFileServices(
    string Root,
    IProjectFiles Files,
    IEngineService Engine,
    IDialogService Dialogs,
    IDelay Delay,
    Action<string> Report,
    Func<string, Task>? Run = null,
    Func<IReadOnlyList<StepActionChoice>>? Actions = null,
    Func<IReadOnlyList<string>>? SharedTargets = null,
    Func<StepFileViewModel, Task>? Rename = null,
    Func<StepFileViewModel, Task>? Delete = null);

/// <summary>An editable step file in a tab.</summary>
public sealed partial class StepFileViewModel : WorkspaceTabViewModel, IDisposable
{
    /// <summary>How long after the last change the text is validated.</summary>
    public static readonly TimeSpan ValidationDelay = TimeSpan.FromMilliseconds(300);

    private readonly StepFileServices _services;
    private string? _diskText;
    private string _savedNormalized = string.Empty;
    private string _lineEnding = "\n";
    private string? _changedDiskText;
    private bool _loading;
    private int _textVersion;
    private CancellationTokenSource? _pendingValidation;
    private IReadOnlyList<Diagnostic> _diagnostics = [];

    /// <summary>Creates the tab; call <see cref="LoadFromDisk"/> next.</summary>
    /// <param name="file">The file, relative to the project root.</param>
    /// <param name="services">What the tab needs from the app.</param>
    public StepFileViewModel(string file, StepFileServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        File = file;
        _services = services;
        Document = new TextDocument();
        Document.TextChanged += (_, _) => OnTextChanged();
        Document.UndoStack.PropertyChanged += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
        if (StepOutline.SectionsOf(file).Count > 0)
        {
            Steps = new StepListViewModel(file, Document, services.Actions ?? (() => []), () => LineMarks, services.SharedTargets, () => Diagnostics);
            Steps.StepSelected += (_, line) => Reveal(line);
            DiagnosticsChanged += (_, _) => Steps.RefreshMarks();
        }

        if (TargetsOutline.CanHaveTargets(file))
        {
            Targets = new TargetsEditorViewModel(Document, services.SharedTargets ?? (() => []));
            IsTargetsShown = Steps is null;
        }
    }

    /// <summary>The step list of a test or flow file; null for other files.</summary>
    public StepListViewModel? Steps { get; }

    /// <summary>True for a test or flow file, which has a step list.</summary>
    public bool HasSteps => Steps is not null;

    /// <summary>The targets editor of a test, flow or targets file; null for other files.</summary>
    public TargetsEditorViewModel? Targets { get; }

    /// <summary>True for a file with a targets editor.</summary>
    public bool HasTargets => Targets is not null;

    /// <summary>True for a test file, which can be run on its own.</summary>
    public bool IsTest => NewFiles.KindOf(File) == NewFileKind.Test;

    /// <summary>True when the side panel (step list or targets editor) is shown.</summary>
    public bool HasSidePanel => HasSteps || HasTargets;

    /// <summary>True when the side panel shows the targets editor rather than the step list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidePanelIndex))]
    private bool _isTargetsShown;

    /// <summary>The side panel's tab: 0 for the step list, 1 for the targets editor.</summary>
    public int SidePanelIndex
    {
        get => IsTargetsShown ? 1 : 0;
        set => IsTargetsShown = value == 1 || !HasSteps;
    }

    /// <summary>Shows a target in the targets editor, with a failure's match counts beside its candidates.</summary>
    /// <param name="name">The target's name.</param>
    /// <param name="counts">The failure's candidates and counts, or null.</param>
    /// <returns>False when the file does not declare the target.</returns>
    public bool ShowTarget(string name, IReadOnlyList<CandidateMatches>? counts)
    {
        if (Targets?.Select(name, counts) != true)
        {
            return false;
        }

        IsTargetsShown = true;
        if (Targets.Outline.Targets.FirstOrDefault(t => t.Name == name) is { } target)
        {
            Reveal(target.Line);
        }

        return true;
    }

    /// <summary>
    /// Raised with the problems of the text being edited, or with null when the
    /// tab has no unsaved changes any more and the file's own problems apply.
    /// </summary>
    public event EventHandler<IReadOnlyList<Diagnostic>?>? ContentDiagnosticsChanged;

    /// <summary>Raised after the file was saved, so the workspace validates it from disk.</summary>
    public event EventHandler? Saved;

    /// <summary>Raised when the problems shown in this tab changed.</summary>
    public event EventHandler? DiagnosticsChanged;

    /// <summary>The file, relative to the project root.</summary>
    public string File { get; }

    /// <summary>The text being edited, with its undo history.</summary>
    public TextDocument Document { get; }

    /// <inheritdoc />
    public override string Title => (IsDirty ? "● " : string.Empty) + File[(File.LastIndexOf('/') + 1)..];

    /// <inheritdoc />
    public override string ToolTip => IsDirty ? $"{File} (unsaved changes)" : File;

    /// <summary>The engine's problems for the text being edited, as last reported; null when the tab has no unsaved changes.</summary>
    public IReadOnlyList<Diagnostic>? ContentDiagnostics { get; private set; }

    /// <summary>Where the caret is, as a character offset; kept here so a new view of the tab puts it back.</summary>
    public int CaretOffset { get; set; }

    /// <summary>How far the editor is scrolled, in pixels from the top; kept here so a new view of the tab puts it back.</summary>
    public double VerticalScroll { get; set; }

    /// <summary>The problems of this file shown in the tab: of the text being edited while it has unsaved changes, else of the file on disk.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>The lines (from 1) that have problems, with the worst problem's severity; a problem spanning lines marks each.</summary>
    public IReadOnlyDictionary<int, LineMark> LineMarks { get; private set; } = new Dictionary<int, LineMark>();

    /// <summary>The messages of the problems on a line, one per line of text, or null when it has none.</summary>
    /// <param name="line">The line number, from 1.</param>
    /// <returns>The messages, or null.</returns>
    public string? MessagesAt(int line)
    {
        var here = _diagnostics.Where(d => d.Line <= line && line <= (d.EndLine ?? d.Line)).ToList();
        return here.Count == 0 ? null : string.Join(Environment.NewLine, here.Select(d => $"{d.Code}: {d.Message}"));
    }

    /// <summary>True when the text differs from the file as last read or saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ToolTip))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand))]
    private bool _isDirty;

    /// <summary>Why the file could not be read, if it could not.</summary>
    [ObservableProperty]
    private string? _loadError;

    /// <summary>Why the last save failed, if it did; the text is kept.</summary>
    [ObservableProperty]
    private string? _saveError;

    /// <summary>True when the file was deleted on disk; saving creates it again.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDeletedOnDisk;

    /// <summary>True when the file changed on disk while this tab has unsaved changes: the bar offering reload or keep is shown.</summary>
    [ObservableProperty]
    private bool _hasExternalChange;

    /// <summary>"2 errors, 1 warning" for the problems shown, or empty.</summary>
    [ObservableProperty]
    private string _problemSummary = string.Empty;

    /// <summary>The line to bring into view (from 1); the view scrolls when it is set, even to the same value.</summary>
    [ObservableProperty]
    private int? _revealedLine;

    /// <summary>Reads the file from disk into the editor, forgetting the undo history and any unsaved changes.</summary>
    public void LoadFromDisk()
    {
        string? text;
        try
        {
            text = _services.Files.TryReadText(_services.Root, File);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadError = $"{File} cannot be read: {ex.Message}";
            return;
        }

        if (text is null)
        {
            LoadError = $"{File} cannot be read: it does not exist.";
            IsDeletedOnDisk = true;
            return;
        }

        SetBaseline(text);
        _loading = true;
        try
        {
            Document.Text = text;
            Document.UndoStack.ClearAll();
        }
        finally
        {
            _loading = false;
        }

        LoadError = null;
        SaveError = null;
        IsDeletedOnDisk = false;
        HasExternalChange = false;
        UpdateDirty();
    }

    /// <summary>
    /// Puts back text that was being edited before the project was opened
    /// again, keeping the file on disk as the baseline.
    /// </summary>
    /// <param name="text">The edited text.</param>
    public void RestoreEdits(string text) => Document.Text = text;

    /// <summary>
    /// Reacts to the file changing on disk: nothing when the disk holds what this
    /// tab last read or saved (such as its own save); a reload when the tab has
    /// no unsaved changes; otherwise the bar offering reload or keep.
    /// </summary>
    public void OnDiskChanged()
    {
        string? text;
        try
        {
            text = _services.Files.TryReadText(_services.Root, File);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _services.Report($"{File} changed on disk but cannot be read: {ex.Message}");
            return;
        }

        if (text is null)
        {
            IsDeletedOnDisk = true;
            return;
        }

        IsDeletedOnDisk = false;
        if (text == _diskText)
        {
            return;
        }

        if (!IsDirty)
        {
            LoadFromDisk();
            return;
        }

        _changedDiskText = text;
        HasExternalChange = true;
    }

    /// <summary>Marks the lines that have problems.</summary>
    /// <param name="diagnostics">Problems of any file; only this file's are used.</param>
    public void ApplyDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = [.. diagnostics.Where(d => d.File == File)];
        var marks = new Dictionary<int, LineMark>();
        foreach (var d in _diagnostics)
        {
            var mark = d.Severity == DiagnosticSeverity.Warning ? LineMark.Warning : LineMark.Error;
            for (var line = d.Line; line <= Math.Max(d.Line, d.EndLine ?? d.Line); line++)
            {
                marks[line] = marks.TryGetValue(line, out var seen) && seen > mark ? seen : mark;
            }
        }

        LineMarks = marks;
        var errors = _diagnostics.Count(d => d.Severity != DiagnosticSeverity.Warning);
        var warnings = _diagnostics.Count - errors;
        ProblemSummary = (errors, warnings) switch
        {
            (0, 0) => string.Empty,
            (_, 0) => errors == 1 ? "1 error" : $"{errors} errors",
            (0, _) => warnings == 1 ? "1 warning" : $"{warnings} warnings",
            _ => $"{errors} error{(errors == 1 ? string.Empty : "s")}, {warnings} warning{(warnings == 1 ? string.Empty : "s")}",
        };
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Brings a line into view.</summary>
    /// <param name="number">The line number, from 1; clamped to the text.</param>
    public void Reveal(int number)
    {
        RevealedLine = null;
        RevealedLine = Math.Clamp(number, 1, Math.Max(1, Document.LineCount));
    }

    /// <summary>
    /// Saves the text: UTF-8 without a byte-order mark, with the line endings the
    /// file had when it was read, replacing the file in one step. Asks first when
    /// the file changed on disk since it was read. Never throws.
    /// </summary>
    /// <returns>True when the file was written.</returns>
    public async Task<bool> SaveAsync()
    {
        string? onDisk;
        try
        {
            onDisk = _services.Files.TryReadText(_services.Root, File);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onDisk = _diskText;
            _services.Report($"{File} could not be read before saving: {ex.Message}");
        }

        if (onDisk is not null && _diskText is not null && onDisk != _diskText && !await _services.Dialogs.AskOverwriteAsync(File))
        {
            return false;
        }

        var text = Normalize(Document.Text);
        try
        {
            _services.Files.WriteText(_services.Root, File, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = $"{File} could not be saved: {ex.Message} Your text is kept; try again or copy it elsewhere.";
            _services.Report($"Saving {File} failed: {ex}");
            return false;
        }

        SetBaseline(text);
        SaveError = null;
        IsDeletedOnDisk = false;
        HasExternalChange = false;
        LoadError = null;
        UpdateDirty();
        Saved?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    public override async Task<bool> ConfirmCloseAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        return await _services.Dialogs.AskUnsavedChangesAsync([File]) switch
        {
            UnsavedChangesChoice.Save => await SaveAsync(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save() => await SaveAsync();

    private bool CanSave() => IsDirty || IsDeletedOnDisk;

    /// <summary>Puts back the text as last read or saved (undoable).</summary>
    [RelayCommand(CanExecute = nameof(IsDirty))]
    private void Revert()
    {
        if (_diskText is not null)
        {
            Document.Text = _diskText;
        }
    }

    /// <summary>Throws away the unsaved changes and loads the file as it is now on disk.</summary>
    [RelayCommand]
    private void ReloadFromDisk() => LoadFromDisk();

    /// <summary>Keeps the text being edited; a later save overwrites the file without asking again.</summary>
    [RelayCommand]
    private void KeepMine()
    {
        // The file on disk becomes the baseline for both "changed on disk" and
        // "unsaved", so the tab stays unsaved while its text differs from the disk
        // (review D0002, finding 3).
        if (_changedDiskText is not null)
        {
            SetBaseline(_changedDiskText);
        }

        HasExternalChange = false;
        UpdateDirty();
    }

    /// <summary>Stops a validation that is waiting to run.</summary>
    public void Dispose()
    {
        _pendingValidation?.Cancel();
        _pendingValidation?.Dispose();
        _pendingValidation = null;
    }

    /// <summary>Renames the file (asks for the new name).</summary>
    /// <returns>A task that completes when the file is renamed or was not.</returns>
    [RelayCommand]
    private Task RenameAsync() => _services.Rename?.Invoke(this) ?? Task.CompletedTask;

    /// <summary>Deletes the file (asks first).</summary>
    /// <returns>A task that completes when the file is deleted or was not.</returns>
    [RelayCommand]
    private Task DeleteAsync() => _services.Delete?.Invoke(this) ?? Task.CompletedTask;

    /// <summary>Runs this file's test (after asking about unsaved changes).</summary>
    /// <returns>A task that completes when the run has started or was refused.</returns>
    [RelayCommand]
    private Task RunAsync() => _services.Run?.Invoke(File) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Document.UndoStack.Undo();

    private bool CanUndo() => Document.UndoStack.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Document.UndoStack.Redo();

    private bool CanRedo() => Document.UndoStack.CanRedo;

    private void SetBaseline(string diskText)
    {
        _diskText = diskText;
        _lineEnding = DetectLineEnding(diskText);
        _savedNormalized = Normalize(diskText);
        _changedDiskText = null;
    }

    private string Normalize(string text) => text.ReplaceLineEndings(_lineEnding);

    private static string DetectLineEnding(string text)
    {
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        return newline > 0 && text[newline - 1] == '\r' ? "\r\n" : "\n";
    }

    private void OnTextChanged()
    {
        if (_loading)
        {
            return;
        }

        UpdateDirty();
    }

    private void UpdateDirty()
    {
        IsDirty = Normalize(Document.Text) != _savedNormalized;
        _textVersion++;
        _pendingValidation?.Cancel();
        _pendingValidation?.Dispose();
        _pendingValidation = null;
        if (IsDirty)
        {
            _pendingValidation = new CancellationTokenSource();
            _ = ValidateLaterAsync(_textVersion, Document.Text, _pendingValidation.Token);
        }
        else
        {
            ContentDiagnostics = null;
            ContentDiagnosticsChanged?.Invoke(this, null);
        }
    }

    private async Task ValidateLaterAsync(int version, string text, CancellationToken cancellationToken)
    {
        try
        {
            await _services.Delay.WaitAsync(ValidationDelay, cancellationToken);
            var diagnostics = await _services.Engine.ValidateContentAsync(File, text, cancellationToken);
            if (version == _textVersion && IsDirty)
            {
                ContentDiagnostics = [.. diagnostics.Where(d => d.File == File)];
                ContentDiagnosticsChanged?.Invoke(this, ContentDiagnostics);
            }
        }
        catch (OperationCanceledException)
        {
            // Newer text arrived; its own validation follows.
        }
        catch (Exception ex)
        {
            // Validation while typing is a help, not a must: note it and go on.
            _services.Report($"Checking the unsaved text of {File} failed: {ex.Message}");
        }
    }
}
