using System;
using System.Collections.Generic;

namespace Conduit.Application
{
    /// <summary>
    /// Conduit application configuration.
    /// </summary>
    public class ConduitConfiguration
    {
        /// <summary>
        /// Gets or sets the application name.
        /// </summary>
        public string ApplicationName { get; set; } = "Conduit Application";

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the environment (Development, Staging, Production).
        /// </summary>
        public string Environment { get; set; } = "Development";

        /// <summary>
        /// Gets or sets component discovery settings.
        /// </summary>
        public ComponentDiscoverySettings ComponentDiscovery { get; set; } = new();

        /// <summary>
        /// Gets or sets messaging settings.
        /// </summary>
        public MessagingSettings Messaging { get; set; } = new();

        /// <summary>
        /// Gets or sets security settings.
        /// </summary>
        public SecuritySettings Security { get; set; } = new();

        /// <summary>
        /// Gets or sets resilience settings.
        /// </summary>
        public ResilienceSettings Resilience { get; set; } = new();

        /// <summary>
        /// Gets or sets feature flags.
        /// </summary>
        public Dictionary<string, bool> Features { get; set; } = new();

        /// <summary>
        /// Gets or sets custom settings.
        /// </summary>
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    /// <summary>
    /// Component discovery settings.
    /// </summary>
    public class ComponentDiscoverySettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to enable component discovery.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the assemblies to scan for components.
        /// </summary>
        public List<string> AssembliesToScan { get; set; } = new();

        /// <summary>
        /// Gets or sets the plugin directories to scan.
        /// </summary>
        public List<string> PluginDirectories { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to enable hot reload.
        /// </summary>
        public bool EnableHotReload { get; set; } = false;

        /// <summary>
        /// Gets or sets the hot reload interval in milliseconds.
        /// </summary>
        public int HotReloadInterval { get; set; } = 5000;
    }

    /// <summary>
    /// Messaging settings.
    /// </summary>
    public class MessagingSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to enable the message bus.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum retry attempts for failed messages.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the dead letter queue size.
        /// </summary>
        public int DeadLetterQueueSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to enable flow control.
        /// </summary>
        public bool EnableFlowControl { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum concurrent messages.
        /// </summary>
        public int MaxConcurrentMessages { get; set; } = 100;
    }

    /// <summary>
    /// Security settings.
    /// </summary>
    public class SecuritySettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to enable authentication.
        /// </summary>
        public bool EnableAuthentication { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to enable authorization.
        /// </summary>
        public bool EnableAuthorization { get; set; } = false;

        /// <summary>
        /// Gets or sets the JWT secret key.
        /// </summary>
        public string? JwtSecretKey { get; set; }

        /// <summary>
        /// Gets or sets the JWT issuer.
        /// </summary>
        public string? JwtIssuer { get; set; }

        /// <summary>
        /// Gets or sets the JWT audience.
        /// </summary>
        public string? JwtAudience { get; set; }

        /// <summary>
        /// Gets or sets the JWT expiration in minutes.
        /// </summary>
        public int JwtExpirationMinutes { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether to enable encryption.
        /// </summary>
        public bool EnableEncryption { get; set; } = false;

        /// <summary>
        /// Gets or sets the encryption key.
        /// </summary>
        public string? EncryptionKey { get; set; }
    }

    /// <summary>
    /// Resilience settings.
    /// </summary>
    public class ResilienceSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to enable circuit breakers.
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Gets or sets the circuit breaker failure threshold.
        /// </summary>
        public int CircuitBreakerFailureThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the circuit breaker timeout in milliseconds.
        /// </summary>
        public int CircuitBreakerTimeoutMs { get; set; } = 60000;

        /// <summary>
        /// Gets or sets a value indicating whether to enable retry policies.
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// Gets or sets the default retry count.
        /// </summary>
        public int DefaultRetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets a value indicating whether to enable timeouts.
        /// </summary>
        public bool EnableTimeout { get; set; } = true;

        /// <summary>
        /// Gets or sets the default timeout in milliseconds.
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 30000;
    }
}
