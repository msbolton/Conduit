using FluentAssertions;
using Conduit.Api;
using Conduit.Messaging;

namespace Conduit.Messaging.Tests;

public class HandlerRegistryBasicTests
{
    private readonly HandlerRegistry _registry;

    public HandlerRegistryBasicTests()
    {
        _registry = new HandlerRegistry();
    }

    [Fact]
    public void HandlerRegistry_Constructor_ShouldInitializeSuccessfully()
    {
        // Act
        var registry = new HandlerRegistry();

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void RegisterCommandHandler_WithValidCommandAndHandler_ShouldSucceed()
    {
        // Arrange
        var handler = new TestCommandHandler();

        // Act & Assert
        var act = () => _registry.RegisterCommandHandler(typeof(TestCommand), handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterCommandHandler_WithNullCommandType_ShouldThrow()
    {
        // Arrange
        var handler = new TestCommandHandler();

        // Act & Assert
        var act = () => _registry.RegisterCommandHandler(null!, handler);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("commandType");
    }

    [Fact]
    public void RegisterCommandHandler_WithNullHandler_ShouldThrow()
    {
        // Act & Assert
        var act = () => _registry.RegisterCommandHandler(typeof(TestCommand), null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handler");
    }

    [Fact]
    public void RegisterCommandHandler_WithDuplicateCommandType_ShouldThrow()
    {
        // Arrange
        var handler1 = new TestCommandHandler();
        var handler2 = new TestCommandHandler();
        _registry.RegisterCommandHandler(typeof(TestCommand), handler1);

        // Act & Assert
        var act = () => _registry.RegisterCommandHandler(typeof(TestCommand), handler2);
        act.Should().Throw<HandlerAlreadyRegisteredException>()
            .WithMessage("*A handler for command type TestCommand is already registered*");
    }

    [Fact]
    public void GetCommandHandler_WithRegisteredHandler_ShouldReturnHandler()
    {
        // Arrange
        var handler = new TestCommandHandler();
        _registry.RegisterCommandHandler(typeof(TestCommand), handler);

        // Act
        var result = _registry.GetCommandHandler(typeof(TestCommand));

        // Assert
        result.Should().Be(handler);
    }

    [Fact]
    public void GetCommandHandler_WithUnregisteredHandler_ShouldReturnNull()
    {
        // Act
        var result = _registry.GetCommandHandler(typeof(TestCommand));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RegisterQueryHandler_WithValidQueryAndHandler_ShouldSucceed()
    {
        // Arrange
        var handler = new TestQueryHandler();

        // Act & Assert
        var act = () => _registry.RegisterQueryHandler(typeof(TestQuery), handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterQueryHandler_WithNullQueryType_ShouldThrow()
    {
        // Arrange
        var handler = new TestQueryHandler();

        // Act & Assert
        var act = () => _registry.RegisterQueryHandler(null!, handler);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("queryType");
    }

    [Fact]
    public void RegisterQueryHandler_WithNullHandler_ShouldThrow()
    {
        // Act & Assert
        var act = () => _registry.RegisterQueryHandler(typeof(TestQuery), null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handler");
    }

    [Fact]
    public void RegisterQueryHandler_WithDuplicateQueryType_ShouldThrow()
    {
        // Arrange
        var handler1 = new TestQueryHandler();
        var handler2 = new TestQueryHandler();
        _registry.RegisterQueryHandler(typeof(TestQuery), handler1);

        // Act & Assert
        var act = () => _registry.RegisterQueryHandler(typeof(TestQuery), handler2);
        act.Should().Throw<HandlerAlreadyRegisteredException>()
            .WithMessage("*A handler for query type TestQuery is already registered*");
    }

    [Fact]
    public void GetQueryHandler_WithRegisteredHandler_ShouldReturnHandler()
    {
        // Arrange
        var handler = new TestQueryHandler();
        _registry.RegisterQueryHandler(typeof(TestQuery), handler);

        // Act
        var result = _registry.GetQueryHandler(typeof(TestQuery));

        // Assert
        result.Should().Be(handler);
    }

    [Fact]
    public void GetQueryHandler_WithUnregisteredHandler_ShouldReturnNull()
    {
        // Act
        var result = _registry.GetQueryHandler(typeof(TestQuery));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RegisterEventHandler_WithValidEventAndHandler_ShouldSucceed()
    {
        // Arrange
        var handler = new TestEventHandler();

        // Act & Assert
        var act = () => _registry.RegisterEventHandler(typeof(TestEvent), handler);
        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterEventHandler_WithNullEventType_ShouldThrow()
    {
        // Arrange
        var handler = new TestEventHandler();

        // Act & Assert
        var act = () => _registry.RegisterEventHandler(null!, handler);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("eventType");
    }

    [Fact]
    public void RegisterEventHandler_WithNullHandler_ShouldThrow()
    {
        // Act & Assert
        var act = () => _registry.RegisterEventHandler(typeof(TestEvent), null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handler");
    }

    [Fact]
    public void RegisterEventHandler_WithMultipleHandlers_ShouldAllowMultipleRegistrations()
    {
        // Arrange
        var handler1 = new TestEventHandler();
        var handler2 = new TestEventHandler();

        // Act & Assert
        var act1 = () => _registry.RegisterEventHandler(typeof(TestEvent), handler1);
        var act2 = () => _registry.RegisterEventHandler(typeof(TestEvent), handler2);
        act1.Should().NotThrow();
        act2.Should().NotThrow();
    }

    [Fact]
    public void GetEventHandlers_WithRegisteredHandlers_ShouldReturnAllHandlers()
    {
        // Arrange
        var handler1 = new TestEventHandler();
        var handler2 = new TestEventHandler();
        _registry.RegisterEventHandler(typeof(TestEvent), handler1);
        _registry.RegisterEventHandler(typeof(TestEvent), handler2);

        // Act
        var result = _registry.GetEventHandlers(typeof(TestEvent));

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(handler1);
        result.Should().Contain(handler2);
    }

    [Fact]
    public void GetEventHandlers_WithUnregisteredEvent_ShouldReturnEmptyList()
    {
        // Act
        var result = _registry.GetEventHandlers(typeof(TestEvent));

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void UnregisterCommandHandler_WithRegisteredHandler_ShouldRemoveHandler()
    {
        // Arrange
        var handler = new TestCommandHandler();
        _registry.RegisterCommandHandler(typeof(TestCommand), handler);

        // Act
        var result = _registry.UnregisterCommandHandler(typeof(TestCommand));

        // Assert
        result.Should().BeTrue();
        _registry.GetCommandHandler(typeof(TestCommand)).Should().BeNull();
    }

    [Fact]
    public void UnregisterCommandHandler_WithUnregisteredHandler_ShouldReturnFalse()
    {
        // Act
        var result = _registry.UnregisterCommandHandler(typeof(TestCommand));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnregisterQueryHandler_WithRegisteredHandler_ShouldRemoveHandler()
    {
        // Arrange
        var handler = new TestQueryHandler();
        _registry.RegisterQueryHandler(typeof(TestQuery), handler);

        // Act
        var result = _registry.UnregisterQueryHandler(typeof(TestQuery));

        // Assert
        result.Should().BeTrue();
        _registry.GetQueryHandler(typeof(TestQuery)).Should().BeNull();
    }

    [Fact]
    public void UnregisterQueryHandler_WithUnregisteredHandler_ShouldReturnFalse()
    {
        // Act
        var result = _registry.UnregisterQueryHandler(typeof(TestQuery));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllHandlers()
    {
        // Arrange
        var commandHandler = new TestCommandHandler();
        var queryHandler = new TestQueryHandler();
        var eventHandler = new TestEventHandler();

        _registry.RegisterCommandHandler(typeof(TestCommand), commandHandler);
        _registry.RegisterQueryHandler(typeof(TestQuery), queryHandler);
        _registry.RegisterEventHandler(typeof(TestEvent), eventHandler);

        // Act
        _registry.Clear();

        // Assert
        _registry.GetCommandHandler(typeof(TestCommand)).Should().BeNull();
        _registry.GetQueryHandler(typeof(TestQuery)).Should().BeNull();
        _registry.GetEventHandlers(typeof(TestEvent)).Should().BeEmpty();
    }

    // Test classes - simplified without full IMessage implementation
    public class TestCommand { }
    public class TestQuery { }
    public class TestEvent { }
    public class TestCommandHandler { }
    public class TestQueryHandler { }
    public class TestEventHandler { }
}