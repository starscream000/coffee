// Fakes of the app's services for the view model tests.

using System.Text.Json;
using Desktop.App.Services;
using Desktop.Engine;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Fakes;

/// <summary>Settings kept in memory.</summary>
internal sealed class MemorySettingsStore : ISettingsStore
{
    public AppSettings Settings { get; set; } = new();

    public int Saves { get; private set; }

    public AppSettings Load() => Settings;

    public void Save(AppSettings settings)
    {
        Settings = settings;
        Saves++;
    }
}

/// <summary>A folder dialog that returns a fixed answer.</summary>
internal sealed class FakeFolderPicker(string? answer) : IFolderPicker
{
    public Task<string?> PickFolderAsync(string title) => Task.FromResult(answer);
}

/// <summary>Project files in memory: relative path to text.</summary>
internal sealed class FakeProjectFiles : IProjectFiles
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    public Action<IReadOnlyCollection<string>>? Changed { get; private set; }

    /// <summary>Paths whose writing fails, with the error.</summary>
    public Dictionary<string, Exception> WriteFailures { get; } = new(StringComparer.Ordinal);

    /// <summary>Every write, in order.</summary>
    public List<(string File, string Text)> Writes { get; } = [];

    public string ReadText(string root, string relativePath) =>
        Files.TryGetValue(relativePath, out var text) ? text : throw new FileNotFoundException("not found", relativePath);

    public string? TryReadText(string root, string relativePath) => Files.GetValueOrDefault(relativePath);

    public bool Exists(string root, string relativePath) =>
        Files.ContainsKey(relativePath) || Files.Keys.Any(f => f.StartsWith(relativePath + "/", StringComparison.Ordinal));

    public void Move(string root, string source, string destination)
    {
        if (Files.ContainsKey(destination) || !Files.Remove(source, out var text))
        {
            throw new IOException($"Cannot move {source} to {destination}.");
        }

        Files[destination] = text;
    }

    public void Delete(string root, string relativePath) => Files.Remove(relativePath);

    public IReadOnlyList<string> FindFiles(string root, string ending) =>
        [.. Files.Keys.Where(f => f.EndsWith(ending, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)];

    public void WriteText(string root, string relativePath, string text)
    {
        if (WriteFailures.TryGetValue(relativePath, out var failure))
        {
            throw failure;
        }

        Writes.Add((relativePath, text));
        Files[relativePath] = text;
    }

    public IDisposable Watch(string root, Action<IReadOnlyCollection<string>> changed)
    {
        Changed = changed;
        return new Stop(() => Changed = null);
    }

    private sealed class Stop(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}

/// <summary>An engine whose answers the test sets.</summary>
internal sealed class FakeEngineService : IEngineService
{
    public event EventHandler? StateChanged;

    public event EventHandler<EngineLogLine>? LogLine;

    public event EventHandler<EngineEvent>? EventReceived;

    public EngineState State { get; set; } = EngineState.Stopped;

    public InitializeResult? Info { get; set; }

    public Exception? Failure { get; set; }

    /// <summary>What StartAsync does; becomes Ready by default.</summary>
    public Func<FakeEngineService, Task>? OnStart { get; set; }

    public Func<string, OpenProjectResult> OpenProject { get; set; } = root => Project(root);

    public IReadOnlyList<TestInfo> Tests { get; set; } = [];

    public IReadOnlyList<ActionInfo> Actions { get; set; } = [];

    public Func<IReadOnlyList<string>, IReadOnlyList<Diagnostic>> Validate { get; set; } = _ => [];

    public List<string> Calls { get; } = [];

    public FakeEngineService() =>
        StartRun = _ => new StartRunResult { RunId = $"run-{Started.Count}", ResultsDir = $"/work/shop-tests/.runs/run-{Started.Count}" };

    public static OpenProjectResult Project(string root, params Diagnostic[] diagnostics) => new()
    {
        Root = root,
        ConfigFile = root + "/cfg.yaml",
        Environments = ["local", "staging"],
        DefaultEnvironment = "local",
        Logins = ["customer"],
        Diagnostics = diagnostics,
    };

    public static InitializeResult ReadyInfo(params string[] browsers) => new()
    {
        ProtocolVersion = Protocol.ProtocolVersion.Current,
        Engine = new SoftwareInfo { Name = "fake", Version = "1.0.0" },
        Capabilities = new EngineCapabilities { Browsers = browsers },
    };

    public void SetState(EngineState state, Exception? failure = null)
    {
        State = state;
        Failure = failure;
        Info = state == EngineState.Ready ? Info ?? ReadyInfo() : null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Log(string text) => LogLine?.Invoke(this, new EngineLogLine(DateTimeOffset.Now, EngineLogSource.Stderr, text));

    public void Raise(EngineEvent engineEvent) => EventReceived?.Invoke(this, engineEvent);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("start");
        if (OnStart is not null)
        {
            await OnStart(this);
        }
        else
        {
            SetState(EngineState.Ready);
        }
    }

    public Task ShutdownAsync()
    {
        Calls.Add("shutdown");
        SetState(EngineState.Stopped);
        return Task.CompletedTask;
    }

    /// <summary>Runs before each request is answered, with the method's name: may wait, or throw to fail it.</summary>
    public Func<string, Task>? BeforeAnswer { get; set; }

    public async Task<OpenProjectResult> OpenProjectAsync(string root, CancellationToken cancellationToken = default)
    {
        Calls.Add($"openProject {root}");
        await Before("openProject");
        return OpenProject(root);
    }

    public async Task<IReadOnlyList<TestInfo>> ListTestsAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("listTests");
        await Before("listTests");
        return Tests;
    }

    public async Task<IReadOnlyList<ActionInfo>> ListActionsAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("listActions");
        await Before("listActions");
        return Actions;
    }

    public async Task<IReadOnlyList<Diagnostic>> ValidateAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        Calls.Add($"validate {string.Join(',', files)}");
        var answer = Validate(files);
        await Before("validate");
        return answer;
    }

    /// <summary>Problems for a buffer being edited, by file and text.</summary>
    public Func<string, string, IReadOnlyList<Diagnostic>> ValidateContent { get; set; } = (_, _) => [];

    public async Task<IReadOnlyList<Diagnostic>> ValidateContentAsync(string file, string text, CancellationToken cancellationToken = default)
    {
        Calls.Add($"validateContent {file}");
        var answer = ValidateContent(file, text);
        await Before("validateContent");
        return answer;
    }

    /// <summary>The parameters of each startRun.</summary>
    public List<StartRunParams> Started { get; } = [];

    /// <summary>What startRun answers; by default run-N in a folder of that name.</summary>
    public Func<StartRunParams, StartRunResult> StartRun { get; set; }

    /// <summary>What openSnapshot does: nothing, or throw.</summary>
    public Action<OpenSnapshotParams> OpenSnapshot { get; set; } = _ => { };

    public async Task<StartRunResult> StartRunAsync(StartRunParams parameters, CancellationToken cancellationToken = default)
    {
        Calls.Add("startRun");
        Started.Add(parameters);
        await Before("startRun");
        return StartRun(parameters);
    }

    public async Task CancelRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        Calls.Add($"cancelRun {runId}");
        await Before("cancelRun");
    }

    public async Task OpenSnapshotAsync(OpenSnapshotParams parameters, CancellationToken cancellationToken = default)
    {
        Calls.Add($"openSnapshot {parameters.TestId} {parameters.StepId}");
        await Before("openSnapshot");
        OpenSnapshot(parameters);
    }

    /// <summary>An error as the engine answers it.</summary>
    public static EngineRequestException Refusal(string method, string name, string message, object? data = null) =>
        new(method, new JsonRpcError
        {
            Code = -32000,
            Message = message,
            Data = JsonSerializer.SerializeToElement(data ?? new { name }, Desktop.Protocol.Json.ProtocolJson.Options),
        });

    private Task Before(string method) => BeforeAnswer?.Invoke(method) ?? Task.CompletedTask;
}

