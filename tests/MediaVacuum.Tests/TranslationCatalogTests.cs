using MediaVacuum.Core.Services;

namespace MediaVacuum.Tests;

public class TranslationCatalogTests
{
    [Fact]
    public void AllLanguages_HaveTheSameKeys()
    {
        var localization = new LocalizationService();
        var reference = localization.GetStrings("en").Keys.OrderBy(k => k).ToArray();

        Assert.NotEmpty(reference);

        foreach (var language in localization.AvailableLanguages)
        {
            var keys = localization.GetStrings(language.Code).Keys.OrderBy(k => k).ToArray();
            Assert.Equal(reference, keys);
        }
    }

    [Fact]
    public void AllTranslations_AreNotEmpty()
    {
        var localization = new LocalizationService();

        foreach (var language in localization.AvailableLanguages)
        {
            foreach (var pair in localization.GetStrings(language.Code))
            {
                Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"{language.Code}:{pair.Key} is empty");
            }
        }
    }

    [Fact]
    public void MissingKey_FallsBackToKey()
    {
        var localization = new LocalizationService();
        Assert.Equal("ThisKeyDoesNotExist", localization["ThisKeyDoesNotExist"]);
    }
}
