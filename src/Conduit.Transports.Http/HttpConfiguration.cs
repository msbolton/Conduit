using Conduit.Transports.Core;

namespace Conduit.Transports.Http;

/// <summary>
/// Configuration settings for HTTP transport
/// </summary>
public class HttpConfiguration : TransportConfiguration
{
    /// <summary>
    /// Base URL for HTTP endpoints
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Timeout for HTTP requests
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of concurrent connections
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 100;

    /// <summary>
    /// Whether to use HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// HTTP headers to include in requests
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();

    /// <summary>
    /// Authentication configuration
    /// </summary>
    public HttpAuthenticationConfig? Authentication { get; set; }

    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public HttpRetryConfig Retry { get; set; } = new();

    /// <summary>
    /// Compression settings
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Buffer size for HTTP operations
    /// </summary>
    public int BufferSize { get; set; } = 8192;
}

/// <summary>
/// HTTP authentication configuration
/// </summary>
public class HttpAuthenticationConfig
{
    /// <summary>
    /// Authentication type (Bearer, Basic, ApiKey, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Authentication token or key
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Username for basic authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for basic authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// API key header name
    /// </summary>
    public string? ApiKeyHeader { get; set; }
}

/// <summary>
/// HTTP retry policy configuration
/// </summary>
public class HttpRetryConfig
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether to use exponential backoff
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// HTTP status codes that should trigger a retry
    /// </summary>
    public HashSet<int> RetryableStatusCodes { get; set; } = new()
    {
        408, // Request Timeout
        429, // Too Many Requests
        500, // Internal Server Error
        502, // Bad Gateway
        503, // Service Unavailable
        504  // Gateway Timeout
    };
}