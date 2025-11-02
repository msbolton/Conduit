using FluentAssertions;
using Conduit.Security;
using System.Security.Claims;

namespace Conduit.Security.Tests;

public class SimpleSecurityContextTests
{
    [Fact]
    public void SecurityContext_WithValidPrincipal_ShouldCreateCorrectly()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "User")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var context = new SecurityContext(principal);

        // Assert
        context.Should().NotBeNull();
        context.GetPrincipal().Should().Be(principal);
        context.IsAuthenticated.Should().BeTrue();
        context.UserName.Should().Be("testuser");
    }

    [Fact]
    public void SecurityContext_WithUnauthenticatedPrincipal_ShouldReturnFalse()
    {
        // Arrange
        var principal = new ClaimsPrincipal();

        // Act
        var context = new SecurityContext(principal);

        // Assert
        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void SecurityContext_WithTenantId_ShouldSetCorrectly()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Name, "testuser") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var tenantId = "tenant123";

        // Act
        var context = new SecurityContext(principal, tenantId: tenantId);

        // Assert
        context.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void SecurityContext_HasRole_ShouldReturnCorrectly()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "User")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var context = new SecurityContext(principal);

        // Assert
        context.HasRole("User").Should().BeTrue();
        context.HasRole("Admin").Should().BeFalse();
    }

    [Fact]
    public void SecurityContext_GetRoles_ShouldReturnAllRoles()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ClaimTypes.Role, "Manager")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        // Act
        var context = new SecurityContext(principal);

        // Assert
        var roles = context.GetRoles();
        roles.Should().Contain("User");
        roles.Should().Contain("Manager");
    }

    [Fact]
    public void SecurityContext_CreateSimple_ShouldWork()
    {
        // Act
        var context = SecurityContext.CreateSimple("user123", "testuser", new[] { "User" });

        // Assert
        context.UserId.Should().Be("user123");
        context.UserName.Should().Be("testuser");
        context.HasRole("User").Should().BeTrue();
        context.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void SecurityContext_Anonymous_ShouldCreateUnauthenticated()
    {
        // Act
        var context = SecurityContext.Anonymous();

        // Assert
        context.IsAuthenticated.Should().BeFalse();
        context.UserId.Should().BeNull();
    }
}