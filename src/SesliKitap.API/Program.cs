using Serilog;
using SesliKitap.API.BackgroundServices;
using SesliKitap.API.Consumers;
using SesliKitap.API.Extensions;
using SesliKitap.API.Hubs;
using SesliKitap.Business;
using SesliKitap.Business.Interfaces;
using SesliKitap.Data;

var builder = WebApplication.CreateBuilder(args);
EnvFileLoader.LoadNearest(builder.Configuration);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

var maxUploadBytes = builder.Configuration.GetValue<long?>("Upload:MaxBookBytes") ?? 314_572_800L;
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = maxUploadBytes + (1024 * 1024);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection (veya .env içindeki DB_* değişkenleri) eksik.");

builder.Services
    .AddApiPresentation(builder.Configuration, builder.Environment)
    .AddDataLayer(connectionString)
    .AddBusinessLayer(builder.Configuration, bus =>
    {
        bus.AddConsumer<BookProcessedConsumer>();
        bus.AddConsumer<PipelineStageChangedConsumer>();
        bus.AddConsumer<PipelineErrorConsumer>();
        bus.AddConsumer<VoiceProfileReadyConsumer>();
        bus.AddConsumer<ChapterSynthesizedConsumer>();
        bus.AddConsumer<SynthesisCompletedConsumer>();
    })
    .AddScoped<INotificationService, SignalRNotificationService>()
    .AddHostedService<StuckJobWatchdogService>()
    .AddHostedService<StorageCleanupBackgroundService>();

var app = builder.Build();
app.UseApiPipeline();
await app.InitializeInfrastructureAsync();
await app.RunAsync();

/// <summary>
/// Entegrasyon testleri ve <c>WebApplicationFactory&lt;Program&gt;</c> için giriş noktası.
/// </summary>
public partial class Program;
