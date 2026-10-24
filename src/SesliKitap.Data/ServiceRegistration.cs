using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SesliKitap.Data.Context;
using SesliKitap.Data.Repositories;

namespace SesliKitap.Data;

/// <summary>
/// Veri katmanı (<c>SesliKitap.Data</c>) bağımlılık enjeksiyonu (DI) kayıt uzantıları.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// PostgreSQL veritabanı bağlamını, denetim kesicisini ve jenerik depoları DI konteynerine kaydeder.
    /// </summary>
    /// <param name="services">Servis koleksiyonu.</param>
    /// <param name="connectionString">PostgreSQL bağlantı dizesi.</param>
    /// <returns>Güncellenmiş servis koleksiyonu.</returns>
    public static IServiceCollection AddDataLayer(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<AuditingInterceptor>();

        services.AddDbContext<AppDbContext>(
            (sp, options) => ConfigureDbContext(options, sp, connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Scoped);

        services.AddDbContextFactory<AppDbContext>(
            (sp, options) => ConfigureDbContext(options, sp, connectionString),
            lifetime: ServiceLifetime.Scoped);

        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IChapterRepository, ChapterRepository>();
        services.AddScoped<IVoiceProfileRepository, VoiceProfileRepository>();
        services.AddScoped<ISynthesisJobRepository, SynthesisJobRepository>();

        return services;
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        IServiceProvider serviceProvider,
        string connectionString)
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        })
        .AddInterceptors(serviceProvider.GetRequiredService<AuditingInterceptor>());
    }
}
