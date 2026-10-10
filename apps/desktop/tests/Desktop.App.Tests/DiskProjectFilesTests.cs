// The project files on disk: finding files by their ending, moving and
// deleting them, in a temporary folder.

using Desktop.App.Services;

namespace Desktop.App.Tests;

public sealed class DiskProjectFilesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("desktop-files-").FullName;
    private readonly DiskProjectFiles _files = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string path) => _files.WriteText(_root, path, "version: 1\n");

    [Fact]
    public void Finds_files_by_ending_and_leaves_out_data_and_hidden_folders()
    {
        Write("targets/shop.targets.yaml");
        Write("tests/a/b.TARGETS.yaml");
        Write(".cfe/runs/x.targets.yaml");
        Write("node_modules/p/y.targets.yaml");
        Write(".git/z.targets.yaml");

        Assert.Equal(["targets/shop.targets.yaml", "tests/a/b.TARGETS.yaml"], _files.FindFiles(_root, ".targets.yaml"));
        Assert.Empty(_files.FindFiles(Path.Combine(_root, "missing"), ".targets.yaml"));
    }

    [Fact]
    public void Moves_into_a_new_folder_never_over_a_file_and_deletes()
    {
        Write("tests/a.test.yaml");
        Write("tests/b.test.yaml");

        _files.Move(_root, "tests/a.test.yaml", "tests/new/c.test.yaml");
        Assert.True(_files.Exists(_root, "tests/new/c.test.yaml"));
        Assert.False(_files.Exists(_root, "tests/a.test.yaml"));
        Assert.Throws<IOException>(() => _files.Move(_root, "tests/new/c.test.yaml", "tests/b.test.yaml"));

        _files.Delete(_root, "tests/b.test.yaml");
        Assert.False(_files.Exists(_root, "tests/b.test.yaml"));
        Assert.True(_files.Exists(_root, "tests/new"));
    }
}
