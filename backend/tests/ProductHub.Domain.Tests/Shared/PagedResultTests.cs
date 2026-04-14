using FluentAssertions;
using ProductHub.Shared.Models;

namespace ProductHub.Domain.Tests.Shared;

public sealed class PagedResultTests
{
    [Fact]
    public void Create_ShouldSetPropertiesCorrectly()
    {
        var items = Enumerable.Range(1, 5).ToList();

        var result = PagedResult<int>.Create(items, totalCount: 50, pageNumber: 2, pageSize: 5);

        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(50);
        result.PageNumber.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalPages.Should().Be(10);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Empty_ShouldReturnEmptyResult()
    {
        var result = PagedResult<int>.Empty();

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void LastPage_ShouldHaveNoNextPage()
    {
        var result = PagedResult<int>.Create([1, 2], totalCount: 7, pageNumber: 4, pageSize: 2);

        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }
}
