using FluentAssertions;
using Conduit.Transports.Tcp;
using Conduit.Transports.Core;

namespace Conduit.Transports.Tcp.Tests;

public class TcpConfigurationTests
{
    [Fact]
    public void TcpConfiguration_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new TcpConfiguration();

        // Assert
        config.Type.Should().Be(TransportType.Tcp);
        config.Name.Should().Be("TCP");
        config.Host.Should().Be("0.0.0.0");
        config.Port.Should().Be(5000);
        config.IsServer.Should().BeFalse();
        config.RemoteHost.Should().BeNull();
        config.RemotePort.Should().BeNull();
        config.MaxConnections.Should().Be(100);
        config.Backlog.Should().Be(100);
        config.ReceiveBufferSize.Should().Be(8192);
        config.SendBufferSize.Should().Be(8192);
        config.UseKeepAlive.Should().BeTrue();
        config.KeepAliveInterval.Should().Be(60000);
        config.KeepAliveRetryCount.Should().Be(3);
        config.NoDelay.Should().BeTrue();
        config.LingerTime.Should().Be(0);
        config.ReuseAddress.Should().BeTrue();
        config.FramingProtocol.Should().Be(FramingProtocol.LengthPrefixed);
        config.MaxMessageSize.Should().Be(1048576);
        config.UseSsl.Should().BeFalse();
        config.SslCertificatePath.Should().BeNull();
        config.SslCertificatePassword.Should().BeNull();
        config.ValidateServerCertificate.Should().BeTrue();
        config.ServerName.Should().BeNull();
        config.HeartbeatInterval.Should().Be(30000);
        config.HeartbeatTimeout.Should().Be(60000);
        config.UseConnectionPooling.Should().BeTrue();
        config.ConnectionPoolSize.Should().Be(5);
        config.ConnectionPoolTimeout.Should().Be(30000);
    }

    [Fact]
    public void TcpConfiguration_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var config = new TcpConfiguration();

        // Act
        config.Host = "192.168.1.100";
        config.Port = 8080;
        config.IsServer = true;
        config.RemoteHost = "remote.example.com";
        config.RemotePort = 9090;
        config.MaxConnections = 200;
        config.UseKeepAlive = false;
        config.NoDelay = false;
        config.FramingProtocol = FramingProtocol.NewlineDelimited;
        config.UseSsl = true;
        config.SslCertificatePath = "/path/to/cert.pem";

        // Assert
        config.Host.Should().Be("192.168.1.100");
        config.Port.Should().Be(8080);
        config.IsServer.Should().BeTrue();
        config.RemoteHost.Should().Be("remote.example.com");
        config.RemotePort.Should().Be(9090);
        config.MaxConnections.Should().Be(200);
        config.UseKeepAlive.Should().BeFalse();
        config.NoDelay.Should().BeFalse();
        config.FramingProtocol.Should().Be(FramingProtocol.NewlineDelimited);
        config.UseSsl.Should().BeTrue();
        config.SslCertificatePath.Should().Be("/path/to/cert.pem");
    }

    [Fact]
    public void TcpConfiguration_BuildConnectionString_ServerMode_ShouldReturnCorrectString()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            Host = "192.168.1.100",
            Port = 8080,
            IsServer = true
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        connectionString.Should().Be("tcp://192.168.1.100:8080?server=true");
    }

    [Fact]
    public void TcpConfiguration_BuildConnectionString_ClientMode_ShouldReturnCorrectString()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = false,
            RemoteHost = "remote.example.com",
            RemotePort = 9090
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        connectionString.Should().Be("tcp://remote.example.com:9090");
    }

    [Fact]
    public void TcpConfiguration_BuildConnectionString_ClientModeDefaultValues_ShouldUseDefaults()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = false
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        connectionString.Should().Be("tcp://localhost:5000");
    }

    [Fact]
    public void TcpConfiguration_Validate_ValidServerConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = true,
            Port = 8080,
            MaxConnections = 50
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void TcpConfiguration_Validate_ValidClientConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = false,
            RemoteHost = "remote.example.com",
            RemotePort = 9090
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void TcpConfiguration_Validate_InvalidPort_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            Port = -1
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Port")
            .WithMessage("*Port must be between 0 and 65535*");
    }

    [Fact]
    public void TcpConfiguration_Validate_PortTooHigh_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            Port = 70000
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Port");
    }

    [Fact]
    public void TcpConfiguration_Validate_InvalidRemotePort_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            RemotePort = -1
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("RemotePort");
    }

    [Fact]
    public void TcpConfiguration_Validate_ZeroMaxConnections_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            MaxConnections = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxConnections");
    }

    [Fact]
    public void TcpConfiguration_Validate_ZeroBacklog_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            Backlog = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Backlog");
    }

    [Fact]
    public void TcpConfiguration_Validate_ZeroReceiveBufferSize_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            ReceiveBufferSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ReceiveBufferSize");
    }

    [Fact]
    public void TcpConfiguration_Validate_ZeroSendBufferSize_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            SendBufferSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("SendBufferSize");
    }

    [Fact]
    public void TcpConfiguration_Validate_ZeroMaxMessageSize_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            MaxMessageSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxMessageSize");
    }

    [Fact]
    public void TcpConfiguration_Validate_ClientModeWithoutRemoteHost_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = false,
            RemoteHost = null
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentException>()
            .WithParameterName("RemoteHost")
            .WithMessage("*RemoteHost must be specified for client mode*");
    }

    [Fact]
    public void TcpConfiguration_Validate_ClientModeWithEmptyRemoteHost_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = false,
            RemoteHost = ""
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentException>()
            .WithParameterName("RemoteHost");
    }

    [Fact]
    public void TcpConfiguration_Validate_ServerModeWithSslButNoCertificate_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = true,
            UseSsl = true,
            SslCertificatePath = null
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentException>()
            .WithParameterName("SslCertificatePath")
            .WithMessage("*SslCertificatePath must be specified when UseSsl is enabled in server mode*");
    }

    [Fact]
    public void TcpConfiguration_Validate_ServerModeWithSslButEmptyCertificate_ShouldThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = true,
            UseSsl = true,
            SslCertificatePath = ""
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentException>()
            .WithParameterName("SslCertificatePath");
    }

    [Fact]
    public void TcpConfiguration_Validate_ServerModeWithSslAndValidCertificate_ShouldNotThrow()
    {
        // Arrange
        var config = new TcpConfiguration
        {
            IsServer = true,
            UseSsl = true,
            SslCertificatePath = "/path/to/cert.pem"
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void FramingProtocol_EnumValues_ShouldBeCorrect()
    {
        // Assert
        ((int)FramingProtocol.LengthPrefixed).Should().Be(0);
        ((int)FramingProtocol.NewlineDelimited).Should().Be(1);
        ((int)FramingProtocol.CrlfDelimited).Should().Be(2);
        ((int)FramingProtocol.CustomDelimiter).Should().Be(3);
    }

    [Fact]
    public void TcpConfiguration_CompleteConfiguration_ShouldAllowFullCustomization()
    {
        // Act
        var config = new TcpConfiguration
        {
            Host = "10.0.0.1",
            Port = 9999,
            IsServer = true,
            MaxConnections = 500,
            Backlog = 200,
            ReceiveBufferSize = 16384,
            SendBufferSize = 16384,
            UseKeepAlive = true,
            KeepAliveInterval = 30000,
            KeepAliveRetryCount = 5,
            NoDelay = false,
            LingerTime = 10,
            ReuseAddress = false,
            FramingProtocol = FramingProtocol.CrlfDelimited,
            MaxMessageSize = 2097152,
            UseSsl = true,
            SslCertificatePath = "/etc/ssl/server.crt",
            SslCertificatePassword = "password123",
            ValidateServerCertificate = false,
            ServerName = "server.example.com",
            HeartbeatInterval = 15000,
            HeartbeatTimeout = 45000,
            UseConnectionPooling = false,
            ConnectionPoolSize = 10,
            ConnectionPoolTimeout = 60000
        };

        // Assert
        config.Host.Should().Be("10.0.0.1");
        config.Port.Should().Be(9999);
        config.IsServer.Should().BeTrue();
        config.MaxConnections.Should().Be(500);
        config.FramingProtocol.Should().Be(FramingProtocol.CrlfDelimited);
        config.UseSsl.Should().BeTrue();
        config.UseConnectionPooling.Should().BeFalse();

        // Should validate without throwing
        var act = () => config.Validate();
        act.Should().NotThrow();
    }
}