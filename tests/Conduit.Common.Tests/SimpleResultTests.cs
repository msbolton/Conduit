using FluentAssertions;
using Conduit.Common;

namespace Conduit.Common.Tests;

/// <summary>
/// Simple working tests for Result functionality
/// </summary>
public class SimpleResultTests
{
    [Fact]
    public void Success_WithValue_ShouldCreateSuccessfulResult()
    {
        // Arrange
        const string value = "test";

        // Act
        var result = Result<string>.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void Failure_WithError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = new Error("TEST_ERROR", "Test error message");

        // Act
        var result = Result<string>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ImplicitOperator_WithValue_ShouldCreateSuccessfulResult()
    {
        // Arrange
        const string value = "test";

        // Act
        Result<string> result = value;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void ImplicitOperator_WithError_ShouldCreateFailedResult()
    {
        // Arrange
        var error = new Error("TEST_ERROR", "Test error message");

        // Act
        Result<string> result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Match_WithSuccessfulResult_ShouldExecuteSuccessFunction()
    {
        // Arrange
        const string value = "test";
        var result = Result<string>.Success(value);

        // Act
        var output = result.Match(
            v => v.ToUpper(),
            e => "ERROR"
        );

        // Assert
        output.Should().Be("TEST");
    }

    [Fact]
    public void Match_WithFailedResult_ShouldExecuteFailureFunction()
    {
        // Arrange
        var error = new Error("TEST_ERROR", "Test error message");
        var result = Result<string>.Failure(error);

        // Act
        var output = result.Match(
            v => v.ToUpper(),
            e => "ERROR"
        );

        // Assert
        output.Should().Be("ERROR");
    }

    [Fact]
    public void ToString_WithSuccessfulResult_ShouldReturnSuccessString()
    {
        // Arrange
        const string value = "test";
        var result = Result<string>.Success(value);

        // Act
        var stringResult = result.ToString();

        // Assert
        stringResult.Should().Contain("Success");
        stringResult.Should().Contain("test");
    }

    [Fact]
    public void ToString_WithFailedResult_ShouldReturnFailureString()
    {
        // Arrange
        var error = new Error("TEST_ERROR", "Test error message");
        var result = Result<string>.Failure(error);

        // Act
        var stringResult = result.ToString();

        // Assert
        stringResult.Should().Contain("Failure");
        stringResult.Should().Contain("TEST_ERROR");
    }
}

/// <summary>
/// Simple tests for Error functionality
/// </summary>
public class SimpleErrorTests
{
    [Fact]
    public void Constructor_WithCodeAndMessage_ShouldSetProperties()
    {
        // Arrange
        const string code = "TEST_ERROR";
        const string message = "Test error message";

        // Act
        var error = new Error(code, message);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Equals_WithSameCodeAndMessage_ShouldReturnTrue()
    {
        // Arrange
        var error1 = new Error("TEST_ERROR", "Test error message");
        var error2 = new Error("TEST_ERROR", "Test error message");

        // Act & Assert
        error1.Should().Be(error2);
    }

    [Fact]
    public void Equals_WithDifferentCode_ShouldReturnFalse()
    {
        // Arrange
        var error1 = new Error("TEST_ERROR_1", "Test error message");
        var error2 = new Error("TEST_ERROR_2", "Test error message");

        // Act & Assert
        error1.Should().NotBe(error2);
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var error = new Error("TEST_ERROR", "Test error message");

        // Act
        var result = error.ToString();

        // Assert
        result.Should().Contain("TEST_ERROR");
        result.Should().Contain("Test error message");
    }
}