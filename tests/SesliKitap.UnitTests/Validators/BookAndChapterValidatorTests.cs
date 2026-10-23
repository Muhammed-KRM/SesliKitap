using FluentAssertions;
using Microsoft.Extensions.Options;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.DTOs.Chapter;
using SesliKitap.Business.Options;
using SesliKitap.Business.Validators;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Validators;

public sealed class BookAndChapterValidatorTests
{
    private static BookUploadRequestValidator CreateUploadValidator() =>
        new(Options.Create(new UploadOptions { MaxBookBytes = 314_572_800 }));

    [Fact]
    public void BookUploadRequestValidator_ValidPdf_Passes()
    {
        var validator = CreateUploadValidator();
        var file = FormFileBuilder.CreatePdf("kitap.pdf");
        var result = validator.Validate(new BookUploadRequest { Dosya = file, Baslik = "Geçerli Başlık", Yazar = "Yazar" });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BookUploadRequestValidator_DisallowedExtension_Fails()
    {
        var validator = CreateUploadValidator();
        var file = FormFileBuilder.FromBytes([1, 2, 3], "kitap.docx");
        var result = validator.Validate(new BookUploadRequest { Dosya = file });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void BookUpdateRequestValidator_EmptyTitle_Fails()
    {
        var validator = new BookUpdateRequestValidator();
        validator.Validate(new BookUpdateRequest("", "Yazar", null, null)).IsValid.Should().BeFalse();
        validator.Validate(new BookUpdateRequest("Başlık", "Yazar", "Açıklama", null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ChapterUpdateRequestValidator_ValidatesFields()
    {
        var validator = new ChapterUpdateRequestValidator();
        validator.Validate(new ChapterUpdateRequest("Başlık", "Metin", true, 1)).IsValid.Should().BeTrue();
        validator.Validate(new ChapterUpdateRequest("", "Metin", true, 1)).IsValid.Should().BeFalse();
        validator.Validate(new ChapterUpdateRequest("Başlık", "", true, 1)).IsValid.Should().BeFalse();
        validator.Validate(new ChapterUpdateRequest("Başlık", "Metin", true, 0)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ChapterMergeRequestValidator_RequiresAtLeastTwoUniqueIds()
    {
        var validator = new ChapterMergeRequestValidator();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        validator.Validate(new ChapterMergeRequest([id1, id2])).IsValid.Should().BeTrue();
        validator.Validate(new ChapterMergeRequest([id1])).IsValid.Should().BeFalse();
        validator.Validate(new ChapterMergeRequest([id1, id1])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ChapterSplitRequestValidator_RequiresPositiveIndex()
    {
        var validator = new ChapterSplitRequestValidator();
        validator.Validate(new ChapterSplitRequest(10, "Yeni Bölüm")).IsValid.Should().BeTrue();
        validator.Validate(new ChapterSplitRequest(0, "Yeni Bölüm")).IsValid.Should().BeFalse();
    }
}
