using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Common;
using ProductHub.Domain.Interfaces;
using ProductHub.Infrastructure.Persistence.Interceptors;

namespace ProductHub.Infrastructure.Tests.Interceptors;

public sealed class AuditSaveChangesInterceptorTests : IDisposable
{
    private readonly AuditDbContext _context;

    public AuditSaveChangesInterceptorTests()
    {
        var interceptor = new AuditSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        _context = new AuditDbContext(options);
    }

    [Fact]
    public async Task SaveChanges_WhenEntityAdded_ShouldSetCreatedAt()
    {
        var entity = new AuditableAggregate("Novo");
        var before = DateTime.UtcNow;

        _context.AuditableAggregates.Add(entity);
        await _context.SaveChangesAsync();

        entity.CreatedAt.Should().BeOnOrAfter(before);
        entity.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task SaveChanges_WhenEntityAdded_ShouldSetUpdatedAt()
    {
        var entity = new AuditableAggregate("Novo");
        var before = DateTime.UtcNow;

        _context.AuditableAggregates.Add(entity);
        await _context.SaveChangesAsync();

        entity.UpdatedAt.Should().NotBeNull();
        entity.UpdatedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SaveChanges_WhenEntityModified_ShouldUpdateUpdatedAt()
    {
        var entity = new AuditableAggregate("Original");
        _context.AuditableAggregates.Add(entity);
        await _context.SaveChangesAsync();

        entity.Rename("Alterado");
        _context.AuditableAggregates.Update(entity);
        var before = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        entity.UpdatedAt.Should().NotBeNull();
        entity.UpdatedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task SaveChanges_WhenEntityModified_ShouldNotChangeCreatedAt()
    {
        var entity = new AuditableAggregate("Original");
        _context.AuditableAggregates.Add(entity);
        await _context.SaveChangesAsync();

        var createdAtAfterInsert = entity.CreatedAt;

        entity.Rename("Alterado");
        _context.AuditableAggregates.Update(entity);
        await _context.SaveChangesAsync();

        entity.CreatedAt.Should().Be(createdAtAfterInsert);
    }

    [Fact]
    public void SavingChanges_Sync_WhenEntityAdded_ShouldSetCreatedAt()
    {
        var entity = new AuditableAggregate("SyncTest");
        var before = DateTime.UtcNow;

        _context.AuditableAggregates.Add(entity);
        _context.SaveChanges();

        entity.CreatedAt.Should().BeOnOrAfter(before);
    }

    public void Dispose() => _context.Dispose();
}

internal sealed class AuditableAggregate(string name) : AggregateRoot, IAuditableEntity
{
    public string Name { get; private set; } = name;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public void Rename(string newName) => Name = newName;
}

internal sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditableAggregate> AuditableAggregates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<AuditableAggregate>().HasKey(e => e.Id);
}
