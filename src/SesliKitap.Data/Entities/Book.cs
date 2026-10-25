using SesliKitap.Data.Entities.Base;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Kütüphanedeki bir kitabı ve işleme/seslendirme durumunu temsil eden varlık.
/// </summary>
public sealed class Book : AuditableEntity, ISoftDeletable
{
    /// <summary>
    /// Kitap başlığı.
    /// </summary>
    public string Baslik { get; set; } = string.Empty;

    /// <summary>
    /// Kitabın yazar(lar)ı.
    /// </summary>
    public string? Yazar { get; set; }

    /// <summary>
    /// Kitap açıklaması veya özeti.
    /// </summary>
    public string? Aciklama { get; set; }

    /// <summary>
    /// Kitabın dili (ISO 639-1 kodu, varsayılan: "tr").
    /// </summary>
    public string Dil { get; set; } = "tr";

    /// <summary>
    /// Kitabın yayın yılı.
    /// </summary>
    public int? YayinYili { get; set; }

    /// <summary>
    /// Kitabın sisteme eklenme kaynağı.
    /// </summary>
    public KaynakTipi KaynakTipi { get; set; }

    /// <summary>
    /// Dış kaynaktaki benzersiz kimlik (Internet Archive identifier, Open Library key vb.).
    /// </summary>
    public string? KaynakKimligi { get; set; }

    /// <summary>
    /// Dış kaynaktaki indirme veya bilgi sayfası URL'si.
    /// </summary>
    public string? KaynakUrl { get; set; }

    /// <summary>
    /// Orijinal kaynak dosyanın formatı (PDF, EPUB, TXT).
    /// </summary>
    public DosyaFormati DosyaFormati { get; set; }

    /// <summary>
    /// Orijinal kaynak dosyanın MinIO nesne anahtarı.
    /// </summary>
    public string? OrijinalDosyaYolu { get; set; }

    /// <summary>
    /// Kullanıcının yüklediği orijinal dosya adı (yalnızca gösterim amaçlı).
    /// </summary>
    public string? OrijinalDosyaAdi { get; set; }

    /// <summary>
    /// Orijinal kaynak dosyanın bayt cinsinden boyutu.
    /// </summary>
    public long DosyaBoyutu { get; set; }

    /// <summary>
    /// Orijinal dosyanın SHA-256 özeti (64 karakter onaltılık).
    /// </summary>
    public string? DosyaHash { get; set; }

    /// <summary>
    /// Kitap kapağının MinIO nesne anahtarı.
    /// </summary>
    public string? KapakYolu { get; set; }

    /// <summary>
    /// Kaynak belgedeki toplam sayfa sayısı.
    /// </summary>
    public int? SayfaSayisi { get; set; }

    /// <summary>
    /// Seslendirmeye dahil olan bölümlerin toplam kelime sayısı.
    /// </summary>
    public int KelimeSayisi { get; set; }

    /// <summary>
    /// Sesi hazır olan bölümlerin toplam süresi (saniye).
    /// </summary>
    public double ToplamSureSn { get; set; }

    /// <summary>
    /// Kitabın yaşam döngüsü durumu.
    /// </summary>
    public KitapDurumu Durum { get; set; }

    /// <summary>
    /// Son oluşan hata mesajı (varsa).
    /// </summary>
    public string? HataMesaji { get; set; }

    /// <summary>
    /// Kitap için seçilen varsayılan ses profili kimliği.
    /// </summary>
    public Guid? VarsayilanSesProfiliId { get; set; }

    /// <summary>
    /// Kitabın en son dinlendiği tarih ve saat (UTC).
    /// </summary>
    public DateTime? SonDinlenmeTarihi { get; set; }

    /// <summary>
    /// Metin çıkarma ve temizlik istatistiklerini içeren JSON verisi (jsonb).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// PostgreSQL tam metin arama (tsvector) alanı (Faz 9).
    /// </summary>
    public NpgsqlTypes.NpgsqlTsVector? AramaVektoru { get; set; }

    /// <summary>
    /// Kaydın yumuşak silinmiş olup olmadığını belirtir.
    /// </summary>
    public bool SilindiMi { get; set; }

    /// <summary>
    /// Kaydın silinme zamanı (UTC).
    /// </summary>
    public DateTime? SilinmeTarihi { get; set; }

    /// <summary>
    /// Kitaba ait bölümler.
    /// </summary>
    public ICollection<BookChapter> Bolumler { get; set; } = new List<BookChapter>();

    /// <summary>
    /// Kitaba ait dinleme konumu.
    /// </summary>
    public ListeningProgress? Konum { get; set; }
}
