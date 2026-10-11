// The New project form (instruction D0004, task 13): the project's name, the
// folder to create it in, the address of the application under test and the
// first environment's name. It checks what it can before asking the engine
// (createProject), and puts the engine's refusals next to the field they are
// about.

using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Desktop.App.Services;
using Desktop.Engine;
using Desktop.Protocol;
using Desktop.Protocol.Messages;

namespace Desktop.App.ViewModels;

/// <summary>The New project form.</summary>
public sealed partial class NewProjectViewModel : ObservableObject
{
    /// <summary>The first environment's name when none is given, as the engine names it.</summary>
    public const string DefaultEnvironment = "local";

    private readonly IFolderPicker _folderPicker;
    private readonly Func<CreateProjectParams, Task<bool>> _create;
    private bool _folderNameEdited;
    private bool _settingFolderName;

    /// <summary>Creates the form.</summary>
    /// <param name="folderPicker">Asks for the folder to create the project in.</param>
    /// <param name="create">Creates and opens the project; false when the person kept the open project instead. Throws what the engine refused.</param>
    /// <param name="location">The folder offered to create the project in, such as the parent of the most recent project; null for none.</param>
    public NewProjectViewModel(IFolderPicker folderPicker, Func<CreateProjectParams, Task<bool>> create, string? location = null)
    {
        _folderPicker = folderPicker;
        _create = create;
        _location = location ?? string.Empty;
    }

    /// <summary>Raised when the form is done: the project was created, or the person cancelled.</summary>
    public event EventHandler? Closed;

    /// <summary>The project's name, written into its config file.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Root), nameof(RootText))]
    private string _name = string.Empty;

    /// <summary>The folder the project's folder is created in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Root), nameof(RootText))]
    private string _location;

    /// <summary>The project's own folder, inside <see cref="Location"/>; made from the name until changed by hand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Root), nameof(RootText))]
    private string _folderName = string.Empty;

    /// <summary>The address of the application under test, such as <c>http://localhost:5173</c>.</summary>
    [ObservableProperty]
    private string _baseUrl = string.Empty;

    /// <summary>The first environment's name.</summary>
    [ObservableProperty]
    private string _environment = DefaultEnvironment;

    /// <summary>What is wrong with the name, or null.</summary>
    [ObservableProperty]
    private string? _nameError;

    /// <summary>What is wrong with the folder: not chosen, not empty, not writable; or null.</summary>
    [ObservableProperty]
    private string? _folderError;

    /// <summary>What is wrong with the address, or null.</summary>
    [ObservableProperty]
    private string? _baseUrlError;

    /// <summary>What is wrong with the environment's name, or null.</summary>
    [ObservableProperty]
    private string? _environmentError;

    /// <summary>A failure that belongs to no field, such as the engine not running; or null.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>True while the engine creates the project.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isCreating;

    /// <summary>The folder that will hold the project; empty until a location and a folder name are given.</summary>
    public string Root => Location.Trim().Length == 0 || FolderName.Trim().Length == 0 ? string.Empty : Path.Combine(Location.Trim(), FolderName.Trim());

    /// <summary>Says where the project goes.</summary>
    public string RootText => Root.Length == 0 ? "Choose a folder to create the project in." : $"The project goes into {Root}";

    /// <summary>A folder name made from a project name: lower case, words joined by "-".</summary>
    /// <param name="name">The project's name.</param>
    /// <returns>The folder name; empty when the name has no letters or digits.</returns>
    public static string FolderNameOf(string name) =>
        Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    /// <summary>Asks for the folder to create the project in.</summary>
    /// <returns>A task that completes when the dialog is closed.</returns>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _folderPicker.PickFolderAsync("Create the project in") is { } folder)
        {
            Location = folder;
            FolderError = null;
        }
    }

    /// <summary>Checks the fields, then creates and opens the project. Never throws: problems end next to their field.</summary>
    /// <returns>A task that completes when the project is open or a problem is shown.</returns>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        if (!Check())
        {
            return;
        }

        IsCreating = true;
        try
        {
            var created = await _create(new CreateProjectParams
            {
                Root = Root,
                Name = Name.Trim(),
                BaseUrl = BaseUrl.Trim(),
                Environment = Environment.Trim(),
            });
            if (created)
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (EngineRequestException ex)
        {
            Place(ex);
        }
        catch (Exception ex)
        {
            Error = $"The project could not be created: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
        }
    }

    /// <summary>Closes the form without creating anything.</summary>
    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, EventArgs.Empty);

    private bool CanCreate() => !IsCreating;

    partial void OnNameChanged(string value)
    {
        NameError = null;
        if (!_folderNameEdited)
        {
            _settingFolderName = true;
            FolderName = FolderNameOf(value);
            _settingFolderName = false;
        }
    }

    partial void OnFolderNameChanged(string value)
    {
        _folderNameEdited |= !_settingFolderName;
        FolderError = null;
    }

    partial void OnLocationChanged(string value) => FolderError = null;

    partial void OnBaseUrlChanged(string value) => BaseUrlError = null;

    partial void OnEnvironmentChanged(string value) => EnvironmentError = null;

    /// <summary>The checks the app can make itself, with the same words as the engine where it has them.</summary>
    private bool Check()
    {
        Error = null;
        NameError = Name.Trim().Length == 0 ? "Give the project a name." : null;
        FolderError = Location.Trim().Length == 0 ? "Choose a folder to create the project in."
            : FolderName.Trim().Length == 0 ? "Give the project's folder a name."
            : FolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || FolderName.Trim() is "." or ".." ? $"\"{FolderName}\" cannot be a folder's name."
            : null;
        BaseUrlError = Uri.TryCreate(BaseUrl.Trim(), UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? null
            : "Give the full address of the application under test, starting with http:// or https://, such as http://localhost:5173.";
        EnvironmentError = EnvironmentName().IsMatch(Environment.Trim())
            ? null
            : "The environment's name must start with a letter and hold only letters, digits, - and _.";
        return NameError is null && FolderError is null && BaseUrlError is null && EnvironmentError is null;
    }

    /// <summary>Puts the engine's refusal next to the field it is about; the engine's own words.</summary>
    private void Place(EngineRequestException ex)
    {
        var message = ex.Message;
        switch (ex.Name)
        {
            case ErrorCodes.FolderNotEmpty:
                FolderError = message;
                break;
            case ErrorCodes.InvalidParams when message.Contains("name", StringComparison.OrdinalIgnoreCase) && message.Contains("project", StringComparison.OrdinalIgnoreCase) && !message.Contains("environment", StringComparison.OrdinalIgnoreCase):
                NameError = message;
                break;
            case ErrorCodes.InvalidParams when message.Contains("baseUrl", StringComparison.OrdinalIgnoreCase) || message.Contains("address", StringComparison.OrdinalIgnoreCase):
                BaseUrlError = message;
                break;
            case ErrorCodes.InvalidParams when message.Contains("environment", StringComparison.OrdinalIgnoreCase):
                EnvironmentError = message;
                break;
            case ErrorCodes.InvalidParams when message.Contains("folder", StringComparison.OrdinalIgnoreCase):
                FolderError = message;
                break;
            case ErrorCodes.InternalError:
                // Creating folders and files failed: the folder cannot be written.
                FolderError = $"The project could not be written into {Root}: {message}";
                break;
            default:
                Error = message;
                break;
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$")]
    private static partial Regex EnvironmentName();
}
