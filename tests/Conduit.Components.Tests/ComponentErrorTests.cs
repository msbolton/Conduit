using FluentAssertions;
using Conduit.Components;

namespace Conduit.Components.Tests;

public class ComponentErrorTests
{
    [Fact]
    public void ComponentError_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var componentId = "test-component";
        var code = "TEST_ERROR";
        var message = "Test error message";
        var severity = ComponentErrorSeverity.Error;
        var exception = new InvalidOperationException("Test exception");
        var context = new { key = "value" };

        // Act
        var error = new ComponentError(componentId, code, message, severity, exception, context);

        // Assert
        error.ComponentId.Should().Be(componentId);
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Severity.Should().Be(severity);
        error.Exception.Should().Be(exception);
        error.Context.Should().Be(context);
        error.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ComponentError_Warning_ShouldCreateWarningError()
    {
        // Arrange
        var componentId = "warning-component";
        var code = "WARN_001";
        var message = "Warning message";
        var context = new { level = "warning" };

        // Act
        var error = ComponentError.Warning(componentId, code, message, context);

        // Assert
        error.ComponentId.Should().Be(componentId);
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Severity.Should().Be(ComponentErrorSeverity.Warning);
        error.Exception.Should().BeNull();
        error.Context.Should().Be(context);
    }

    [Fact]
    public void ComponentError_Error_ShouldCreateErrorWithException()
    {
        // Arrange
        var componentId = "error-component";
        var code = "ERR_001";
        var message = "Error message";
        var exception = new ArgumentException("Invalid argument");
        var context = new { operation = "test" };

        // Act
        var error = ComponentError.Error(componentId, code, message, exception, context);

        // Assert
        error.ComponentId.Should().Be(componentId);
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Severity.Should().Be(ComponentErrorSeverity.Error);
        error.Exception.Should().Be(exception);
        error.Context.Should().Be(context);
    }

    [Fact]
    public void ComponentError_Critical_ShouldCreateCriticalError()
    {
        // Arrange
        var componentId = "critical-component";
        var code = "CRIT_001";
        var message = "Critical system failure";
        var exception = new SystemException("System failure");

        // Act
        var error = ComponentError.Critical(componentId, code, message, exception);

        // Assert
        error.ComponentId.Should().Be(componentId);
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Severity.Should().Be(ComponentErrorSeverity.Critical);
        error.Exception.Should().Be(exception);
    }

    [Fact]
    public void ComponentError_FromException_ShouldCreateErrorFromException()
    {
        // Arrange
        var componentId = "exception-component";
        var exception = new NotImplementedException("Feature not implemented");
        var context = new { feature = "advanced" };

        // Act
        var error = ComponentError.FromException(componentId, exception, context: context);

        // Assert
        error.ComponentId.Should().Be(componentId);
        error.Code.Should().Be("NotImplementedException");
        error.Message.Should().Be("Feature not implemented");
        error.Severity.Should().Be(ComponentErrorSeverity.Error);
        error.Exception.Should().Be(exception);
        error.Context.Should().Be(context);
    }

    [Fact]
    public void ComponentError_FromException_WithCustomCode_ShouldUseCustomCode()
    {
        // Arrange
        var componentId = "custom-component";
        var exception = new Exception("Generic error");
        var customCode = "CUSTOM_001";

        // Act
        var error = ComponentError.FromException(componentId, exception, customCode);

        // Assert
        error.ComponentId.Should().Be(componentId);
        error.Code.Should().Be(customCode);
        error.Message.Should().Be("Generic error");
        error.Exception.Should().Be(exception);
    }

    [Theory]
    [InlineData(ComponentErrorSeverity.Information)]
    [InlineData(ComponentErrorSeverity.Warning)]
    [InlineData(ComponentErrorSeverity.Error)]
    [InlineData(ComponentErrorSeverity.Critical)]
    public void ComponentErrorSeverity_AllValues_ShouldBeValid(ComponentErrorSeverity severity)
    {
        // Act & Assert
        Enum.IsDefined(typeof(ComponentErrorSeverity), severity).Should().BeTrue();
    }

    [Fact]
    public void ComponentErrorSeverity_Values_ShouldBeInOrder()
    {
        // Assert
        ((int)ComponentErrorSeverity.Information).Should().Be(0);
        ((int)ComponentErrorSeverity.Warning).Should().Be(1);
        ((int)ComponentErrorSeverity.Error).Should().Be(2);
        ((int)ComponentErrorSeverity.Critical).Should().Be(3);
    }

    [Fact]
    public void ComponentError_WithNullContext_ShouldHandleGracefully()
    {
        // Act
        var error = ComponentError.Warning("test", "code", "message", null);

        // Assert
        error.Context.Should().BeNull();
    }

    [Fact]
    public void ComponentError_Timestamp_ShouldBeRecentUtc()
    {
        // Act
        var error = ComponentError.Error("test", "code", "message");

        // Assert
        error.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        error.Timestamp.Offset.Should().Be(TimeSpan.Zero); // Should be UTC
    }
}