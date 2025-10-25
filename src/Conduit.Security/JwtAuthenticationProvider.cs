using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Conduit.Security
{
    /// <summary>
    /// JWT-based authentication provider.
    /// Implements token-based authentication using JSON Web Tokens (RFC 7519).
    /// </summary>
    public class JwtAuthenticationProvider : IAuthenticationProvider
    {
        private readonly JwtOptions _options;
        private readonly ILogger? _logger;
        private readonly JwtSecurityTokenHandler _tokenHandler;
        private readonly ConcurrentDictionary<string, RevokedToken> _revokedTokens;
        private readonly ConcurrentDictionary<string, RefreshTokenInfo> _refreshTokens;
        private readonly ConcurrentDictionary<string, LoginAttemptTracker> _loginAttempts;

        /// <inheritdoc/>
        public string ProviderName => "JWT";

        /// <summary>
        /// Initializes a new instance of the JwtAuthenticationProvider class.
        /// </summary>
        public JwtAuthenticationProvider(
            JwtOptions options,
            ILogger<JwtAuthenticationProvider>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _tokenHandler = new JwtSecurityTokenHandler();
            _revokedTokens = new ConcurrentDictionary<string, RevokedToken>();
            _refreshTokens = new ConcurrentDictionary<string, RefreshTokenInfo>();
            _loginAttempts = new ConcurrentDictionary<string, LoginAttemptTracker>();

            ValidateOptions();
        }

        /// <inheritdoc/>
        public async Task<AuthenticationResult> AuthenticateAsync(
            IDictionary<string, object> credentials,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(credentials, nameof(credentials));

            try
            {
                // Extract username from credentials
                if (!credentials.TryGetValue("username", out var usernameObj) ||
                    usernameObj is not string username)
                {
                    return AuthenticationResult.Failed(
                        "Username is required",
                        AuthenticationErrorCodes.InvalidCredentials);
                }

                // Check for account lockout
                if (IsAccountLocked(username))
                {
                    _logger?.LogWarning("Authentication failed: Account {Username} is locked", username);
                    return AuthenticationResult.Failed(
                        "Account is locked due to too many failed login attempts",
                        AuthenticationErrorCodes.AccountLocked);
                }

                // Validate credentials (in real implementation, this would check against a user store)
                var validationResult = await ValidateCredentialsAsync(credentials, cancellationToken);
                if (!validationResult.IsValid)
                {
                    RecordFailedLoginAttempt(username);
                    return AuthenticationResult.Failed(
                        validationResult.ErrorMessage ?? "Invalid credentials",
                        AuthenticationErrorCodes.InvalidCredentials);
                }

                // Reset login attempts on successful authentication
                ResetLoginAttempts(username);

                // Create claims for the user
                var claims = CreateClaimsFromCredentials(credentials, validationResult);

                // Create security context
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "JWT"));
                var context = CreateSecurityContext(principal, null, validationResult.TenantId);

                // Generate JWT token
                var token = GenerateToken(claims);
                var expiresAt = DateTime.UtcNow.Add(_options.TokenExpiration);

                // Generate refresh token if enabled
                string? refreshToken = null;
                if (_options.EnableRefreshTokens)
                {
                    refreshToken = GenerateRefreshToken(username, validationResult.TenantId);
                }

                _logger?.LogInformation("User {Username} authenticated successfully", username);

                return AuthenticationResult.Succeeded(context, token, refreshToken, expiresAt);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Authentication failed with exception");
                return AuthenticationResult.Failed("Authentication failed", "AUTHENTICATION_ERROR");
            }
        }

        /// <inheritdoc/>
        public async Task<TokenValidationResult> ValidateTokenAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(token, nameof(token));

            try
            {
                // Check if token is revoked
                if (await IsTokenRevokedAsync(token, cancellationToken))
                {
                    return TokenValidationResult.Invalid(
                        "Token has been revoked",
                        AuthenticationErrorCodes.TokenRevoked);
                }

                // Validate token
                var validationParameters = CreateValidationParameters();
                var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken)
                {
                    return TokenValidationResult.Invalid(
                        "Invalid token format",
                        AuthenticationErrorCodes.TokenInvalid);
                }

                // Extract tenant ID if present
                var tenantId = jwtToken.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;

                // Create security context
                var context = CreateSecurityContext(principal, token, tenantId);

                var expiresAt = jwtToken.ValidTo;

                _logger?.LogDebug("Token validated successfully for user {User}", principal.Identity?.Name);

                return TokenValidationResult.Valid(principal, context, expiresAt);
            }
            catch (SecurityTokenExpiredException)
            {
                return TokenValidationResult.Invalid(
                    "Token has expired",
                    AuthenticationErrorCodes.TokenExpired);
            }
            catch (SecurityTokenException ex)
            {
                _logger?.LogWarning(ex, "Token validation failed");
                return TokenValidationResult.Invalid(
                    "Token validation failed",
                    AuthenticationErrorCodes.TokenInvalid);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Token validation failed with exception");
                return TokenValidationResult.Invalid(
                    "Token validation failed",
                    AuthenticationErrorCodes.TokenInvalid);
            }
        }

        /// <inheritdoc/>
        public async Task<AuthenticationResult> RefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(refreshToken, nameof(refreshToken));

            try
            {
                if (!_options.EnableRefreshTokens)
                {
                    return AuthenticationResult.Failed(
                        "Refresh tokens are not enabled",
                        "REFRESH_NOT_ENABLED");
                }

                // Validate refresh token
                if (!_refreshTokens.TryGetValue(refreshToken, out var refreshInfo))
                {
                    _logger?.LogWarning("Invalid refresh token");
                    return AuthenticationResult.Failed(
                        "Invalid refresh token",
                        AuthenticationErrorCodes.RefreshTokenInvalid);
                }

                if (refreshInfo.ExpiresAt < DateTime.UtcNow)
                {
                    _refreshTokens.TryRemove(refreshToken, out _);
                    return AuthenticationResult.Failed(
                        "Refresh token has expired",
                        AuthenticationErrorCodes.RefreshTokenExpired);
                }

                // Create new claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, refreshInfo.UserId),
                    new Claim(ClaimTypes.Name, refreshInfo.Username),
                    new Claim("token_type", "access")
                };

                if (!string.IsNullOrEmpty(refreshInfo.TenantId))
                {
                    claims.Add(new Claim("tenant_id", refreshInfo.TenantId));
                }

                // Add stored roles
                claims.AddRange(refreshInfo.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

                // Generate new access token
                var token = GenerateToken(claims);
                var expiresAt = DateTime.UtcNow.Add(_options.TokenExpiration);

                // Create security context
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "JWT"));
                var context = CreateSecurityContext(principal, token, refreshInfo.TenantId);

                // Optionally rotate refresh token
                string? newRefreshToken = null;
                if (_options.RotateRefreshTokens)
                {
                    _refreshTokens.TryRemove(refreshToken, out _);
                    newRefreshToken = GenerateRefreshToken(refreshInfo.Username, refreshInfo.TenantId);
                }
                else
                {
                    newRefreshToken = refreshToken;
                }

                _logger?.LogInformation("Token refreshed for user {Username}", refreshInfo.Username);

                return AuthenticationResult.Succeeded(context, token, newRefreshToken, expiresAt);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Token refresh failed with exception");
                return AuthenticationResult.Failed("Token refresh failed", "REFRESH_ERROR");
            }
        }

        /// <inheritdoc/>
        public Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(token, nameof(token));

            try
            {
                var jwtToken = _tokenHandler.ReadJwtToken(token);
                var expiresAt = jwtToken.ValidTo;

                _revokedTokens.TryAdd(token, new RevokedToken
                {
                    Token = token,
                    RevokedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt
                });

                _logger?.LogInformation("Token revoked");

                // Clean up expired revoked tokens
                CleanupExpiredRevokedTokens();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to revoke token");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> IsTokenRevokedAsync(string token, CancellationToken cancellationToken = default)
        {
            var isRevoked = _revokedTokens.ContainsKey(token);
            return Task.FromResult(isRevoked);
        }

        /// <inheritdoc/>
        public SecurityContext CreateSecurityContext(
            ClaimsPrincipal principal,
            string? token = null,
            string? tenantId = null)
        {
            return SecurityContext.FromPrincipal(principal, token, tenantId);
        }

        private string GenerateToken(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.Add(_options.TokenExpiration),
                signingCredentials: credentials
            );

            return _tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken(string username, string? tenantId)
        {
            var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                              Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            var refreshInfo = new RefreshTokenInfo
            {
                Token = refreshToken,
                UserId = Guid.NewGuid().ToString(), // In real implementation, get from user store
                Username = username,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_options.RefreshTokenExpiration),
                Roles = new List<string>() // In real implementation, get from user store
            };

            _refreshTokens.TryAdd(refreshToken, refreshInfo);

            return refreshToken;
        }

        private TokenValidationParameters CreateValidationParameters()
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));

            return new TokenValidationParameters
            {
                ValidateIssuer = _options.ValidateIssuer,
                ValidateAudience = _options.ValidateAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = key,
                ClockSkew = _options.ClockSkew
            };
        }

        private List<Claim> CreateClaimsFromCredentials(
            IDictionary<string, object> credentials,
            CredentialValidationResult validationResult)
        {
            var claims = new List<Claim>();

            if (credentials.TryGetValue("username", out var username))
            {
                claims.Add(new Claim(ClaimTypes.Name, username.ToString()!));
                claims.Add(new Claim(ClaimTypes.NameIdentifier, username.ToString()!));
            }

            if (credentials.TryGetValue("email", out var email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email.ToString()!));
            }

            if (!string.IsNullOrEmpty(validationResult.TenantId))
            {
                claims.Add(new Claim("tenant_id", validationResult.TenantId));
            }

            claims.Add(new Claim("token_type", "access"));
            claims.Add(new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

            return claims;
        }

        private Task<CredentialValidationResult> ValidateCredentialsAsync(
            IDictionary<string, object> credentials,
            CancellationToken cancellationToken)
        {
            // This is a placeholder - in a real implementation, you would:
            // 1. Query user store (database, LDAP, etc.)
            // 2. Verify password hash
            // 3. Check if account is enabled
            // 4. Load user roles and permissions

            if (!credentials.TryGetValue("password", out var password) ||
                string.IsNullOrEmpty(password?.ToString()))
            {
                return Task.FromResult(new CredentialValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Password is required"
                });
            }

            // Placeholder validation - always succeeds for demo
            return Task.FromResult(new CredentialValidationResult
            {
                IsValid = true,
                TenantId = credentials.TryGetValue("tenant_id", out var tid) ? tid?.ToString() : null
            });
        }

        private void ValidateOptions()
        {
            if (string.IsNullOrEmpty(_options.SecretKey))
            {
                throw new ArgumentException("SecretKey is required", nameof(_options.SecretKey));
            }

            if (_options.SecretKey.Length < 32)
            {
                throw new ArgumentException("SecretKey must be at least 32 characters", nameof(_options.SecretKey));
            }

            if (string.IsNullOrEmpty(_options.Issuer))
            {
                throw new ArgumentException("Issuer is required", nameof(_options.Issuer));
            }

            if (string.IsNullOrEmpty(_options.Audience))
            {
                throw new ArgumentException("Audience is required", nameof(_options.Audience));
            }
        }

        private bool IsAccountLocked(string username)
        {
            if (!_loginAttempts.TryGetValue(username, out var tracker))
            {
                return false;
            }

            if (tracker.LockedUntil.HasValue && tracker.LockedUntil.Value > DateTime.UtcNow)
            {
                return true;
            }

            // Reset if lock period has expired
            if (tracker.LockedUntil.HasValue && tracker.LockedUntil.Value <= DateTime.UtcNow)
            {
                tracker.FailedAttempts = 0;
                tracker.LockedUntil = null;
            }

            return false;
        }

        private void RecordFailedLoginAttempt(string username)
        {
            var tracker = _loginAttempts.GetOrAdd(username, _ => new LoginAttemptTracker());

            tracker.FailedAttempts++;
            tracker.LastAttemptAt = DateTime.UtcNow;

            if (tracker.FailedAttempts >= _options.MaxLoginAttempts)
            {
                tracker.LockedUntil = DateTime.UtcNow.Add(_options.LockoutDuration);
                _logger?.LogWarning("Account {Username} locked after {Attempts} failed attempts",
                    username, tracker.FailedAttempts);
            }
        }

        private void ResetLoginAttempts(string username)
        {
            _loginAttempts.TryRemove(username, out _);
        }

        private void CleanupExpiredRevokedTokens()
        {
            var now = DateTime.UtcNow;
            var expiredTokens = _revokedTokens
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _revokedTokens.TryRemove(token, out _);
            }
        }

        private class RevokedToken
        {
            public string Token { get; set; } = string.Empty;
            public DateTime RevokedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        private class RefreshTokenInfo
        {
            public string Token { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string? TenantId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public List<string> Roles { get; set; } = new();
        }

        private class LoginAttemptTracker
        {
            public int FailedAttempts { get; set; }
            public DateTime? LastAttemptAt { get; set; }
            public DateTime? LockedUntil { get; set; }
        }

        private class CredentialValidationResult
        {
            public bool IsValid { get; set; }
            public string? ErrorMessage { get; set; }
            public string? TenantId { get; set; }
        }
    }

    /// <summary>
    /// Options for JWT authentication.
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// Gets or sets the secret key for signing tokens.
        /// Must be at least 32 characters.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token issuer.
        /// </summary>
        public string Issuer { get; set; } = "Conduit";

        /// <summary>
        /// Gets or sets the token audience.
        /// </summary>
        public string Audience { get; set; } = "Conduit";

        /// <summary>
        /// Gets or sets the token expiration duration.
        /// </summary>
        public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the refresh token expiration duration.
        /// </summary>
        public TimeSpan RefreshTokenExpiration { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets whether to validate the issuer.
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to validate the audience.
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Gets or sets the clock skew for token validation.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to enable refresh tokens.
        /// </summary>
        public bool EnableRefreshTokens { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to rotate refresh tokens on use.
        /// </summary>
        public bool RotateRefreshTokens { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum login attempts before lockout.
        /// </summary>
        public int MaxLoginAttempts { get; set; } = 5;

        /// <summary>
        /// Gets or sets the account lockout duration.
        /// </summary>
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    }
}
