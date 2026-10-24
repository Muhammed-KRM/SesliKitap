using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// <see cref="VoiceProfile"/> varlığına özgü veri erişim sözleşmesi.
/// </summary>
public interface IVoiceProfileRepository : IRepository<VoiceProfile>
{
    /// <summary>
    /// Silinmemiş tüm ses profillerini varsayılan olma durumu ve oluşturulma tarihine göre sıralı getirir.
    /// </summary>
    Task<IReadOnlyList<VoiceProfile>> GetAllOrderedAsync(CancellationToken ct = default);

    /// <summary>
    /// Aynı isimde (büyük/küçük harf duyarsız) başka bir aktif ses profili olup olmadığını kontrol eder.
    /// </summary>
    Task<bool> ExistsByNameAsync(string ad, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen ses profilini tek varsayılan profil olarak işaretler ve diğer profillerin varsayılanlığını kaldırır.
    /// </summary>
    Task SetDefaultAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Yumuşak silinmiş ve silinme tarihi belirtilen eşikten eski olan ses profillerini getirir.
    /// </summary>
    Task<IReadOnlyList<VoiceProfile>> GetExpiredSoftDeletedAsync(DateTime cutoffUtc, CancellationToken ct = default);

    /// <summary>
    /// Ses profilini veritabanından kalıcı olarak siler.
    /// </summary>
    Task HardDeleteAsync(VoiceProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Belirtilen yıl ve ayda oluşturulan ses profillerinin örnek ses üretimi için harcadığı toplam GPU milisaniyesini döndürür.
    /// </summary>
    Task<long> SumGpuMsForMonthAsync(int year, int month, CancellationToken ct = default);
}
