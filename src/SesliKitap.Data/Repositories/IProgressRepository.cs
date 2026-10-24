using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// Dinleme konumu (<see cref="ListeningProgress"/>) veri erişim sözleşmesi.
/// </summary>
public interface IProgressRepository : IRepository<ListeningProgress>
{
    /// <summary>
    /// Belirtilen kitaba ait dinleme konumunu getirir.
    /// </summary>
    /// <param name="bookId">Kitap kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Dinleme konumu veya <c>null</c>.</returns>
    Task<ListeningProgress?> GetByBookAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>
    /// Son-yazan-kazanır (<c>istemci_zamani</c>) kuralıyla dinleme konumunu atomik olarak ekler veya günceller.
    /// </summary>
    /// <param name="progress">Eklenecek veya güncellenecek konum bilgisi.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Veritabanındaki güncel (kazanan) <see cref="ListeningProgress"/> kaydı.</returns>
    Task<ListeningProgress> UpsertAsync(ListeningProgress progress, CancellationToken ct = default);
}
