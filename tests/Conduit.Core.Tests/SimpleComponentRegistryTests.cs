using FluentAssertions;
using Moq;
using Conduit.Api;
using Conduit.Core;

namespace Conduit.Core.Tests;

public class SimpleComponentRegistryTests
{
    [Fact]
    public void ComponentRegistry_Constructor_ShouldInitializeCorrectly()
    {
        // Act
        var registry = new ComponentRegistry();

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void ComponentRegistry_Register_ShouldReturnTrue()
    {
        // Arrange
        var registry = new ComponentRegistry();
        var mockComponent = new Mock<IPluggableComponent>();
        var descriptor = new ComponentDescriptor
        {
            Id = "test-component",
            Name = "Test Component",
            ComponentType = typeof(IPluggableComponent),
            State = ComponentState.Registered
        };

        // Act
        var result = registry.Register(mockComponent.Object, descriptor);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ComponentRegistry_GetComponent_ShouldReturnRegisteredComponent()
    {
        // Arrange
        var registry = new ComponentRegistry();
        var mockComponent = new Mock<IPluggableComponent>();
        var descriptor = new ComponentDescriptor
        {
            Id = "test-component",
            ComponentType = typeof(IPluggableComponent)
        };
        registry.Register(mockComponent.Object, descriptor);

        // Act
        var component = registry.GetComponent("test-component");

        // Assert
        component.Should().Be(mockComponent.Object);
    }

    [Fact]
    public void ComponentRegistry_GetComponent_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var registry = new ComponentRegistry();

        // Act
        var component = registry.GetComponent("non-existent");

        // Assert
        component.Should().BeNull();
    }

    [Fact]
    public void ComponentRegistry_GetDescriptor_ShouldReturnRegisteredDescriptor()
    {
        // Arrange
        var registry = new ComponentRegistry();
        var mockComponent = new Mock<IPluggableComponent>();
        var descriptor = new ComponentDescriptor
        {
            Id = "test-component",
            ComponentType = typeof(IPluggableComponent)
        };
        registry.Register(mockComponent.Object, descriptor);

        // Act
        var result = registry.GetDescriptor("test-component");

        // Assert
        result.Should().Be(descriptor);
    }

    [Fact]
    public void ComponentRegistry_GetDescriptor_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var registry = new ComponentRegistry();

        // Act
        var descriptor = registry.GetDescriptor("non-existent");

        // Assert
        descriptor.Should().BeNull();
    }

    [Fact]
    public void ComponentRegistry_IsRegistered_ShouldReturnCorrectValue()
    {
        // Arrange
        var registry = new ComponentRegistry();
        var mockComponent = new Mock<IPluggableComponent>();
        var descriptor = new ComponentDescriptor
        {
            Id = "test-component",
            ComponentType = typeof(IPluggableComponent)
        };
        registry.Register(mockComponent.Object, descriptor);

        // Act & Assert
        registry.IsRegistered("test-component").Should().BeTrue();
        registry.IsRegistered("non-existent").Should().BeFalse();
    }

    [Fact]
    public void ComponentRegistry_GetAllDescriptors_ShouldReturnAllRegistered()
    {
        // Arrange
        var registry = new ComponentRegistry();
        var mockComponent1 = new Mock<IPluggableComponent>();
        var mockComponent2 = new Mock<IPluggableComponent>();

        var descriptor1 = new ComponentDescriptor { Id = "component-1", ComponentType = typeof(IPluggableComponent) };
        var descriptor2 = new ComponentDescriptor { Id = "component-2", ComponentType = typeof(IPluggableComponent) };

        registry.Register(mockComponent1.Object, descriptor1);
        registry.Register(mockComponent2.Object, descriptor2);

        // Act
        var descriptors = registry.GetAllDescriptors();

        // Assert
        descriptors.Should().HaveCount(2);
        descriptors.Should().Contain(descriptor1);
        descriptors.Should().Contain(descriptor2);
    }

    [Fact]
    public void ComponentRegistry_Register_WithDuplicateId_ShouldReturnFalse()
    {
        // Arrange
        var registry = new ComponentRegistry();
        var mockComponent1 = new Mock<IPluggableComponent>();
        var mockComponent2 = new Mock<IPluggableComponent>();

        var descriptor1 = new ComponentDescriptor { Id = "same-id", ComponentType = typeof(IPluggableComponent) };
        var descriptor2 = new ComponentDescriptor { Id = "same-id", ComponentType = typeof(IPluggableComponent) };

        registry.Register(mockComponent1.Object, descriptor1);

        // Act
        var result = registry.Register(mockComponent2.Object, descriptor2);

        // Assert
        result.Should().BeFalse();
    }
}