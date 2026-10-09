// The engine as a child process: `node <engine>/dist/main.js --stdio`, with
// stdin, stdout and stderr redirected (docs/protocol.md, "Transport").

using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Desktop.Engine;

/// <summary>A running engine process.</summary>
public sealed class EngineProcess : IEngineTransport
{
    private const int StderrTailLines = 40;
    private readonly Process _process;
    private readonly Queue<string> _stderrTail = new();
    private readonly Lock _stderrLock = new();
    private readonly Action<string> _onStderrLine;

    private EngineProcess(Process process, Action<string> onStderrLine)
    {
        _process = process;
        _onStderrLine = onStderrLine;
    }

    /// <inheritdoc />
    public Stream Input => _process.StandardOutput.BaseStream;

    /// <inheritdoc />
    public Stream Output => _process.StandardInput.BaseStream;

    /// <inheritdoc />
    public Task<int> Exited { get; private set; } = Task.FromResult(-1);

    /// <summary>The process id.</summary>
    public int Id => _process.Id;

    /// <inheritdoc />
    public string StderrTail
    {
        get
        {
            lock (_stderrLock)
            {
                return string.Join(Environment.NewLine, _stderrTail);
            }
        }
    }

    /// <summary>Starts the engine.</summary>
    /// <param name="launch">What to start.</param>
    /// <param name="onStderrLine">Called on a background thread for each line the engine writes to stderr.</param>
    /// <returns>The running process.</returns>
    /// <exception cref="EngineStartException">The operating system could not start Node.</exception>
    public static EngineProcess Start(EngineLaunch launch, Action<string> onStderrLine)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(onStderrLine);
        var info = new ProcessStartInfo
        {
            FileName = launch.NodePath,
            WorkingDirectory = Path.GetDirectoryName(launch.EngineMainPath) ?? Environment.CurrentDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in launch.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var engine = new EngineProcess(process, onStderrLine);
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                engine.AddStderr(e.Data);
            }
        };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            throw new EngineStartException($"Node could not be started from \"{launch.NodePath}\": {ex.Message}. Check the Node path in Settings.", ex);
        }

        engine.Exited = engine.WaitForExit();
        process.BeginErrorReadLine();
        return engine;
    }

    /// <inheritdoc />
    public void Kill()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            _process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The pipe is already broken because the engine exited.
        }

        if (await Task.WhenAny(Exited, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false) != Exited)
        {
            Kill();
        }

        _process.Dispose();
    }

    private void AddStderr(string line)
    {
        lock (_stderrLock)
        {
            _stderrTail.Enqueue(line);
            while (_stderrTail.Count > StderrTailLines)
            {
                _stderrTail.Dequeue();
            }
        }

        _onStderrLine(line);
    }

    private async Task<int> WaitForExit()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);
        return _process.ExitCode;
    }
}
