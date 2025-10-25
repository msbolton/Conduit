namespace Conduit.Api;

/// <summary>
/// Represents a service contract provided by a component.
/// </summary>
public class ServiceContract
{
    /// <summary>
    /// Gets or sets the service interface type.
    /// </summary>
    public Type ServiceType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the implementation type.
    /// </summary>
    public Type ImplementationType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the service lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets or sets the service name for named registrations.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the service version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the service description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a factory function for creating the service instance.
    /// </summary>
    public Func<IServiceProvider, object>? Factory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this service replaces existing registrations.
    /// </summary>
    public bool ReplaceExisting { get; set; }

    /// <summary>
    /// Gets or sets the service priority for ordering multiple implementations.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets tags for service categorization.
    /// </summary>
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a service contract for singleton lifetime.
    /// </summary>
    public static ServiceContract Singleton<TService, TImplementation>()
        where TImplementation : TService
    {
        return new ServiceContract
        {
            ServiceType = typeof(TService),
            ImplementationType = typeof(TImplementation),
            Lifetime = ServiceLifetime.Singleton
        };
    }

    /// <summary>
    /// Creates a service contract for scoped lifetime.
    /// </summary>
    public static ServiceContract Scoped<TService, TImplementation>()
        where TImplementation : TService
    {
        return new ServiceContract
        {
            ServiceType = typeof(TService),
            ImplementationType = typeof(TImplementation),
            Lifetime = ServiceLifetime.Scoped
        };
    }

    /// <summary>
    /// Creates a service contract for transient lifetime.
    /// </summary>
    public static ServiceContract Transient<TService, TImplementation>()
        where TImplementation : TService
    {
        return new ServiceContract
        {
            ServiceType = typeof(TService),
            ImplementationType = typeof(TImplementation),
            Lifetime = ServiceLifetime.Transient
        };
    }
}

/// <summary>
/// Defines the lifetime of a service.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>
    /// A single instance is created and shared.
    /// </summary>
    Singleton,

    /// <summary>
    /// A new instance is created for each scope.
    /// </summary>
    Scoped,

    /// <summary>
    /// A new instance is created each time.
    /// </summary>
    Transient
}