namespace MediaVacuum.Core.Interfaces;

public interface IContextMenuService
{
    bool IsInstalled { get; }

    void Install(string verb);

    void Uninstall();
}
