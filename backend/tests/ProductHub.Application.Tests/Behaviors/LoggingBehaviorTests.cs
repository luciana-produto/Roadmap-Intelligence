using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ProductHub.Application.Common.Behaviors;

namespace ProductHub.Application.Tests.Behaviors;

public sealed class LoggingBehaviorTests
{
    private static readonly NullLogger<LoggingBehavior<TestRequest, string>> Logger = new();

    [Fact]
    public async Task Handle_ShouldCallNextAndReturnResponse()
    {
        var behavior = new LoggingBehavior<TestRequest, string>(Logger);
        Task<string> Next(CancellationToken _) => Task.FromResult("response");

        var result = await behavior.Handle(new TestRequest("test"), Next, CancellationToken.None);

        result.Should().Be("response");
    }

    [Fact]
    public async Task Handle_WhenNextThrows_ShouldRethrow()
    {
        var behavior = new LoggingBehavior<TestRequest, string>(Logger);
        Task<string> Next(CancellationToken _) => throw new InvalidOperationException("failure");

        var act = async () => await behavior.Handle(new TestRequest("test"), Next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }

    private sealed record TestRequest(string Value) : IRequest<string>;
}
