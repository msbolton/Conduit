namespace Conduit.Api;

/// <summary>
/// Interface for pluggable components that can contribute behaviors to the pipeline,
/// expose features, and provide services to the system.
/// </summary>
/// <remarks>
/// Pluggable components are the foundation of the Conduit component system with:
/// - Lifecycle management (attach/detach)
/// - Dynamically contribute behaviors to the message pipeline
/// - Expose discoverable features that can be activated
/// - Provide services through well-defined contracts
/// - Register message handlers for domain operations
/// </remarks>
public interface IPluggableComponent
{
    /// <summary>
    /// Gets the unique identifier for this component instance.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the name of this component.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of this component.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the description of this component.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets or sets the configuration for this component.
    /// </summary>
    ComponentConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the security context for this component.
    /// </summary>
    ISecurityContext? SecurityContext { get; set; }

    /// <summary>
    /// Called when the component is attached to the component host.
    /// This is where the component should initialize its resources and
    /// subscribe to necessary events.
    /// </summary>
    /// <param name="context">The component context providing access to core services</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnAttachAsync(ComponentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the component is being detached from the component host.
    /// This is where the component should clean up resources and unsubscribe
    /// from events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnDetachAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Contributes behaviors to the message processing pipeline.
    /// These behaviors will be dynamically inserted into the pipeline
    /// based on their placement rules.
    /// </summary>
    /// <returns>Array of behavior contributions, or empty array if none</returns>
    IEnumerable<IBehaviorContribution> ContributeBehaviors();

    /// <summary>
    /// Exposes features that this component provides.
    /// Features are capabilities that can be discovered and activated
    /// by other components or the system.
    /// </summary>
    /// <returns>Array of component features, or empty array if none</returns>
    IEnumerable<ComponentFeature> ExposeFeatures();

    /// <summary>
    /// Provides service contracts that this component implements.
    /// These services can be discovered and used by other components
    /// through the service locator.
    /// </summary>
    /// <returns>Array of service contracts, or empty array if none</returns>
    IEnumerable<ServiceContract> ProvideServices();

    /// <summary>
    /// Registers message handlers for commands, events, and queries.
    /// This allows the component to participate in the domain operations.
    /// </summary>
    /// <returns>Array of message handler registrations, or empty array if none</returns>
    IEnumerable<MessageHandlerRegistration> RegisterHandlers();

    /// <summary>
    /// Gets the component manifest containing metadata about this component.
    /// </summary>
    ComponentManifest Manifest { get; }

    /// <summary>
    /// Validates that this component is compatible with the given core version.
    /// </summary>
    /// <param name="coreVersion">The version of the Conduit Framework Core</param>
    /// <returns>true if compatible, false otherwise</returns>
    bool IsCompatibleWith(string coreVersion);

    /// <summary>
    /// Gets the isolation requirements for this component.
    /// Components can specify their isolation level for security and stability.
    /// </summary>
    IsolationRequirements IsolationRequirements { get; }

    /// <summary>
    /// Gets the current state of this component.
    /// </summary>
    /// <returns>The current component state</returns>
    ComponentState GetState();

    /// <summary>
    /// Starts the component asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the start operation</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the component asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the stop operation</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the component with configuration.
    /// </summary>
    /// <param name="configuration">Component configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the initialization</returns>
    Task InitializeAsync(ComponentConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches the component from its context.
    /// </summary>
    void OnDetach();

    /// <summary>
    /// Disposes the component asynchronously.
    /// </summary>
    /// <returns>Task representing the disposal</returns>
    Task DisposeAsync();

    /// <summary>
    /// Checks the health of the component.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Component health information</returns>
    Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disposes the component synchronously.
    /// </summary>
    void Dispose();
}