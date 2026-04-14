using FluentAssertions;
using ProductHub.Domain.Common;

namespace ProductHub.Domain.Tests.Common;

public sealed class BaseEntityTests
{
    [Fact]
    public void NewEntity_ShouldHaveNonEmptyId()
    {
        var entity = new TestEntity();

        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddEventToCollection()
    {
        var entity = new TestEntity();
        var domainEvent = new TestDomainEvent();

        entity.RaiseEvent(domainEvent);

        entity.DomainEvents.Should().HaveCount(1);
        entity.DomainEvents[0].Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var entity = new TestEntity();
        entity.RaiseEvent(new TestDomainEvent());
        entity.RaiseEvent(new TestDomainEvent());

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    private sealed class TestEntity : BaseEntity
    {
        public void RaiseEvent(DomainEvent @event) => RaiseDomainEvent(@event);
    }

    private sealed record TestDomainEvent : DomainEvent;
}
