// Real mouse and key input for the engine's recording browser in tests, sent
// through the browser's remote debugging port (the Chrome DevTools Protocol
// over a WebSocket, with what .NET has built in). The engine opens that port
// only for tests, when its TEST_RECORDING_DEBUG_PORT variable is set.

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Desktop.App.Tests.Runs;

/// <summary>Drives the first page of a browser through its remote debugging port, as a person would.</summary>
internal sealed class BrowserInput : IAsyncDisposable
{
    private readonly ClientWebSocket _socket;
    private int _nextId;

    private BrowserInput(ClientWebSocket socket) => _socket = socket;

    /// <summary>A free TCP port on this machine.</summary>
    /// <returns>The port.</returns>
    public static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Connects to the first page of the browser listening on <paramref name="port"/>, waiting for it to appear.</summary>
    /// <param name="port">The remote debugging port.</param>
    /// <param name="timeout">How long to wait for the browser and its page.</param>
    /// <returns>The connected input.</returns>
    /// <exception cref="TimeoutException">No page appeared in time.</exception>
    public static async Task<BrowserInput> ConnectAsync(int port, TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            try
            {
                var targets = await http.GetFromJsonAsync<JsonElement>($"http://127.0.0.1:{port}/json/list");
                foreach (var target in targets.EnumerateArray())
                {
                    if (target.GetProperty("type").GetString() == "page" && target.TryGetProperty("webSocketDebuggerUrl", out var url))
                    {
                        var socket = new ClientWebSocket();
                        await socket.ConnectAsync(new Uri(url.GetString()!), CancellationToken.None);
                        return new BrowserInput(socket);
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"No browser page appeared on port {port}.");
    }

    /// <summary>Clicks the middle of the element a CSS selector finds, once it is there: moves, presses and releases the mouse.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>A task that completes when the click was sent.</returns>
    public async Task ClickAsync(string selector)
    {
        var point = await WaitForAsync(
            $$"""(() => { const e = document.querySelector({{JsonSerializer.Serialize(selector)}}); if (!e) return null; e.scrollIntoView({ block: 'center' }); const r = e.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()""");
        var x = point.GetProperty("x").GetDouble();
        var y = point.GetProperty("y").GetDouble();
        await SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x, y });
        await SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "left", clickCount = 1 });
        await SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "left", clickCount = 1 });
    }

    /// <summary>Types text key by key into the focused element.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes when every key was sent.</returns>
    public async Task TypeAsync(string text)
    {
        foreach (var c in text)
        {
            var key = c.ToString();
            await SendAsync("Input.dispatchKeyEvent", new { type = "keyDown", key, text = key, unmodifiedText = key });
            await SendAsync("Input.dispatchKeyEvent", new { type = "keyUp", key });
        }
    }

    /// <summary>Presses Enter.</summary>
    /// <returns>A task that completes when the key was sent.</returns>
    public async Task PressEnterAsync()
    {
        await SendAsync("Input.dispatchKeyEvent", new { type = "keyDown", key = "Enter", code = "Enter", windowsVirtualKeyCode = 13, text = "\r" });
        await SendAsync("Input.dispatchKeyEvent", new { type = "keyUp", key = "Enter", code = "Enter", windowsVirtualKeyCode = 13 });
    }

    /// <summary>Waits until the page's text contains <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>A task that completes when it does.</returns>
    public Task WaitForTextAsync(string text) =>
        WaitForAsync($"document.body && document.body.innerText.includes({JsonSerializer.Serialize(text)}) ? true : null");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            // The browser went first.
        }

        _socket.Dispose();
    }

    /// <summary>Evaluates an expression until it gives something other than null, for at most 15 seconds.</summary>
    private async Task<JsonElement> WaitForAsync(string expression)
    {
        for (var i = 0; i < 75; i++)
        {
            var answer = await SendAsync("Runtime.evaluate", new { expression, returnByValue = true });
            if (answer.TryGetProperty("result", out var result) && result.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value.Clone();
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"The page never gave a value for: {expression}");
    }

    /// <summary>Sends one command and waits for its answer; events in between are skipped.</summary>
    private async Task<JsonElement> SendAsync(string method, object parameters)
    {
        var id = ++_nextId;
        var request = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters });
        await _socket.SendAsync(request, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        var buffer = new byte[64 * 1024];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await _socket.ReceiveAsync(buffer, timeout.Token);
                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(message.ToArray()));
            if (document.RootElement.TryGetProperty("id", out var answered) && answered.GetInt32() == id)
            {
                if (document.RootElement.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException($"{method} failed: {error.GetRawText()}");
                }

                return document.RootElement.GetProperty("result").Clone();
            }
        }
    }
}
