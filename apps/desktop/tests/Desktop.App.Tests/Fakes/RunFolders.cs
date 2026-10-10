// Writes run folders as the engine does (<root>/<data folder>/runs/<runId>/
// events.ndjson, one JSON-RPC notification per line), in a temporary project
// folder that is deleted afterwards.

using System.Text.Json;
using Desktop.App.Services;
using Desktop.Protocol.Json;
using Desktop.Protocol.Messages;

namespace Desktop.App.Tests.Fakes;

/// <summary>A temporary project folder with run folders.</summary>
internal sealed class RunFolders : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("desktop-runs-").FullName;

    /// <summary>The line the engine writes for an event.</summary>
    public static string Line(EngineEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(engineEvent);
        var method = EngineEvents.ByMethod.Single(p => p.Value == engineEvent.GetType()).Key;
        var parameters = JsonSerializer.SerializeToElement(engineEvent, engineEvent.GetType(), ProtocolJson.Options);
        return JsonSerializer.Serialize(new { jsonrpc = "2.0", method, @params = parameters });
    }

    /// <summary>The folder of a run.</summary>
    public string Folder(string runId) => Path.Combine(DiskRunRecords.RunsFolder(Root), runId);

    /// <summary>Writes a run's events file from events and raw lines, in order.</summary>
    public string Write(string runId, params object[] lines)
    {
        var folder = Directory.CreateDirectory(Folder(runId)).FullName;
        var text = string.Concat(lines.Select(l => (l is EngineEvent e ? Line(e) : (string)l) + "\n"));
        File.WriteAllText(Path.Combine(folder, DiskRunRecords.EventsFile), text);
        return folder;
    }

    /// <summary>Writes a complete run: one passing test.</summary>
    public string WritePassed(string runId, string env = "local")
    {
        var s = new RunScript(runId);
        return Write(
            runId,
            s.Started(RunScript.Planned("t1", "tests/a.test.yaml", "A")) with { Env = env },
            s.TestStarted("t1"),
            s.Step("t1", "s1", "Open"),
            s.Passed("t1", "s1"),
            s.TestFinished("t1", RunOutcome.Passed),
            s.Finished(RunOutcome.Passed, passed: 1));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
