// Fakes of the app's services for the view model tests.

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

    public string ReadText(string root, string relativePath) =>
        Files.TryGetValue(relativePath, out var text) ? text : throw new FileNotFoundException("not found", relativePath);

    public IReadOnlyList<string> FindTestFiles(string root) =>
        [.. Files.Keys.Where(f => f.EndsWith(".test.yaml", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

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

    /// <summary>Null means "method not found".</summary>
    public IReadOnlyList<TestInfo>? Tests { get; set; } = [];

    public IReadOnlyList<ActionInfo> Actions { get; set; } = [];

    public Func<IReadOnlyList<string>, IReadOnlyList<Diagnostic>> Validate { get; set; } = _ => [];

    public List<string> Calls { get; } = [];

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

    public async Task<IReadOnlyList<TestInfo>?> ListTestsAsync(CancellationToken cancellationToken = default)
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

    private Task Before(string method) => BeforeAnswer?.Invoke(method) ?? Task.CompletedTask;
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
