// Checks build settings every project of the desktop app shares: Avalonia's
// build telemetry is replaced by an empty target, so no build sends it
// (instruction D0004, task 4).

using System.Xml.Linq;

namespace Desktop.Protocol.Tests;

public sealed class BuildSettingsTests
{
    [Fact]
    public void Avalonias_build_telemetry_target_is_replaced_by_an_empty_one()
    {
        var targets = XDocument.Load(RepoPaths.Of("apps/desktop/Directory.Build.targets"));
        var stats = Assert.Single(
            targets.Descendants("Target"),
            target => (string?)target.Attribute("Name") == "AvaloniaStats");
        Assert.Empty(stats.Elements());
    }
}
