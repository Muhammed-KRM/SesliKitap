using SesliKitap.Business.DTOs.Voice;
using SesliKitap.Business.Events;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Ses profillerini oluşturma, güncelleme, varsayılan yapma ve örnek ses akışı sunma servis sözleşmesi (Bölüm 4.9).
/// </summary>
public interface IVoiceProfileService
{
    /// <summary>Tüm aktif ses profillerini listeler (Redis önbellekli).</summary>
    Task<IReadOnlyList<VoiceProfileDto>> GetListAsync(CancellationToken ct = default);

    /// <summary>Belirtilen kimlikli ses profilini getirir.</summary>
    Task<VoiceProfileDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Yeni bir ses profili yükler ve hazırlama kuyruğuna gönderir.</summary>
    Task<VoiceProfileDto> CreateAsync(VoiceProfileCreateRequest request, string traceId, CancellationToken ct = default);

    /// <summary>Ses profilinin adını, açıklamasını ve sentez parametrelerini günceller.</summary>
    Task<VoiceProfileDto> UpdateAsync(Guid id, VoiceProfileUpdateRequest request, CancellationToken ct = default);

    /// <summary>Ses profilini varsayılan profil olarak işaretler.</summary>
    Task SetDefaultAsync(Guid id, CancellationToken ct = default);

    /// <summary>Ses profili için örnek ses üretimini yeniden tetikler.</summary>
    Task<VoiceProfileDto> RegenerateSampleAsync(Guid id, VoiceRegenerateSampleRequest request, string traceId, CancellationToken ct = default);

    /// <summary>Ses profilinin örnek sesini (<c>sample.ogg</c>) akış olarak açar.</summary>
    Task<(Stream Stream, string ContentType)> OpenSampleAsync(Guid id, CancellationToken ct = default);

    /// <summary>Ses profilini yumuşak siler.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Python Worker'dan gelen <see cref="VoiceProfileReadyEvent"/> olayını işler (idempotent).</summary>
    Task HandleReadyAsync(VoiceProfileReadyEvent readyEvent, CancellationToken ct = default);
}
