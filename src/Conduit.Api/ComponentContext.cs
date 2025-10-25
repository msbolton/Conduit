namespace Conduit.Api;

/// <summary>
/// Provides runtime context for components, including access to core services.
/// </summary>
public class ComponentContext
{
    /// <summary>
    /// Initializes a new instance of the ComponentContext class.
    /// </summary>
    public ComponentContext(
        IServiceProvider serviceProvider,
        IMessageBus messageBus,
        ILogger logger,
        IMetricsCollector metricsCollector)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        MessageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        MetricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
    }

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets the message bus for sending commands, publishing events, and querying.
    /// </summary>
    public IMessageBus MessageBus { get; }

    /// <summary>
    /// Gets the logger for this component.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Gets the metrics collector for recording component metrics.
    /// </summary>
    public IMetricsCollector MetricsCollector { get; }

    /// <summary>
    /// Gets the component registry for accessing other components.
    /// </summary>
    public IComponentRegistry? ComponentRegistry { get; set; }

    /// <summary>
    /// Gets the configuration provider.
    /// </summary>
    public IConfigurationProvider? ConfigurationProvider { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token source for the component.
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    /// <summary>
    /// Gets a service from the service provider.
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance</returns>
    public T GetRequiredService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets an optional service from the service provider.
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance or null</returns>
    public T? GetService<T>() where T : class
    {
        return ServiceProvider.GetService<T>();
    }

    /// <summary>
    /// Checks if a feature is enabled.
    /// </summary>
    /// <param name="featureKey">The feature key</param>
    /// <returns>True if the feature is enabled, false otherwise</returns>
    public bool IsFeatureEnabled(string featureKey)
    {
        if (ConfigurationProvider == null)
            return false;

        return ConfigurationProvider.GetValue($"Features:{featureKey}", false);
    }

    /// <summary>
    /// Gets a configuration value.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value if not found</param>
    /// <returns>The configuration value or default</returns>
    public T GetConfiguration<T>(string key, T defaultValue = default!)
    {
        if (ConfigurationProvider == null)
            return defaultValue;

        return ConfigurationProvider.GetValue(key, defaultValue);
    }
}

/// <summary>
/// Interface for accessing services.
/// </summary>
public interface IServiceProvider
{
    /// <summary>
    /// Gets a required service.
    /// </summary>
    T GetRequiredService<T>() where T : notnull;

    /// <summary>
    /// Gets an optional service.
    /// </summary>
    T? GetService<T>() where T : class;
}

/// <summary>
/// Interface for configuration access.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Gets a configuration value.
    /// </summary>
    T GetValue<T>(string key, T defaultValue = default!);
}

/// <summary>
/// Interface for component registry.
/// </summary>
public interface IComponentRegistry
{
    /// <summary>
    /// Gets a component by ID.
    /// </summary>
    IPluggableComponent? GetComponent(string componentId);

    /// <summary>
    /// Gets all registered components.
    /// </summary>
    IEnumerable<IPluggableComponent> GetAllComponents();
}

/// <summary>
/// Interface for logging.
/// </summary>
public interface ILogger
{
    void LogDebug(string message, params object[] args);
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogCritical(Exception exception, string message, params object[] args);
}