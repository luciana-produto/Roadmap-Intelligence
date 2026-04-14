using FluentAssertions;
using ProductHub.Domain.Errors;

namespace ProductHub.Domain.Tests.Errors;

public sealed class DomainErrorTests
{
    [Fact]
    public void None_ShouldHaveEmptyCodeAndDescription()
    {
        DomainError.None.Code.Should().BeEmpty();
        DomainError.None.Description.Should().BeEmpty();
    }

    [Fact]
    public void NotFound_ShouldFormatCodeAndDescriptionCorrectly()
    {
        var error = DomainError.NotFound("Product", 42);

        error.Code.Should().Be("Product.NotFound");
        error.Description.Should().Contain("Product").And.Contain("42");
    }

    [Fact]
    public void Validation_ShouldFormatCodeAndDescriptionCorrectly()
    {
        var error = DomainError.Validation("Name", "Name is required.");

        error.Code.Should().Be("Name.Validation");
        error.Description.Should().Be("Name is required.");
    }

    [Fact]
    public void Conflict_ShouldFormatCodeCorrectly()
    {
        var error = DomainError.Conflict("Roadmap");

        error.Code.Should().Be("Roadmap.Conflict");
        error.Description.Should().Contain("Roadmap");
    }
}
