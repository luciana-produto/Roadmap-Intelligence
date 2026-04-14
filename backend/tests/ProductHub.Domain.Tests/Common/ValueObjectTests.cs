using FluentAssertions;
using ProductHub.Domain.Common;

namespace ProductHub.Domain.Tests.Common;

public sealed class ValueObjectTests
{
    [Fact]
    public void Equals_WithSameComponents_ShouldReturnTrue()
    {
        var a = new MoneyValueObject(100m, "BRL");
        var b = new MoneyValueObject(100m, "BRL");

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentComponents_ShouldReturnFalse()
    {
        var a = new MoneyValueObject(100m, "BRL");
        var b = new MoneyValueObject(200m, "BRL");

        a.Equals(b).Should().BeFalse();
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        var a = new MoneyValueObject(100m, "BRL");

        a.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_WithSameComponents_ShouldBeEqual()
    {
        var a = new MoneyValueObject(100m, "BRL");
        var b = new MoneyValueObject(100m, "BRL");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    private sealed class MoneyValueObject(decimal amount, string currency) : ValueObject
    {
        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }
}
