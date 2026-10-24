using SesliKitap.Data.Entities.Base;
using SesliKitap.Data.Enums;

namespace SesliKitap.Data.Entities;

/// <summary>
/// Bir kitabın seçilen ses profiliyle seslendirilme işini temsil eden varlık (<c>synthesis_jobs</c>, Bölüm 4.6.2).
/// </summary>
public sealed class SynthesisJob : AuditableEntity
{
    /// <summary>Seslendirilen kitabın kimliği.</summary>
    public Guid BookId { get; set; }

    /// <summary>Seslendirilen kitap navigasyon özelliği.</summary>
    public Book? Book { get; set; }

    /// <summary>Kullanılan ses profilinin kimliği.</summary>
    public Guid VoiceProfileId { get; set; }

    /// <summary>Kullanılan ses profili navigasyon özelliği.</summary>
    public VoiceProfile? VoiceProfile { get; set; }

    /// <summary>İş başlatılırken belirlenen TTS motor öncelik sırası (virgülle ayrılmış).</summary>
    public string MotorSirasi { get; set; } = "voxcpm2_modal";

    /// <summary>İş boyunca fiilen kullanılan TTS motorları (virgülle ayrılmış).</summary>
    public string? KullanilanMotorlar { get; set; }

    /// <summary>İşin mevcut durumu.</summary>
    public SentezIsDurumu Durum { get; set; } = SentezIsDurumu.Kuyrukta;

    /// <summary>İşteki toplam bölüm sayısı.</summary>
    public int ToplamBolum { get; set; }

    /// <summary>Tamamlanan bölüm sayısı.</summary>
    public int TamamlananBolum { get; set; }

    /// <summary>Hata alan bölüm sayısı.</summary>
    public int HataliBolum { get; set; }

    /// <summary>Genel tamamlanma yüzdesi (0–100).</summary>
    public int Yuzde { get; set; }

    /// <summary>Şu an işlenmekte olan bölümün kimliği.</summary>
    public Guid? MevcutBolumId { get; set; }

    /// <summary>Son ilerleme/durum mesajı.</summary>
    public string? Mesaj { get; set; }

    /// <summary>Toplam TTS parçası sayısı.</summary>
    public int ToplamParca { get; set; }

    /// <summary>Önbellekten karşılanan parça sayısı (maliyetsiz).</summary>
    public int OnbellektenParca { get; set; }

    /// <summary>GPU üzerinde yeni üretilen parça sayısı.</summary>
    public int UretilenParca { get; set; }

    /// <summary>Bu iş için harcanan toplam GPU çıkarım süresi (milisaniye).</summary>
    public long GpuMs { get; set; }

    /// <summary>Hesaplanan tahmini/gerçekleşen maliyet (USD).</summary>
    public decimal TahminiMaliyetUsd { get; set; }

    /// <summary>Başlangıçta hesaplanan tahmini toplam ses süresi (saniye).</summary>
    public double? TahminiSureSn { get; set; }

    /// <summary>Fiilen üretilen toplam ses süresi (saniye).</summary>
    public double UretilenSesSn { get; set; }

    /// <summary>İş başlatılırken dondurulan ses işleme ve duraklama ayarları (<c>jsonb</c>).</summary>
    public SynthesisJobSettings Ayarlar { get; set; } = new();

    /// <summary>Uçtan uca izleme kimliği.</summary>
    public string? TraceId { get; set; }

    /// <summary>Worker tarafından fiilen işlenmeye başlandığı zaman (UTC).</summary>
    public DateTime? BaslangicZamani { get; set; }

    /// <summary>İşin tamamlandığı, iptal edildiği veya hata aldığı zaman (UTC).</summary>
    public DateTime? BitisZamani { get; set; }

    /// <summary>Son ilerleme bildiriminin alındığı zaman (Watchdog kontrolü için, UTC).</summary>
    public DateTime? SonIlerlemeZamani { get; set; }

    /// <summary>Varsa son hata mesajı.</summary>
    public string? HataMesaji { get; set; }

    /// <summary>PostgreSQL iyimser eşzamanlılık belirteci (<c>xmin</c>).</summary>
    public uint Xmin { get; set; }

    /// <summary>Bu işe dahil edilen bölümlerin durum kayıtları.</summary>
    public ICollection<SynthesisJobChapter> Bolumler { get; set; } = new List<SynthesisJobChapter>();
}

/// <summary>
/// Sentez işi başlatılırken <c>synthesis_jobs.ayarlar</c> JSON kolonunda saklanan ayarlar.
/// </summary>
public sealed class SynthesisJobSettings
{
    /// <summary>Cümleler arası duraklama süresi (ms).</summary>
    public int CumleArasiMs { get; set; } = 300;

    /// <summary>Paragraflar arası duraklama süresi (ms).</summary>
    public int ParagrafArasiMs { get; set; } = 700;

    /// <summary>Bölüm başlığı okunduktan sonraki duraklama süresi (ms).</summary>
    public int BaslikSonrasiMs { get; set; } = 1200;

    /// <summary>Diyalog geçişlerindeki duraklama süresi (ms).</summary>
    public int DiyalogMs { get; set; } = 400;

    /// <summary>Bölüm başlığının seslendirilip seslendirilmeyeceği.</summary>
    public bool BolumBasligiOku { get; set; } = true;

    /// <summary>Opus bit hızı (kbps).</summary>
    public int OpusBitrateKbps { get; set; } = 48;

    /// <summary>MP3 bit hızı (kbps).</summary>
    public int Mp3BitrateKbps { get; set; } = 64;

    /// <summary>Hedef ses yüksekliği (LUFS).</summary>
    public double HedefLufs { get; set; } = -18.0;

    /// <summary>Yalnızca eksik veya güncel olmayan bölümlerin mi seslendirileceği.</summary>
    public bool SadeceEksikler { get; set; } = true;
}
