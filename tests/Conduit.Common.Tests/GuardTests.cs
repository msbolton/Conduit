using FluentAssertions;
using Conduit.Common;

namespace Conduit.Common.Tests;

public class GuardTests
{
    [Fact]
    public void NotNull_WithValidObject_ShouldReturnObject()
    {
        // Arrange
        var testObject = new object();

        // Act
        var result = Guard.NotNull(testObject);

        // Assert
        result.Should().Be(testObject);
    }

    [Fact]
    public void NotNull_WithNullObject_ShouldThrowArgumentNullException()
    {
        // Arrange
        object? nullObject = null;

        // Act & Assert
        var act = () => Guard.NotNull(nullObject);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NotNull_WithCustomParameterName_ShouldIncludeParameterNameInException()
    {
        // Arrange
        object? nullObject = null;
        var parameterName = "customParam";

        // Act & Assert
        var act = () => Guard.NotNull(nullObject, parameterName);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void NotNullOrEmpty_WithValidString_ShouldReturnString()
    {
        // Arrange
        var testString = "valid string";

        // Act
        var result = Guard.NotNullOrEmpty(testString);

        // Assert
        result.Should().Be(testString);
    }

    [Fact]
    public void NotNullOrEmpty_WithNullString_ShouldThrowArgumentNullException()
    {
        // Arrange
        string? nullString = null;

        // Act & Assert
        var act = () => Guard.NotNullOrEmpty(nullString);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NotNullOrEmpty_WithEmptyString_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act & Assert
        var act = () => Guard.NotNullOrEmpty(emptyString);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be empty*");
    }

    [Fact]
    public void NotNullOrEmpty_WithCustomParameterName_ShouldIncludeParameterNameInException()
    {
        // Arrange
        var emptyString = string.Empty;
        var parameterName = "customParam";

        // Act & Assert
        var act = () => Guard.NotNullOrEmpty(emptyString, parameterName);
        act.Should().Throw<ArgumentException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithValidString_ShouldReturnString()
    {
        // Arrange
        var testString = "valid string";

        // Act
        var result = Guard.NotNullOrWhiteSpace(testString);

        // Assert
        result.Should().Be(testString);
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithNullString_ShouldThrowArgumentNullException()
    {
        // Arrange
        string? nullString = null;

        // Act & Assert
        var act = () => Guard.NotNullOrWhiteSpace(nullString);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithEmptyString_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act & Assert
        var act = () => Guard.NotNullOrWhiteSpace(emptyString);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be empty or whitespace*");
    }

    [Fact]
    public void NotNullOrWhiteSpace_WithWhitespaceString_ShouldThrowArgumentException()
    {
        // Arrange
        var whitespaceString = "   ";

        // Act & Assert
        var act = () => Guard.NotNullOrWhiteSpace(whitespaceString);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be empty or whitespace*");
    }

    [Fact]
    public void AgainstNull_WithValidObject_ShouldNotThrow()
    {
        // Arrange
        var testObject = new object();

        // Act & Assert
        var act = () => Guard.AgainstNull(testObject);
        act.Should().NotThrow();
    }

    [Fact]
    public void AgainstNull_WithNullObject_ShouldThrowArgumentNullException()
    {
        // Arrange
        object? nullObject = null;

        // Act & Assert
        var act = () => Guard.AgainstNull(nullObject);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AgainstNull_WithCustomParameterName_ShouldIncludeParameterNameInException()
    {
        // Arrange
        object? nullObject = null;
        var parameterName = "customParam";

        // Act & Assert
        var act = () => Guard.AgainstNull(nullObject, parameterName);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName(parameterName);
    }

    [Fact]
    public void AgainstNullOrEmpty_WithValidString_ShouldNotThrow()
    {
        // Arrange
        var testString = "valid string";

        // Act & Assert
        var act = () => Guard.AgainstNullOrEmpty(testString);
        act.Should().NotThrow();
    }

    [Fact]
    public void AgainstNullOrEmpty_WithNullString_ShouldThrowArgumentNullException()
    {
        // Arrange
        string? nullString = null;

        // Act & Assert
        var act = () => Guard.AgainstNullOrEmpty(nullString);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AgainstNullOrEmpty_WithEmptyString_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyString = string.Empty;

        // Act & Assert
        var act = () => Guard.AgainstNullOrEmpty(emptyString);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NotNegative_WithPositiveValue_ShouldReturnValue()
    {
        // Arrange
        var positiveValue = 10;

        // Act
        var result = Guard.NotNegative(positiveValue);

        // Assert
        result.Should().Be(positiveValue);
    }

    [Fact]
    public void NotNegative_WithZeroValue_ShouldReturnValue()
    {
        // Arrange
        var zeroValue = 0;

        // Act
        var result = Guard.NotNegative(zeroValue);

        // Assert
        result.Should().Be(zeroValue);
    }

    [Fact]
    public void NotNegative_WithNegativeValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var negativeValue = -1;

        // Act & Assert
        var act = () => Guard.NotNegative(negativeValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NotZero_WithPositiveValue_ShouldReturnValue()
    {
        // Arrange
        var positiveValue = 10;

        // Act
        var result = Guard.NotZero(positiveValue);

        // Assert
        result.Should().Be(positiveValue);
    }

    [Fact]
    public void NotZero_WithNegativeValue_ShouldReturnValue()
    {
        // Arrange
        var negativeValue = -1;

        // Act
        var result = Guard.NotZero(negativeValue);

        // Assert
        result.Should().Be(negativeValue);
    }

    [Fact]
    public void NotZero_WithZeroValue_ShouldThrowArgumentException()
    {
        // Arrange
        var zeroValue = 0;

        // Act & Assert
        var act = () => Guard.NotZero(zeroValue);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be zero*");
    }

    [Fact]
    public void AgainstNegative_WithPositiveValue_ShouldNotThrow()
    {
        // Arrange
        var positiveValue = 10;

        // Act & Assert
        var act = () => Guard.AgainstNegative(positiveValue);
        act.Should().NotThrow();
    }

    [Fact]
    public void AgainstNegative_WithZeroValue_ShouldNotThrow()
    {
        // Arrange
        var zeroValue = 0;

        // Act & Assert
        var act = () => Guard.AgainstNegative(zeroValue);
        act.Should().NotThrow();
    }

    [Fact]
    public void AgainstNegative_WithNegativeValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var negativeValue = -1;

        // Act & Assert
        var act = () => Guard.AgainstNegative(negativeValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AgainstNegativeOrZero_WithPositiveValue_ShouldNotThrow()
    {
        // Arrange
        var positiveValue = 10;

        // Act & Assert
        var act = () => Guard.AgainstNegativeOrZero(positiveValue);
        act.Should().NotThrow();
    }

    [Fact]
    public void AgainstNegativeOrZero_WithZeroValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var zeroValue = 0;

        // Act & Assert
        var act = () => Guard.AgainstNegativeOrZero(zeroValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AgainstNegativeOrZero_WithNegativeValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var negativeValue = -1;

        // Act & Assert
        var act = () => Guard.AgainstNegativeOrZero(negativeValue);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InRange_WithValueInRange_ShouldNotThrow()
    {
        // Arrange
        var value = 5;
        var min = 1;
        var max = 10;

        // Act & Assert
        var act = () => Guard.InRange(value, min, max);
        act.Should().NotThrow();
    }

    [Fact]
    public void InRange_WithValueAtMinimum_ShouldNotThrow()
    {
        // Arrange
        var value = 1;
        var min = 1;
        var max = 10;

        // Act & Assert
        var act = () => Guard.InRange(value, min, max);
        act.Should().NotThrow();
    }

    [Fact]
    public void InRange_WithValueAtMaximum_ShouldNotThrow()
    {
        // Arrange
        var value = 10;
        var min = 1;
        var max = 10;

        // Act & Assert
        var act = () => Guard.InRange(value, min, max);
        act.Should().NotThrow();
    }

    [Fact]
    public void InRange_WithValueBelowMinimum_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var value = 0;
        var min = 1;
        var max = 10;

        // Act & Assert
        var act = () => Guard.InRange(value, min, max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InRange_WithValueAboveMaximum_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var value = 11;
        var min = 1;
        var max = 10;

        // Act & Assert
        var act = () => Guard.InRange(value, min, max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NotNullOrEmpty_WithCollection_WithValidCollection_ShouldReturnCollection()
    {
        // Arrange
        var collection = new List<string> { "item1", "item2" };

        // Act
        var result = Guard.NotNullOrEmpty(collection);

        // Assert
        result.Should().BeSameAs(collection);
    }

    [Fact]
    public void NotNullOrEmpty_WithCollection_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        List<string>? nullCollection = null;

        // Act & Assert
        var act = () => Guard.NotNullOrEmpty(nullCollection);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NotNullOrEmpty_WithCollection_WithEmptyCollection_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyCollection = new List<string>();

        // Act & Assert
        var act = () => Guard.NotNullOrEmpty(emptyCollection);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Collection cannot be empty*");
    }

    [Fact]
    public void Requires_WithTrueCondition_ShouldNotThrow()
    {
        // Arrange
        bool condition = true;
        string message = "Condition failed";

        // Act & Assert
        var act = () => Guard.Requires(condition, message);
        act.Should().NotThrow();
    }

    [Fact]
    public void Requires_WithFalseCondition_ShouldThrowArgumentException()
    {
        // Arrange
        bool condition = false;
        string message = "Condition failed";

        // Act & Assert
        var act = () => Guard.Requires(condition, message);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Condition failed*");
    }

    [Fact]
    public void Matches_WithMatchingPattern_ShouldReturnString()
    {
        // Arrange
        var email = "test@example.com";
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        // Act
        var result = Guard.Matches(email, emailPattern);

        // Assert
        result.Should().Be(email);
    }

    [Fact]
    public void Matches_WithNonMatchingPattern_ShouldThrowArgumentException()
    {
        // Arrange
        var invalidEmail = "not-an-email";
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

        // Act & Assert
        var act = () => Guard.Matches(invalidEmail, emailPattern);
        act.Should().Throw<ArgumentException>();
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
        var act = () => Guard.NotEmpty(emptyGuid);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*GUID cannot be empty*");
    }

    [Fact]
    public void NotDefault_WithNonDefaultValue_ShouldReturnValue()
    {
        // Arrange
        var nonDefaultValue = 42;

        // Act
        var result = Guard.NotDefault(nonDefaultValue);

        // Assert
        result.Should().Be(nonDefaultValue);
    }

    [Fact]
    public void NotDefault_WithDefaultValue_ShouldThrowArgumentException()
    {
        // Arrange
        var defaultValue = 0;

        // Act & Assert
        var act = () => Guard.NotDefault(defaultValue);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Value cannot be the default value*");
    }

    [Fact]
    public void NoNullElements_WithValidCollection_ShouldReturnCollection()
    {
        // Arrange
        var collection = new List<string> { "item1", "item2", "item3" };

        // Act
        var result = Guard.NoNullElements(collection);

        // Assert
        result.Should().BeEquivalentTo(collection);
    }

    [Fact]
    public void NoNullElements_WithNullElement_ShouldThrowArgumentException()
    {
        // Arrange
        var collectionWithNull = new List<string?> { "item1", null, "item3" };

        // Act & Assert
        var act = () => Guard.NoNullElements(collectionWithNull);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Collection cannot contain null elements*");
    }
}