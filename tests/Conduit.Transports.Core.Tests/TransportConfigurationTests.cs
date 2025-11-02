using FluentAssertions;
using Conduit.Transports.Core;

namespace Conduit.Transports.Core.Tests;

public class TransportConfigurationTests
{
    [Fact]
    public void TransportConfiguration_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new TransportConfiguration();

        // Assert
        config.Type.Should().Be(default(TransportType));
        config.Name.Should().BeEmpty();
        config.Enabled.Should().BeTrue();
        config.Properties.Should().NotBeNull().And.BeEmpty();
        config.Connection.Should().NotBeNull();
        config.Protocol.Should().NotBeNull();
        config.Security.Should().NotBeNull();
        config.Performance.Should().NotBeNull();
    }

    [Fact]
    public void TransportConfiguration_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var config = new TransportConfiguration();

        // Act
        config.Type = TransportType.Tcp;
        config.Name = "test-transport";
        config.Enabled = false;

        // Assert
        config.Type.Should().Be(TransportType.Tcp);
        config.Name.Should().Be("test-transport");
        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void TransportConfiguration_GetProperty_WithExistingKey_ShouldReturnValue()
    {
        // Arrange
        var config = new TransportConfiguration();
        config.Properties["test-string"] = "test-value";
        config.Properties["test-int"] = 42;
        config.Properties["test-bool"] = true;

        // Act & Assert
        config.GetProperty<string>("test-string").Should().Be("test-value");
        config.GetProperty<int>("test-int").Should().Be(42);
        config.GetProperty<bool>("test-bool").Should().BeTrue();
    }

    [Fact]
    public void TransportConfiguration_GetProperty_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var config = new TransportConfiguration();

        // Act & Assert
        config.GetProperty<string>("non-existent").Should().BeNull();
        config.GetProperty<int>("non-existent").Should().Be(0);
        config.GetProperty<bool>("non-existent").Should().BeFalse();
    }

    [Fact]
    public void TransportConfiguration_GetProperty_WithWrongType_ShouldReturnDefault()
    {
        // Arrange
        var config = new TransportConfiguration();
        config.Properties["test-value"] = "string-value";

        // Act & Assert
        config.GetProperty<int>("test-value").Should().Be(0);
        config.GetProperty<bool>("test-value").Should().BeFalse();
    }

    [Fact]
    public void TransportConfiguration_SetProperty_ShouldStoreValue()
    {
        // Arrange
        var config = new TransportConfiguration();

        // Act
        config.SetProperty("custom-key", "custom-value");
        config.SetProperty("number", 123);
        config.SetProperty("flag", true);

        // Assert
        config.Properties["custom-key"].Should().Be("custom-value");
        config.Properties["number"].Should().Be(123);
        config.Properties["flag"].Should().Be(true);
    }

    [Fact]
    public void TransportConfiguration_SetProperty_WithExistingKey_ShouldOverwrite()
    {
        // Arrange
        var config = new TransportConfiguration();
        config.SetProperty("key", "original");

        // Act
        config.SetProperty("key", "updated");

        // Assert
        config.Properties["key"].Should().Be("updated");
    }

    [Fact]
    public void ConnectionSettings_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var settings = new ConnectionSettings();

        // Assert
        settings.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(30));
        settings.ReadTimeout.Should().Be(TimeSpan.FromSeconds(60));
        settings.WriteTimeout.Should().Be(TimeSpan.FromSeconds(30));
        settings.KeepAliveInterval.Should().Be(TimeSpan.FromSeconds(30));
        settings.MaxRetries.Should().Be(3);
        settings.RetryDelay.Should().Be(TimeSpan.FromSeconds(5));
        settings.AutoReconnect.Should().BeTrue();
        settings.ReconnectDelay.Should().Be(TimeSpan.FromSeconds(5));
        settings.MaxConcurrentConnections.Should().Be(10);
        settings.PoolSize.Should().Be(5);
        settings.IdleTimeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ConnectionSettings_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var settings = new ConnectionSettings();

        // Act
        settings.ConnectTimeout = TimeSpan.FromSeconds(60);
        settings.MaxRetries = 5;
        settings.AutoReconnect = false;

        // Assert
        settings.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(60));
        settings.MaxRetries.Should().Be(5);
        settings.AutoReconnect.Should().BeFalse();
    }

    [Fact]
    public void ProtocolSettings_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var settings = new ProtocolSettings();

        // Assert
        settings.PreferredVersion.Should().BeNull();
        settings.SupportedVersions.Should().BeEmpty();
        settings.AutoNegotiate.Should().BeTrue();
        settings.NegotiationTimeout.Should().Be(TimeSpan.FromSeconds(10));
        settings.MaxMessageSize.Should().Be(1024 * 1024);
        settings.CompressionEnabled.Should().BeFalse();
        settings.CompressionThreshold.Should().Be(1024);
        settings.Headers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ProtocolSettings_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var settings = new ProtocolSettings();

        // Act
        settings.PreferredVersion = "1.2";
        settings.SupportedVersions = new[] { "1.0", "1.1", "1.2" };
        settings.CompressionEnabled = true;
        settings.Headers["custom-header"] = "custom-value";

        // Assert
        settings.PreferredVersion.Should().Be("1.2");
        settings.SupportedVersions.Should().BeEquivalentTo("1.0", "1.1", "1.2");
        settings.CompressionEnabled.Should().BeTrue();
        settings.Headers.Should().ContainKey("custom-header").WhoseValue.Should().Be("custom-value");
    }

    [Fact]
    public void SecuritySettings_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var settings = new SecuritySettings();

        // Assert
        settings.TlsEnabled.Should().BeFalse();
        settings.VerifyHostname.Should().BeTrue();
        settings.VerifyCertificate.Should().BeTrue();
        settings.CertificatePath.Should().BeNull();
        settings.CertificatePassword.Should().BeNull();
        settings.TrustedCertificatePath.Should().BeNull();
        settings.CipherSuites.Should().BeEmpty();
        settings.MinimumTlsVersion.Should().Be("TLS 1.2");
        settings.RequireClientCertificate.Should().BeFalse();
        settings.Username.Should().BeNull();
        settings.Password.Should().BeNull();
        settings.Token.Should().BeNull();
    }

    [Fact]
    public void SecuritySettings_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var settings = new SecuritySettings();

        // Act
        settings.TlsEnabled = true;
        settings.CertificatePath = "/path/to/cert.pem";
        settings.Username = "test-user";
        settings.CipherSuites = new[] { "TLS_RSA_WITH_AES_128_CBC_SHA", "TLS_RSA_WITH_AES_256_CBC_SHA" };

        // Assert
        settings.TlsEnabled.Should().BeTrue();
        settings.CertificatePath.Should().Be("/path/to/cert.pem");
        settings.Username.Should().Be("test-user");
        settings.CipherSuites.Should().BeEquivalentTo("TLS_RSA_WITH_AES_128_CBC_SHA", "TLS_RSA_WITH_AES_256_CBC_SHA");
    }

    [Fact]
    public void PerformanceSettings_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var settings = new PerformanceSettings();

        // Assert
        settings.SendBufferSize.Should().Be(8192);
        settings.ReceiveBufferSize.Should().Be(8192);
        settings.NoDelay.Should().BeTrue();
        settings.KeepAlive.Should().BeTrue();
        settings.PrefetchCount.Should().Be(10);
        settings.BatchSize.Should().Be(100);
        settings.BatchTimeout.Should().Be(TimeSpan.FromMilliseconds(100));
        settings.MaxConcurrentOperations.Should().Be(100);
        settings.BatchingEnabled.Should().BeFalse();
        settings.PipeliningEnabled.Should().BeFalse();
    }

    [Fact]
    public void PerformanceSettings_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var settings = new PerformanceSettings();

        // Act
        settings.SendBufferSize = 16384;
        settings.BatchingEnabled = true;
        settings.PrefetchCount = 50;

        // Assert
        settings.SendBufferSize.Should().Be(16384);
        settings.BatchingEnabled.Should().BeTrue();
        settings.PrefetchCount.Should().Be(50);
    }

    [Fact]
    public void TransportConfiguration_CompleteConfiguration_ShouldAllowFullCustomization()
    {
        // Act
        var config = new TransportConfiguration
        {
            Type = TransportType.Tcp,
            Name = "production-transport",
            Enabled = true,
            Connection = new ConnectionSettings
            {
                ConnectTimeout = TimeSpan.FromSeconds(45),
                MaxRetries = 5,
                AutoReconnect = true
            },
            Protocol = new ProtocolSettings
            {
                PreferredVersion = "2.0",
                CompressionEnabled = true,
                MaxMessageSize = 2 * 1024 * 1024
            },
            Security = new SecuritySettings
            {
                TlsEnabled = true,
                Username = "api-user",
                MinimumTlsVersion = "TLS 1.3"
            },
            Performance = new PerformanceSettings
            {
                BatchingEnabled = true,
                PrefetchCount = 25,
                SendBufferSize = 32768
            }
        };

        config.SetProperty("custom-setting", "custom-value");
        config.SetProperty("retry-attempts", 10);

        // Assert
        config.Type.Should().Be(TransportType.Tcp);
        config.Name.Should().Be("production-transport");
        config.Connection.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(45));
        config.Protocol.CompressionEnabled.Should().BeTrue();
        config.Security.TlsEnabled.Should().BeTrue();
        config.Performance.BatchingEnabled.Should().BeTrue();
        config.GetProperty<string>("custom-setting").Should().Be("custom-value");
        config.GetProperty<int>("retry-attempts").Should().Be(10);
    }
}