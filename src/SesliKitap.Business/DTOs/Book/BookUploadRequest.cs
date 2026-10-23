using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SesliKitap.Business.DTOs.Book;

/// <summary>
/// <c>POST /api/books/upload</c> uç noktası için multipart form-data yükleme isteği.
/// </summary>
public sealed class BookUploadRequest
{
    /// <summary>
    /// Yüklenecek kitap dosyası (PDF, EPUB veya TXT).
    /// </summary>
    public required IFormFile Dosya { get; init; }

    /// <summary>
    /// Opsiyonel kitap başlığı (verilmezse dosya meta verisi veya dosya adından türetilir).
    /// </summary>
    [MaxLength(500)]
    public string? Baslik { get; init; }

    /// <summary>
    /// Opsiyonel yazar adı.
    /// </summary>
    [MaxLength(300)]
    public string? Yazar { get; init; }
}
