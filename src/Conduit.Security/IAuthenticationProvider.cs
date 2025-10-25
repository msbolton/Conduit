using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Security
{
    /// <summary>
    /// Interface for authentication providers.
    /// Handles user authentication, token validation, and token lifecycle management.
    /// </summary>
    public interface IAuthenticationProvider
    {
        /// <summary>
        /// Gets the name of this authentication provider.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Authenticates a user with the provided credentials.
        /// </summary>
        /// <param name="credentials">Authentication credentials (username/password, API key, etc.)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Authentication result containing security context and token</returns>
        Task<AuthenticationResult> AuthenticateAsync(
            IDictionary<string, object> credentials,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates an authentication token.
        /// </summary>
        /// <param name="token">The token to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result with security context if valid</returns>
        Task<TokenValidationResult> ValidateTokenAsync(
            string token,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes an authentication token using a refresh token.
        /// </summary>
        /// <param name="refreshToken">The refresh token</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>New authentication result with refreshed tokens</returns>
        Task<AuthenticationResult> RefreshTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes a token, making it invalid for future use.
        /// </summary>
        /// <param name="token">The token to revoke</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RevokeTokenAsync(
            string token,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a token is revoked.
        /// </summary>
        /// <param name="token">The token to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the token is revoked</returns>
        Task<bool> IsTokenRevokedAsync(
            string token,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a security context from a ClaimsPrincipal.
        /// </summary>
        /// <param name="principal">The claims principal</param>
        /// <param name="token">Optional authentication token</param>
        /// <param name="tenantId">Optional tenant identifier</param>
        /// <returns>Security context</returns>
        SecurityContext CreateSecurityContext(
            ClaimsPrincipal principal,
            string? token = null,
            string? tenantId = null);
    }

    /// <summary>
    /// Result of an authentication operation.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Gets whether authentication was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the security context (only if successful).
        /// </summary>
        public SecurityContext? SecurityContext { get; set; }

        /// <summary>
        /// Gets the authentication token (only if successful).
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Gets the refresh token (only if successful).
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Gets when the token expires (UTC).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets the error message (only if failed).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets the error code (only if failed).
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets additional metadata about the authentication.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Creates a successful authentication result.
        /// </summary>
        public static AuthenticationResult Succeeded(
            SecurityContext context,
            string token,
            string? refreshToken = null,
            DateTime? expiresAt = null)
        {
            return new AuthenticationResult
            {
                Success = true,
                SecurityContext = context,
                Token = token,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// Creates a failed authentication result.
        /// </summary>
        public static AuthenticationResult Failed(string errorMessage, string? errorCode = null)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }

    /// <summary>
    /// Result of a token validation operation.
    /// </summary>
    public class TokenValidationResult
    {
        /// <summary>
        /// Gets whether the token is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the security context (only if valid).
        /// </summary>
        public SecurityContext? SecurityContext { get; set; }

        /// <summary>
        /// Gets the claims principal (only if valid).
        /// </summary>
        public ClaimsPrincipal? Principal { get; set; }

        /// <summary>
        /// Gets when the token expires (UTC).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets the validation error message (only if invalid).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets the validation error code (only if invalid).
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets additional validation metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static TokenValidationResult Valid(
            ClaimsPrincipal principal,
            SecurityContext context,
            DateTime? expiresAt = null)
        {
            return new TokenValidationResult
            {
                IsValid = true,
                Principal = principal,
                SecurityContext = context,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// Creates a failed validation result.
        /// </summary>
        public static TokenValidationResult Invalid(string errorMessage, string? errorCode = null)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }

    /// <summary>
    /// Authentication options.
    /// </summary>
    public class AuthenticationOptions
    {
        /// <summary>
        /// Gets or sets the token expiration duration.
        /// </summary>
        public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the refresh token expiration duration.
        /// </summary>
        public TimeSpan RefreshTokenExpiration { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets whether to allow concurrent sessions.
        /// </summary>
        public bool AllowConcurrentSessions { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to require strong passwords.
        /// </summary>
        public bool RequireStrongPasswords { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum login attempts before lockout.
        /// </summary>
        public int MaxLoginAttempts { get; set; } = 5;

        /// <summary>
        /// Gets or sets the account lockout duration.
        /// </summary>
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets whether to enable token refresh.
        /// </summary>
        public bool EnableTokenRefresh { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable remember me functionality.
        /// </summary>
        public bool EnableRememberMe { get; set; } = true;

        /// <summary>
        /// Gets or sets the clock skew tolerance for token validation.
        /// </summary>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Common authentication error codes.
    /// </summary>
    public static class AuthenticationErrorCodes
    {
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string AccountLocked = "ACCOUNT_LOCKED";
        public const string AccountDisabled = "ACCOUNT_DISABLED";
        public const string TokenExpired = "TOKEN_EXPIRED";
        public const string TokenRevoked = "TOKEN_REVOKED";
        public const string TokenInvalid = "TOKEN_INVALID";
        public const string RefreshTokenExpired = "REFRESH_TOKEN_EXPIRED";
        public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
        public const string ProviderNotSupported = "PROVIDER_NOT_SUPPORTED";
        public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";
    }
}
