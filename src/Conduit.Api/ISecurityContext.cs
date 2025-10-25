namespace Conduit.Api;

/// <summary>
/// Represents the security context for component execution.
/// </summary>
public interface ISecurityContext
{
    /// <summary>
    /// Gets the authenticated user identity.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the authenticated user name.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets the roles assigned to the user.
    /// </summary>
    IReadOnlySet<string> Roles { get; }

    /// <summary>
    /// Gets the claims associated with the user.
    /// </summary>
    IReadOnlyDictionary<string, object> Claims { get; }

    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the authentication scheme used.
    /// </summary>
    string? AuthenticationScheme { get; }

    /// <summary>
    /// Gets the security token if available.
    /// </summary>
    string? Token { get; }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    /// <param name="role">The role to check</param>
    /// <returns>True if the user has the role, false otherwise</returns>
    bool IsInRole(string role);

    /// <summary>
    /// Checks if the user has a specific claim.
    /// </summary>
    /// <param name="claimType">The claim type to check</param>
    /// <returns>True if the user has the claim, false otherwise</returns>
    bool HasClaim(string claimType);

    /// <summary>
    /// Gets a claim value.
    /// </summary>
    /// <typeparam name="T">The type to convert the claim value to</typeparam>
    /// <param name="claimType">The claim type</param>
    /// <param name="defaultValue">The default value if claim not found</param>
    /// <returns>The claim value or default</returns>
    T GetClaimValue<T>(string claimType, T defaultValue = default!);

    /// <summary>
    /// Checks if the user has permission for a specific action.
    /// </summary>
    /// <param name="permission">The permission to check</param>
    /// <returns>True if the user has permission, false otherwise</returns>
    bool HasPermission(string permission);

    /// <summary>
    /// Gets the tenant ID for multi-tenant scenarios.
    /// </summary>
    string? TenantId { get; }
}