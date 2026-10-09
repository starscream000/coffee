// Starts the engine built in this checkout (packages/engine/dist/main.js) for
// the tests that talk to a real engine. Skips those tests when it is not
// built, unless DESKTOP_TESTS_REQUIRE_ENGINE=1 (set in CI) makes that a failure.

using Desktop.Protocol.Tests;

namespace Desktop.Engine.Tests;

/// <summary>Finds and starts the real engine for a test.</summary>
internal static class RealEngine
{
    /// <summary>Absolute path of the demo project.</summary>
    public static string DemoApp => RepoPaths.Of("examples/demo-app");

    /// <summary>Finds the checkout's engine and Node, or skips the test.</summary>
    public static EngineLaunch LaunchOrSkip()
    {
        try
        {
            return EngineLocator.Locate(EngineSearchInput.ForThisMachine(null, null)).Launch;
        }
        catch (EngineNotFoundException ex)
        {
            if (Environment.GetEnvironmentVariable("DESKTOP_TESTS_REQUIRE_ENGINE") == "1")
            {
                throw;
            }

            Assert.Skip($"No built engine or no Node: {ex.Message.Split(Environment.NewLine)[0]} Run \"pnpm build\" at the repository root.");
            throw;
        }
    }

    /// <summary>Starts a session with the real engine.</summary>
    public static async Task<EngineSession> StartAsync(List<string>? stderr = null)
    {
        var session = new EngineSession(EngineProcess.Start);
        if (stderr is not null)
        {
            session.StderrLine += (_, line) =>
            {
                lock (stderr)
                {
                    stderr.Add(line);
                }
            };
        }

        await session.StartAsync(LaunchOrSkip(), "0.0.0-test", TestContext.Current.CancellationToken);
        return session;
    }
}
