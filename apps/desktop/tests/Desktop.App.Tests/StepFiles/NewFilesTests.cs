// New test, flow and targets files (instruction D0003, task 17): their paths
// from the folder and name typed, what is refused, and their first text.

using Desktop.App.StepFiles;

namespace Desktop.App.Tests.StepFiles;

public sealed class NewFilesTests
{
    [Theory]
    [InlineData("tests/checkout", "guest-checkout", NewFileKind.Test, "tests/checkout/guest-checkout.test.yaml")]
    [InlineData("tests/checkout/", " guest.test.yaml ", NewFileKind.Test, "tests/checkout/guest.test.yaml")]
    [InlineData("", "login", NewFileKind.Flow, "login.flow.yaml")]
    [InlineData("targets", "shop", NewFileKind.Targets, "targets/shop.targets.yaml")]
    [InlineData("tests\\windows", "a", NewFileKind.Test, "tests/windows/a.test.yaml")]
    public void Paths_get_their_ending_and_forward_slashes(string folder, string name, NewFileKind kind, string expected)
    {
        Assert.Equal(expected, NewFiles.PathFor(folder, name, kind));
    }

    [Theory]
    [InlineData("tests", "", "Enter a name")]
    [InlineData("tests", "a/b", "without folders")]
    [InlineData("../outside", "a", "\"..\" cannot be part")]
    [InlineData("/etc", "a", "relative to the project")]
    [InlineData("tests", "a:b", "cannot be part")]
    [InlineData("tests", "a.flow.yaml", "The name of a test ends with .test.yaml, not .flow.yaml.")]
    public void Names_and_folders_that_do_not_fit_are_refused_with_the_reason(string folder, string name, string reason)
    {
        var error = Assert.Throws<ArgumentException>(() => NewFiles.PathFor(folder, name, NewFileKind.Test));

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_new_test_or_flow_has_one_step_and_a_targets_file_none()
    {
        Assert.Equal("version: 1\nname: Guest checkout\nsteps:\n  - goto: /\n", NewFiles.Template(NewFileKind.Test, "tests/guest-checkout.test.yaml"));
        Assert.Equal("version: 1\nname: Log in as admin\nsteps:\n  - goto: /\n", NewFiles.Template(NewFileKind.Flow, "flows/log_in_as_admin.flow.yaml"));
        Assert.Equal("version: 1\ntargets: {}\n", NewFiles.Template(NewFileKind.Targets, "targets/shop.targets.yaml"));
        Assert.Equal(NewFileKind.Flow, NewFiles.KindOf("flows/a.FLOW.yaml"));
        Assert.Null(NewFiles.KindOf("cfe.config.yaml"));
    }
}
