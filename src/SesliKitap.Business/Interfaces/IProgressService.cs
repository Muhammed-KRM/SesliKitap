using SesliKitap.Business.DTOs.Progress;

namespace SesliKitap.Business.Interfaces;

/// <summary>
/// Kitap dinleme konumu okuma ve cihazlar arası senkron güncelleme servisi sözleşmesi.
/// </summary>
public interface IProgressService
{
    /// <summary>
    /// Belirtilen kitabın son dinleme konumunu getirir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Dinleme konumu veya kayıt yoksa <c>null</c>.</returns>
    Task<ProgressDto?> GetAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Kitabın dinleme konumunu son-yazan-kazanır (<c>istemciZamani</c>) kuralıyla günceller ve diğer cihazlara SignalR ile bildirir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="request">Konum güncelleme isteği.</param>
    /// <param name="senderConnectionId">İsteği gönderen SignalR bağlantı kimliği (varsa bildirimden dışlanır).</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Sunucudaki kazanan güncel <see cref="ProgressDto"/>.</returns>
    Task<ProgressDto> UpdateAsync(
        Guid bookId,
        ProgressUpdateRequest request,
        string? senderConnectionId = null,
        CancellationToken ct = default);
}
