using Microsoft.EntityFrameworkCore;
using SesliKitap.API.Middleware;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data.Context;

namespace SesliKitap.API.Extensions;

/// <summary>
/// HTTP istek hattı (middleware pipeline) ve başlangıç altyapı hazırlığı uzantıları.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// SesliKitap HTTP ara katmanlarını (Middleware) mimari dokümandaki (Bölüm 3.2.1) sırayla yapılandırır.
    /// </summary>
    /// <param name="app">Web uygulaması.</param>
    /// <returns>Web uygulaması.</returns>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseResponseCompression();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestResponseLoggingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SesliKitap API v1");
            });
        }

        app.UseRouting();
        app.UseCors("AllowFrontend");
        app.UseRateLimiter();
        app.UseMiddleware<ApiKeyAuthMiddleware>();
        app.UseAuthorization();

        app.MapControllers().RequireRateLimiting("general");

        return app;
    }

    /// <summary>
    /// Uygulama açılışında PostgreSQL veritabanı migration'larını uygular ve MinIO bucket'ını hazırlar.
    /// </summary>
    /// <param name="app">Web uygulaması.</param>
    /// <param name="ct">İptal belirteci.</param>
    public static async Task InitializeInfrastructureAsync(this WebApplication app, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("PostgreSQL veritabanı migration'ları başarıyla uygulandı.");
            }
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Veritabanı migration uygulanırken kritik hata oluştu. Uygulama durduruluyor.");
            throw;
        }

        var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
        await fileStorage.EnsureBucketAsync(ct);
    }
}
