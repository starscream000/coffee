// The form Record opens (instruction D0004, task 14): the test's name (its
// file is made from it, under tests/), the start address, the environment and
// an optional saved login. Without a browser it shows the engine's install
// command instead of starting. The engine's refusals are shown next to the
// name (a file that exists) or below the form.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels.Recording;

/// <summary>The form that starts a recording.</summary>
public sealed partial class RecordStartViewModel : ObservableObject
{
    /// <summary>The login choice that means none.</summary>
    public const string NoLogin = "(none)";

    private readonly Func<StartRecordingParams, Task> _start;
    private readonly Func<string, bool> _fileExists;

    /// <summary>Creates the form.</summary>
    /// <param name="environments">The project's environments.</param>
    /// <param name="environment">The environment chosen for runs, offered first.</param>
    /// <param name="logins">The project's saved logins.</param>
    /// <param name="noBrowser">Why recording cannot start (no browser, with the install command); null when it can.</param>
    /// <param name="fileExists">Says whether a project file exists, so a name that is taken is said before asking the engine.</param>
    /// <param name="start">Starts the recording; throws what the engine refused.</param>
    public RecordStartViewModel(
        IReadOnlyList<string> environments,
        string? environment,
        IReadOnlyList<string> logins,
        string? noBrowser,
        Func<string, bool> fileExists,
        Func<StartRecordingParams, Task> start)
    {
        Environments = environments;
        _environment = environment ?? (environments.Count > 0 ? environments[0] : null);
        Logins = [NoLogin, .. logins];
        NoBrowser = noBrowser;
        _fileExists = fileExists;
        _start = start;
    }

    /// <summary>Raised when the form is done: recording started, or the person cancelled.</summary>
    public event EventHandler? Closed;

    /// <summary>The project's environments.</summary>
    public IReadOnlyList<string> Environments { get; }

    /// <summary>True when the project has environments to choose from.</summary>
    public bool HasEnvironments => Environments.Count > 0;

    /// <summary>The saved logins, after <see cref="NoLogin"/>.</summary>
    public IReadOnlyList<string> Logins { get; }

    /// <summary>True when the project has saved logins.</summary>
    public bool HasLogins => Logins.Count > 1;

    /// <summary>Why recording cannot start, with the engine's install command; null when it can.</summary>
    public string? NoBrowser { get; }

    /// <summary>True when recording can start here.</summary>
    public bool HasBrowser => NoBrowser is null;

    /// <summary>The test's name, written into the file.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(File), nameof(FileText))]
    private string _testName = string.Empty;

    /// <summary>Where the browser starts: a path under the environment's address, or a full address.</summary>
    [ObservableProperty]
    private string _startUrl = "/";

    /// <summary>The environment to record in.</summary>
    [ObservableProperty]
    private string? _environment;

    /// <summary>The saved login to start with, or <see cref="NoLogin"/>.</summary>
    [ObservableProperty]
    private string _login = NoLogin;

    /// <summary>What is wrong with the name, or null.</summary>
    [ObservableProperty]
    private string? _nameError;

    /// <summary>What is wrong with the start address, or null.</summary>
    [ObservableProperty]
    private string? _startUrlError;

    /// <summary>A refusal that belongs to no field, or null.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>True while the engine opens the recording browser.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isStarting;

    /// <summary>The file the recording is written to, relative to the project root; empty without a name.</summary>
    public string File => NewProjectViewModel.FolderNameOf(TestName) is { Length: > 0 } slug ? $"tests/{slug}.test.yaml" : string.Empty;

    /// <summary>Says where the test is saved.</summary>
    public string FileText => File.Length == 0 ? "Name the test; its file is made from the name." : $"Saved as {File}";

    /// <summary>Checks the fields, then starts recording. Never throws: problems end next to their field.</summary>
    /// <returns>A task that completes when recording started or a problem is shown.</returns>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        Error = null;
        NameError = File.Length == 0 ? "Give the test a name, such as \"Add a to-do\"."
            : _fileExists(File) ? $"A test is already saved as {File}. Choose another name." : null;
        var start = StartUrl.Trim();
        StartUrlError = start.Length == 0 || start.StartsWith('/') || start.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || start.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? null
            : "Start with / for a path under the environment's address (such as /login), or give a full address starting with http:// or https://.";
        if (NameError is not null || StartUrlError is not null)
        {
            return;
        }

        IsStarting = true;
        try
        {
            await _start(new StartRecordingParams
            {
                File = File,
                StartUrl = start.Length == 0 ? null : start,
                Environment = Environment,
                Login = Login == NoLogin ? null : Login,
                Name = TestName.Trim(),
            });
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.FileExists)
        {
            NameError = ex.Message;
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.RecordingInProgress)
        {
            Error = $"A recording is already going on. Stop it before starting another. ({ex.Message})";
        }
        catch (EngineRequestException ex) when (ex.Name == ErrorCodes.RunInProgress)
        {
            Error = $"Tests are running. Recording can start once the run has ended. ({ex.Message})";
        }
        catch (Exception ex) when (ex is EngineException)
        {
            Error = $"Recording did not start: {ex.Message}";
        }
        finally
        {
            IsStarting = false;
        }
    }

    /// <summary>Closes the form without recording.</summary>
    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, EventArgs.Empty);

    private bool CanStart() => HasBrowser && !IsStarting;

    partial void OnTestNameChanged(string value) => NameError = null;

    partial void OnStartUrlChanged(string value) => StartUrlError = null;
}
