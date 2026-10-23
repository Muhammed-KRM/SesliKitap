using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SesliKitap.Data.Entities;
using SesliKitap.Data.Entities.Base;

namespace SesliKitap.Data.Context;

/// <summary>
/// SesliKitap uygulamasının ana Entity Framework Core veritabanı bağlamı.
/// </summary>
public partial class AppDbContext : DbContext
{
    /// <summary>
    /// <see cref="AppDbContext"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    /// <param name="options">Veritabanı bağlamı yapılandırma seçenekleri.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>Kütüphanedeki kitaplar tablosu.</summary>
    public virtual DbSet<Book> Books => Set<Book>();

    /// <summary>Kitap bölümleri tablosu.</summary>
    public virtual DbSet<BookChapter> BookChapters => Set<BookChapter>();

    /// <summary>HTTP uç noktası (endpoint) istek/yanıt logları (Katman 1).</summary>
    public virtual DbSet<EndpointLog> EndpointLogs => Set<EndpointLog>();

    /// <summary>Fonksiyon ve servis seviyesi hata/uyarı logları (Katman 2).</summary>
    public virtual DbSet<FunctionLog> FunctionLogs => Set<FunctionLog>();

    /// <summary>Kitap işleme ve seslendirme pipeline aşama logları (Katman 3).</summary>
    public virtual DbSet<PipelineLog> PipelineLogs => Set<PipelineLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            ApplySnakeCaseNaming(entityType);

            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                ApplySoftDeleteFilter(modelBuilder, entityType.ClrType);
            }
        }
    }

    private static void ApplySoftDeleteFilter(ModelBuilder modelBuilder, Type entityClrType)
    {
        var parameter = Expression.Parameter(entityClrType, "e");
        var property = Expression.Property(parameter, nameof(ISoftDeletable.SilindiMi));
        var filter = Expression.Lambda(Expression.Not(property), parameter);
        modelBuilder.Entity(entityClrType).HasQueryFilter(filter);
    }

    private static void ApplySnakeCaseNaming(IMutableEntityType entityType)
    {
        var tableName = entityType.GetTableName();
        if (!string.IsNullOrEmpty(tableName))
        {
            entityType.SetTableName(ToSnakeCase(tableName));
        }

        foreach (var property in entityType.GetProperties())
        {
            var storeObject = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
            var columnName = property.GetColumnName(storeObject);
            if (!string.IsNullOrEmpty(columnName))
            {
                property.SetColumnName(ToSnakeCase(columnName));
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return SnakeCaseRegex().Replace(input, "$1_$2").ToLowerInvariant();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCaseRegex();
}
