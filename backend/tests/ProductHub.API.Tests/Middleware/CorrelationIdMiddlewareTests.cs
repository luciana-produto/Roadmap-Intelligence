using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ProductHub.API.Middleware;
using ProductHub.Shared.Constants;

namespace ProductHub.API.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenHeaderPresent_ShouldUseIncomingCorrelationId()
    {
        const string expectedCid = "incoming-cid-abc";
        var context = new DefaultHttpContext();
        context.Request.Headers[AppConstants.Http.CorrelationIdHeader] = expectedCid;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items[AppConstants.Http.CorrelationIdHeader].Should().Be(expectedCid);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_ShouldGenerateNewCorrelationId()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var cid = context.Items[AppConstants.Http.CorrelationIdHeader]?.ToString();
        cid.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderEmpty_ShouldGenerateNewCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[AppConstants.Http.CorrelationIdHeader] = string.Empty;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var cid = context.Items[AppConstants.Http.CorrelationIdHeader]?.ToString();
        cid.Should().NotBeNullOrWhiteSpace();
        cid.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddCorrelationIdToResponseHeaders()
    {
        const string expectedCid = "response-cid-xyz";
        var context = new DefaultHttpContext();
        context.Request.Headers[AppConstants.Http.CorrelationIdHeader] = expectedCid;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Headers[AppConstants.Http.CorrelationIdHeader]
            .ToString().Should().Be(expectedCid);
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNext()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdInContextShouldMatchResponseHeader()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var itemCid = context.Items[AppConstants.Http.CorrelationIdHeader]?.ToString();
        var headerCid = context.Response.Headers[AppConstants.Http.CorrelationIdHeader].ToString();
        itemCid.Should().Be(headerCid);
    }
}
