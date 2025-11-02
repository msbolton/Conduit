using FluentAssertions;
using Conduit.Resilience;

namespace Conduit.Resilience.Tests;

public class RetryStrategyTests
{
    [Fact]
    public void RetryStrategy_Enumeration_ShouldHaveCorrectValues()
    {
        // Assert - verify all expected retry strategies exist
        RetryStrategy.Linear.Should().Be(RetryStrategy.Linear);
        RetryStrategy.Exponential.Should().Be(RetryStrategy.Exponential);
        RetryStrategy.Fixed.Should().Be(RetryStrategy.Fixed);
        RetryStrategy.Immediate.Should().Be(RetryStrategy.Immediate);
    }

    [Fact]
    public void RetryStrategy_Enumeration_ShouldHaveCorrectCount()
    {
        // Act
        var values = Enum.GetValues<RetryStrategy>();

        // Assert
        values.Should().HaveCount(4);
        values.Should().Contain(RetryStrategy.Linear);
        values.Should().Contain(RetryStrategy.Exponential);
        values.Should().Contain(RetryStrategy.Fixed);
        values.Should().Contain(RetryStrategy.Immediate);
    }

    [Theory]
    [InlineData(RetryStrategy.Linear)]
    [InlineData(RetryStrategy.Exponential)]
    [InlineData(RetryStrategy.Fixed)]
    [InlineData(RetryStrategy.Immediate)]
    public void RetryStrategy_AllValues_ShouldBeValid(RetryStrategy strategy)
    {
        // Act & Assert - verify each strategy value is defined
        Enum.IsDefined(typeof(RetryStrategy), strategy).Should().BeTrue();
    }

    [Fact]
    public void RetryStrategy_ToString_ShouldReturnCorrectNames()
    {
        // Assert
        RetryStrategy.Linear.ToString().Should().Be("Linear");
        RetryStrategy.Exponential.ToString().Should().Be("Exponential");
        RetryStrategy.Fixed.ToString().Should().Be("Fixed");
        RetryStrategy.Immediate.ToString().Should().Be("Immediate");
    }

    [Fact]
    public void RetryStrategy_Parse_ShouldWorkCorrectly()
    {
        // Act & Assert
        Enum.Parse<RetryStrategy>("Linear").Should().Be(RetryStrategy.Linear);
        Enum.Parse<RetryStrategy>("Exponential").Should().Be(RetryStrategy.Exponential);
        Enum.Parse<RetryStrategy>("Fixed").Should().Be(RetryStrategy.Fixed);
        Enum.Parse<RetryStrategy>("Immediate").Should().Be(RetryStrategy.Immediate);
    }
}