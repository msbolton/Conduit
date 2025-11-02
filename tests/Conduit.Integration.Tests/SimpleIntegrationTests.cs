using FluentAssertions;
using Conduit.Common;
using Conduit.Messaging;

namespace Conduit.Integration.Tests;

/// <summary>
/// Simple integration tests that verify modules work together
/// </summary>
public class SimpleIntegrationTests
{
    [Fact]
    public void CommonAndMessaging_Integration_ShouldWork()
    {
        // Arrange - Use Common Guard with Messaging components
        var action = () =>
        {
            const int maxConcurrent = 100;
            const int rateLimit = 1000;
            const int maxQueueSize = 10000;

            // Validate using Common Guard
            Guard.AgainstNegativeOrZero(maxConcurrent, nameof(maxConcurrent));
            Guard.AgainstNegativeOrZero(rateLimit, nameof(rateLimit));
            Guard.AgainstNegativeOrZero(maxQueueSize, nameof(maxQueueSize));

            // Create Messaging component
            return new FlowController(maxConcurrent, rateLimit, maxQueueSize);
        };

        // Act & Assert
        action.Should().NotThrow();
        var flowController = action();
        flowController.IsHealthy.Should().BeTrue();
        flowController.QueueDepth.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void CommonResult_WithMessagingComponents_ShouldWork()
    {
        // Arrange - Use Common Result pattern with Messaging components
        var flowController = new FlowController();
        var dlq = new DeadLetterQueue();
        var correlator = new MessageCorrelator();
        var registry = new HandlerRegistry();

        // Act - Wrap components in Result pattern
        var flowResult = Result<bool>.Success(flowController.IsHealthy);
        var dlqResult = Result<int>.Success(dlq.Count);
        var correlatorResult = Result<MessageCorrelator>.Success(correlator);
        var registryResult = Result<HandlerRegistry>.Success(registry);

        // Assert
        flowResult.IsSuccess.Should().BeTrue();
        flowResult.Value.Should().BeTrue();

        dlqResult.IsSuccess.Should().BeTrue();
        dlqResult.Value.Should().BeGreaterThanOrEqualTo(0);

        correlatorResult.IsSuccess.Should().BeTrue();
        correlatorResult.Value.Should().NotBeNull();

        registryResult.IsSuccess.Should().BeTrue();
        registryResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void MessagingComponents_WithGuardValidation_ShouldIntegrate()
    {
        // Arrange - Create Messaging components with Guard validation
        var components = new List<object>();

        // Act - Create components using Guard validation
        var action = () =>
        {
            var flowController = new FlowController();
            Guard.NotNull(flowController, nameof(flowController));
            components.Add(flowController);

            var dlq = new DeadLetterQueue();
            Guard.NotNull(dlq, nameof(dlq));
            components.Add(dlq);

            var correlator = new MessageCorrelator();
            Guard.NotNull(correlator, nameof(correlator));
            components.Add(correlator);

            var registry = new HandlerRegistry();
            Guard.NotNull(registry, nameof(registry));
            components.Add(registry);
        };

        // Assert
        action.Should().NotThrow();
        components.Should().HaveCount(4);
        components.Should().OnlyContain(c => c != null);
    }

    [Fact]
    public void ErrorHandling_CrossModule_ShouldUseCommonPatterns()
    {
        // Arrange - Test error handling across modules
        var errorScenarios = new List<Result<string>>();

        // Act - Test various error scenarios
        try
        {
            // Test Guard validation failure
            Guard.AgainstNegativeOrZero(-1, "invalidParam");
            errorScenarios.Add(Result<string>.Success("Should not reach here"));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            errorScenarios.Add(Result<string>.Failure(new Error("GUARD_ERROR", ex.Message)));
        }

        try
        {
            // Test empty string validation
            Guard.NotNullOrEmpty(string.Empty, "emptyParam");
            errorScenarios.Add(Result<string>.Success("Should not reach here"));
        }
        catch (ArgumentException ex)
        {
            errorScenarios.Add(Result<string>.Failure(new Error("VALIDATION_ERROR", ex.Message)));
        }

        // Assert - Error scenarios should be properly captured
        errorScenarios.Should().HaveCount(2);
        errorScenarios.Should().OnlyContain(r => r.IsFailure);
        errorScenarios[0].Error.Code.Should().Be("GUARD_ERROR");
        errorScenarios[1].Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public void CommonUtilities_WithMessagingFlow_ShouldValidateInputs()
    {
        // Arrange
        const int validMaxConcurrent = 50;
        const int validRateLimit = 500;
        const int validMaxQueueSize = 1000;

        // Act - Validate inputs using Common utilities
        var validationAction = () =>
        {
            Guard.AgainstNegativeOrZero(validMaxConcurrent, nameof(validMaxConcurrent));
            Guard.AgainstNegativeOrZero(validRateLimit, nameof(validRateLimit));
            Guard.AgainstNegativeOrZero(validMaxQueueSize, nameof(validMaxQueueSize));

            return new FlowController(validMaxConcurrent, validRateLimit, validMaxQueueSize);
        };

        // Assert
        validationAction.Should().NotThrow();
        var flowController = validationAction();

        // Validate results with Guard
        Guard.InRange(flowController.QueueDepth, 0, validMaxQueueSize, "QueueDepth");
        flowController.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void ResultPattern_WithMessagingOperations_ShouldProvideErrorHandling()
    {
        // Arrange
        var operations = new List<Result<string>>();

        // Act - Perform operations using Result pattern
        try
        {
            var flowController = new FlowController();
            Guard.NotNull(flowController, nameof(flowController));
            operations.Add(Result<string>.Success("FlowController created successfully"));
        }
        catch (Exception ex)
        {
            operations.Add(Result<string>.Failure(new Error("FLOW_ERROR", ex.Message)));
        }

        try
        {
            var dlq = new DeadLetterQueue(1000);
            Guard.NotNull(dlq, nameof(dlq));
            operations.Add(Result<string>.Success("DeadLetterQueue created successfully"));
        }
        catch (Exception ex)
        {
            operations.Add(Result<string>.Failure(new Error("DLQ_ERROR", ex.Message)));
        }

        // Assert
        operations.Should().HaveCount(2);
        operations.Should().OnlyContain(r => r.IsSuccess);
        operations[0].Value.Should().Contain("FlowController");
        operations[1].Value.Should().Contain("DeadLetterQueue");
    }
}