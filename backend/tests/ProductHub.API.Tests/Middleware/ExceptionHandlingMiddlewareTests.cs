using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ProductHub.API.Middleware;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Shared.Constants;
using ValidationException = ProductHub.Application.Common.Exceptions.ValidationException;

namespace ProductHub.API.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    private static readonly NullLogger<ExceptionHandlingMiddleware> Logger = new();

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_ShouldCallNext()
    {
        var context = CreateContext();
        var nextCalled = false;
        var middleware = new ExceptionHandlingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, Logger);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationException_ShouldReturn422()
    {
        var context = CreateContext();
        var failures = new[] { new ValidationFailure("Name", "Campo obrigatório") };
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ValidationException(failures),
            Logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        context.Response.ContentType.Should().Be("application/problem+json");

        var body = await ReadResponseBodyAsync(context);
        body.GetProperty("status").GetInt32().Should().Be(422);
        body.GetProperty("title").GetString().Should().Be("Erro de Validação");
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationException_ShouldIncludeErrors()
    {
        var context = CreateContext();
        var failures = new[]
        {
            new ValidationFailure("Name", "Campo obrigatório"),
            new ValidationFailure("Email", "E-mail inválido")
        };
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ValidationException(failures),
            Logger);

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        body.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.EnumerateObject().Should().HaveCount(2);
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundException_ShouldReturn404()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new NotFoundException("Produto", Guid.NewGuid()),
            Logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        var body = await ReadResponseBodyAsync(context);
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("title").GetString().Should().Be("Recurso Não Encontrado");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnauthorizedAccessException_ShouldReturn401()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new UnauthorizedAccessException(),
            Logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);

        var body = await ReadResponseBodyAsync(context);
        body.GetProperty("status").GetInt32().Should().Be(401);
        body.GetProperty("title").GetString().Should().Be("Acesso Negado");
    }

    [Fact]
    public async Task InvokeAsync_WhenGenericException_ShouldReturn500()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("erro inesperado"),
            Logger);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);

        var body = await ReadResponseBodyAsync(context);
        body.GetProperty("status").GetInt32().Should().Be(500);
        body.GetProperty("title").GetString().Should().Be("Erro Interno");
    }

    [Fact]
    public async Task InvokeAsync_WhenException_ShouldIncludeCorrelationIdFromContext()
    {
        var context = CreateContext();
        const string expectedCid = "abc123";
        context.Items[AppConstants.Http.CorrelationIdHeader] = expectedCid;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(),
            Logger);

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        body.GetProperty("correlationId").GetString().Should().Be(expectedCid);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionWithoutCorrelationId_ShouldGenerateFallbackCid()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(),
            Logger);

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
