namespace MediaVacuum.Core.Interfaces;

public interface IInstallerService
{
    long GetAppDataSize();

    void RemoveAppData(bool includeYtDlp, bool includeFfmpeg, bool includeLogs);

    void ScheduleSelfDelete();
}
