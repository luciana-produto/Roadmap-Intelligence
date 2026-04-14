using FluentAssertions;
using ProductHub.Application.Roadmap.Queries.GetCapacity;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Application.Tests.Common;

public sealed class RoadmapCapacitySummaryTests
{
    [Fact]
    public void MapCapacity_WhenAdditionalDemandsExist_ShouldSeparateCommittedAndAdditionalHours()
    {
        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var capacity = RoadmapCapacity.Create(projectId, 2026, 2, 100);

        var plannedDemand = RoadmapDemand.Create(
            title: "Planejada",
            description: null,
            projectId: projectId,
            quarterYear: 2026,
            quarterNumber: 2,
            type: DemandType.Planned,
            classification: DemandClassification.Evolution,
            productIds: [productId],
            hours: 60);

        var additionalDemand = RoadmapDemand.Create(
            title: "Adicional",
            description: null,
            projectId: projectId,
            quarterYear: 2026,
            quarterNumber: 2,
            type: DemandType.Additional,
            classification: DemandClassification.Evolution,
            productIds: [productId],
            hours: 25);

        var summary = GetRoadmapCapacityQueryHandler.MapCapacity(
            capacity,
            projectId,
            2026,
            2,
            [plannedDemand, additionalDemand]);

        summary.CapacityHours.Should().Be(100);
        summary.CommittedHours.Should().Be(60);
        summary.AdditionalHours.Should().Be(25);
        summary.TotalDemandHours.Should().Be(85);
        summary.RemainingHours.Should().Be(40);
        summary.OverCapacityHours.Should().Be(0);
    }
}