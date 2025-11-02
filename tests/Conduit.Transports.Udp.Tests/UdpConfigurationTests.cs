using FluentAssertions;
using Conduit.Transports.Udp;
using Conduit.Transports.Core;

namespace Conduit.Transports.Udp.Tests;

public class UdpConfigurationTests
{
    [Fact]
    public void UdpConfiguration_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var config = new UdpConfiguration();

        // Assert
        config.Type.Should().Be(TransportType.Custom);
        config.Name.Should().Be("UDP");
        config.Host.Should().Be("0.0.0.0");
        config.Port.Should().Be(0);
        config.RemoteHost.Should().BeNull();
        config.RemotePort.Should().BeNull();
        config.MaxDatagramSize.Should().Be(65507);
        config.ReceiveBufferSize.Should().Be(65536);
        config.SendBufferSize.Should().Be(65536);
        config.AllowBroadcast.Should().BeFalse();
        config.ReuseAddress.Should().BeTrue();
        config.MulticastTimeToLive.Should().Be(1);
        config.MulticastLoopback.Should().BeTrue();
        config.MulticastGroup.Should().BeNull();
        config.MulticastInterface.Should().BeNull();
        config.UseIPv6.Should().BeFalse();
        config.ReceiveTimeout.Should().Be(0);
        config.SendTimeout.Should().Be(5000);
        config.ExclusiveAddressUse.Should().BeFalse();
        config.RequireAcknowledgement.Should().BeFalse();
        config.AcknowledgementTimeout.Should().Be(1000);
        config.MaxRetransmissions.Should().Be(3);
        config.EnableFragmentation.Should().BeFalse();
        config.FragmentSize.Should().Be(1400);
    }

    [Fact]
    public void UdpConfiguration_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var config = new UdpConfiguration();

        // Act
        config.Host = "192.168.1.100";
        config.Port = 8080;
        config.RemoteHost = "remote.example.com";
        config.RemotePort = 9090;
        config.MaxDatagramSize = 1500;
        config.AllowBroadcast = true;
        config.UseIPv6 = true;
        config.RequireAcknowledgement = true;
        config.EnableFragmentation = true;

        // Assert
        config.Host.Should().Be("192.168.1.100");
        config.Port.Should().Be(8080);
        config.RemoteHost.Should().Be("remote.example.com");
        config.RemotePort.Should().Be(9090);
        config.MaxDatagramSize.Should().Be(1500);
        config.AllowBroadcast.Should().BeTrue();
        config.UseIPv6.Should().BeTrue();
        config.RequireAcknowledgement.Should().BeTrue();
        config.EnableFragmentation.Should().BeTrue();
    }

    [Fact]
    public void UdpConfiguration_BuildConnectionString_MulticastMode_ShouldReturnCorrectString()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MulticastGroup = "239.255.255.250",
            Port = 1900
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        connectionString.Should().Be("udp://239.255.255.250:1900?multicast=true");
    }

    [Fact]
    public void UdpConfiguration_BuildConnectionString_ClientMode_ShouldReturnCorrectString()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            RemoteHost = "remote.example.com",
            RemotePort = 9090
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        connectionString.Should().Be("udp://remote.example.com:9090");
    }

    [Fact]
    public void UdpConfiguration_BuildConnectionString_ListenMode_ShouldReturnCorrectString()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            Host = "192.168.1.100",
            Port = 8080
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        connectionString.Should().Be("udp://192.168.1.100:8080?listen=true");
    }

    [Fact]
    public void UdpConfiguration_Validate_ValidConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            Port = 8080,
            MaxDatagramSize = 1400,
            ReceiveBufferSize = 32768,
            SendBufferSize = 32768
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void UdpConfiguration_Validate_InvalidPort_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
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
    public void UdpConfiguration_Validate_PortTooHigh_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            Port = 70000
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("Port");
    }

    [Fact]
    public void UdpConfiguration_Validate_InvalidRemotePort_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            RemotePort = -1
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("RemotePort");
    }

    [Fact]
    public void UdpConfiguration_Validate_InvalidMaxDatagramSize_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MaxDatagramSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxDatagramSize")
            .WithMessage("*MaxDatagramSize must be between 1 and 65507*");
    }

    [Fact]
    public void UdpConfiguration_Validate_MaxDatagramSizeTooLarge_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MaxDatagramSize = 70000
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MaxDatagramSize");
    }

    [Fact]
    public void UdpConfiguration_Validate_ZeroReceiveBufferSize_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            ReceiveBufferSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ReceiveBufferSize");
    }

    [Fact]
    public void UdpConfiguration_Validate_ZeroSendBufferSize_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            SendBufferSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("SendBufferSize");
    }

    [Fact]
    public void UdpConfiguration_Validate_ZeroMulticastTimeToLive_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MulticastTimeToLive = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("MulticastTimeToLive");
    }

    [Fact]
    public void UdpConfiguration_Validate_ZeroFragmentSize_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            FragmentSize = 0
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("FragmentSize");
    }

    [Fact]
    public void UdpConfiguration_Validate_FragmentSizeLargerThanMaxDatagram_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MaxDatagramSize = 1000,
            FragmentSize = 1500
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("FragmentSize")
            .WithMessage("*FragmentSize must be between 1 and 1000*");
    }

    [Fact]
    public void UdpConfiguration_Validate_MulticastAndBroadcast_ShouldThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MulticastGroup = "239.255.255.250",
            AllowBroadcast = true
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot use both multicast and broadcast simultaneously*");
    }

    [Fact]
    public void UdpConfiguration_Validate_MulticastConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            MulticastGroup = "239.255.255.250",
            Port = 1900,
            MulticastTimeToLive = 2,
            MulticastInterface = "192.168.1.100"
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void UdpConfiguration_Validate_BroadcastConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            AllowBroadcast = true,
            Port = 8080
        };

        // Act & Assert
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void UdpConfiguration_CompleteConfiguration_ShouldAllowFullCustomization()
    {
        // Act
        var config = new UdpConfiguration
        {
            Host = "10.0.0.1",
            Port = 9999,
            RemoteHost = "remote.example.com",
            RemotePort = 8888,
            MaxDatagramSize = 8192,
            ReceiveBufferSize = 131072,
            SendBufferSize = 131072,
            AllowBroadcast = false,
            ReuseAddress = false,
            MulticastTimeToLive = 5,
            MulticastLoopback = false,
            MulticastGroup = "239.1.1.1",
            MulticastInterface = "192.168.1.100",
            UseIPv6 = true,
            ReceiveTimeout = 10000,
            SendTimeout = 3000,
            ExclusiveAddressUse = true,
            RequireAcknowledgement = true,
            AcknowledgementTimeout = 2000,
            MaxRetransmissions = 5,
            EnableFragmentation = true,
            FragmentSize = 1200
        };

        // Assert
        config.Host.Should().Be("10.0.0.1");
        config.Port.Should().Be(9999);
        config.RemoteHost.Should().Be("remote.example.com");
        config.RemotePort.Should().Be(8888);
        config.MaxDatagramSize.Should().Be(8192);
        config.AllowBroadcast.Should().BeFalse();
        config.UseIPv6.Should().BeTrue();
        config.RequireAcknowledgement.Should().BeTrue();
        config.EnableFragmentation.Should().BeTrue();

        // Should validate without throwing (remove broadcast conflict for this test)
        config.AllowBroadcast = false; // Ensure no conflict with multicast
        var act = () => config.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void UdpConfiguration_IPv6Configuration_ShouldWork()
    {
        // Arrange
        var config = new UdpConfiguration
        {
            UseIPv6 = true,
            Host = "::1", // IPv6 localhost
            Port = 8080
        };

        // Act
        var connectionString = config.BuildConnectionString();

        // Assert
        config.UseIPv6.Should().BeTrue();
        connectionString.Should().Be("udp://::1:8080?listen=true");
    }

    [Fact]
    public void UdpConfiguration_AcknowledgementConfiguration_ShouldSetCorrectValues()
    {
        // Arrange
        var config = new UdpConfiguration();

        // Act
        config.RequireAcknowledgement = true;
        config.AcknowledgementTimeout = 5000;
        config.MaxRetransmissions = 10;

        // Assert
        config.RequireAcknowledgement.Should().BeTrue();
        config.AcknowledgementTimeout.Should().Be(5000);
        config.MaxRetransmissions.Should().Be(10);
    }

    [Fact]
    public void UdpConfiguration_FragmentationConfiguration_ShouldSetCorrectValues()
    {
        // Arrange
        var config = new UdpConfiguration();

        // Act
        config.EnableFragmentation = true;
        config.FragmentSize = 1200;
        config.MaxDatagramSize = 8192;

        // Assert
        config.EnableFragmentation.Should().BeTrue();
        config.FragmentSize.Should().Be(1200);

        // Should validate correctly
        var act = () => config.Validate();
        act.Should().NotThrow();
    }
}