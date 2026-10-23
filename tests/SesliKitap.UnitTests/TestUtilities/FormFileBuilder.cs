using System.Text;
using Microsoft.AspNetCore.Http;

namespace SesliKitap.UnitTests.TestUtilities;

public static class FormFileBuilder
{
    public static IFormFile CreatePdf(string fileName = "kitap.pdf", string bodyContent = "Test PDF Content")
    {
        var bytes = Encoding.UTF8.GetBytes($"%PDF-1.7\n1 0 obj\n<<>>\nendobj\n{bodyContent}\n%%EOF");
        return FromBytes(bytes, fileName, "application/pdf");
    }

    public static IFormFile CreateEpub(string fileName = "kitap.epub")
    {
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var tail = Encoding.ASCII.GetBytes("mimetypeapplication/epub+zipMETA-INF/container.xml");
        var bytes = header.Concat(new byte[26]).Concat(tail).ToArray();
        return FromBytes(bytes, fileName, "application/epub+zip");
    }

    public static IFormFile CreateTxt(string fileName = "kitap.txt", string content = "Merhaba dünya, Türkçe metin içeriği.")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return FromBytes(bytes, fileName, "text/plain");
    }

    public static IFormFile CreateDisguisedJpegAsPdf(string fileName = "sahte.pdf")
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        return FromBytes(bytes, fileName, "application/pdf");
    }

    public static IFormFile FromBytes(byte[] bytes, string fileName, string contentType = "application/octet-stream")
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "dosya", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
