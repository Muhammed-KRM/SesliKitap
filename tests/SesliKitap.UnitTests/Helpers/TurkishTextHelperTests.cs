using FluentAssertions;
using SesliKitap.Business.Helpers;

namespace SesliKitap.UnitTests.Helpers;

public sealed class TurkishTextHelperTests
{
    [Fact]
    public void ToLowerTr_And_ToUpperTr_HandlesTurkishDottedAndDotlessI()
    {
        TurkishTextHelper.ToLowerTr("İSTANBUL IĞDIR ÇAĞDAŞ").Should().Be("istanbul ığdır çağdaş");
        TurkishTextHelper.ToUpperTr("istanbul ığdır çağdaş").Should().Be("İSTANBUL IĞDIR ÇAĞDAŞ");
    }

    [Theory]
    [InlineData("Kürk Mantolu Madonna", "kurk-mantolu-madonna")]
    [InlineData("Çalıkuşu (2. Baskı)!", "calikusu-2-baski")]
    [InlineData("   ", "kitap")]
    public void Slugify_GeneratesExpectedTurkishAsciiSlug(string input, string expected)
    {
        TurkishTextHelper.Slugify(input).Should().Be(expected);
    }

    [Fact]
    public void CountWords_CountsTurkishUnicodeWordsWithApostrophesAsSingleWord()
    {
        var text = "İstanbul'da 1923 yılında Türkiye'nin yeni bir dönemi başladı.";
        TurkishTextHelper.CountWords(text).Should().Be(8);
    }

    [Fact]
    public void NormalizeForSearch_FoldsTurkishDiacriticsAndLowercases()
    {
        TurkishTextHelper.NormalizeForSearch("  Şükrü   Ğümüş   İsmail  ").Should().Be("sukru gumus ismail");
    }
}
