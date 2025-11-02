using FluentAssertions;
using Conduit.Application;

namespace Conduit.Application.Tests;

public class ConduitConfigurationTests
{
    [Fact]
    public void ConduitConfiguration_DefaultConstructor_ShouldInitializeWithDefaults()
    {
        // Act
        var config = new ConduitConfiguration();

        // Assert
        config.ApplicationName.Should().Be("Conduit Application");
        config.Version.Should().Be("1.0.0");
        config.Environment.Should().Be("Development");
        config.ComponentDiscovery.Should().NotBeNull();
        config.Messaging.Should().NotBeNull();
        config.Features.Should().NotBeNull();
        config.Features.Should().BeEmpty();
    }

    [Fact]
    public void ConduitConfiguration_Properties_ShouldBeSettable()
    {
        // Arrange
        var config = new ConduitConfiguration();

        // Act
        config.ApplicationName = "Test Application";
        config.Version = "2.0.0";
        config.Environment = "Production";

        // Assert
        config.ApplicationName.Should().Be("Test Application");
        config.Version.Should().Be("2.0.0");
        config.Environment.Should().Be("Production");
    }

    [Fact]
    public void ConduitConfiguration_Features_ShouldSupportAddRemove()
    {
        // Arrange
        var config = new ConduitConfiguration();

        // Act
        config.Features["Feature1"] = true;
        config.Features["Feature2"] = false;
        config.Features["Feature3"] = true;

        // Assert
        config.Features.Should().HaveCount(3);
        config.Features["Feature1"].Should().BeTrue();
        config.Features["Feature2"].Should().BeFalse();
        config.Features["Feature3"].Should().BeTrue();
    }

    [Fact]
    public void ConduitConfiguration_ComponentDiscovery_ShouldBeConfigurable()
    {
        // Arrange
        var config = new ConduitConfiguration();

        // Act
        config.ComponentDiscovery.Enabled = false;

        // Assert
        config.ComponentDiscovery.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ConduitConfiguration_Messaging_ShouldBeConfigurable()
    {
        // Arrange
        var config = new ConduitConfiguration();

        // Act
        config.Messaging.Enabled = false;

        // Assert
        config.Messaging.Enabled.Should().BeFalse();
    }
}

public class ComponentDiscoverySettingsTests
{
    [Fact]
    public void ComponentDiscoverySettings_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var settings = new ComponentDiscoverySettings();

        // Assert
        settings.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ComponentDiscoverySettings_Enabled_ShouldBeSettable()
    {
        // Arrange
        var settings = new ComponentDiscoverySettings();

        // Act
        settings.Enabled = false;

        // Assert
        settings.Enabled.Should().BeFalse();
    }
}

public class MessagingSettingsTests
{
    [Fact]
    public void MessagingSettings_DefaultConstructor_ShouldInitializeCorrectly()
    {
        // Act
        var settings = new MessagingSettings();

        // Assert
        settings.Enabled.Should().BeTrue();
    }

    [Fact]
    public void MessagingSettings_Enabled_ShouldBeSettable()
    {
        // Arrange
        var settings = new MessagingSettings();

        // Act
        settings.Enabled = false;

        // Assert
        settings.Enabled.Should().BeFalse();
    }
}