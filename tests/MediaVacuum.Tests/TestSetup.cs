using System.Runtime.CompilerServices;
using MediaVacuum.Core;

namespace MediaVacuum.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Keep test logs out of the real %LOCALAPPDATA%\MediaVacuum\logs folder.
        var directory = Path.Combine(Path.GetTempPath(), "MediaVacuum.Tests");
        AppLog.Initialize(Path.Combine(directory, "tests.log"));
    }
}
