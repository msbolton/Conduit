using FluentAssertions;
using Conduit.Api;
using Xunit;

namespace Conduit.Api.Tests;

public class ComponentStateExtensionsTests
{
    [Fact]
    public void IsActive_WithRunningState_ShouldReturnTrue()
    {
        // Arrange
        var state = ComponentState.Running;

        // Act
        var result = state.IsActive();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized)]
    [InlineData(ComponentState.Registered)]
    [InlineData(ComponentState.Initializing)]
    [InlineData(ComponentState.Initialized)]
    [InlineData(ComponentState.Starting)]
    [InlineData(ComponentState.Stopping)]
    [InlineData(ComponentState.Stopped)]
    [InlineData(ComponentState.Failed)]
    [InlineData(ComponentState.Recovering)]
    [InlineData(ComponentState.Recovered)]
    [InlineData(ComponentState.Disposing)]
    [InlineData(ComponentState.Disposed)]
    public void IsActive_WithNonRunningStates_ShouldReturnFalse(ComponentState state)
    {
        // Act
        var result = state.IsActive();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ComponentState.Disposed)]
    [InlineData(ComponentState.Failed)]
    public void IsTerminal_WithTerminalStates_ShouldReturnTrue(ComponentState state)
    {
        // Act
        var result = state.IsTerminal();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized)]
    [InlineData(ComponentState.Registered)]
    [InlineData(ComponentState.Initializing)]
    [InlineData(ComponentState.Initialized)]
    [InlineData(ComponentState.Starting)]
    [InlineData(ComponentState.Running)]
    [InlineData(ComponentState.Stopping)]
    [InlineData(ComponentState.Stopped)]
    [InlineData(ComponentState.Recovering)]
    [InlineData(ComponentState.Recovered)]
    [InlineData(ComponentState.Disposing)]
    public void IsTerminal_WithNonTerminalStates_ShouldReturnFalse(ComponentState state)
    {
        // Act
        var result = state.IsTerminal();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ComponentState.Initialized)]
    [InlineData(ComponentState.Stopped)]
    [InlineData(ComponentState.Recovered)]
    public void CanStart_WithStartableStates_ShouldReturnTrue(ComponentState state)
    {
        // Act
        var result = state.CanStart();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized)]
    [InlineData(ComponentState.Registered)]
    [InlineData(ComponentState.Initializing)]
    [InlineData(ComponentState.Starting)]
    [InlineData(ComponentState.Running)]
    [InlineData(ComponentState.Stopping)]
    [InlineData(ComponentState.Failed)]
    [InlineData(ComponentState.Recovering)]
    [InlineData(ComponentState.Disposing)]
    [InlineData(ComponentState.Disposed)]
    public void CanStart_WithNonStartableStates_ShouldReturnFalse(ComponentState state)
    {
        // Act
        var result = state.CanStart();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanStop_WithRunningState_ShouldReturnTrue()
    {
        // Arrange
        var state = ComponentState.Running;

        // Act
        var result = state.CanStop();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized)]
    [InlineData(ComponentState.Registered)]
    [InlineData(ComponentState.Initializing)]
    [InlineData(ComponentState.Initialized)]
    [InlineData(ComponentState.Starting)]
    [InlineData(ComponentState.Stopping)]
    [InlineData(ComponentState.Stopped)]
    [InlineData(ComponentState.Failed)]
    [InlineData(ComponentState.Recovering)]
    [InlineData(ComponentState.Recovered)]
    [InlineData(ComponentState.Disposing)]
    [InlineData(ComponentState.Disposed)]
    public void CanStop_WithNonRunningStates_ShouldReturnFalse(ComponentState state)
    {
        // Act
        var result = state.CanStop();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized, ComponentState.Registered)]
    [InlineData(ComponentState.Uninitialized, ComponentState.Initializing)]
    [InlineData(ComponentState.Registered, ComponentState.Initializing)]
    [InlineData(ComponentState.Initializing, ComponentState.Initialized)]
    [InlineData(ComponentState.Initializing, ComponentState.Failed)]
    [InlineData(ComponentState.Initialized, ComponentState.Starting)]
    [InlineData(ComponentState.Starting, ComponentState.Running)]
    [InlineData(ComponentState.Starting, ComponentState.Failed)]
    [InlineData(ComponentState.Running, ComponentState.Stopping)]
    [InlineData(ComponentState.Stopping, ComponentState.Stopped)]
    [InlineData(ComponentState.Stopping, ComponentState.Failed)]
    [InlineData(ComponentState.Stopped, ComponentState.Starting)]
    [InlineData(ComponentState.Stopped, ComponentState.Disposing)]
    [InlineData(ComponentState.Failed, ComponentState.Recovering)]
    [InlineData(ComponentState.Recovering, ComponentState.Recovered)]
    [InlineData(ComponentState.Recovering, ComponentState.Failed)]
    [InlineData(ComponentState.Recovered, ComponentState.Starting)]
    [InlineData(ComponentState.Disposing, ComponentState.Disposed)]
    public void CanTransitionTo_WithValidTransitions_ShouldReturnTrue(ComponentState current, ComponentState target)
    {
        // Act
        var result = current.CanTransitionTo(target);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized, ComponentState.Disposing)]
    [InlineData(ComponentState.Registered, ComponentState.Disposing)]
    [InlineData(ComponentState.Initializing, ComponentState.Disposing)]
    [InlineData(ComponentState.Initialized, ComponentState.Disposing)]
    [InlineData(ComponentState.Starting, ComponentState.Disposing)]
    [InlineData(ComponentState.Running, ComponentState.Disposing)]
    [InlineData(ComponentState.Stopping, ComponentState.Disposing)]
    [InlineData(ComponentState.Stopped, ComponentState.Disposing)]
    [InlineData(ComponentState.Recovering, ComponentState.Disposing)]
    [InlineData(ComponentState.Recovered, ComponentState.Disposing)]
    public void CanTransitionTo_WithNonTerminalToDisposing_ShouldReturnTrue(ComponentState current, ComponentState target)
    {
        // Act
        var result = current.CanTransitionTo(target);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(ComponentState.Failed, ComponentState.Disposing)]
    [InlineData(ComponentState.Disposed, ComponentState.Disposing)]
    public void CanTransitionTo_WithTerminalToDisposing_ShouldReturnFalse(ComponentState current, ComponentState target)
    {
        // Act
        var result = current.CanTransitionTo(target);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ComponentState.Uninitialized, ComponentState.Running)]
    [InlineData(ComponentState.Registered, ComponentState.Running)]
    [InlineData(ComponentState.Initialized, ComponentState.Running)]
    [InlineData(ComponentState.Running, ComponentState.Starting)]
    [InlineData(ComponentState.Stopped, ComponentState.Running)]
    [InlineData(ComponentState.Failed, ComponentState.Running)]
    [InlineData(ComponentState.Disposed, ComponentState.Running)]
    [InlineData(ComponentState.Disposed, ComponentState.Starting)]
    [InlineData(ComponentState.Disposed, ComponentState.Initializing)]
    [InlineData(ComponentState.Running, ComponentState.Initialized)]
    [InlineData(ComponentState.Stopping, ComponentState.Running)]
    [InlineData(ComponentState.Recovering, ComponentState.Running)]
    public void CanTransitionTo_WithInvalidTransitions_ShouldReturnFalse(ComponentState current, ComponentState target)
    {
        // Act
        var result = current.CanTransitionTo(target);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_SelfTransition_ShouldReturnFalse()
    {
        // Arrange
        var states = Enum.GetValues<ComponentState>();

        foreach (var state in states)
        {
            // Act
            var result = state.CanTransitionTo(state);

            // Assert - Disposing can transition to itself due to the wildcard rule
            if (state == ComponentState.Disposing)
            {
                result.Should().BeTrue($"state {state} can transition to itself per wildcard rule");
            }
            else
            {
                result.Should().BeFalse($"state {state} should not transition to itself");
            }
        }
    }

    [Fact]
    public void CanTransitionTo_AllValidTransitionsTest()
    {
        // Test all explicitly valid transitions
        var validTransitions = new[]
        {
            (ComponentState.Uninitialized, ComponentState.Registered),
            (ComponentState.Uninitialized, ComponentState.Initializing),
            (ComponentState.Registered, ComponentState.Initializing),
            (ComponentState.Initializing, ComponentState.Initialized),
            (ComponentState.Initializing, ComponentState.Failed),
            (ComponentState.Initialized, ComponentState.Starting),
            (ComponentState.Starting, ComponentState.Running),
            (ComponentState.Starting, ComponentState.Failed),
            (ComponentState.Running, ComponentState.Stopping),
            (ComponentState.Stopping, ComponentState.Stopped),
            (ComponentState.Stopping, ComponentState.Failed),
            (ComponentState.Stopped, ComponentState.Starting),
            (ComponentState.Stopped, ComponentState.Disposing),
            (ComponentState.Failed, ComponentState.Recovering),
            (ComponentState.Recovering, ComponentState.Recovered),
            (ComponentState.Recovering, ComponentState.Failed),
            (ComponentState.Recovered, ComponentState.Starting),
            (ComponentState.Disposing, ComponentState.Disposed)
        };

        foreach (var (current, target) in validTransitions)
        {
            // Act
            var result = current.CanTransitionTo(target);

            // Assert
            result.Should().BeTrue($"transition from {current} to {target} should be valid");
        }
    }

    [Fact]
    public void ComponentState_AllEnumValues_ShouldBeHandledByExtensions()
    {
        // Arrange
        var allStates = Enum.GetValues<ComponentState>();

        foreach (var state in allStates)
        {
            // Act & Assert - Should not throw exceptions
            var isActive = state.IsActive();
            var isTerminal = state.IsTerminal();
            var canStart = state.CanStart();
            var canStop = state.CanStop();

            // Verify each state has expected behavior
            isActive.Should().Be(state == ComponentState.Running);
            isTerminal.Should().Be(state == ComponentState.Disposed || state == ComponentState.Failed);
            canStart.Should().Be(state == ComponentState.Initialized ||
                                 state == ComponentState.Stopped ||
                                 state == ComponentState.Recovered);
            canStop.Should().Be(state == ComponentState.Running);
        }
    }

    [Fact]
    public void CanTransitionTo_ExhaustiveInvalidTransitionTest()
    {
        // Test a comprehensive set of invalid transitions
        var invalidTransitions = new[]
        {
            // Cannot go backward in normal flow
            (ComponentState.Registered, ComponentState.Uninitialized),
            (ComponentState.Initialized, ComponentState.Registered),
            (ComponentState.Running, ComponentState.Starting),
            (ComponentState.Stopped, ComponentState.Stopping),

            // Cannot skip states
            (ComponentState.Uninitialized, ComponentState.Running),
            (ComponentState.Registered, ComponentState.Running),
            (ComponentState.Initialized, ComponentState.Running),
            (ComponentState.Stopped, ComponentState.Running),

            // Cannot transition from terminal states (except specific cases)
            (ComponentState.Disposed, ComponentState.Running),
            (ComponentState.Disposed, ComponentState.Starting),
            (ComponentState.Disposed, ComponentState.Initializing),

            // Cannot transition to uninitialized from other states
            (ComponentState.Running, ComponentState.Uninitialized),
            (ComponentState.Stopped, ComponentState.Uninitialized),
            (ComponentState.Failed, ComponentState.Uninitialized)
        };

        foreach (var (current, target) in invalidTransitions)
        {
            // Act
            var result = current.CanTransitionTo(target);

            // Assert
            result.Should().BeFalse($"transition from {current} to {target} should be invalid");
        }
    }
}