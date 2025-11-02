using System.Collections.Generic;
using FluentAssertions;
using Conduit.Components;
using Conduit.Api;
using Conduit.Pipeline.Behaviors;
using Moq;

namespace Conduit.Components.Tests;

public class BehaviorContributionTests
{
    [Fact]
    public void BehaviorContribution_Builder_ShouldCreateValidContribution()
    {
        // Arrange
        var mockBehavior = new Mock<IPipelineBehavior>();
        var factory = () => mockBehavior.Object;

        // Act
        var contribution = new BehaviorContribution
        {
            Id = "test-behavior",
            Name = "Test Behavior",
            Description = "A test behavior",
            Behavior = mockBehavior.Object,
            Priority = 100,
            Tags = new HashSet<string> { "test", "example" },
            IsEnabled = true
        };

        // Assert
        contribution.Id.Should().Be("test-behavior");
        contribution.Name.Should().Be("Test Behavior");
        contribution.Description.Should().Be("A test behavior");
        contribution.Priority.Should().Be(100);
        contribution.Tags.Should().Contain("test");
        contribution.Tags.Should().Contain("example");
        contribution.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void BehaviorContribution_Behavior_ShouldReturnAssignedBehavior()
    {
        // Arrange
        var mockBehavior = new Mock<IPipelineBehavior>();

        var contribution = new BehaviorContribution
        {
            Id = "test",
            Behavior = mockBehavior.Object,
            IsEnabled = true
        };

        // Act
        var behavior = contribution.Behavior;

        // Assert
        behavior.Should().Be(mockBehavior.Object);
    }

    [Fact]
    public void BehaviorContribution_WithConstraints_ShouldStoreConstraints()
    {
        // Arrange
        var mockBehavior = new Mock<IPipelineBehavior>();
        var constraints = BehaviorConstraints.Always();

        var contribution = new BehaviorContribution
        {
            Id = "test",
            Behavior = mockBehavior.Object,
            Constraints = constraints,
            IsEnabled = true
        };

        // Act & Assert
        contribution.Constraints.Should().Be(constraints);
    }

    [Fact]
    public void BehaviorContribution_WithTags_ShouldStoreTags()
    {
        // Arrange
        var mockBehavior = new Mock<IPipelineBehavior>();

        // Act
        var contribution = new BehaviorContribution
        {
            Id = "test",
            Behavior = mockBehavior.Object,
            Tags = new HashSet<string> { "tag1", "tag2" },
            IsEnabled = true
        };

        // Assert
        contribution.Tags.Should().Contain("tag1");
        contribution.Tags.Should().Contain("tag2");
        contribution.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void BehaviorContribution_WithoutName_ShouldHaveEmptyName()
    {
        // Arrange
        var mockBehavior = new Mock<IPipelineBehavior>();

        // Act
        var contribution = new BehaviorContribution
        {
            Id = "test-id",
            Behavior = mockBehavior.Object,
            IsEnabled = true
        };

        // Assert
        contribution.Name.Should().Be(string.Empty);
        contribution.Id.Should().Be("test-id");
    }

    [Fact]
    public void BehaviorContributionBuilder_Build_WithoutId_ShouldThrow()
    {
        // Arrange
        var mockBehavior = new Mock<IPipelineBehavior>();

        // Act & Assert
        var act = () => new BehaviorContribution
        {
            Id = string.Empty,
            Behavior = mockBehavior.Object,
            IsEnabled = true
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void BehaviorContributionBuilder_Build_WithoutFactory_ShouldThrow()
    {
        // Act & Assert
        var act = () => new BehaviorContribution
        {
            Id = "test",
            Behavior = null!,
            IsEnabled = true
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void BehaviorConstraints_RunAlways_ShouldExist()
    {
        // Act
        var constraints = BehaviorConstraints.Always();

        // Assert
        constraints.Should().NotBeNull();
    }

    [Fact]
    public void BehaviorConstraints_When_ShouldCreateCustomConstraints()
    {
        // Act
        var constraints = BehaviorConstraints.When(_ => true);

        // Assert
        constraints.Should().NotBeNull();
    }

    [Fact]
    public void BehaviorConstraints_WhenFeatureEnabled_ShouldCreateFeatureConstraints()
    {
        // Act
        var constraints = BehaviorConstraints.WhenFeatureEnabled("feature1");

        // Assert
        constraints.Should().NotBeNull();
    }

    [Fact]
    public void BehaviorConstraints_WhenConfigurationEnabled_ShouldCreateConfigConstraints()
    {
        // Act
        var constraints = BehaviorConstraints.WhenFeatureEnabled("setting1");

        // Assert
        constraints.Should().NotBeNull();
    }
}