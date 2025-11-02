using FluentAssertions;
using Conduit.Components;

namespace Conduit.Components.Tests;

public class ComponentScopeTests
{
    [Fact]
    public void ComponentScope_Enumeration_ShouldHaveCorrectValues()
    {
        // Assert - verify all expected scope values exist
        ComponentScope.Singleton.Should().Be(ComponentScope.Singleton);
        ComponentScope.Transient.Should().Be(ComponentScope.Transient);
        ComponentScope.Scoped.Should().Be(ComponentScope.Scoped);
    }

    [Fact]
    public void ComponentScope_Enumeration_ShouldHaveCorrectCount()
    {
        // Act
        var values = Enum.GetValues<ComponentScope>();

        // Assert
        values.Should().HaveCount(3);
        values.Should().Contain(ComponentScope.Singleton);
        values.Should().Contain(ComponentScope.Transient);
        values.Should().Contain(ComponentScope.Scoped);
    }

    [Theory]
    [InlineData(ComponentScope.Singleton)]
    [InlineData(ComponentScope.Transient)]
    [InlineData(ComponentScope.Scoped)]
    public void ComponentScope_AllValues_ShouldBeValid(ComponentScope scope)
    {
        // Act & Assert
        Enum.IsDefined(typeof(ComponentScope), scope).Should().BeTrue();
    }

    [Fact]
    public void ComponentScope_ToString_ShouldReturnCorrectNames()
    {
        // Assert
        ComponentScope.Singleton.ToString().Should().Be("Singleton");
        ComponentScope.Transient.ToString().Should().Be("Transient");
        ComponentScope.Scoped.ToString().Should().Be("Scoped");
    }

    [Fact]
    public void ComponentScope_Parse_ShouldWorkCorrectly()
    {
        // Act & Assert
        Enum.Parse<ComponentScope>("Singleton").Should().Be(ComponentScope.Singleton);
        Enum.Parse<ComponentScope>("Transient").Should().Be(ComponentScope.Transient);
        Enum.Parse<ComponentScope>("Scoped").Should().Be(ComponentScope.Scoped);
    }

    [Fact]
    public void ComponentScope_Values_ShouldHaveExpectedIntegerValues()
    {
        // Assert - verify enum values if they have specific integer assignments
        ((int)ComponentScope.Singleton).Should().BeGreaterThanOrEqualTo(0);
        ((int)ComponentScope.Transient).Should().BeGreaterThanOrEqualTo(0);
        ((int)ComponentScope.Scoped).Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData("singleton", ComponentScope.Singleton)]
    [InlineData("TRANSIENT", ComponentScope.Transient)]
    [InlineData("Scoped", ComponentScope.Scoped)]
    public void ComponentScope_ParseIgnoreCase_ShouldWorkCorrectly(string input, ComponentScope expected)
    {
        // Act
        var result = Enum.Parse<ComponentScope>(input, ignoreCase: true);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ComponentScope_TryParse_WithValidInput_ShouldSucceed()
    {
        // Act
        var success = Enum.TryParse<ComponentScope>("Singleton", out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().Be(ComponentScope.Singleton);
    }

    [Fact]
    public void ComponentScope_TryParse_WithInvalidInput_ShouldFail()
    {
        // Act
        var success = Enum.TryParse<ComponentScope>("Invalid", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().Be(default(ComponentScope));
    }
}