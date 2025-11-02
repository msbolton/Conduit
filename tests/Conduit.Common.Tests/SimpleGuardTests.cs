using FluentAssertions;
using Conduit.Common;

namespace Conduit.Common.Tests;

/// <summary>
/// Simple working tests for Guard functionality
/// </summary>
public class SimpleGuardTests
{
    [Fact]
    public void NotNull_WithValidValue_ShouldReturnValue()
    {
        // Arrange
        const string validValue = "test";

        // Act
        var result = Guard.NotNull(validValue);

        // Assert
        result.Should().Be(validValue);
    }

    [Fact]
    public void NotNull_WithNullValue_ShouldThrowArgumentNullException()
    {
        // Arrange
        string? nullValue = null;

        // Act & Assert
        var action = () => Guard.NotNull(nullValue);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NotNullOrEmpty_WithValidString_ShouldReturnValue()
    {
        // Arrange
        const string validValue = "test";

        // Act
        var result = Guard.NotNullOrEmpty(validValue);

        // Assert
        result.Should().Be(validValue);
    }

    [Fact]
    public void NotNullOrEmpty_WithEmptyString_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyValue = string.Empty;

        // Act & Assert
        var action = () => Guard.NotNullOrEmpty(emptyValue);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InRange_WithValueInRange_ShouldReturnValue()
    {
        // Arrange
        const int value = 15;
        const int min = 10;
        const int max = 20;

        // Act
        var result = Guard.InRange(value, min, max);

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void InRange_WithValueOutOfRange_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        const int value = 25;
        const int min = 10;
        const int max = 20;

        // Act & Assert
        var action = () => Guard.InRange(value, min, max);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NotEmpty_WithValidGuid_ShouldReturnGuid()
    {
        // Arrange
        var validGuid = Guid.NewGuid();

        // Act
        var result = Guard.NotEmpty(validGuid);

        // Assert
        result.Should().Be(validGuid);
    }

    [Fact]
    public void NotEmpty_WithEmptyGuid_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyGuid = Guid.Empty;

        // Act & Assert
        var action = () => Guard.NotEmpty(emptyGuid);
        action.Should().Throw<ArgumentException>();
    }
}