using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Conduit.Api;
using Conduit.Common;

namespace Conduit.Security
{
    /// <summary>
    /// Implementation of security context for component authentication and authorization.
    /// </summary>
    public class SecurityContext : ISecurityContext
    {
        private readonly ClaimsPrincipal _principal;
        private readonly string? _authenticationToken;
        private readonly ConcurrentDictionary<string, object> _claims;
        private readonly HashSet<string> _roles;
        private readonly HashSet<string> _permissions;
        private readonly string? _tenantId;

        /// <summary>
        /// Initializes a new instance of the SecurityContext class.
        /// </summary>
        public SecurityContext(
            ClaimsPrincipal principal,
            string? authenticationToken = null,
            string? tenantId = null)
        {
            _principal = principal ?? throw new ArgumentNullException(nameof(principal));
            _authenticationToken = authenticationToken;
            _tenantId = tenantId;
            _claims = new ConcurrentDictionary<string, object>();
            _roles = new HashSet<string>();
            _permissions = new HashSet<string>();

            // Extract roles from claims
            ExtractRolesFromClaims();

            // Extract permissions from claims
            ExtractPermissionsFromClaims();

            // Extract custom claims
            ExtractCustomClaims();
        }

        public IPrincipal GetPrincipal()
        {
            return _principal;
        }

        public bool IsUserAuthenticated()
        {
            return _principal.Identity?.IsAuthenticated ?? false;
        }

        public bool HasRole(string role)
        {
            Guard.AgainstNullOrEmpty(role, nameof(role));
            return _roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> GetRoles()
        {
            return _roles.ToArray();
        }

        public bool HasPermission(string permission)
        {
            Guard.AgainstNullOrEmpty(permission, nameof(permission));
            return _permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> GetPermissions()
        {
            return _permissions.ToArray();
        }

        public string? GetAuthenticationToken()
        {
            return _authenticationToken;
        }

        public object? GetClaim(string claimName)
        {
            Guard.AgainstNullOrEmpty(claimName, nameof(claimName));

            if (_claims.TryGetValue(claimName, out var value))
            {
                return value;
            }

            // Try to get from ClaimsPrincipal
            var claim = _principal.FindFirst(claimName);
            return claim?.Value;
        }

        public string? GetTenantId()
        {
            return _tenantId;
        }

        // ISecurityContext interface implementation
        public string? UserId => GetUserId();
        public string? UserName => GetUserName();
        public IReadOnlySet<string> Roles => _roles.ToHashSet();
        public IReadOnlyDictionary<string, object> Claims => _claims.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        public bool IsAuthenticated => _principal.Identity?.IsAuthenticated ?? false;
        public string? AuthenticationScheme => _principal.Identity?.AuthenticationType;
        public string? Token => GetAuthenticationToken();
        public string? TenantId => GetTenantId();

        public bool IsInRole(string role) => HasRole(role);

        public bool HasClaim(string claimType)
        {
            return _claims.ContainsKey(claimType) || _principal.Claims.Any(c => c.Type == claimType);
        }

        public T GetClaimValue<T>(string claimType, T defaultValue = default!)
        {
            var claimValue = GetClaim(claimType);
            if (claimValue == null) return defaultValue;

            try
            {
                if (typeof(T) == typeof(string))
                    return (T)(object)claimValue.ToString()!;

                return (T)Convert.ChangeType(claimValue, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Gets the user identifier from the principal.
        /// </summary>
        public string? GetUserId()
        {
            return _principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                   _principal.FindFirst("sub")?.Value;
        }

        /// <summary>
        /// Gets the user name from the principal.
        /// </summary>
        public string? GetUserName()
        {
            return _principal.Identity?.Name ??
                   _principal.FindFirst(ClaimTypes.Name)?.Value;
        }

        /// <summary>
        /// Gets the user email from the principal.
        /// </summary>
        public string? GetEmail()
        {
            return _principal.FindFirst(ClaimTypes.Email)?.Value ??
                   _principal.FindFirst("email")?.Value;
        }

        /// <summary>
        /// Adds a custom claim to the context.
        /// </summary>
        public void AddClaim(string claimName, object value)
        {
            Guard.AgainstNullOrEmpty(claimName, nameof(claimName));
            Guard.AgainstNull(value, nameof(value));

            _claims[claimName] = value;
        }

        /// <summary>
        /// Adds a role to the context.
        /// </summary>
        public void AddRole(string role)
        {
            Guard.AgainstNullOrEmpty(role, nameof(role));
            _roles.Add(role);
        }

        /// <summary>
        /// Adds a permission to the context.
        /// </summary>
        public void AddPermission(string permission)
        {
            Guard.AgainstNullOrEmpty(permission, nameof(permission));
            _permissions.Add(permission);
        }

        /// <summary>
        /// Checks if the context has any of the specified roles.
        /// </summary>
        public bool HasAnyRole(params string[] roles)
        {
            return roles.Any(role => HasRole(role));
        }

        /// <summary>
        /// Checks if the context has all of the specified roles.
        /// </summary>
        public bool HasAllRoles(params string[] roles)
        {
            return roles.All(role => HasRole(role));
        }

        /// <summary>
        /// Checks if the context has any of the specified permissions.
        /// </summary>
        public bool HasAnyPermission(params string[] permissions)
        {
            return permissions.Any(permission => HasPermission(permission));
        }

        /// <summary>
        /// Checks if the context has all of the specified permissions.
        /// </summary>
        public bool HasAllPermissions(params string[] permissions)
        {
            return permissions.All(permission => HasPermission(permission));
        }

        /// <summary>
        /// Creates an anonymous security context.
        /// </summary>
        public static SecurityContext Anonymous()
        {
            var identity = new ClaimsIdentity();
            var principal = new ClaimsPrincipal(identity);
            return new SecurityContext(principal);
        }

        /// <summary>
        /// Creates a security context from a ClaimsPrincipal.
        /// </summary>
        public static SecurityContext FromPrincipal(ClaimsPrincipal principal, string? token = null, string? tenantId = null)
        {
            return new SecurityContext(principal, token, tenantId);
        }

        /// <summary>
        /// Creates a security context with a simple user identity.
        /// </summary>
        public static SecurityContext CreateSimple(
            string userId,
            string userName,
            string[]? roles = null,
            string? tenantId = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName)
            };

            if (roles != null)
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            if (tenantId != null)
            {
                claims.Add(new Claim("tenant_id", tenantId));
            }

            var identity = new ClaimsIdentity(claims, "Simple");
            var principal = new ClaimsPrincipal(identity);

            return new SecurityContext(principal, null, tenantId);
        }

        private void ExtractRolesFromClaims()
        {
            var roleClaims = _principal.FindAll(ClaimTypes.Role)
                .Concat(_principal.FindAll("role"))
                .Concat(_principal.FindAll("roles"));

            foreach (var claim in roleClaims)
            {
                // Handle comma-separated roles
                var roles = claim.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var role in roles)
                {
                    _roles.Add(role.Trim());
                }
            }
        }

        private void ExtractPermissionsFromClaims()
        {
            var permissionClaims = _principal.FindAll("permission")
                .Concat(_principal.FindAll("permissions"))
                .Concat(_principal.FindAll("scope"))
                .Concat(_principal.FindAll("scopes"));

            foreach (var claim in permissionClaims)
            {
                // Handle space or comma-separated permissions
                var permissions = claim.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var permission in permissions)
                {
                    _permissions.Add(permission.Trim());
                }
            }
        }

        private void ExtractCustomClaims()
        {
            // Extract tenant ID
            var tenantClaim = _principal.FindFirst("tenant_id") ??
                             _principal.FindFirst("tenantId") ??
                             _principal.FindFirst("tenant");

            if (tenantClaim != null && string.IsNullOrEmpty(_tenantId))
            {
                _claims["tenant_id"] = tenantClaim.Value;
            }

            // Extract other common claims
            var emailClaim = _principal.FindFirst(ClaimTypes.Email) ??
                            _principal.FindFirst("email");
            if (emailClaim != null)
            {
                _claims["email"] = emailClaim.Value;
            }
        }
    }

