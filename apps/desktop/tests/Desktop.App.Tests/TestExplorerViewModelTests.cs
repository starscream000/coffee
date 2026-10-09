// Tests of the test explorer: the tree, search, tags, the fallback, problem counts.

using Desktop.App.Tests.Fakes;
using Desktop.App.ViewModels;

namespace Desktop.App.Tests;

public sealed class TestExplorerViewModelTests
{
    private static TestExplorerViewModel Loaded()
    {
        var explorer = new TestExplorerViewModel();
        explorer.Load([
            Make.Test("tests/checkout/guest.test.yaml", "Guest checks out", "smoke", "checkout"),
            Make.Test("tests/checkout/member.test.yaml", "Member checks out", "checkout"),
            Make.Test("tests/login.test.yaml", "Customer logs in", "smoke"),
            Make.Test("root.test.yaml", "At the root"),
        ]);
        return explorer;
    }

    private static List<string> Flatten(IEnumerable<ExplorerNodeViewModel> nodes) =>
        [.. nodes.SelectMany(n => (IEnumerable<string>)[n.IsFolder ? n.Path + "/" : n.Path, .. Flatten(n.Children)])];

    [Fact]
    public void Builds_a_folder_tree_with_folders_first_and_names_in_order()
    {
        var explorer = Loaded();
        Assert.Equal(
            ["tests/", "tests/checkout/", "tests/checkout/guest.test.yaml", "tests/checkout/member.test.yaml", "tests/login.test.yaml", "root.test.yaml"],
            Flatten(explorer.Roots));
        Assert.Equal("4 tests", explorer.Summary);
        Assert.Null(explorer.Notice);
    }

    [Fact]
    public void Shows_the_test_name_and_tags()
    {
        var guest = Loaded().Roots[0].Children[0].Children[0];
        Assert.Equal("Guest checks out", guest.DisplayName);
        Assert.Equal(["smoke", "checkout"], guest.Tags);
        Assert.True(guest.IsTest);
    }

    [Fact]
    public void Searches_names_and_paths_ignoring_case()
    {
        var explorer = Loaded();
        explorer.SearchText = "CHECKS OUT";
        Assert.Equal(["tests/", "tests/checkout/", "tests/checkout/guest.test.yaml", "tests/checkout/member.test.yaml"], Flatten(explorer.Roots));
        Assert.Equal("2 of 4 tests", explorer.Summary);
        explorer.SearchText = "login";
        Assert.Equal(["tests/", "tests/login.test.yaml"], Flatten(explorer.Roots));
    }

    [Fact]
    public void Filters_by_tag_and_lists_the_tags()
    {
        var explorer = Loaded();
        Assert.Equal([TestExplorerViewModel.AllTags, "checkout", "smoke"], explorer.Tags);
        explorer.SelectedTag = "smoke";
        Assert.Equal(["tests/", "tests/checkout/", "tests/checkout/guest.test.yaml", "tests/login.test.yaml"], Flatten(explorer.Roots));
    }

    [Fact]
    public void Falls_back_to_file_paths_with_a_notice()
    {
        var explorer = new TestExplorerViewModel();
        explorer.LoadFallback(["b.test.yaml", "a/x.test.yaml"]);
        Assert.Equal(["a/", "a/x.test.yaml", "b.test.yaml"], Flatten(explorer.Roots));
        Assert.Equal("x.test.yaml", explorer.Roots[0].Children[0].DisplayName);
        Assert.Contains("cannot list tests", explorer.Notice, StringComparison.Ordinal);
        Assert.Equal([TestExplorerViewModel.AllTags], explorer.Tags);
    }

    [Fact]
    public void Adds_problem_counts_up_the_folders()
    {
        var explorer = Loaded();
        explorer.ApplyProblemCounts(new Dictionary<string, (int, int)>
        {
            ["tests/checkout/guest.test.yaml"] = (2, 1),
            ["tests/login.test.yaml"] = (0, 3),
        });
        var tests = explorer.Roots[0];
        Assert.Equal((2, 4), (tests.ErrorCount, tests.WarningCount));
        Assert.True(tests.Children[0].HasErrors);
        Assert.True(tests.Children[1].HasWarningsOnly);
        Assert.Equal(0, explorer.Roots[1].ErrorCount);
    }

    [Fact]
    public void Selecting_a_test_activates_its_file_and_selecting_a_folder_does_not()
    {
        var explorer = Loaded();
        var activated = new List<string>();
        explorer.FileActivated += (_, f) => activated.Add(f);
        explorer.SelectedNode = explorer.Roots[0];
        explorer.SelectedNode = explorer.Roots[0].Children[1];
        Assert.Equal(["tests/login.test.yaml"], activated);
    }
}
