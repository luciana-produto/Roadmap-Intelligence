using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapDemandProductConfiguration : IEntityTypeConfiguration<RoadmapDemandProduct>
{
    public void Configure(EntityTypeBuilder<RoadmapDemandProduct> builder)
    {
        builder.ToTable("RoadmapDemandProducts");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DemandId, x.ProductId }).IsUnique();
    }
}
