using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Roadmap;
using ProductHub.Infrastructure.Persistence;
using ProductHub.Infrastructure.Repositories;

namespace ProductHub.Infrastructure.Tests.Repositories;

public sealed class RoadmapDemandRepositoryTests
{
    [Fact]
    public async Task ReplaceDependenciesAsync_ShouldPersistLatestDependencyLinks()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var interceptor = new ProductHub.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor();
        await using var context = new AppDbContext(options, interceptor);
        var repository = new RoadmapDemandRepository(context);

        var projectId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var source = RoadmapDemand.Create("Source", null, projectId, 2026, 2, DemandType.Planned, DemandClassification.Evolution, [productId]);
        var dependencyA = RoadmapDemand.Create("Dependency A", null, Guid.NewGuid(), 2026, 2, DemandType.Planned, DemandClassification.Evolution, [Guid.NewGuid()]);
        var dependencyB = RoadmapDemand.Create("Dependency B", null, Guid.NewGuid(), 2026, 3, DemandType.Planned, DemandClassification.Evolution, [Guid.NewGuid()]);

        context.RoadmapDemands.AddRange(source, dependencyA, dependencyB);
        await context.SaveChangesAsync();

        await repository.ReplaceDependenciesAsync(source.Id, [dependencyA.Id, dependencyB.Id]);
        await context.SaveChangesAsync();

        await repository.ReplaceDependenciesAsync(source.Id, [dependencyB.Id]);
        await context.SaveChangesAsync();

        var links = await repository.GetDependenciesByDemandIdsAsync([source.Id]);

        links.Should().ContainSingle(link => link.DemandId == source.Id && link.DependsOnDemandId == dependencyB.Id);
        links.Should().NotContain(link => link.DemandId == source.Id && link.DependsOnDemandId == dependencyA.Id);
    }
}