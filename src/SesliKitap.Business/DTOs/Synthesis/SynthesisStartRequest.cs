namespace SesliKitap.Business.DTOs.Synthesis;

/// <summary>
/// Bir kitap için seslendirme (sentez) işi başlatma isteği (Bölüm 4.15.4).
/// </summary>
/// <param name="VoiceProfileId">Kullanılacak ses profili kimliği.</param>
/// <param name="ChapterIds">Yalnızca belirli bölümler seslendirilecekse bölüm kimlikleri; <c>null</c> ise tüm dahil bölümler.</param>
/// <param name="SadeceEksikler"><c>true</c> ise yalnızca sesi olmayan veya metni değişmiş (<c>GuncelDegil</c>) bölümler seslendirilir.</param>
/// <param name="Force">Aylık bütçe aşılıyorsa uyarıyı geçersiz kılıp işi yine de başlatma onayı.</param>
/// <param name="CumleArasiMs">Özelleştirilmiş cümle arası duraklama (milisaniye); belirtilmezse varsayılan kullanılır.</param>
/// <param name="ParagrafArasiMs">Özelleştirilmiş paragraf arası duraklama (milisaniye); belirtilmezse varsayılan kullanılır.</param>
public sealed record SynthesisStartRequest(
    Guid VoiceProfileId,
    IReadOnlyList<Guid>? ChapterIds = null,
    bool SadeceEksikler = true,
    bool Force = false,
    int? CumleArasiMs = null,
    int? ParagrafArasiMs = null);
