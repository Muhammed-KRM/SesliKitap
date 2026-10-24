using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// Bölüm seslendirme ve ses profili örnek üretimi için varsayılan ses işleme ayarları (Bölüm 4.11).
/// </summary>
public sealed class SynthesisDefaultsOptions
{
    /// <summary>Yapılandırma bölüm adı.</summary>
    public const string SectionName = "SynthesisDefaults";

    /// <summary>Cümleler arası duraklama süresi (milisaniye).</summary>
    [Range(50, 2000)]
    public int CumleArasiMs { get; init; } = 300;

    /// <summary>Paragraflar arası duraklama süresi (milisaniye).</summary>
    [Range(100, 4000)]
    public int ParagrafArasiMs { get; init; } = 700;

    /// <summary>Bölüm başlığı okunduktan sonraki duraklama süresi (milisaniye).</summary>
    [Range(200, 5000)]
    public int BaslikSonrasiMs { get; init; } = 1200;

    /// <summary>Diyalog geçişlerindeki duraklama süresi (milisaniye).</summary>
    [Range(50, 2000)]
    public int DiyalogMs { get; init; } = 400;

    /// <summary>Bölüm başlığının seslendirmeye dahil edilip edilmeyeceği.</summary>
    public bool BolumBasligiOku { get; init; } = true;

    /// <summary>Opus (OGG) kodlama bit hızı (kbps).</summary>
    [Range(24, 192)]
    public int OpusBitrateKbps { get; init; } = 48;

    /// <summary>MP3 kodlama bit hızı (kbps).</summary>
    [Range(32, 320)]
    public int Mp3BitrateKbps { get; init; } = 64;

    /// <summary>EBU R128 ses yüksekliği hedefi (LUFS).</summary>
    [Range(-30.0, -10.0)]
    public double HedefLufs { get; init; } = -18.0;

    /// <summary>Ses profili oluşturulduğunda örnek ses üretmek için kullanılan telifsiz Türkçe paragraf.</summary>
    [Required]
    public string SampleParagraph { get; init; } =
        "Sabahın erken saatlerinde boğazdan geçen vapurun sesi, açık pencereden içeri süzülüyordu. " +
        "Yirmi dokuz Ekim sabahı saat sekiz otuzda başlayan bu yolculuk, hepimiz için yepyeni bir başlangıçtı! " +
        "— Hazır mısın? diye sordu ihtiyar kaptan, gözlerini ufuk çizgisine dikerek.";
}
