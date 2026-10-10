// The demo web server (examples/demo-app/server) on a free port, and a
// temporary copy of the demo project whose environments point at it, so that
// real runs never write inside examples/. Mirrors the engine's own harness
// (packages/engine/src/testing/demo-app.ts).

using System.Diagnostics;
using System.Text.RegularExpressions;
using Desktop.Protocol;
using Desktop.Protocol.Tests;

namespace Desktop.App.Tests.Runs;

/// <summary>A running demo server and a copy of the demo project; disposing stops the one and deletes the other.</summary>
internal sealed partial class DemoCopy : IAsyncDisposable
{
    /// <summary>The base URL the demo config names; replaced with the server's in the copy.</summary>
    private const string ConfigBaseUrl = "http://localhost:4310";

    private readonly Process _server;

    private DemoCopy(Process server, string url, string root)
    {
        _server = server;
        Url = url;
        Root = root;
    }

    /// <summary>The server's base URL.</summary>
    public string Url { get; }

    /// <summary>The copy's folder.</summary>
    public string Root { get; }

    /// <summary>Starts the server with <paramref name="node"/> and copies the project.</summary>
    /// <param name="node">The Node executable.</param>
    /// <returns>The running copy.</returns>
    /// <exception cref="InvalidOperationException">The server printed no URL within 10 seconds.</exception>
    public static async Task<DemoCopy> StartAsync(string node)
    {
        var demo = RepoPaths.Of("examples/demo-app");
        var start = new ProcessStartInfo(node) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(demo, "server", "server.ts"));
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add("0");
        var server = Process.Start(start) ?? throw new InvalidOperationException("The demo server did not start.");
        string? url = null;
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            try
            {
                while (url is null && await server.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
                {
                    url = Listening().Match(line) is { Success: true } m ? m.Groups[1].Value : null;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (url is null)
        {
            server.Kill(entireProcessTree: true);
            server.Dispose();
            throw new InvalidOperationException("The demo server printed no URL within 10 seconds.");
        }

        var root = Directory.CreateTempSubdirectory("desktop-demo-").FullName;
        Copy(demo, root);
        var config = Path.Combine(root, Product.ConfigFile);
        await File.WriteAllTextAsync(config, (await File.ReadAllTextAsync(config)).Replace(ConfigBaseUrl, url, StringComparison.Ordinal));
        return new DemoCopy(server, url, root);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_server.HasExited)
        {
            _server.Kill(entireProcessTree: true);
            await _server.WaitForExitAsync();
        }

        _server.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A browser or the engine may still hold a file on Windows; the temp folder is cleaned up eventually.
        }
    }

    private static void Copy(string from, string to)
    {
        foreach (var dir in Directory.EnumerateDirectories(from))
        {
            var name = Path.GetFileName(dir);
            if (name is Product.DataDir or "node_modules")
            {
                continue;
            }

            Copy(dir, Directory.CreateDirectory(Path.Combine(to, name)).FullName);
        }

        foreach (var file in Directory.EnumerateFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }

    [GeneratedRegex(@"listening on (http://\S+)")]
    private static partial Regex Listening();
}
