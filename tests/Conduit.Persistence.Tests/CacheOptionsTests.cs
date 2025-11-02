using FluentAssertions;
using Conduit.Persistence.Caching;

namespace Conduit.Persistence.Tests;

public class CacheOptionsTests
{
    [Fact]
    public void CacheOptions_Constructor_ShouldSetDefaultValues()
    {
        // Act
        var options = new CacheOptions();

        // Assert
        options.EnableCaching.Should().BeTrue();
        options.KeyPrefix.Should().Be("conduit");
        options.Expiration.Should().Be(TimeSpan.FromMinutes(5));
        options.CacheListQueries.Should().BeTrue();
        options.ListExpiration.Should().Be(TimeSpan.FromMinutes(1));
        options.CacheOnWrite.Should().BeTrue();
    }

    [Fact]
    public void CacheOptions_SetProperties_ShouldUpdateValues()
    {
        // Arrange
        var options = new CacheOptions();
        var customExpiration = TimeSpan.FromHours(1);
        var customListExpiration = TimeSpan.FromMinutes(30);

        // Act
        options.EnableCaching = false;
        options.KeyPrefix = "custom";
        options.Expiration = customExpiration;
        options.CacheListQueries = false;
        options.ListExpiration = customListExpiration;
        options.CacheOnWrite = false;

        // Assert
        options.EnableCaching.Should().BeFalse();
        options.KeyPrefix.Should().Be("custom");
        options.Expiration.Should().Be(customExpiration);
        options.CacheListQueries.Should().BeFalse();
        options.ListExpiration.Should().Be(customListExpiration);
        options.CacheOnWrite.Should().BeFalse();
    }

    [Fact]
    public void CacheOptions_SetNullExpiration_ShouldAllowNull()
    {
        // Arrange
        var options = new CacheOptions();

        // Act
        options.Expiration = null;
        options.ListExpiration = null;

        // Assert
        options.Expiration.Should().BeNull();
        options.ListExpiration.Should().BeNull();
    }

    [Fact]
    public void CacheOptions_EmptyKeyPrefix_ShouldAllowEmpty()
    {
        // Arrange
        var options = new CacheOptions();

        // Act
        options.KeyPrefix = "";

        // Assert
        options.KeyPrefix.Should().BeEmpty();
    }
}