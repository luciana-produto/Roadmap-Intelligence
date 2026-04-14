using FluentAssertions;
using MediatR;
using ProductHub.Application.Common.Behaviors;
using ProductHub.Application.Tests.Common;

namespace ProductHub.Application.Tests.Behaviors;

public sealed class SlowRequestBehaviorTests
{
    [Fact]
    public async Task Handle_FastRequest_ShouldReturnResponse()
    {
        var logger = new TestLogger<SlowRequestBehavior<TestRequest, string>>();
        var behavior = new SlowRequestBehavior<TestRequest, string>(logger);
        Task<string> Next(CancellationToken _) => Task.FromResult("fast-response");

        var result = await behavior.Handle(new TestRequest(), Next, CancellationToken.None);

        result.Should().Be("fast-response");
        logger.HasWarning("SLOW REQUEST").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SlowRequest_ShouldLogWarning()
    {
        var logger = new TestLogger<SlowRequestBehavior<TestRequest, string>>();
        var behavior = new SlowRequestBehavior<TestRequest, string>(logger);
        async Task<string> Next(CancellationToken _)
        {
            await Task.Delay(TimeSpan.FromSeconds(16));
            return "slow-response";
        }

        var result = await behavior.Handle(new TestRequest(), Next, CancellationToken.None);

        result.Should().Be("slow-response");
        logger.HasWarning("SLOW REQUEST").Should().BeTrue();
    }

    private sealed record TestRequest : IRequest<string>;
}
