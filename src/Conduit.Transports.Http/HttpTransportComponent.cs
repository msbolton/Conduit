using Conduit.Api;
using Conduit.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLifetime = Conduit.Api.ServiceLifetime;

namespace Conduit.Transports.Http;

/// <summary>
/// HTTP transport component for the Conduit framework
/// </summary>
public class HttpTransportComponent : AbstractPluggableComponent
{
    /// <summary>
    /// Initializes a new instance of the HttpTransportComponent class
    /// </summary>
    public HttpTransportComponent(ILogger<HttpTransportComponent> logger) : base(logger)
    {
        Manifest = new ComponentManifest
        {
            Id = "http-transport",
            Name = "HTTP Transport",
            Version = "0.9.0-alpha",
            Description = "HTTP/REST transport implementation for web-based messaging",
            Author = "Conduit Framework",
            MinFrameworkVersion = "0.9.0",
            Dependencies = new List<ComponentDependency>
            {
                new() { Id = "transport-core", MinVersion = "0.9.0" },
                new() { Id = "messaging", MinVersion = "0.9.0" }
            },
            Tags = new HashSet<string> { "transport", "http", "rest", "web", "api" }
        };
    }

    /// <summary>
    /// Registers message handlers for this component
    /// </summary>
    public override IEnumerable<MessageHandlerRegistration> RegisterHandlers()
    {
        // HTTP transport doesn't register specific message handlers
        // It provides transport capabilities for other components
        return Enumerable.Empty<MessageHandlerRegistration>();
    }

    /// <summary>
    /// Provides services for dependency injection
    /// </summary>
    public override IEnumerable<ServiceContract> ProvideServices()
    {
        yield return new ServiceContract
        {
            ServiceType = typeof(HttpTransport),
            ImplementationType = typeof(HttpTransport),
            Lifetime = ServiceLifetime.Singleton,
            Name = "HttpTransport",
            Description = "HTTP/REST transport for messaging"
        };

        yield return new ServiceContract
        {
            ServiceType = typeof(HttpConfiguration),
            ImplementationType = typeof(HttpConfiguration),
            Lifetime = ServiceLifetime.Singleton,
            Name = "HttpConfiguration",
            Description = "Configuration for HTTP transport"
        };
    }

    /// <summary>
    /// Exposes features provided by this component
    /// </summary>
    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        yield return new ComponentFeature
        {
            Id = "HttpMessaging",
            Name = "HTTP/REST Messaging",
            Description = "Send and receive messages over HTTP/REST protocols",
            Version = Version,
            IsEnabledByDefault = false,
            Metadata = new Dictionary<string, object>
            {
                ["BaseUrl"] = "http://localhost:8080",
                ["RequestTimeout"] = "00:00:30",
                ["MaxConcurrentConnections"] = 100,
                ["EnableCompression"] = true,
                ["EnableRetry"] = true,
                ["MaxRetryAttempts"] = 3
            }
        };

        yield return new ComponentFeature
        {
            Id = "HttpServerSupport",
            Name = "HTTP Server Support",
            Description = "Process incoming HTTP requests as messages",
            Version = Version,
            IsEnabledByDefault = false,
            Metadata = new Dictionary<string, object>
            {
                ["EnableServer"] = false,
                ["ServerPort"] = 8080,
                ["EnableHttps"] = false,
                ["MaxRequestSize"] = 1048576 // 1MB
            }
        };

        yield return new ComponentFeature
        {
            Id = "HttpAuthentication",
            Name = "HTTP Authentication",
            Description = "Support for various HTTP authentication methods",
            Version = Version,
            IsEnabledByDefault = false,
            Metadata = new Dictionary<string, object>
            {
                ["AuthenticationType"] = "None",
                ["SupportedTypes"] = new[] { "Bearer", "Basic", "ApiKey" }
            }
        };
    }

    /// <summary>
    /// Contributes behaviors to the message pipeline
    /// </summary>
    public override IEnumerable<IBehaviorContribution> ContributeBehaviors()
    {
        // Future: Could add HTTP-specific behaviors like:
        // - HTTP header injection
        // - Request/response logging
        // - Content type transformation
        return Enumerable.Empty<IBehaviorContribution>();
    }

    /// <summary>
    /// Initializes the HTTP transport component
    /// </summary>
    protected override async Task<bool> OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Initializing HTTP Transport Component v{Version}", Version);

        try
        {
            // Validate HTTP configuration if available
            var configuration = Context?.ServiceProvider?.GetService<HttpConfiguration>();
            if (configuration != null)
            {
                ValidateConfiguration(configuration);
                Logger?.LogInformation("HTTP transport configured with base URL: {BaseUrl}", configuration.BaseUrl);
            }

            Logger?.LogInformation("HTTP Transport Component initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to initialize HTTP Transport Component");
            return false;
        }
    }

    /// <summary>
    /// Starts the HTTP transport component
    /// </summary>
    protected override async Task<bool> OnStartAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Starting HTTP Transport Component");

        try
        {
            // Start HTTP transport if configured
            var transport = Context?.ServiceProvider?.GetService<HttpTransport>();
            if (transport != null)
            {
                await transport.ConnectAsync(cancellationToken);
                Logger?.LogInformation("HTTP transport started successfully");
            }

            Logger?.LogInformation("HTTP Transport Component started successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to start HTTP Transport Component");
            return false;
        }
    }

    /// <summary>
    /// Stops the HTTP transport component
    /// </summary>
    protected override async Task<bool> OnStopAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Stopping HTTP Transport Component");

        try
        {
            var transport = Context?.ServiceProvider?.GetService<HttpTransport>();
            if (transport != null)
            {
                await transport.DisconnectAsync(cancellationToken);
                Logger?.LogInformation("HTTP transport stopped successfully");
            }

            Logger?.LogInformation("HTTP Transport Component stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error stopping HTTP Transport Component");
            return false;
        }
    }

    private static void ValidateConfiguration(HttpConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.BaseUrl))
        {
            throw new InvalidOperationException("HTTP transport BaseUrl cannot be empty");
        }

        if (!Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid HTTP transport BaseUrl: {configuration.BaseUrl}");
        }

        if (configuration.RequestTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("HTTP transport RequestTimeout must be positive");
        }

        if (configuration.MaxConcurrentConnections <= 0)
        {
            throw new InvalidOperationException("HTTP transport MaxConcurrentConnections must be positive");
        }
    }
}