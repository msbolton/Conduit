using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Security
{
    /// <summary>
    /// Access control service implementing Role-Based Access Control (RBAC)
    /// and Attribute-Based Access Control (ABAC).
    /// </summary>
    public class AccessControl : IAccessControl
    {
        private readonly AccessControlOptions _options;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, Role> _roles;
        private readonly ConcurrentDictionary<string, Permission> _permissions;
        private readonly ConcurrentDictionary<string, Policy> _policies;
        private readonly ConcurrentDictionary<string, CachedAuthorizationResult> _cache;

        /// <summary>
        /// Initializes a new instance of the AccessControl class.
        /// </summary>
        public AccessControl(
            AccessControlOptions? options = null,
            ILogger<AccessControl>? logger = null)
        {
            _options = options ?? new AccessControlOptions();
            _logger = logger;
            _roles = new ConcurrentDictionary<string, Role>();
            _permissions = new ConcurrentDictionary<string, Permission>();
            _policies = new ConcurrentDictionary<string, Policy>();
            _cache = new ConcurrentDictionary<string, CachedAuthorizationResult>();

            InitializeDefaultRolesAndPermissions();
        }

        /// <summary>
        /// Checks if a principal is authorized to perform an action on a resource.
        /// </summary>
        public Task<AuthorizationResult> AuthorizeAsync(
            IPrincipal principal,
            string action,
            string? resource = null,
            IDictionary<string, object>? context = null,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(principal, nameof(principal));
            Guard.AgainstNullOrEmpty(action, nameof(action));

            try
            {
                // Check cache if enabled
                if (_options.EnableCaching)
                {
                    var cacheKey = CreateCacheKey(principal, action, resource);
                    if (_cache.TryGetValue(cacheKey, out var cachedResult) &&
                        cachedResult.ExpiresAt > DateTime.UtcNow)
                    {
                        _logger?.LogDebug("Authorization cache hit for {Principal}/{Action}/{Resource}",
                            principal.Identity?.Name, action, resource);
                        return Task.FromResult(cachedResult.Result);
                    }
                }

                var result = EvaluateAuthorization(principal, action, resource, context);

                // Cache result if enabled
                if (_options.EnableCaching && result.IsAuthorized)
                {
                    var cacheKey = CreateCacheKey(principal, action, resource);
                    _cache[cacheKey] = new CachedAuthorizationResult
                    {
                        Result = result,
                        ExpiresAt = DateTime.UtcNow.Add(_options.CacheDuration)
                    };
                }

                _logger?.LogInformation("Authorization {Result} for {Principal} to {Action} on {Resource}",
                    result.IsAuthorized ? "granted" : "denied",
                    principal.Identity?.Name,
                    action,
                    resource ?? "any");

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Authorization check failed");
                return Task.FromResult(AuthorizationResult.Deny("Authorization check failed"));
            }
        }

        /// <summary>
        /// Checks if a principal has a specific role.
        /// </summary>
        public bool HasRole(IPrincipal principal, string roleName)
        {
            Guard.AgainstNull(principal, nameof(principal));
            Guard.AgainstNullOrEmpty(roleName, nameof(roleName));

            if (principal is ClaimsPrincipal claimsPrincipal)
            {
                return claimsPrincipal.IsInRole(roleName);
            }

            return principal.IsInRole(roleName);
        }

        /// <summary>
        /// Checks if a principal has any of the specified roles.
        /// </summary>
        public bool HasAnyRole(IPrincipal principal, params string[] roles)
        {
            return roles.Any(role => HasRole(principal, role));
        }

        /// <summary>
        /// Checks if a principal has all of the specified roles.
        /// </summary>
        public bool HasAllRoles(IPrincipal principal, params string[] roles)
        {
            return roles.All(role => HasRole(principal, role));
        }

        /// <summary>
        /// Gets all roles for a principal.
        /// </summary>
        public IEnumerable<string> GetRoles(IPrincipal principal)
        {
            Guard.AgainstNull(principal, nameof(principal));

            if (principal is ClaimsPrincipal claimsPrincipal)
            {
                return claimsPrincipal.FindAll(ClaimTypes.Role).Select(c => c.Value);
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Gets all permissions for a principal.
        /// </summary>
        public IEnumerable<string> GetPermissions(IPrincipal principal)
        {
            Guard.AgainstNull(principal, nameof(principal));

            var permissions = new HashSet<string>();

            // Get direct permissions from claims
            if (principal is ClaimsPrincipal claimsPrincipal)
            {
                var permissionClaims = claimsPrincipal.FindAll("permission")
                    .Concat(claimsPrincipal.FindAll("permissions"))
                    .Concat(claimsPrincipal.FindAll("scope"))
                    .Concat(claimsPrincipal.FindAll("scopes"));

                foreach (var claim in permissionClaims)
                {
                    var perms = claim.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var perm in perms)
                    {
                        permissions.Add(perm.Trim());
                    }
                }
            }

            // Get permissions from roles
            var roles = GetRoles(principal);
            foreach (var roleName in roles)
            {
                if (_roles.TryGetValue(roleName, out var role))
                {
                    foreach (var permission in role.Permissions)
                    {
                        permissions.Add(permission);
                    }
                }
            }

            return permissions;
        }

        /// <summary>
        /// Defines a new role.
        /// </summary>
        public void DefineRole(string name, string description, params string[] permissions)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));

            var role = new Role
            {
                Name = name,
                Description = description,
                Permissions = new HashSet<string>(permissions ?? Array.Empty<string>())
            };

            _roles[name] = role;
            _logger?.LogInformation("Defined role {RoleName} with {PermissionCount} permissions",
                name, role.Permissions.Count);
        }

        /// <summary>
        /// Adds a permission to a role.
        /// </summary>
        public void AddPermissionToRole(string roleName, string permission)
        {
            Guard.AgainstNullOrEmpty(roleName, nameof(roleName));
            Guard.AgainstNullOrEmpty(permission, nameof(permission));

            if (!_roles.TryGetValue(roleName, out var role))
            {
                throw new InvalidOperationException($"Role {roleName} not found");
            }

            role.Permissions.Add(permission);
            InvalidateCache();
        }

        /// <summary>
        /// Removes a permission from a role.
        /// </summary>
        public void RemovePermissionFromRole(string roleName, string permission)
        {
            Guard.AgainstNullOrEmpty(roleName, nameof(roleName));
            Guard.AgainstNullOrEmpty(permission, nameof(permission));

            if (_roles.TryGetValue(roleName, out var role))
            {
                role.Permissions.Remove(permission);
                InvalidateCache();
            }
        }

        /// <summary>
        /// Defines a new permission.
        /// </summary>
        public void DefinePermission(string name, string description, string? resource = null)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));

            var permission = new Permission
            {
                Name = name,
                Description = description,
                Resource = resource
            };

            _permissions[name] = permission;
            _logger?.LogInformation("Defined permission {PermissionName}", name);
        }

        /// <summary>
        /// Defines an authorization policy.
        /// </summary>
        public void DefinePolicy(string name, Func<IPrincipal, IDictionary<string, object>?, bool> evaluator)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(evaluator, nameof(evaluator));

            var policy = new Policy
            {
                Name = name,
                Evaluator = evaluator
            };

            _policies[name] = policy;
            _logger?.LogInformation("Defined policy {PolicyName}", name);
        }

        /// <summary>
        /// Evaluates a policy.
        /// </summary>
        public bool EvaluatePolicy(string policyName, IPrincipal principal, IDictionary<string, object>? context = null)
        {
            Guard.AgainstNullOrEmpty(policyName, nameof(policyName));
            Guard.AgainstNull(principal, nameof(principal));

            if (!_policies.TryGetValue(policyName, out var policy))
            {
                _logger?.LogWarning("Policy {PolicyName} not found", policyName);
                return false;
            }

            return policy.Evaluator(principal, context);
        }

        /// <summary>
        /// Invalidates the authorization cache.
        /// </summary>
        public void InvalidateCache()
        {
            _cache.Clear();
            _logger?.LogInformation("Authorization cache invalidated");
        }

        /// <summary>
        /// Gets authorization statistics.
        /// </summary>
        public AccessControlStatistics GetStatistics()
        {
            return new AccessControlStatistics
            {
                RoleCount = _roles.Count,
                PermissionCount = _permissions.Count,
                PolicyCount = _policies.Count,
                CacheSize = _cache.Count,
                Roles = _roles.Keys.ToList(),
                Permissions = _permissions.Keys.ToList()
            };
        }

        private AuthorizationResult EvaluateAuthorization(
            IPrincipal principal,
            string action,
            string? resource,
            IDictionary<string, object>? context)
        {
            // Check if user is authenticated
            if (principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return AuthorizationResult.Deny("User is not authenticated");
            }

            // Build required permission from action and resource
            var requiredPermission = resource != null
                ? $"{action}:{resource}"
                : action;

            // Get user permissions
            var userPermissions = GetPermissions(principal);

            // Check for exact permission match
            if (userPermissions.Contains(requiredPermission))
            {
                return AuthorizationResult.Allow();
            }

            // Check for wildcard permission match
            if (CheckWildcardPermissions(userPermissions, requiredPermission))
            {
                return AuthorizationResult.Allow();
            }

            // Check for admin role (if configured to bypass checks)
            if (_options.AdminRoleBypassesChecks &&
                HasRole(principal, _options.AdminRoleName))
            {
                return AuthorizationResult.Allow();
            }

            // Evaluate custom policies if any match the action
            var policyName = $"policy:{action}";
            if (_policies.ContainsKey(policyName))
            {
                if (EvaluatePolicy(policyName, principal, context))
                {
                    return AuthorizationResult.Allow();
                }
            }

            return AuthorizationResult.Deny($"User does not have permission: {requiredPermission}");
        }

        private bool CheckWildcardPermissions(IEnumerable<string> userPermissions, string requiredPermission)
        {
            foreach (var permission in userPermissions)
            {
                // Check for full wildcard
                if (permission == "*")
                {
                    return true;
                }

                // Check for action wildcard (e.g., "read:*")
                if (permission.EndsWith(":*"))
                {
                    var permissionAction = permission.Substring(0, permission.Length - 2);
                    if (requiredPermission.StartsWith(permissionAction + ":"))
                    {
                        return true;
                    }
                }

                // Check for resource wildcard (e.g., "*:users")
                if (permission.StartsWith("*:"))
                {
                    var permissionResource = permission.Substring(2);
                    if (requiredPermission.EndsWith(":" + permissionResource))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string CreateCacheKey(IPrincipal principal, string action, string? resource)
        {
            var principalId = principal.Identity?.Name ?? "anonymous";
            return $"{principalId}|{action}|{resource ?? "null"}";
        }

        private void InitializeDefaultRolesAndPermissions()
        {
            // Define standard permissions
            DefinePermission("read", "Read access");
            DefinePermission("write", "Write access");
            DefinePermission("delete", "Delete access");
            DefinePermission("admin", "Administrative access");

            // Define standard roles
            DefineRole("Administrator", "Full system access", "*");
            DefineRole("User", "Standard user access", "read");
            DefineRole("Guest", "Guest access");
        }

        private class Role
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public HashSet<string> Permissions { get; set; } = new();
        }

        private class Permission
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? Resource { get; set; }
        }

        private class Policy
        {
            public string Name { get; set; } = string.Empty;
            public Func<IPrincipal, IDictionary<string, object>?, bool> Evaluator { get; set; } = null!;
        }

        private class CachedAuthorizationResult
        {
            public AuthorizationResult Result { get; set; } = null!;
            public DateTime ExpiresAt { get; set; }
        }
    }

    /// <summary>
    /// Result of an authorization check.
    /// </summary>
    public class AuthorizationResult
    {
        /// <summary>
        /// Gets whether authorization was granted.
        /// </summary>
        public bool IsAuthorized { get; set; }

        /// <summary>
        /// Gets the reason for denial (if denied).
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets additional metadata about the authorization.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Creates an authorized result.
        /// </summary>
        public static AuthorizationResult Allow()
        {
            return new AuthorizationResult { IsAuthorized = true };
        }

        /// <summary>
        /// Creates a denied result.
        /// </summary>
        public static AuthorizationResult Deny(string reason)
        {
            return new AuthorizationResult
            {
                IsAuthorized = false,
                Reason = reason
            };
        }
    }

    /// <summary>
    /// Options for access control.
    /// </summary>
    public class AccessControlOptions
    {
        /// <summary>
        /// Gets or sets whether to enable authorization caching.
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache duration.
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether the admin role bypasses authorization checks.
        /// </summary>
        public bool AdminRoleBypassesChecks { get; set; } = true;

        /// <summary>
        /// Gets or sets the admin role name.
        /// </summary>
        public string AdminRoleName { get; set; } = "Administrator";

        /// <summary>
        /// Gets or sets the authorization mode (RBAC or ABAC).
        /// </summary>
        public AuthorizationMode Mode { get; set; } = AuthorizationMode.Rbac;
    }

    /// <summary>
    /// Authorization modes.
    /// </summary>
    public enum AuthorizationMode
    {
        /// <summary>Role-Based Access Control</summary>
        Rbac,

        /// <summary>Attribute-Based Access Control</summary>
        Abac,

        /// <summary>Hybrid mode (both RBAC and ABAC)</summary>
        Hybrid
    }

    /// <summary>
    /// Access control statistics.
    /// </summary>
    public class AccessControlStatistics
    {
        public int RoleCount { get; set; }
        public int PermissionCount { get; set; }
        public int PolicyCount { get; set; }
        public int CacheSize { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }
}
