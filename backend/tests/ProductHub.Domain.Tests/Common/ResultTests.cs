using FluentAssertions;
using ProductHub.Domain.Common;
using ProductHub.Domain.Errors;

namespace ProductHub.Domain.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(DomainError.None);
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        var error = DomainError.NotFound("Product", 1);

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void SuccessOfT_ShouldReturnValue()
    {
        var value = "test-value";

        var result = Result.Success(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void FailureOfT_AccessingValue_ShouldThrow()
    {
        var result = Result.Failure<string>(DomainError.NotFound("Product", 1));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Success_WithError_ShouldThrow()
    {
        var act = () => Result.Failure(DomainError.None);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failure_WithNoError_ShouldThrow()
    {
        var act = () =>
        {
            var _ = new SuccessResultWithErrorExposedForTesting(true, DomainError.NotFound("X", 1));
        };

        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class SuccessResultWithErrorExposedForTesting(bool isSuccess, DomainError error)
        : Result(isSuccess, error);
}
