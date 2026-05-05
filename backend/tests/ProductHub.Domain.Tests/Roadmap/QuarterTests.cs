using FluentAssertions;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Domain.Tests.Roadmap;

public sealed class QuarterTests
{
    [Fact]
    public void Create_WhenUsingPrioritizedBacklogSentinel_ShouldReturnSpecialBacklogLabel()
    {
        var quarter = Quarter.Create(Quarter.PrioritizedBacklogYear, Quarter.PrioritizedBacklogNumber);

        quarter.IsPrioritizedBacklog.Should().BeTrue();
        quarter.IsSpecialBacklog.Should().BeTrue();
        quarter.Label.Should().Be("Backlog - Prioritário");
    }

    [Fact]
    public void Parse_WhenUsingPrioritizedBacklogLabel_ShouldReturnPrioritizedBacklogQuarter()
    {
        var quarter = Quarter.Parse("Backlog - Prioritário");

        quarter.Year.Should().Be(Quarter.PrioritizedBacklogYear);
        quarter.Number.Should().Be(Quarter.PrioritizedBacklogNumber);
        quarter.IsPrioritizedBacklog.Should().BeTrue();
    }
}