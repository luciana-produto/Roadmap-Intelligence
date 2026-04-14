using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapProductConfiguration : IEntityTypeConfiguration<RoadmapProduct>
{
    public void Configure(EntityTypeBuilder<RoadmapProduct> builder)
    {
        builder.ToTable("RoadmapProducts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);
    }
}
