using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductHub.Domain.Common;
using ProductHub.Infrastructure.Repositories;

namespace ProductHub.Infrastructure.Tests.Repositories;

public sealed class RepositoryTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly Repository<TestAggregate> _repository;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(options);
        _repository = new Repository<TestAggregate>(_context);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistEntity()
    {
        var entity = new TestAggregate("Item A");

        await _repository.AddAsync(entity);
        await _context.SaveChangesAsync();

        var found = await _repository.GetByIdAsync(entity.Id);
        found.Should().NotBeNull();
        found!.Name.Should().Be("Item A");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        await _repository.AddAsync(new TestAggregate("A"));
        await _repository.AddAsync(new TestAggregate("B"));
        await _context.SaveChangesAsync();

        var all = await _repository.GetAllAsync();

        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_ShouldFilterCorrectly()
    {
        await _repository.AddAsync(new TestAggregate("Alpha"));
        await _repository.AddAsync(new TestAggregate("Beta"));
        await _context.SaveChangesAsync();

        var found = await _repository.FindAsync(e => e.Name == "Alpha");

        found.Should().HaveCount(1);
        found.First().Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var found = await _repository.GetByIdAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task Remove_ShouldDeleteEntity()
    {
        var entity = new TestAggregate("ToRemove");
        await _repository.AddAsync(entity);
        await _context.SaveChangesAsync();

        _repository.Remove(entity);
        await _context.SaveChangesAsync();

        var found = await _repository.GetByIdAsync(entity.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task Update_ShouldModifyEntity()
    {
        var entity = new TestAggregate("Original");
        await _repository.AddAsync(entity);
        await _context.SaveChangesAsync();

        entity.Rename("Updated");
        _repository.Update(entity);
        await _context.SaveChangesAsync();

        var found = await _repository.GetByIdAsync(entity.Id);
        found!.Name.Should().Be("Updated");
    }

    public void Dispose() => _context.Dispose();
}

internal sealed class TestAggregate(string name) : AggregateRoot
{
    public string Name { get; private set; } = name;
    public void Rename(string newName) => Name = newName;
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestAggregate> TestAggregates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<TestAggregate>().HasKey(e => e.Id);
}
