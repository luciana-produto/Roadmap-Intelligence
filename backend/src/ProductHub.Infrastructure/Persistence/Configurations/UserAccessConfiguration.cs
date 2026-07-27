using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductHub.Domain.Access;

namespace ProductHub.Infrastructure.Persistence.Configurations;

public sealed class UserAccessConfiguration : IEntityTypeConfiguration<UserAccess>
{
    public void Configure(EntityTypeBuilder<UserAccess> builder)
    {
        builder.ToTable("UserAccess");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.CanEditRoadmap).IsRequired();
        builder.Property(x => x.CanManageRegistrations).IsRequired();
    }
}
