using System.ComponentModel.DataAnnotations;

namespace SesliKitap.Business.Options;

/// <summary>
/// Uzak GPU TTS motorları, maliyet katsayıları ve bütçe seçenekleri (Bölüm 4.11).
/// </summary>
public sealed class TtsOptions
{
    /// <summary>Yapılandırma bölüm adı.</summary>
    public const string SectionName = "Tts";

    /// <summary>Denenmesi gereken TTS motorlarının öncelik sırası.</summary>
    [Required]
    [MinLength(1)]
    public string[] EngineOrder { get; init; } = ["voxcpm2_modal", "voxcpm2_alt", "xtts2_modal"];

    /// <summary>Saatlik GPU maliyeti (USD, Modal L4 varsayılanı: 0.80 USD/saat).</summary>
    [Range(0.01, 100.0)]
    public decimal GpuPricePerHourUsd { get; init; } = 0.80m;

    /// <summary>Konteyner ısınma ve boşta kalma ek yük çarpanı (varsayılan: 1.15).</summary>
    [Range(1.0, 3.0)]
    public double IdleOverheadFactor { get; init; } = 1.15;

    /// <summary>Tahmini gerçek zaman faktörü (RTF, varsayılan: 0.20).</summary>
    [Range(0.01, 5.0)]
    public double EstimatedRtf { get; init; } = 0.20;

    /// <summary>Aylık GPU bütçe sınırı (USD, varsayılan: 30 USD).</summary>
    [Range(0.0, 10000.0)]
    public decimal MonthlyBudgetUsd { get; init; } = 30.0m;

    /// <summary>Ortalama Türkçe sesli okuma hızı (kelime/dakika, varsayılan: 140).</summary>
    [Range(60, 300)]
    public int WordsPerMinute { get; init; } = 140;

    /// <summary>Tahmini konteyner soğuk başlatma süresi (saniye, varsayılan: 90).</summary>
    [Range(0, 600)]
    public int ColdStartSec { get; init; } = 90;

    /// <summary>Uzak TTS sunucusunun sağlık kontrolü URL'si (opsiyonel).</summary>
    public string? HealthUrl { get; init; }

    /// <summary>Uzak TTS sunucusu Bearer kimlik doğrulama anahtarı.</summary>
    public string? ApiToken { get; init; }
}
