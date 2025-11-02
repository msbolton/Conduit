using System.Security.Principal;

namespace Conduit.Security;

/// <summary>
/// Interface for access control service implementing Role-Based Access Control (RBAC)
/// and Attribute-Based Access Control (ABAC).
/// </summary>
public interface IAccessControl
{
    /// <summary>
    /// Checks if a principal is authorized to perform an action on a resource.
    /// </summary>
    Task<AuthorizationResult> AuthorizeAsync(
        IPrincipal principal,
        string action,
        string? resource = null,
        IDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a principal has a specific role.
    /// </summary>
    bool HasRole(IPrincipal principal, string roleName);

    /// <summary>
    /// Checks if a principal has any of the specified roles.
    /// </summary>
    bool HasAnyRole(IPrincipal principal, params string[] roles);

    /// <summary>
    /// Checks if a principal has all of the specified roles.
    /// </summary>
    bool HasAllRoles(IPrincipal principal, params string[] roles);

    /// <summary>
    /// Gets all roles for a principal.
    /// </summary>
    IEnumerable<string> GetRoles(IPrincipal principal);

    /// <summary>
    /// Gets all permissions for a principal.
    /// </summary>
    IEnumerable<string> GetPermissions(IPrincipal principal);

    /// <summary>
    /// Defines a role with the specified permissions.
    /// </summary>
    void DefineRole(string name, string description, params string[] permissions);

    /// <summary>
    /// Adds a permission to an existing role.
    /// </summary>
    void AddPermissionToRole(string roleName, string permission);

    /// <summary>
    /// Removes a permission from an existing role.
    /// </summary>
    void RemovePermissionFromRole(string roleName, string permission);

    /// <summary>
    /// Defines a permission.
    /// </summary>
    void DefinePermission(string name, string description, string? resource = null);

    /// <summary>
    /// Defines a custom policy.
    /// </summary>
    void DefinePolicy(string name, Func<IPrincipal, IDictionary<string, object>?, bool> evaluator);

    /// <summary>
    /// Evaluates a custom policy.
    /// </summary>
    bool EvaluatePolicy(string policyName, IPrincipal principal, IDictionary<string, object>? context = null);

    /// <summary>
    /// Invalidates the authorization cache.
    /// </summary>
    void InvalidateCache();
}