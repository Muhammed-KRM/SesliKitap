using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SesliKitap.API.Middleware;
using SesliKitap.Business.Options;
using SesliKitap.Business.Services;
using SesliKitap.UnitTests.TestUtilities;

namespace SesliKitap.UnitTests.Middleware;

public sealed class ApiKeyAuthMiddlewareTests
{
    private const string ValidApiKey = "0123456789abcdef0123456789abcdef0123456789abcdef";

    private static (ApiKeyAuthMiddleware Middleware, MediaTokenManager TokenManager) CreateMiddleware(
        RequestDelegate next,
        string environmentName = "Development")
    {
        var securityOptions = Options.Create(new SecurityOptions
        {
            ApiKey = ValidApiKey,
            MediaTokenSecret = "media_secret_0123456789abcdef0123456789abcdef",
            MediaTokenLifetimeHours = 6
        });

        var clock = new FakeClock();
        var tokenManager = new MediaTokenManager(securityOptions, clock);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var middleware = new ApiKeyAuthMiddleware(
            next,
            securityOptions,
            tokenManager,
            memoryCache,
            envMock.Object,
            NullLogger<ApiKeyAuthMiddleware>.Instance);

        return (middleware, tokenManager);
    }

    [Fact]
    public async Task InvokeAsync_WithValidApiKeyHeader_ShouldAllowRequest()
    {
        var nextCalled = false;
        var (sut, _) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/auth/verify";
        context.Request.Headers[ApiKeyAuthMiddleware.ApiKeyHeaderName] = ValidApiKey;

        await sut.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[ApiKeyAuthMiddleware.AuthenticatedItemKey].Should().Be(true);
    }

    [Fact]
    public async Task InvokeAsync_WithoutApiKey_OnProtectedEndpoint_ShouldReturn401()
    {
        var nextCalled = false;
        var (sut, _) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = "GET";
        context.Request.Path = "/api/auth/verify";

        await sut.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WithAllowAnonymousAttribute_WithoutHeader_ShouldAllowUnauthenticated()
    {
        var nextCalled = false;
        var (sut, _) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/health";
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousApiKeyAttribute()),
            "Health"));

        await sut.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[ApiKeyAuthMiddleware.AuthenticatedItemKey].Should().Be(false);
    }

    [Fact]
    public async Task InvokeAsync_OnMediaPath_WithValidMediaToken_ShouldAllowRequest()
    {
        var nextCalled = false;
        var (sut, tokenManager) = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var (token, _) = tokenManager.Create();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = $"/api/chapters/{Guid.NewGuid()}/audio";
        context.Request.QueryString = new QueryString($"?format=opus&t={token}");

        await sut.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenBruteForceThresholdExceeded_ShouldReturn429()
    {
        var (sut, _) = CreateMiddleware(_ => Task.CompletedTask);

        for (var i = 0; i < 21; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.55");
            ctx.Response.Body = new MemoryStream();
            ctx.Request.Method = "GET";
            ctx.Request.Path = "/api/auth/verify";
            ctx.Request.Headers[ApiKeyAuthMiddleware.ApiKeyHeaderName] = "wrong_key";

            await sut.InvokeAsync(ctx);
        }

        var lockedContext = new DefaultHttpContext();
        lockedContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.55");
        lockedContext.Response.Body = new MemoryStream();
        lockedContext.Request.Method = "GET";
        lockedContext.Request.Path = "/api/auth/verify";
        lockedContext.Request.Headers[ApiKeyAuthMiddleware.ApiKeyHeaderName] = ValidApiKey;

        await sut.InvokeAsync(lockedContext);

        lockedContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }
}
