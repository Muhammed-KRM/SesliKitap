namespace SesliKitap.API.Middleware;

/// <summary>
/// İşaretlenen Controller veya aksiyonun <see cref="ApiKeyAuthMiddleware"/> tarafından
/// <c>X-API-Key</c> zorunluluğundan muaf tutulmasını sağlar (örn. <c>GET /api/health</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowAnonymousApiKeyAttribute : Attribute;
