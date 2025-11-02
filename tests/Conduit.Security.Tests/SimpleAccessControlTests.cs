using FluentAssertions;
using Microsoft.Extensions.Logging;
using Conduit.Security;
using System.Security.Claims;
using System.Security.Principal;
using Moq;

namespace Conduit.Security.Tests;

public class SimpleAccessControlTests
{
    private readonly AccessControl _accessControl;
    private readonly Mock<ILogger<AccessControl>> _mockLogger;

    public SimpleAccessControlTests()
    {
        _mockLogger = new Mock<ILogger<AccessControl>>();
        var options = new AccessControlOptions
        {
            EnableCaching = true,
            CacheDuration = TimeSpan.FromMinutes(5)
        };
        _accessControl = new AccessControl(options, _mockLogger.Object);
    }

    [Fact]
    public async Task AuthorizeAsync_WithAdminRole_ShouldReturnAuthorized()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Administrator")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _accessControl.AuthorizeAsync(principal, "read");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithUserRole_ShouldReturnAuthorizedForRead()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim(ClaimTypes.Role, "User")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _accessControl.AuthorizeAsync(principal, "read");

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizeAsync_WithUserRoleForWrite_ShouldReturnDenied()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim(ClaimTypes.Role, "User")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = await _accessControl.AuthorizeAsync(principal, "write");

        // Assert
        result.IsAuthorized.Should().BeFalse();
    }

    [Fact]
    public void HasRole_WithValidRole_ShouldReturnTrue()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim(ClaimTypes.Role, "User")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var result = _accessControl.HasRole(principal, "User");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetRoles_ShouldReturnUserRoles()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "user"),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ClaimTypes.Role, "Guest")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var roles = _accessControl.GetRoles(principal);

        // Assert
        roles.Should().Contain("User");
        roles.Should().Contain("Guest");
    }

    [Fact]
    public void DefineRole_ShouldCreateNewRole()
    {
        // Arrange
        var roleName = "TestRole";
        var permissions = new[] { "read", "write" };

        // Act
        _accessControl.DefineRole(roleName, "Test role", permissions);

        // Assert - verify role was created by checking statistics
        var stats = _accessControl.GetStatistics();
        stats.Roles.Should().Contain(roleName);
    }

    [Fact]
    public void DefinePermission_ShouldCreateNewPermission()
    {
        // Arrange
        var permissionName = "test_permission";

        // Act
        _accessControl.DefinePermission(permissionName, "Test permission");

        // Assert
        var stats = _accessControl.GetStatistics();
        stats.Permissions.Should().Contain(permissionName);
    }

    [Fact]
    public void GetStatistics_ShouldReturnValidStatistics()
    {
        // Act
        var stats = _accessControl.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.RoleCount.Should().BeGreaterThan(0); // Default roles should exist
        stats.PermissionCount.Should().BeGreaterThan(0); // Default permissions should exist
    }
}