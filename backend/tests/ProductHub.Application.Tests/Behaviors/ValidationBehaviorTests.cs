using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ProductHub.Application.Common.Behaviors;
using AppValidationException = ProductHub.Application.Common.Exceptions.ValidationException;

namespace ProductHub.Application.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private static readonly NullLogger<ValidationBehavior<TestRequest, string>> Logger = new();

    [Fact]
    public async Task Handle_WithNoValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([], Logger);
        var nextCalled = false;
        Task<string> Next(CancellationToken _) { nextCalled = true; return Task.FromResult("success"); }

        var result = await behavior.Handle(new TestRequest(), Next, CancellationToken.None);

        result.Should().Be("success");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidValidators_ShouldCallNext()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new AlwaysValidValidator()], Logger);
        Task<string> Next(CancellationToken _) => Task.FromResult("success");

        var result = await behavior.Handle(new TestRequest(), Next, CancellationToken.None);

        result.Should().Be("success");
    }

    [Fact]
    public async Task Handle_WithFailingValidators_ShouldThrowValidationException()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new AlwaysInvalidValidator()], Logger);
        var nextCalled = false;
        Task<string> Next(CancellationToken _) { nextCalled = true; return Task.FromResult(""); }

        var act = async () => await behavior.Handle(new TestRequest(), Next, CancellationToken.None);

        await act.Should().ThrowAsync<AppValidationException>();
        nextCalled.Should().BeFalse();
    }

    private sealed record TestRequest : IRequest<string>;

    private sealed class AlwaysValidValidator : AbstractValidator<TestRequest>;

    private sealed class AlwaysInvalidValidator : AbstractValidator<TestRequest>
    {
        public AlwaysInvalidValidator() =>
            RuleFor(x => x).Must(_ => false).WithMessage("Always invalid.");
    }
}