    /// <summary>
    /// Builder for creating security contexts.
    /// </summary>
    public class SecurityContextBuilder
    {
        private string? _userId;
        private string? _userName;
        private string? _email;
        private string? _tenantId;
        private string? _token;
        private readonly List<string> _roles = new();
        private readonly List<string> _permissions = new();
        private readonly Dictionary<string, object> _customClaims = new();

        public SecurityContextBuilder WithUserId(string userId)
        {
            _userId = userId;
            return this;
        }

        public SecurityContextBuilder WithUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        public SecurityContextBuilder WithEmail(string email)
        {
            _email = email;
            return this;
        }

        public SecurityContextBuilder WithTenantId(string tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        public SecurityContextBuilder WithToken(string token)
        {
            _token = token;
            return this;
        }

        public SecurityContextBuilder WithRole(string role)
        {
            _roles.Add(role);
            return this;
        }

        public SecurityContextBuilder WithRoles(params string[] roles)
        {
            _roles.AddRange(roles);
            return this;
        }

        public SecurityContextBuilder WithPermission(string permission)
        {
            _permissions.Add(permission);
            return this;
        }

        public SecurityContextBuilder WithPermissions(params string[] permissions)
        {
            _permissions.AddRange(permissions);
            return this;
        }

        public SecurityContextBuilder WithClaim(string claimType, object value)
        {
            _customClaims[claimType] = value;
            return this;
        }

        public SecurityContext Build()
        {
            var claims = new List<Claim>();

            if (_userId != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, _userId));
            }

            if (_userName != null)
            {
                claims.Add(new Claim(ClaimTypes.Name, _userName));
            }

            if (_email != null)
            {
                claims.Add(new Claim(ClaimTypes.Email, _email));
            }

            if (_tenantId != null)
            {
                claims.Add(new Claim("tenant_id", _tenantId));
            }

            foreach (var role in _roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            foreach (var permission in _permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            foreach (var claim in _customClaims)
            {
                claims.Add(new Claim(claim.Key, claim.Value.ToString() ?? ""));
            }

            var identity = new ClaimsIdentity(claims, "Custom");
            var principal = new ClaimsPrincipal(identity);

            var context = new SecurityContext(principal, _token, _tenantId);

            return context;
        }
    }
}