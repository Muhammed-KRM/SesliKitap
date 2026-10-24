namespace SesliKitap.Business.Events;

/// <summary>
/// Python Worker'da bir bölümün seslendirmesi, kodlaması ve zaman damgası üretimi tamamlandığında yayınlanan olay (Bölüm 4.7.2).
/// </summary>
public sealed record ChapterSynthesizedEvent : IntegrationEvent
{
    /// <summary>Sentez işi kimliği.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Kitap kimliği.</summary>
    public required Guid BookId { get; init; }

    /// <summary>Bölüm kimliği.</summary>
    public required Guid ChapterId { get; init; }

    /// <summary>Sesin üretildiği bölüm metin sürüm numarası.</summary>
    public required int MetinSurumu { get; init; }

    /// <summary>MinIO üzerindeki Opus (<c>.ogg</c>) ses dosyası anahtarı.</summary>
    public required string OpusYolu { get; init; }

    /// <summary>MinIO üzerindeki MP3 (<c>.mp3</c>) ses dosyası anahtarı.</summary>
    public required string Mp3Yolu { get; init; }

    /// <summary>MinIO üzerindeki cümle zaman damgaları (<c>timestamps.json</c>) anahtarı.</summary>
    public required string ZamanDamgasiYolu { get; init; }

    /// <summary>Opus dosyasının bayt boyutu.</summary>
    public required long OpusBoyutu { get; init; }

    /// <summary>MP3 dosyasının bayt boyutu.</summary>
    public required long Mp3Boyutu { get; init; }

    /// <summary>Üretilen bölüm sesinin saniye cinsinden süresi.</summary>
    public required double SureSn { get; init; }

    /// <summary>Bölümdeki toplam cümle sayısı.</summary>
    public required int CumleSayisi { get; init; }

    /// <summary>Bölümü üretmekte kullanılan TTS motor kimliği (örn. <c>voxcpm2_modal</c>).</summary>
    public required string Motor { get; init; }

    /// <summary>Bu bölüm için harcanan toplam GPU çıkarım süresi (ms).</summary>
    public required long GpuMs { get; init; }

    /// <summary>Önbellekten karşılanan parça sayısı.</summary>
    public required int OnbellekParca { get; init; }

    /// <summary>GPU üzerinde yeni üretilen parça sayısı.</summary>
    public required int UretilenParca { get; init; }

    /// <summary>Varsa bölüm üretimi sırasında oluşan kalite veya motor geçiş uyarıları.</summary>
    public IReadOnlyList<string> Uyarilar { get; init; } = [];
}
