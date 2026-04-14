using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Roadmap;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class RoadmapProjectConfiguration : IEntityTypeConfiguration<RoadmapProject>
{
    public void Configure(EntityTypeBuilder<RoadmapProject> builder)
    {
        builder.ToTable("RoadmapProjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Slug)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Navigation(x => x.Products)
            .HasField("_products")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Products)
            .WithOne()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
