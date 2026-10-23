namespace SesliKitap.API.Extensions;

/// <summary>
/// Proje kök dizinindeki <c>.env</c> dosyasını bulup ortam değişkenlerine ve <see cref="IConfiguration"/>
/// ağacına eşleyen yardımcı sınıf.
/// </summary>
public static class EnvFileLoader
{
    private static readonly IReadOnlyDictionary<string, string> EnvToConfigurationMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN_API_KEY"] = "Security:ApiKey",
            ["MEDIA_TOKEN_SECRET"] = "Security:MediaTokenSecret",
            ["REDIS_CONNECTION_STRING"] = "Redis:ConnectionString",
            ["RABBITMQ_HOST"] = "RabbitMQ:Host",
            ["RABBITMQ_PORT"] = "RabbitMQ:Port",
            ["RABBITMQ_USER"] = "RabbitMQ:Username",
            ["RABBITMQ_PASSWORD"] = "RabbitMQ:Password",
            ["MINIO_ENDPOINT"] = "Minio:Endpoint",
            ["MINIO_ROOT_USER"] = "Minio:AccessKey",
            ["MINIO_ROOT_PASSWORD"] = "Minio:SecretKey",
            ["MINIO_BUCKET_NAME"] = "Minio:BucketName",
            ["MINIO_USE_SSL"] = "Minio:UseSsl",
            ["TTS_MODAL_URL"] = "Tts:ModalUrl",
            ["TTS_MODAL_HEALTH_URL"] = "Tts:HealthUrl",
            ["TTS_ALT_URL"] = "Tts:AltUrl",
            ["TTS_XTTS_URL"] = "Tts:XttsUrl",
            ["TTS_API_TOKEN"] = "Tts:ApiToken",
            ["TTS_GPU_PRICE_PER_HOUR_USD"] = "Tts:GpuPricePerHourUsd",
            ["TTS_IDLE_OVERHEAD_FACTOR"] = "Tts:IdleOverheadFactor",
            ["TTS_ESTIMATED_RTF"] = "Tts:EstimatedRtf",
            ["TTS_MONTHLY_BUDGET_USD"] = "Tts:MonthlyBudgetUsd"
        };

    /// <summary>
    /// Çalışma dizininden yukarı doğru en yakın <c>.env</c> dosyasını bulur, ortam değişkenlerini yükler
    /// ve <paramref name="configuration"/> bölümlerine eşler.
    /// </summary>
    /// <param name="configuration">Uygulama yapılandırma yöneticisi.</param>
    public static void LoadNearest(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var envFilePath = FindNearestEnvFile(Directory.GetCurrentDirectory())
                          ?? FindNearestEnvFile(AppContext.BaseDirectory);

        if (envFilePath is not null)
        {
            foreach (var rawLine in File.ReadAllLines(envFilePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = Unquote(line[(separatorIndex + 1)..].Trim());

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }

                if (string.IsNullOrEmpty(configuration[key]))
                {
                    configuration[key] = value;
                }
            }
        }

        ApplyConfigurationMappings(configuration);
    }

    private static void ApplyConfigurationMappings(IConfiguration configuration)
    {
        foreach (var (envKey, configKey) in EnvToConfigurationMap)
        {
            var val = Environment.GetEnvironmentVariable(envKey) ?? configuration[envKey];
            if (!string.IsNullOrWhiteSpace(val))
            {
                configuration[configKey] = val;
            }
        }

        var originsRaw = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS")
                         ?? configuration["CORS_ALLOWED_ORIGINS"];
        if (!string.IsNullOrWhiteSpace(originsRaw))
        {
            var origins = originsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < origins.Length; i++)
            {
                configuration[$"Security:AllowedOrigins:{i}"] = origins[i];
            }
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
        {
            var host = Environment.GetEnvironmentVariable("DB_HOST") ?? configuration["DB_HOST"];
            var port = Environment.GetEnvironmentVariable("DB_PORT") ?? configuration["DB_PORT"] ?? "5433";
            var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? configuration["DB_NAME"];
            var user = Environment.GetEnvironmentVariable("DB_USER") ?? configuration["DB_USER"];
            var pass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? configuration["DB_PASSWORD"];

            if (!string.IsNullOrWhiteSpace(host) &&
                !string.IsNullOrWhiteSpace(dbName) &&
                !string.IsNullOrWhiteSpace(user))
            {
                configuration["ConnectionStrings:DefaultConnection"] =
                    $"Host={host};Port={port};Database={dbName};Username={user};Password={pass}";
            }
        }
    }

    private static string? FindNearestEnvFile(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }
}
