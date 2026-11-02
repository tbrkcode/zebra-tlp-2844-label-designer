// SPDX-License-Identifier: GPL-3.0-only
using System.Collections;
using System.Globalization;
using System.Resources;
using EtiketStudio.Core;
using Xunit;

namespace EtiketStudio.Tests;

public sealed class LocalizationTests
{
    [Fact] public void ExplicitEnglishWorksOnAnExplicitlyTurkishThread()
    {
        var previous = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR"); Assert.Equal("Products", new LanguageCatalog("en-US")["Products"]); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
    [Fact] public void SwitchingRaisesIndexerAndRevisionNotifications()
    {
        var source = new LanguageCatalog("tr-TR"); var changed = new List<string?>(); source.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        source.Apply("en-US"); Assert.Equal("Products", source["Products"]); Assert.Contains("Item[]", changed); Assert.Contains("Revision", changed); Assert.Equal(1, source.Revision);
        source.Apply("tr-TR"); Assert.Equal("Ürünler", source["Products"]); Assert.Equal(2, source.Revision);
    }
    [Fact] public void EnglishCatalogLivesInTheMainAssembly()
    {
        Assert.Contains("EtiketStudio.Core.Resources.StringsEnglish.resources", typeof(T).Assembly.GetManifestResourceNames());
    }
    [Fact] public void EveryTurkishKeyHasAnEnglishTranslation()
    {
        string[] Keys(string name)
        {
            var resources = new ResourceManager("EtiketStudio.Core.Resources." + name, typeof(T).Assembly).GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
            return resources.Cast<DictionaryEntry>().Select(x => (string)x.Key).Order().ToArray();
        }
        Assert.Equal(Keys("Strings"), Keys("StringsEnglish"));
    }
    [Fact] public void ChangingOneCatalogDoesNotChangeAnotherCatalog()
    {
        var english = new LanguageCatalog("en-US"); var turkish = new LanguageCatalog("tr-TR");
        turkish.Apply("en-US"); turkish.Apply("tr-TR"); Assert.Equal("Products", english["Products"]);
    }
    [Theory] [InlineData("en", "en-US")] [InlineData("EN-us", "en-US")] [InlineData("invalid", "tr-TR")]
    public void LanguageCodesNormalizeToSupportedChoices(string input, string expected) => Assert.Equal(expected, LanguageCatalog.Normalize(input));
}
