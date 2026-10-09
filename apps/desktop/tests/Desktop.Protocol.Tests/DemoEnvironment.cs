// The environment the demo project needs: it declares the secret DEMO_PASSWORD,
// which the engine reads from the environment until the engine track commits a
// demo-only .env (instruction D0003, owner decision 6).

namespace Desktop.Protocol.Tests;

/// <summary>Prepares this test process's environment for engines that open the demo project.</summary>
internal static class DemoEnvironment
{
    /// <summary>Sets DEMO_PASSWORD (any value of 4 characters or more) when it is not set; engines started afterwards inherit it.</summary>
    public static void Ensure()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEMO_PASSWORD")))
        {
            Environment.SetEnvironmentVariable("DEMO_PASSWORD", "demo-password-for-tests");
        }
    }
}
