using SesliKitap.Data.Entities;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.DTOs.Synthesis;

/// <summary>
/// Sentez işine dahil edilen tekil bölümün durum görünüm modeli.
/// </summary>
/// <param name="ChapterId">Bölüm kimliği.</param>
/// <param name="Sira">İşteki sıra numarası.</param>
/// <param name="SiraNo">Kitaptaki bölüm sıra numarası.</param>
/// <param name="Baslik">Bölüm başlığı.</param>
/// <param name="Durum">Bölümün seslendirme durumu.</param>
/// <param name="GpuMs">Harcanan GPU süresi (ms).</param>
/// <param name="SureSn">Üretilen ses süresi (sn).</param>
/// <param name="HataMesaji">Varsa bölüme özgü hata mesajı.</param>
public sealed record SynthesisJobChapterDto(
    Guid ChapterId,
    int Sira,
    int SiraNo,
    string Baslik,
    BolumSesDurumu Durum,
    long GpuMs,
    double? SureSn,
    string? HataMesaji);

/// <summary>
/// Sentez işi detay görünüm modeli (bölüm durumları ve ayarlarıyla birlikte).
/// </summary>
public sealed record SynthesisJobDetailDto(
    SynthesisJobDto Is,
    SynthesisJobSettings Ayarlar,
    IReadOnlyList<SynthesisJobChapterDto> Bolumler);
