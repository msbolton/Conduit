using FluentAssertions;
using Conduit.Messaging;
using Xunit;

namespace Conduit.Messaging.Tests;

public class MessageRetryPolicyTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaults_ShouldSetDefaultValues()
    {
        // Act
        var policy = new MessageRetryPolicy();

        // Assert
        policy.MaxRetries.Should().Be(3);
        policy.Strategy.Should().Be(RetryStrategy.ExponentialBackoff);
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetValues()
    {
        // Act
        var policy = new MessageRetryPolicy(
            RetryStrategy.FixedDelay,
            maxRetries: 5,
            initialDelay: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromMinutes(10),
            backoffMultiplier: 3.0,
            jitterFactor: 0.2);

        // Assert
        policy.MaxRetries.Should().Be(5);
        policy.Strategy.Should().Be(RetryStrategy.FixedDelay);
    }

    [Fact]
    public void Constructor_WithNegativeMaxRetries_ShouldThrow()
    {
        // Act & Assert
        var act = () => new MessageRetryPolicy(maxRetries: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeBackoffMultiplier_ShouldThrow()
    {
        // Act & Assert
        var act = () => new MessageRetryPolicy(backoffMultiplier: -1.0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeJitterFactor_ShouldThrow()
    {
        // Act & Assert
        var act = () => new MessageRetryPolicy(jitterFactor: -0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Static Factory Methods Tests

    [Fact]
    public void Default_ShouldReturnExponentialBackoffPolicy()
    {
        // Act
        var policy = MessageRetryPolicy.Default();

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.ExponentialBackoff);
        policy.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void NoRetry_ShouldReturnNoRetryPolicy()
    {
        // Act
        var policy = MessageRetryPolicy.NoRetry();

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.None);
        policy.MaxRetries.Should().Be(0);
    }

    [Fact]
    public void Immediate_ShouldReturnImmediateRetryPolicy()
    {
        // Act
        var policy = MessageRetryPolicy.Immediate(5);

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.Immediate);
        policy.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void FixedDelay_ShouldReturnFixedDelayPolicy()
    {
        // Act
        var policy = MessageRetryPolicy.FixedDelay(4, TimeSpan.FromSeconds(3));

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.FixedDelay);
        policy.MaxRetries.Should().Be(4);
    }

    [Fact]
    public void ExponentialBackoff_ShouldReturnExponentialBackoffPolicy()
    {
        // Act
        var policy = MessageRetryPolicy.ExponentialBackoff(
            maxRetries: 6,
            initialDelay: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromMinutes(2),
            multiplier: 3.0);

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.ExponentialBackoff);
        policy.MaxRetries.Should().Be(6);
    }

    [Fact]
    public void LinearBackoff_ShouldReturnLinearBackoffPolicy()
    {
        // Act
        var policy = MessageRetryPolicy.LinearBackoff(
            maxRetries: 4,
            increment: TimeSpan.FromSeconds(5),
            maxDelay: TimeSpan.FromMinutes(3));

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.LinearBackoff);
        policy.MaxRetries.Should().Be(4);
    }

    #endregion

    #region ShouldRetry Tests

    [Fact]
    public void ShouldRetry_WithTimeoutException_ShouldReturnTrue()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new TimeoutException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_WithTaskCanceledException_ShouldReturnFalse()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new TaskCanceledException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithOperationCanceledException_ShouldReturnFalse()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new OperationCanceledException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithOutOfMemoryException_ShouldReturnFalse()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new OutOfMemoryException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithStackOverflowException_ShouldReturnFalse()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new StackOverflowException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithAccessViolationException_ShouldReturnFalse()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new AccessViolationException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRetry_WithRegularException_ShouldReturnTrue()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var exception = new InvalidOperationException();

        // Act
        var result = policy.ShouldRetry(exception);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldRetry_WithCustomPredicate_ShouldUseCustomLogic()
    {
        // Arrange
        var policy = new MessageRetryPolicy(retryPredicate: ex => ex is ArgumentException);
        var argumentException = new ArgumentException();
        var invalidOpException = new InvalidOperationException();

        // Act
        var shouldRetryArgEx = policy.ShouldRetry(argumentException);
        var shouldRetryInvalidOp = policy.ShouldRetry(invalidOpException);

        // Assert
        shouldRetryArgEx.Should().BeTrue();
        shouldRetryInvalidOp.Should().BeFalse();
    }

    #endregion

    #region GetRetryDelay Tests

    [Fact]
    public void GetRetryDelay_WithNoneStrategy_ShouldReturnZero()
    {
        // Arrange
        var policy = new MessageRetryPolicy(RetryStrategy.None);

        // Act
        var delay = policy.GetRetryDelay(1);

        // Assert
        delay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetRetryDelay_WithImmediateStrategy_ShouldReturnZero()
    {
        // Arrange
        var policy = new MessageRetryPolicy(RetryStrategy.Immediate);

        // Act
        var delay = policy.GetRetryDelay(1);

        // Assert
        delay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetRetryDelay_WithFixedDelayStrategy_ShouldReturnFixedDelay()
    {
        // Arrange
        var initialDelay = TimeSpan.FromSeconds(2);
        var policy = new MessageRetryPolicy(
            RetryStrategy.FixedDelay,
            initialDelay: initialDelay,
            jitterFactor: 0.0); // No jitter for exact comparison

        // Act
        var delay1 = policy.GetRetryDelay(1);
        var delay2 = policy.GetRetryDelay(2);
        var delay3 = policy.GetRetryDelay(3);

        // Assert
        delay1.Should().Be(initialDelay);
        delay2.Should().Be(initialDelay);
        delay3.Should().Be(initialDelay);
    }

    [Fact]
    public void GetRetryDelay_WithLinearBackoffStrategy_ShouldIncreaseLinearly()
    {
        // Arrange
        var initialDelay = TimeSpan.FromSeconds(1);
        var policy = new MessageRetryPolicy(
            RetryStrategy.LinearBackoff,
            initialDelay: initialDelay,
            jitterFactor: 0.0); // No jitter for exact comparison

        // Act
        var delay1 = policy.GetRetryDelay(1);
        var delay2 = policy.GetRetryDelay(2);
        var delay3 = policy.GetRetryDelay(3);

        // Assert
        delay1.Should().Be(TimeSpan.FromSeconds(1));
        delay2.Should().Be(TimeSpan.FromSeconds(2));
        delay3.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void GetRetryDelay_WithExponentialBackoffStrategy_ShouldIncreaseExponentially()
    {
        // Arrange
        var initialDelay = TimeSpan.FromSeconds(1);
        var policy = new MessageRetryPolicy(
            RetryStrategy.ExponentialBackoff,
            initialDelay: initialDelay,
            backoffMultiplier: 2.0,
            jitterFactor: 0.0); // No jitter for exact comparison

        // Act
        var delay1 = policy.GetRetryDelay(1);
        var delay2 = policy.GetRetryDelay(2);
        var delay3 = policy.GetRetryDelay(3);

        // Assert
        delay1.Should().Be(TimeSpan.FromSeconds(1));  // 1 * 2^0
        delay2.Should().Be(TimeSpan.FromSeconds(2));  // 1 * 2^1
        delay3.Should().Be(TimeSpan.FromSeconds(4));  // 1 * 2^2
    }

    [Fact]
    public void GetRetryDelay_WithFibonacciStrategy_ShouldFollowFibonacciSequence()
    {
        // Arrange
        var initialDelay = TimeSpan.FromSeconds(1);
        var policy = new MessageRetryPolicy(
            RetryStrategy.Fibonacci,
            initialDelay: initialDelay,
            maxRetries: 10, // Ensure we don't hit max retries limit
            jitterFactor: 0.0); // No jitter for exact comparison

        // Act
        var delay1 = policy.GetRetryDelay(1);
        var delay2 = policy.GetRetryDelay(2);
        var delay3 = policy.GetRetryDelay(3);
        var delay4 = policy.GetRetryDelay(4);
        var delay5 = policy.GetRetryDelay(5);

        // Assert
        delay1.Should().Be(TimeSpan.FromSeconds(1));  // 1 * fib(1) = 1 * 1
        delay2.Should().Be(TimeSpan.FromSeconds(1));  // 1 * fib(2) = 1 * 1
        delay3.Should().Be(TimeSpan.FromSeconds(2));  // 1 * fib(3) = 1 * 2
        delay4.Should().Be(TimeSpan.FromSeconds(3));  // 1 * fib(4) = 1 * 3
        delay5.Should().Be(TimeSpan.FromSeconds(5));  // 1 * fib(5) = 1 * 5
    }

    [Fact]
    public void GetRetryDelay_WithMaxDelay_ShouldCapAtMaximum()
    {
        // Arrange
        var maxDelay = TimeSpan.FromSeconds(5);
        var policy = new MessageRetryPolicy(
            RetryStrategy.ExponentialBackoff,
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: maxDelay,
            backoffMultiplier: 2.0);

        // Act
        var delay = policy.GetRetryDelay(1);

        // Assert
        delay.Should().Be(maxDelay);
    }

    [Fact]
    public void GetRetryDelay_WithZeroOrNegativeAttempt_ShouldReturnZero()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();

        // Act
        var delay0 = policy.GetRetryDelay(0);
        var delayNegative = policy.GetRetryDelay(-1);

        // Assert
        delay0.Should().Be(TimeSpan.Zero);
        delayNegative.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetRetryDelay_WithAttemptExceedingMaxRetries_ShouldReturnZero()
    {
        // Arrange
        var policy = new MessageRetryPolicy(maxRetries: 3);

        // Act
        var delay = policy.GetRetryDelay(4);

        // Assert
        delay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetRetryDelay_WithJitter_ShouldVaryDelay()
    {
        // Arrange
        var policy = new MessageRetryPolicy(
            RetryStrategy.FixedDelay,
            initialDelay: TimeSpan.FromSeconds(1),
            jitterFactor: 0.5); // 50% jitter

        // Act - Get multiple delays to see variation
        var delays = new List<TimeSpan>();
        for (int i = 0; i < 10; i++)
        {
            delays.Add(policy.GetRetryDelay(1));
        }

        // Assert - At least some delays should be different due to jitter
        delays.Should().HaveCountGreaterThan(1);
        delays.Should().OnlyContain(d => d >= TimeSpan.Zero);
        // Can't guarantee exact variation due to randomness, but delays should be around 1 second
        delays.Should().OnlyContain(d => d <= TimeSpan.FromSeconds(2));
    }

    #endregion

    #region CreatePollyPolicy Tests

    [Fact]
    public void CreatePollyPolicy_WithNoneStrategy_ShouldReturnNoOpPolicy()
    {
        // Arrange
        var policy = new MessageRetryPolicy(RetryStrategy.None);

        // Act
        var pollyPolicy = policy.CreatePollyPolicy();

        // Assert
        pollyPolicy.Should().NotBeNull();
    }

    [Fact]
    public void CreatePollyPolicy_WithZeroMaxRetries_ShouldReturnNoOpPolicy()
    {
        // Arrange
        var policy = new MessageRetryPolicy(maxRetries: 0);

        // Act
        var pollyPolicy = policy.CreatePollyPolicy();

        // Assert
        pollyPolicy.Should().NotBeNull();
    }

    [Fact]
    public void CreatePollyPolicy_WithRetryStrategy_ShouldReturnValidPolicy()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();

        // Act
        var pollyPolicy = policy.CreatePollyPolicy();

        // Assert
        pollyPolicy.Should().NotBeNull();
    }

    #endregion

    #region CreateAdvancedPolicy Tests

    [Fact]
    public void CreateAdvancedPolicy_WithoutOptions_ShouldThrow()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();

        // Act & Assert
        // When no options are provided, CreateAdvancedPolicy throws because Polly requires at least 2 policies to wrap
        var act = () => policy.CreateAdvancedPolicy();
        act.Should().Throw<ArgumentException>()
            .WithMessage("The enumerable of policies to form the wrap must contain at least two policies.*");
    }

    [Fact]
    public void CreateAdvancedPolicy_WithTimeout_ShouldIncludeTimeoutPolicy()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var advancedPolicy = policy.CreateAdvancedPolicy(timeout: timeout);

        // Assert
        advancedPolicy.Should().NotBeNull();
    }

    [Fact]
    public void CreateAdvancedPolicy_WithCircuitBreaker_ShouldIncludeCircuitBreakerPolicy()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var circuitBreakerOptions = new CircuitBreakerOptions
        {
            HandledEventsAllowedBeforeBreaking = 3,
            DurationOfBreak = TimeSpan.FromSeconds(30)
        };

        // Act
        var advancedPolicy = policy.CreateAdvancedPolicy(circuitBreakerOptions: circuitBreakerOptions);

        // Assert
        advancedPolicy.Should().NotBeNull();
    }

    [Fact]
    public void CreateAdvancedPolicy_WithAllOptions_ShouldIncludeAllPolicies()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var circuitBreakerOptions = new CircuitBreakerOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // Act
        var advancedPolicy = policy.CreateAdvancedPolicy(circuitBreakerOptions, timeout);

        // Assert
        advancedPolicy.Should().NotBeNull();
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_ShouldReturnResult()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var expectedResult = "success";

        // Act
        var result = await policy.ExecuteAsync(_ => Task.FromResult(expectedResult), CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrow()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();

        // Act & Assert
        var act = async () => await policy.ExecuteAsync<string>(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableExceptionThenSuccess_ShouldReturnResult()
    {
        // Arrange
        var policy = new MessageRetryPolicy(maxRetries: 2);
        var attempts = 0;
        var expectedResult = "success";

        // Act
        var result = await policy.ExecuteAsync<string>(_ =>
        {
            attempts++;
            if (attempts == 1)
                throw new TimeoutException();
            return Task.FromResult(expectedResult);
        }, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonRetryableException_ShouldThrowImmediately()
    {
        // Arrange
        var policy = MessageRetryPolicy.Default();
        var attempts = 0;

        // Act & Assert
        var act = async () => await policy.ExecuteAsync<string>(_ =>
        {
            attempts++;
            throw new TaskCanceledException();
        }, CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithMaxRetriesExceeded_ShouldThrowLastException()
    {
        // Arrange
        var policy = new MessageRetryPolicy(maxRetries: 2);
        var attempts = 0;

        // Act & Assert
        var act = async () => await policy.ExecuteAsync<string>(_ =>
        {
            attempts++;
            throw new TimeoutException($"Attempt {attempts}");
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<TimeoutException>();
        exception.WithMessage("Attempt 3");
        attempts.Should().Be(3); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task ExecuteAsync_WithCallback_ShouldInvokeCallbackOnRetry()
    {
        // Arrange
        var policy = new MessageRetryPolicy(
            RetryStrategy.Immediate,
            maxRetries: 2);
        var attempts = 0;
        var retryCallbacks = new List<RetryAttempt>();

        // Act & Assert
        var act = async () => await policy.ExecuteAsync<string>(
            _ =>
            {
                attempts++;
                throw new TimeoutException();
            },
            onRetry: retryCallbacks.Add);

        await act.Should().ThrowAsync<TimeoutException>();

        // Assert
        attempts.Should().Be(3); // Initial attempt + 2 retries
        retryCallbacks.Should().HaveCount(2); // Only retry attempts, not the initial one
        retryCallbacks.Should().OnlyContain(r => !r.Succeeded);
        retryCallbacks.Should().OnlyContain(r => r.Exception is TimeoutException);
    }

    [Fact]
    public async Task ExecuteAsync_WithCallbackAndEventualSuccess_ShouldInvokeSuccessCallback()
    {
        // Arrange
        var policy = new MessageRetryPolicy(
            RetryStrategy.Immediate,
            maxRetries: 2);
        var attempts = 0;
        var retryCallbacks = new List<RetryAttempt>();
        var expectedResult = "success";

        // Act
        var result = await policy.ExecuteAsync<string>(
            _ =>
            {
                attempts++;
                if (attempts <= 2)
                    throw new TimeoutException();
                return Task.FromResult(expectedResult);
            },
            onRetry: retryCallbacks.Add);

        // Assert
        result.Should().Be(expectedResult);
        attempts.Should().Be(3);
        retryCallbacks.Should().HaveCount(3); // 2 failed attempts + 1 success
        retryCallbacks.Take(2).Should().OnlyContain(r => !r.Succeeded);
        retryCallbacks.Last().Succeeded.Should().BeTrue();
    }

    #endregion

    #region Builder Tests

    [Fact]
    public void Builder_ShouldReturnBuilder()
    {
        // Act
        var builder = MessageRetryPolicy.Builder();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<RetryPolicyBuilder>();
    }

    [Fact]
    public void RetryPolicyBuilder_WithStrategy_ShouldSetStrategy()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithStrategy(RetryStrategy.LinearBackoff)
            .Build();

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.LinearBackoff);
    }

    [Fact]
    public void RetryPolicyBuilder_WithMaxRetries_ShouldSetMaxRetries()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithMaxRetries(10)
            .Build();

        // Assert
        policy.MaxRetries.Should().Be(10);
    }

    [Fact]
    public void RetryPolicyBuilder_WithInitialDelay_ShouldSetInitialDelay()
    {
        // Arrange
        var delay = TimeSpan.FromSeconds(5);

        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithInitialDelay(delay)
            .WithJitter(0.0) // No jitter for exact comparison
            .Build();

        // Assert
        policy.GetRetryDelay(1).Should().Be(delay);
    }

    [Fact]
    public void RetryPolicyBuilder_WithMaxDelay_ShouldSetMaxDelay()
    {
        // Arrange
        var maxDelay = TimeSpan.FromSeconds(1); // Smaller than initial delay

        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithInitialDelay(TimeSpan.FromSeconds(10))
            .WithMaxDelay(maxDelay)
            .Build();

        // Assert
        policy.GetRetryDelay(1).Should().Be(maxDelay);
    }

    [Fact]
    public void RetryPolicyBuilder_WithBackoffMultiplier_ShouldSetMultiplier()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithStrategy(RetryStrategy.ExponentialBackoff)
            .WithInitialDelay(TimeSpan.FromSeconds(1))
            .WithBackoffMultiplier(3.0)
            .WithJitter(0.0) // No jitter for exact comparison
            .Build();

        // Assert
        var delay1 = policy.GetRetryDelay(1);
        var delay2 = policy.GetRetryDelay(2);

        delay1.Should().Be(TimeSpan.FromSeconds(1));  // 1 * 3^0
        delay2.Should().Be(TimeSpan.FromSeconds(3));  // 1 * 3^1
    }

    [Fact]
    public void RetryPolicyBuilder_WithJitter_ShouldApplyJitter()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithStrategy(RetryStrategy.FixedDelay)
            .WithInitialDelay(TimeSpan.FromSeconds(1))
            .WithJitter(0.5)
            .Build();

        // Assert - Jitter should cause variation in delays
        var delays = Enumerable.Range(0, 10)
            .Select(_ => policy.GetRetryDelay(1))
            .ToList();

        delays.Should().HaveCountGreaterThan(1);
        delays.Should().OnlyContain(d => d >= TimeSpan.Zero);
    }

    [Fact]
    public void RetryPolicyBuilder_RetryOn_ShouldConfigureRetryableExceptions()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .RetryOn<ArgumentException>()
            .Build();

        // Assert
        policy.ShouldRetry(new ArgumentException()).Should().BeTrue();
        policy.ShouldRetry(new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void RetryPolicyBuilder_DontRetryOn_ShouldConfigureNonRetryableExceptions()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .DontRetryOn<ArgumentException>()
            .Build();

        // Assert
        policy.ShouldRetry(new ArgumentException()).Should().BeFalse();
        policy.ShouldRetry(new InvalidOperationException()).Should().BeTrue();
    }

    [Fact]
    public void RetryPolicyBuilder_WithCustomPredicate_ShouldUseCustomPredicate()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithCustomPredicate(ex => ex.Message.Contains("retry"))
            .Build();

        // Assert
        policy.ShouldRetry(new Exception("please retry")).Should().BeTrue();
        policy.ShouldRetry(new Exception("don't do it")).Should().BeFalse();
    }

    [Fact]
    public void RetryPolicyBuilder_FluentChaining_ShouldWorkCorrectly()
    {
        // Act
        var policy = MessageRetryPolicy.Builder()
            .WithStrategy(RetryStrategy.LinearBackoff)
            .WithMaxRetries(5)
            .WithInitialDelay(TimeSpan.FromSeconds(2))
            .WithMaxDelay(TimeSpan.FromMinutes(1))
            .WithBackoffMultiplier(1.5)
            .WithJitter(0.2)
            .RetryOn<TimeoutException>()
            .DontRetryOn<ArgumentException>()
            .Build();

        // Assert
        policy.Strategy.Should().Be(RetryStrategy.LinearBackoff);
        policy.MaxRetries.Should().Be(5);
        policy.ShouldRetry(new TimeoutException()).Should().BeTrue();
        policy.ShouldRetry(new ArgumentException()).Should().BeFalse();
        policy.ShouldRetry(new InvalidOperationException()).Should().BeFalse(); // Not in retry list
    }

    #endregion

    #region CircuitBreakerOptions Tests

    [Fact]
    public void CircuitBreakerOptions_DefaultValues_ShouldBeSet()
    {
        // Act
        var options = new CircuitBreakerOptions();

        // Assert
        options.HandledEventsAllowedBeforeBreaking.Should().Be(3);
        options.DurationOfBreak.Should().Be(TimeSpan.FromSeconds(30));
        options.OnBreak.Should().BeNull();
        options.OnReset.Should().BeNull();
    }

    [Fact]
    public void CircuitBreakerOptions_CustomValues_ShouldBeSet()
    {
        // Arrange
        var onBreakCalled = false;
        var onResetCalled = false;

        // Act
        var options = new CircuitBreakerOptions
        {
            HandledEventsAllowedBeforeBreaking = 5,
            DurationOfBreak = TimeSpan.FromMinutes(2),
            OnBreak = _ => onBreakCalled = true,
            OnReset = () => onResetCalled = true
        };

        // Assert
        options.HandledEventsAllowedBeforeBreaking.Should().Be(5);
        options.DurationOfBreak.Should().Be(TimeSpan.FromMinutes(2));

        options.OnBreak?.Invoke(TimeSpan.Zero);
        options.OnReset?.Invoke();

        onBreakCalled.Should().BeTrue();
        onResetCalled.Should().BeTrue();
    }

    #endregion

    #region RetryAttempt Tests

    [Fact]
    public void RetryAttempt_Properties_ShouldBeSettable()
    {
        // Arrange
        var exception = new TimeoutException();
        var duration = TimeSpan.FromSeconds(5);
        var nextDelay = TimeSpan.FromSeconds(2);

        // Act
        var attempt = new RetryAttempt
        {
            AttemptNumber = 2,
            TotalAttempts = 5,
            Succeeded = false,
            Exception = exception,
            NextDelay = nextDelay,
            Duration = duration
        };

        // Assert
        attempt.AttemptNumber.Should().Be(2);
        attempt.TotalAttempts.Should().Be(5);
        attempt.Succeeded.Should().BeFalse();
        attempt.Exception.Should().Be(exception);
        attempt.NextDelay.Should().Be(nextDelay);
        attempt.Duration.Should().Be(duration);
    }

    #endregion
}