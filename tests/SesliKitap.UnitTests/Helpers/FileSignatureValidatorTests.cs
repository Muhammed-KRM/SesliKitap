using System.Text;
using FluentAssertions;
using SesliKitap.Business.Helpers;
using SesliKitap.Data.Enums;

namespace SesliKitap.UnitTests.Helpers;

public sealed class FileSignatureValidatorTests
{
    [Fact]
    public async Task DetectBookFormatAsync_ValidPdf_ReturnsPdf()
    {
        using var stream = new MemoryStream("%PDF-1.7\n%Test PDF"u8.ToArray());

        var result = await FileSignatureValidator.DetectBookFormatAsync(stream, ".pdf");

        result.Should().Be(DosyaFormati.Pdf);
        stream.Position.Should().Be(0);
    }

    [Fact]
    public async Task DetectBookFormatAsync_PdfWithGarbagePrefixWithin1024Bytes_ReturnsPdf()
    {
        var garbage = new byte[120];
        Array.Fill(garbage, (byte)0x20);
        var pdfBytes = garbage.Concat("%PDF-1.4\nContent"u8.ToArray()).ToArray();
        using var stream = new MemoryStream(pdfBytes);

        var result = await FileSignatureValidator.DetectBookFormatAsync(stream, ".pdf");

        result.Should().Be(DosyaFormati.Pdf);
    }

    [Fact]
    public async Task DetectBookFormatAsync_ValidEpub_ReturnsEpub()
    {
        var zipMagic = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var epubPayload = zipMagic
            .Concat(new byte[26])
            .Concat("mimetypeapplication/epub+zip"u8.ToArray())
            .ToArray();
        using var stream = new MemoryStream(epubPayload);

        var result = await FileSignatureValidator.DetectBookFormatAsync(stream, ".epub");

        result.Should().Be(DosyaFormati.Epub);
    }

    [Fact]
    public async Task DetectBookFormatAsync_ValidTxtUtf8AndUtf16Bom_ReturnsTxt()
    {
        using var utf8Stream = new MemoryStream("Türkçe düz metin dosyası ğşıİçöü"u8.ToArray());
        var utf8Result = await FileSignatureValidator.DetectBookFormatAsync(utf8Stream, ".txt");
        utf8Result.Should().Be(DosyaFormati.Txt);

        var utf16Bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("UTF-16 Türkçe metin")).ToArray();
        using var utf16Stream = new MemoryStream(utf16Bytes);
        var utf16Result = await FileSignatureValidator.DetectBookFormatAsync(utf16Stream, ".txt");
        utf16Result.Should().Be(DosyaFormati.Txt);
    }

    [Fact]
    public async Task DetectBookFormatAsync_TxtWithNullByteWithoutBom_ReturnsNull()
    {
        var binaryTxt = new byte[] { 0x41, 0x42, 0x00, 0x43, 0x44 };
        using var stream = new MemoryStream(binaryTxt);

        var result = await FileSignatureValidator.DetectBookFormatAsync(stream, ".txt");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectBookFormatAsync_ExtensionContentMismatch_ReturnsNull()
    {
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        using var stream = new MemoryStream(jpegBytes);

        var result = await FileSignatureValidator.DetectBookFormatAsync(stream, ".pdf");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 }, true)] // WAV
    [InlineData(new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00 }, true)] // MP3 ID3
    [InlineData(new byte[] { 0xFF, 0xFB, 0x90, 0x64 }, true)] // MP3 Frame Sync
    [InlineData(new byte[] { 0x4F, 0x67, 0x67, 0x53, 0x00, 0x02 }, true)] // OGG
    [InlineData(new byte[] { 0x66, 0x4C, 0x61, 0x43, 0x00, 0x00 }, true)] // FLAC
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x93, 0x42 }, true)] // WEBM / MKV
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20 }, true)] // M4A
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, false)] // PDF is not audio
    public async Task IsSupportedAudioAsync_RecognizesAllSupportedAudioSignatures(byte[] header, bool expected)
    {
        using var stream = new MemoryStream(header);

        var result = await FileSignatureValidator.IsSupportedAudioAsync(stream);

        result.Should().Be(expected);
    }
}
