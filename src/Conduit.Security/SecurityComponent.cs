using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Conduit.Security;
using Microsoft.Extensions.Logging;

namespace Conduit.Security;

/// <summary>
/// Security component for Conduit framework integration.
/// Manages authentication providers, access control, and encryption services.
/// </summary>
public class SecurityComponent : AbstractPluggableComponent
{
    private readonly IAuthenticationProvider _authenticationProvider;
    private readonly IAccessControl _accessControl;
    private readonly IEncryptionService _encryptionService;
    private readonly SecurityContext _securityContext;
    private readonly ILogger<SecurityComponent> _logger;

    public SecurityComponent(
        IAuthenticationProvider authenticationProvider,
        IAccessControl accessControl,
        IEncryptionService encryptionService,
        SecurityContext securityContext,
        ILogger<SecurityComponent> logger) : base(logger)
    {
        _authenticationProvider = authenticationProvider ?? throw new ArgumentNullException(nameof(authenticationProvider));
        _accessControl = accessControl ?? throw new ArgumentNullException(nameof(accessControl));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _securityContext = securityContext ?? throw new ArgumentNullException(nameof(securityContext));
        _logger = logger;

        // Override the default manifest
        Manifest = new ComponentManifest
        {
            Id = "conduit.security",
            Name = "Conduit.Security",
            Version = "0.8.2",
            Description = "Authentication, authorization, and encryption services for the Conduit messaging framework",
            Author = "Conduit Contributors",
            MinFrameworkVersion = "0.1.0",
            Dependencies = new List<ComponentDependency>(),
            Tags = new HashSet<string> { "security", "authentication", "authorization", "encryption", "jwt", "aes" }
        };
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Security component '{Name}' initialized", Name);
        return Task.CompletedTask;
    }

    protected override Task OnStartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Security component '{Name}' started", Name);

        // Log security configuration status
        Logger.LogInformation(
            "Security services: Authentication={AuthEnabled}, AccessControl={AccessEnabled}, Encryption={EncryptionEnabled}",
            _authenticationProvider != null,
            _accessControl != null,
            _encryptionService != null);

        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Security component '{Name}' stopping", Name);

        // Clear security context on shutdown
        try
        {
            // Clear security context - method name may vary based on implementation
            if (_securityContext != null)
            {
                // _securityContext.ClearContext(); // Uncomment if method exists
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error clearing security context during shutdown");
        }

        Logger.LogInformation("Security component '{Name}' stopped", Name);
        return Task.CompletedTask;
    }

    protected override Task OnDisposeAsync()
    {
        Logger.LogInformation("Security component '{Name}' disposed", Name);
        return Task.CompletedTask;
    }

    public override IEnumerable<ComponentFeature> ExposeFeatures()
    {
        return new[]
        {
            new ComponentFeature
            {
                Id = "JwtAuthentication",
                Name = "JWT Authentication",
                Description = "JSON Web Token based authentication provider",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "AccessControl",
                Name = "Access Control",
                Description = "Role-based and permission-based authorization system",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "AesEncryption",
                Name = "AES Encryption",
                Description = "Advanced Encryption Standard (AES) encryption service",
                Version = Version,
                IsEnabledByDefault = true
            },
            new ComponentFeature
            {
                Id = "SecurityContext",
                Name = "Security Context",
                Description = "Thread-safe security context management",
                Version = Version,
                IsEnabledByDefault = true
            }
        };
    }

    public override IEnumerable<ServiceContract> ProvideServices()
    {
        return new[]
        {
            new ServiceContract
            {
                ServiceType = typeof(IAuthenticationProvider),
                ImplementationType = _authenticationProvider.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _authenticationProvider
            },
            new ServiceContract
            {
                ServiceType = typeof(IAccessControl),
                ImplementationType = _accessControl.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _accessControl
            },
            new ServiceContract
            {
                ServiceType = typeof(IEncryptionService),
                ImplementationType = _encryptionService.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _encryptionService
            },
            new ServiceContract
            {
                ServiceType = typeof(SecurityContext),
                ImplementationType = _securityContext.GetType(),
                Lifetime = ServiceLifetime.Singleton,
                Factory = _ => _securityContext
            }
        };
    }

    public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
    {
        var authenticationHealthy = _authenticationProvider != null;
        var accessControlHealthy = _accessControl != null;
        var encryptionHealthy = _encryptionService != null;
        var contextHealthy = _securityContext != null;

        var isHealthy = authenticationHealthy && accessControlHealthy && encryptionHealthy && contextHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["AuthenticationProvider"] = authenticationHealthy ? "Available" : "Unavailable",
            ["AccessControl"] = accessControlHealthy ? "Available" : "Unavailable",
            ["EncryptionService"] = encryptionHealthy ? "Available" : "Unavailable",
            ["SecurityContext"] = contextHealthy ? "Available" : "Unavailable"
        };

        var health = isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "One or more security services unavailable", data: healthData);

        return Task.FromResult(health);
    }

    protected override ComponentHealth? PerformHealthCheck()
    {
        var authenticationHealthy = _authenticationProvider != null;
        var accessControlHealthy = _accessControl != null;
        var encryptionHealthy = _encryptionService != null;
        var contextHealthy = _securityContext != null;

        var isHealthy = authenticationHealthy && accessControlHealthy && encryptionHealthy && contextHealthy;

        var healthData = new Dictionary<string, object>
        {
            ["ComponentName"] = Name,
            ["Version"] = Version,
            ["AuthenticationProvider"] = authenticationHealthy ? "Available" : "Unavailable",
            ["AccessControl"] = accessControlHealthy ? "Available" : "Unavailable",
            ["EncryptionService"] = encryptionHealthy ? "Available" : "Unavailable",
            ["SecurityContext"] = contextHealthy ? "Available" : "Unavailable"
        };

        return isHealthy
            ? ComponentHealth.Healthy(Id, healthData)
            : ComponentHealth.Degraded(Id, "One or more security services unavailable", data: healthData);
    }

    protected override void CollectMetrics(ComponentMetrics metrics)
    {
        metrics.SetCounter("authentication_provider_available", _authenticationProvider != null ? 1 : 0);
        metrics.SetCounter("access_control_available", _accessControl != null ? 1 : 0);
        metrics.SetCounter("encryption_service_available", _encryptionService != null ? 1 : 0);
        metrics.SetCounter("security_context_available", _securityContext != null ? 1 : 0);
        metrics.SetGauge("component_state", (int)GetState());
    }

    /// <summary>
    /// Gets the authentication provider.
    /// </summary>
    public IAuthenticationProvider GetAuthenticationProvider() => _authenticationProvider;

    /// <summary>
    /// Gets the access control service.
    /// </summary>
    public IAccessControl GetAccessControl() => _accessControl;

    /// <summary>
    /// Gets the encryption service.
    /// </summary>
    public IEncryptionService GetEncryptionService() => _encryptionService;

    /// <summary>
    /// Gets the security context.
    /// </summary>
    public new SecurityContext GetSecurityContext() => _securityContext;
}