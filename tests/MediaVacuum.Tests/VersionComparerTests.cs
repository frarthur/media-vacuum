using MediaVacuum.Core.Utilities;

namespace MediaVacuum.Tests;

public class VersionComparerTests
{
    [Theory]
    [InlineData("2025.09.05", "2025.08.01", true)]
    [InlineData("2025.01.01", "2025.01.01", false)]
    [InlineData("2024.12.31", "2025.01.01", false)]
    [InlineData("2025.09.05", "0.0.0", true)]
    [InlineData("v2025.09.05", "2025.09.05", false)]
    public void IsNewer_ComparesVersions(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, VersionComparer.IsNewer(candidate, current));
    }
}