/// <summary>Dialogs answered by the test, recording each question.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public UnsavedChangesChoice UnsavedAnswer { get; set; } = UnsavedChangesChoice.Cancel;

    public bool OverwriteAnswer { get; set; }

    public List<string> Asked { get; } = [];

    public Task<UnsavedChangesChoice> AskUnsavedChangesAsync(IReadOnlyList<string> files)
    {
        Asked.Add("unsaved " + string.Join(',', files));
        return Task.FromResult(UnsavedAnswer);
    }

    public Task<bool> AskOverwriteAsync(string file)
    {
        Asked.Add("overwrite " + file);
        return Task.FromResult(OverwriteAnswer);
    }

    public (string Folder, string Name)? NewFileAnswer { get; set; }

    public string? RenameAnswer { get; set; }

    public bool DeleteAnswer { get; set; }

    public Task<(string Folder, string Name)?> AskNewFileAsync(string what, string folder, string ending)
    {
        Asked.Add($"new {what} in {folder}");
        return Task.FromResult(NewFileAnswer);
    }

    public Task<string?> AskRenameAsync(string file)
    {
        Asked.Add("rename " + file);
        return Task.FromResult(RenameAnswer);
    }

    public Task<bool> AskDeleteAsync(string file)
    {
        Asked.Add("delete " + file);
        return Task.FromResult(DeleteAnswer);
    }

    public UnsavedChangesChoice RunAnswer { get; set; } = UnsavedChangesChoice.Cancel;

    public Task<UnsavedChangesChoice> AskSaveBeforeRunAsync(IReadOnlyList<string> files)
    {
        Asked.Add("run " + string.Join(',', files));
        return Task.FromResult(RunAnswer);
    }
}

/// <summary>A delay the test ends by hand: <see cref="Elapse"/> completes every wait so far.</summary>
internal sealed class ManualDelay : IDelay
{
    private readonly List<(TaskCompletionSource Done, CancellationToken Token)> _waits = [];

    /// <summary>Waits not yet ended or cancelled.</summary>
    public int Pending => _waits.Count(w => !w.Done.Task.IsCompleted && !w.Token.IsCancellationRequested);

    public Task WaitAsync(TimeSpan span, CancellationToken cancellationToken)
    {
        var done = new TaskCompletionSource();
        cancellationToken.Register(() => done.TrySetCanceled(cancellationToken));
        _waits.Add((done, cancellationToken));
        return done.Task;
    }

    /// <summary>Ends every wait that was not cancelled.</summary>
    public void Elapse()
    {
        foreach (var (done, _) in _waits.ToList())
        {
            done.TrySetResult();
        }

        _waits.Clear();
    }
}

/// <summary>Builders of protocol values for tests.</summary>
internal static class Make
{
    public static TestInfo Test(string file, string name, params string[] tags) => new() { File = file, Name = name, Tags = tags, Rows = 1 };

    public static Diagnostic Error(string file, int line, string code = "UnknownAction", string message = "Unknown action.") =>
        new() { File = file, Line = line, Column = 3, Severity = DiagnosticSeverity.Error, Code = code, Message = message };

    public static Diagnostic Warning(string file, int line, string code = "UnusedTarget", string message = "Unused.") =>
        new() { File = file, Line = line, Column = 1, Severity = DiagnosticSeverity.Warning, Code = code, Message = message };
}
