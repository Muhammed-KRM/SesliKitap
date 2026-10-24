using System.Linq.Expressions;

namespace SesliKitap.Data.Repositories;

/// <summary>
/// Tüm varlıklar için ortak veri erişim işlemlerini tanımlayan jenerik depo (repository) sözleşmesi.
/// </summary>
/// <typeparam name="T">Varlık tipi.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Belirtilen <paramref name="id"/> değerine sahip varlığı getirir.
    /// </summary>
    /// <param name="id">Varlık benzersiz kimliği.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Bulunursa varlık, aksi halde <c>null</c>.</returns>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tablodaki tüm kayıtları listeler.
    /// </summary>
    /// <param name="asNoTracking">Değişiklik izlemenin kapatılıp kapatılmayacağı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Varlık listesi.</returns>
    Task<IReadOnlyList<T>> GetAllAsync(bool asNoTracking = true, CancellationToken ct = default);

    /// <summary>
    /// Verilen koşula uyan kayıtları getirir.
    /// </summary>
    /// <param name="predicate">Filtreleme ifadesi.</param>
    /// <param name="asNoTracking">Değişiklik izlemenin kapatılıp kapatılmayacağı.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Filtrelenmiş varlık listesi.</returns>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true, CancellationToken ct = default);

    /// <summary>
    /// Verilen koşula uyan en az bir kayıt olup olmadığını kontrol eder.
    /// </summary>
    /// <param name="predicate">Filtreleme ifadesi.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Kayıt varsa <c>true</c>, yoksa <c>false</c>.</returns>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Verilen koşula uyan kayıt sayısını döner.
    /// </summary>
    /// <param name="predicate">Filtreleme ifadesi.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Kayıt sayısı.</returns>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Varlık kümesi üzerinde LINQ sorgusu oluşturmak için <see cref="IQueryable{T}"/> döner.
    /// </summary>
    /// <param name="asNoTracking">Değişiklik izlemenin kapatılıp kapatılmayacağı.</param>
    /// <returns>Sorgulanabilir varlık kümesi.</returns>
    IQueryable<T> Query(bool asNoTracking = true);

    /// <summary>
    /// Yeni bir varlığı bağlama ekler.
    /// </summary>
    /// <param name="entity">Eklenecek varlık.</param>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Eklenen varlık.</returns>
    Task<T> AddAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Birden çok varlığı toplu olarak bağlama ekler.
    /// </summary>
    /// <param name="entities">Eklenecek varlık koleksiyonu.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>
    /// Varlığı güncellenmiş olarak işaretler.
    /// </summary>
    /// <param name="entity">Güncellenecek varlık.</param>
    void Update(T entity);

    /// <summary>
    /// Varlığı silinmiş olarak işaretler (<see cref="SesliKitap.Data.Entities.Base.ISoftDeletable"/> ise yumuşak silme uygulanır).
    /// </summary>
    /// <param name="entity">Silinecek varlık.</param>
    void Delete(T entity);

    /// <summary>
    /// Bağlamdaki bekleyen değişiklikleri veritabanına kaydeder.
    /// </summary>
    /// <param name="ct">İptal belirteci.</param>
    /// <returns>Etkilenen satır sayısı.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Verilen işlemi veritabanı yürütme stratejisi ve transaction içinde çalıştırır.
    /// </summary>
    /// <param name="action">Çalıştırılacak asenkron işlem.</param>
    /// <param name="ct">İptal belirteci.</param>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
