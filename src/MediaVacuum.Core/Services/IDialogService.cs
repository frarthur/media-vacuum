namespace MediaVacuum.Core.Services;

public interface IDialogService
{
    void ShowInfo(string message, string title);

    void ShowError(string message, string title);

    void ShowWarning(string message, string title);

    bool ShowConfirm(string message, string title);

    string? BrowseFolder(string initialDirectory, string title);

    void OpenFolder(string path);

    void OpenFile(string path);

    void OpenFileLocation(string path);

    void ExitApp();
}
