using SesliKitap.Data.Entities.Base;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Bir kitabın tek bir bölümünü, temizlenmiş metnini ve üretilen ses bilgilerini temsil eden varlık.
/// </summary>
public sealed class BookChapter : AuditableEntity
{
    /// <summary>
    /// Bölümün ait olduğu kitabın kimliği.
    /// </summary>
    public Guid BookId { get; set; }

    /// <summary>
    /// Kitap içindeki bölüm sıra numarası (1'den başlar).
    /// </summary>
    public int SiraNo { get; set; }

    /// <summary>
    /// Bölüm başlığı.
    /// </summary>
    public string Baslik { get; set; } = string.Empty;

    /// <summary>
    /// Görüntülenen ve düzenlenen temiz metin (TTS normalizasyonu öncesi).
    /// </summary>
    public string TemizMetin { get; set; } = string.Empty;

    /// <summary>
    /// Bölümdeki toplam kelime sayısı.
    /// </summary>
    public int KelimeSayisi { get; set; }

    /// <summary>
    /// Bölümdeki toplam karakter sayısı.
    /// </summary>
    public int KarakterSayisi { get; set; }

    /// <summary>
    /// Bölümün seslendirmeye dahil edilip edilmeyeceğini belirtir.
    /// </summary>
    public bool DahilMi { get; set; } = true;

    /// <summary>
    /// Bölüm otomatik olarak hariç tutulduysa nedeni (örn. "İçindekiler başlığı").
    /// </summary>
    public string? OtomatikHaricNedeni { get; set; }

    /// <summary>
    /// Metin sürüm numarası (iyimser kilitleme ve ses güncelliği takibi için; her metin düzenlemesinde +1 artar).
    /// </summary>
    public int MetinSurumu { get; set; } = 1;

    /// <summary>
    /// Bölümün ses üretim durumu.
    /// </summary>
    public BolumSesDurumu SesDurumu { get; set; } = BolumSesDurumu.Yok;

    /// <summary>
    /// Mevcut ses dosyasının üretildiği metin sürümü.
    /// </summary>
    public int? SesMetinSurumu { get; set; }

    /// <summary>
    /// Sesin üretildiği ses profili kimliği.
    /// </summary>
    public Guid? SesProfiliId { get; set; }

    /// <summary>
    /// Sesin üretildiği TTS motoru.
    /// </summary>
    public TtsMotoru? SesMotoru { get; set; }

    /// <summary>
    /// Opus (OGG) ses dosyasının MinIO nesne anahtarı.
    /// </summary>
    public string? OpusYolu { get; set; }

    /// <summary>
    /// MP3 ses dosyasının MinIO nesne anahtarı.
    /// </summary>
    public string? Mp3Yolu { get; set; }

    /// <summary>
    /// Cümle zaman damgaları JSON dosyasının MinIO nesne anahtarı.
    /// </summary>
    public string? ZamanDamgasiYolu { get; set; }

    /// <summary>
    /// Opus ses dosyasının bayt cinsinden boyutu.
    /// </summary>
    public long? OpusBoyutu { get; set; }

    /// <summary>
    /// MP3 ses dosyasının bayt cinsinden boyutu.
    /// </summary>
    public long? Mp3Boyutu { get; set; }

    /// <summary>
    /// Üretilen sesin saniye cinsinden süresi.
    /// </summary>
    public double? SureSn { get; set; }

    /// <summary>
    /// Bölümdeki toplam cümle sayısı.
    /// </summary>
    public int? CumleSayisi { get; set; }

    /// <summary>
    /// Bu bölüm için harcanan toplam GPU çıkarım süresi (milisaniye).
    /// </summary>
    public long GpuMs { get; set; }

    /// <summary>
    /// Bölüm işleme/seslendirme sırasında oluşan son hata mesajı.
    /// </summary>
    public string? HataMesaji { get; set; }

    /// <summary>
    /// Bölümün ait olduğu kitap navigasyon özelliği.
    /// </summary>
    public Book Book { get; set; } = null!;
}
