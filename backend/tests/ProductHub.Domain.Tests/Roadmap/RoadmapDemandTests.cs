using FluentAssertions;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Domain.Tests.Roadmap;

public sealed class RoadmapDemandTests
{
    [Fact]
    public void Create_WhenCustomersContainDuplicatesAndWhitespace_ShouldNormalizeValues()
    {
        var productId = Guid.NewGuid();

        var demand = RoadmapDemand.Create(
            title: "Demanda",
            description: "Descricao",
            projectId: Guid.NewGuid(),
            quarterYear: 2026,
            quarterNumber: 2,
            type: DemandType.Planned,
            classification: DemandClassification.Evolution,
            productIds: [productId],
            customers: [" Cliente A ", "cliente a", "", "Cliente B"]);

        demand.Customers.Should().Equal("Cliente A", "Cliente B");
    }

    [Fact]
    public void Update_WhenCustomersIsNull_ShouldClearCustomerList()
    {
        var productId = Guid.NewGuid();
        var demand = RoadmapDemand.Create(
            title: "Demanda",
            description: null,
            projectId: Guid.NewGuid(),
            quarterYear: 2026,
            quarterNumber: 2,
            type: DemandType.Planned,
            classification: DemandClassification.Evolution,
            productIds: [productId],
            customers: ["Cliente A"]);

        demand.Update(
            title: "Demanda atualizada",
            description: null,
            quarterYear: 2026,
            quarterNumber: 3,
            status: DemandStatus.Backlog,
            type: DemandType.Planned,
            classification: DemandClassification.Evolution,
            customers: null);

        demand.Customers.Should().BeEmpty();
    }
}