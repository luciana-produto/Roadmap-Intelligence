using FluentAssertions;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Domain.Tests.Roadmap;

public sealed class RoadmapCapacityTests
{
    [Fact]
    public void Create_WhenObservationHasWhitespace_ShouldNormalizeObservation()
    {
        var capacity = RoadmapCapacity.Create(
            projectId: Guid.NewGuid(),
            quarterYear: 2026,
            quarterNumber: 2,
            capacityHours: 320,
            observation: "  Squad reduzida por feriado  ");

        capacity.Observation.Should().Be("Squad reduzida por feriado");
    }

    [Fact]
    public void Update_WhenObservationIsBlank_ShouldClearObservation()
    {
        var capacity = RoadmapCapacity.Create(
            projectId: Guid.NewGuid(),
            quarterYear: 2026,
            quarterNumber: 2,
            capacityHours: 320,
            observation: "Observacao inicial");

        capacity.Update(280, "   ");

        capacity.CapacityHours.Should().Be(280);
        capacity.Observation.Should().BeNull();
    }
}