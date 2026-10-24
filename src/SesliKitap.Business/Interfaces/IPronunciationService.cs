using SesliKitap.Business.DTOs.Pronunciation;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Telaffuz sözlüğü girdilerini yöneten ve metin önizlemesi sunan iş servisi sözleşmesi (Faz 8).
/// </summary>
public interface IPronunciationService
{
    /// <summary>
    /// Tüm telaffuz sözlüğü girdilerini getirir (Redis önbellekli).
    /// </summary>
    Task<IReadOnlyList<PronunciationDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Yeni bir telaffuz sözlüğü girdisi ekler.
    /// </summary>
    Task<PronunciationDto> CreateAsync(PronunciationCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Mevcut bir telaffuz sözlüğü girdisini günceller.
    /// </summary>
    Task<PronunciationDto> UpdateAsync(Guid id, PronunciationUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Telaffuz sözlüğü girdisini siler.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Verilen metin üzerinde aktif telaffuz sözlüğü kurallarını uygulayarak önizleme üretir.
    /// </summary>
    Task<PronunciationPreviewResponse> PreviewAsync(PronunciationPreviewRequest request, CancellationToken ct = default);
}
