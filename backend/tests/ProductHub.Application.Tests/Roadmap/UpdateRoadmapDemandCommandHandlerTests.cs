using FluentAssertions;
using NSubstitute;
using ProductHub.Application.Common.Exceptions;
using ProductHub.Application.Roadmap.Commands.UpdateDemand;
using ProductHub.Domain.Interfaces;
using ProductHub.Domain.Roadmap;
using ProductHub.Domain.Roadmap.Interfaces;

namespace ProductHub.Application.Tests.Roadmap;

public sealed class UpdateRoadmapDemandCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenProjectIdChanges_ShouldThrowValidationException()
    {
        var demandRepository = Substitute.For<IRoadmapDemandRepository>();
        var projectRepository = Substitute.For<IRoadmapProjectRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var originalProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var demand = RoadmapDemand.Create(
            title: "Demanda",
            description: null,
            projectId: originalProjectId,
            quarterYear: 2026,
            quarterNumber: 2,
            type: DemandType.Planned,
            classification: DemandClassification.Evolution,
            productIds: [productId]);

        demandRepository.GetByIdAsync(demand.Id, Arg.Any<CancellationToken>()).Returns(demand);

        var handler = new UpdateRoadmapDemandCommandHandler(demandRepository, projectRepository, unitOfWork);

        var command = new UpdateRoadmapDemandCommand(
            Id: demand.Id,
            Title: "Demanda",
            Description: null,
            ProjectId: otherProjectId,
            QuarterYear: 2026,
            QuarterNumber: 2,
            Status: DemandStatus.Backlog.ToString(),
            Type: DemandType.Planned.ToString(),
            Classification: DemandClassification.Evolution.ToString(),
            ProductIds: [productId]);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(command.ProjectId));
    }
}