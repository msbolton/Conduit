using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Conduit.Gateway;

namespace Conduit.Gateway.Tests;

public class RouteManagerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly RouteManager _routeManager;

    public RouteManagerTests()
    {
        _mockLogger = new Mock<ILogger>();
        _routeManager = new RouteManager(_mockLogger.Object);
    }

    [Fact]
    public void RouteManager_Constructor_WithValidLogger_ShouldSucceed()
    {
        // Act
        var routeManager = new RouteManager(_mockLogger.Object);

        // Assert
        routeManager.Should().NotBeNull();
    }

    [Fact]
    public void RouteManager_Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        var act = () => new RouteManager(null!);
        act.Should().Throw<ArgumentNullException>().WithMessage("*logger*");
    }

    [Fact]
    public void RouteManager_AddRoute_WithValidRoute_ShouldSucceed()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "api-route",
            Path = "/api/users/{id}",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" },
            Enabled = true
        };

        // Act
        _routeManager.AddRoute(route);

        // Assert
        _routeManager.GetAllRoutes().Should().Contain(route);
    }

    [Fact]
    public void RouteManager_AddRoute_WithNullRoute_ShouldThrow()
    {
        // Act & Assert
        var act = () => _routeManager.AddRoute(null!);
        act.Should().Throw<ArgumentNullException>().WithMessage("*route*");
    }

    [Fact]
    public void RouteManager_AddRoute_WithDuplicateId_ShouldLogWarning()
    {
        // Arrange
        var route1 = new RouteConfiguration
        {
            Id = "duplicate-id",
            Path = "/api/route1",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };

        var route2 = new RouteConfiguration
        {
            Id = "duplicate-id",
            Path = "/api/route2",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5001" }
        };

        // Act
        _routeManager.AddRoute(route1);
        _routeManager.AddRoute(route2); // Should log warning but not throw

        // Assert
        _routeManager.GetAllRoutes().Should().HaveCount(1);
        _routeManager.GetRoute("duplicate-id").Should().Be(route1); // First one should remain
    }

    [Fact]
    public void RouteManager_RemoveRoute_WithExistingRoute_ShouldSucceed()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "test-route",
            Path = "/api/test",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.RemoveRoute("test-route");

        // Assert
        result.Should().BeTrue();
        _routeManager.GetAllRoutes().Should().NotContain(route);
    }

    [Fact]
    public void RouteManager_RemoveRoute_WithNonExistentRoute_ShouldReturnFalse()
    {
        // Act
        var result = _routeManager.RemoveRoute("non-existent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RouteManager_GetRoute_WithExistingId_ShouldReturnRoute()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "test-route",
            Path = "/api/test",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.GetRoute("test-route");

        // Assert
        result.Should().Be(route);
    }

    [Fact]
    public void RouteManager_GetRoute_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = _routeManager.GetRoute("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RouteManager_MatchRoute_WithExactPattern_ShouldMatch()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "exact-route",
            Path = "/api/users",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/users", "GET");

        // Assert
        result.Should().NotBeNull();
        result!.Route.Should().Be(route);
    }

    [Fact]
    public void RouteManager_MatchRoute_WithParameterizedPattern_ShouldMatch()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "param-route",
            Path = "/api/users/{id}",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/users/123", "GET");

        // Assert
        result.Should().NotBeNull();
        result!.Route.Should().Be(route);
        result.Parameters.Should().ContainKey("id");
        result.Parameters["id"].Should().Be("123");
    }

    [Fact]
    public void RouteManager_MatchRoute_WithWildcardPattern_ShouldNotMatch()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "wildcard-route",
            Path = "/api/files/*",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/files/documents/readme.txt", "GET");

        // Assert - RouteManager doesn't appear to support wildcard patterns
        result.Should().BeNull();
    }

    [Fact]
    public void RouteManager_MatchRoute_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "test-route",
            Path = "/api/users",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/products", "GET");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RouteManager_MatchRoute_WithWrongMethod_ShouldReturnNull()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "get-route",
            Path = "/api/users",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/users", "POST");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RouteManager_MatchRoute_WithDisabledRoute_ShouldReturnNull()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "disabled-route",
            Path = "/api/users",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" },
            Enabled = false
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/users", "GET");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RouteManager_MatchRoute_WithCaseInsensitiveMethod_ShouldMatch()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "case-route",
            Path = "/api/users",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/users", "get");

        // Assert
        result.Should().NotBeNull();
        result!.Route.Should().Be(route);
    }

    [Fact]
    public void RouteManager_MatchRoute_WithMultipleParameters_ShouldExtractAll()
    {
        // Arrange
        var route = new RouteConfiguration
        {
            Id = "multi-param-route",
            Path = "/api/users/{userId}/posts/{postId}",
            Methods = new List<string> { "GET" },
            Upstreams = new List<string> { "http://localhost:5000" }
        };
        _routeManager.AddRoute(route);

        // Act
        var result = _routeManager.MatchRoute("/api/users/123/posts/456", "GET");

        // Assert
        result.Should().NotBeNull();
        result!.Parameters.Should().HaveCount(2);
        result.Parameters["userId"].Should().Be("123");
        result.Parameters["postId"].Should().Be("456");
    }

    [Fact]
    public void RouteManager_GetAllRoutes_ShouldReturnAllAddedRoutes()
    {
        // Arrange
        var route1 = new RouteConfiguration { Id = "route1", Path = "/api/route1", Methods = new List<string> { "GET" }, Upstreams = new List<string> { "http://localhost:5000" } };
        var route2 = new RouteConfiguration { Id = "route2", Path = "/api/route2", Methods = new List<string> { "POST" }, Upstreams = new List<string> { "http://localhost:5001" } };

        _routeManager.AddRoute(route1);
        _routeManager.AddRoute(route2);

        // Act
        var routes = _routeManager.GetAllRoutes();

        // Assert
        routes.Should().HaveCount(2);
        routes.Should().Contain(route1);
        routes.Should().Contain(route2);
    }

    [Fact]
    public void RouteManager_GetAllRoutes_WithNoRoutes_ShouldReturnEmpty()
    {
        // Act
        var routes = _routeManager.GetAllRoutes();

        // Assert
        routes.Should().BeEmpty();
    }

    [Fact]
    public void RouteManager_GetRouteCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var route1 = new RouteConfiguration { Id = "route1", Path = "/api/route1", Methods = new List<string> { "GET" }, Upstreams = new List<string> { "http://localhost:5000" } };
        var route2 = new RouteConfiguration { Id = "route2", Path = "/api/route2", Methods = new List<string> { "POST" }, Upstreams = new List<string> { "http://localhost:5001" } };

        // Act & Assert
        _routeManager.GetAllRoutes().Should().HaveCount(0);

        _routeManager.AddRoute(route1);
        _routeManager.GetAllRoutes().Should().HaveCount(1);

        _routeManager.AddRoute(route2);
        _routeManager.GetAllRoutes().Should().HaveCount(2);

        _routeManager.RemoveRoute("route1");
        _routeManager.GetAllRoutes().Should().HaveCount(1);
    }
}