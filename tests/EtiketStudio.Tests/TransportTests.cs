// SPDX-License-Identifier: GPL-3.0-only
using EtiketStudio.Core;
using Xunit;

namespace EtiketStudio.Tests;

public sealed class TransportTests
{
    [Fact] public void ShortWritesResumeAtExactByteOffset()
    {
        var offsets = new List<int>();
        BytePump.WriteAll(10, (offset, requested) => { offsets.Add(offset); return Math.Min(3, requested); });
        Assert.Equal(new[] { 0, 3, 6, 9 }, offsets);
    }
    [Fact] public void ZeroByteWriteFailsWithoutInfiniteLoop() => Assert.Throws<DomainException>(() => BytePump.WriteAll(5, (_, _) => 0));
    [Fact] public void ImpossibleWriteCountIsRejected() => Assert.Throws<DomainException>(() => BytePump.WriteAll(5, (_, n) => n + 1));
    [Fact] public void CancellationStopsBeforeNextWrite()
    {
        using var cts = new CancellationTokenSource(); int calls = 0;
        Assert.Throws<OperationCanceledException>(() => BytePump.WriteAll(10, (_, _) => { calls++; cts.Cancel(); return 1; }, cts.Token));
        Assert.Equal(1, calls);
    }
    [Theory] [InlineData("tr-TR", "Ürünler")] [InlineData("en-US", "Products")]
    public void LocaleResourcesResolveInBothLanguages(string culture, string expected)
    {
        Assert.Equal(expected, new LanguageCatalog(culture)["Products"]);
    }
}
