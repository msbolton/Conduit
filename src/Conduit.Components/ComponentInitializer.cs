using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Core;
using Conduit.Core.Discovery;
using Microsoft.Extensions.Logging;

namespace Conduit.Components
{
    /// <summary>
    /// Handles component initialization with dependency resolution and ordering.
    /// </summary>
    public class ComponentInitializer
    {
        private readonly DependencyResolver _dependencyResolver;
        private readonly ILogger<ComponentInitializer> _logger;

        public ComponentInitializer(ILogger<ComponentInitializer>? logger = null)
        {
            _dependencyResolver = new DependencyResolver();
            _logger = logger ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<ComponentInitializer>();
        }

        /// <summary>
        /// Initializes a collection of components in dependency order.
        /// </summary>
        public async Task<ComponentInitializationResult> InitializeComponentsAsync(
            IEnumerable<IPluggableComponent> components,
            IDictionary<string, ComponentConfiguration>? configurations = null,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(components, nameof(components));

            var componentList = components.ToList();
            var result = new ComponentInitializationResult();

            _logger.LogInformation("Initializing {Count} components", componentList.Count);

            // Build dependency graph
            var descriptors = componentList
                .Select(c => ComponentDescriptor.FromComponent(c))
                .ToList();

            try
            {
                // Resolve initialization order
                var resolutionResult = _dependencyResolver.Resolve(descriptors);
                if (!resolutionResult.Success)
                {
                    throw new InvalidOperationException($"Failed to resolve dependencies: {resolutionResult.ErrorMessage}");
                }
                var initializationOrder = resolutionResult.OrderedComponents;

                _logger.LogDebug("Resolved initialization order: {Order}",
                    string.Join(" -> ", initializationOrder.Select(d => d.Id)));

                // Initialize components in order
                foreach (var descriptor in initializationOrder)
                {
                    var component = componentList.First(c => c.Id == descriptor.Id);
                    var config = configurations?.TryGetValue(descriptor.Id, out var c) == true
                        ? c
                        : new ComponentConfiguration();

                    try
                    {
                        await component.InitializeAsync(config, cancellationToken);
                        result.SuccessfulComponents.Add(component.Id);
                        _logger.LogInformation("Initialized component: {ComponentId}", component.Id);
                    }
                    catch (Exception ex)
                    {
                        result.FailedComponents.Add(component.Id);
                        result.Errors.Add(new ComponentInitializationError
                        {
                            ComponentId = component.Id,
                            Exception = ex,
                            Message = $"Failed to initialize component {component.Id}: {ex.Message}"
                        });

                        _logger.LogError(ex, "Failed to initialize component: {ComponentId}", component.Id);

                        // Decide whether to continue or stop on first failure
                        if (!result.Options.ContinueOnError)
                        {
                            throw new ComponentInitializationException(
                                $"Component initialization failed for {component.Id}", ex);
                        }
                    }
                }

                result.Success = result.FailedComponents.Count == 0;
            }
            catch (CircularDependencyException ex)
            {
                result.Success = false;
                result.Errors.Add(new ComponentInitializationError
                {
                    Message = ex.Message,
                    Exception = ex
                });

                _logger.LogError(ex, "Circular dependency detected in components");
                throw;
            }

            _logger.LogInformation(
                "Component initialization completed: {Successful} successful, {Failed} failed",
                result.SuccessfulComponents.Count, result.FailedComponents.Count);

            return result;
        }

        /// <summary>
        /// Starts a collection of components in dependency order.
        /// </summary>
        public async Task<ComponentStartResult> StartComponentsAsync(
            IEnumerable<IPluggableComponent> components,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(components, nameof(components));

            var componentList = components.ToList();
            var result = new ComponentStartResult();

            _logger.LogInformation("Starting {Count} components", componentList.Count);

            // Build dependency graph
            var descriptors = componentList
                .Select(c => ComponentDescriptor.FromComponent(c))
                .ToList();

            // Resolve start order (same as initialization order)
            var startOrderResult = _dependencyResolver.Resolve(descriptors);
            if (!startOrderResult.Success)
            {
                throw new InvalidOperationException($"Failed to resolve dependencies: {startOrderResult.ErrorMessage}");
            }
            var startOrder = startOrderResult.OrderedComponents;

            // Start components in order
            foreach (var descriptor in startOrder)
            {
                var component = componentList.First(c => c.Id == descriptor.Id);

                try
                {
                    await component.StartAsync(cancellationToken);
                    result.SuccessfulComponents.Add(component.Id);
                    _logger.LogInformation("Started component: {ComponentId}", component.Id);
                }
                catch (Exception ex)
                {
                    result.FailedComponents.Add(component.Id);
                    result.Errors.Add(new ComponentStartError
                    {
                        ComponentId = component.Id,
                        Exception = ex,
                        Message = $"Failed to start component {component.Id}: {ex.Message}"
                    });

                    _logger.LogError(ex, "Failed to start component: {ComponentId}", component.Id);

                    if (!result.Options.ContinueOnError)
                    {
                        throw new ComponentStartException(
                            $"Component start failed for {component.Id}", ex);
                    }
                }
            }

            result.Success = result.FailedComponents.Count == 0;

            _logger.LogInformation(
                "Component start completed: {Successful} successful, {Failed} failed",
                result.SuccessfulComponents.Count, result.FailedComponents.Count);

            return result;
        }

        /// <summary>
        /// Stops a collection of components in reverse dependency order.
        /// </summary>
        public async Task<ComponentStopResult> StopComponentsAsync(
            IEnumerable<IPluggableComponent> components,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(components, nameof(components));

            var componentList = components.ToList();
            var result = new ComponentStopResult();

            _logger.LogInformation("Stopping {Count} components", componentList.Count);

            // Build dependency graph
            var descriptors = componentList
                .Select(c => ComponentDescriptor.FromComponent(c))
                .ToList();

            // Resolve stop order (reverse of initialization order)
            var stopOrderResult = _dependencyResolver.Resolve(descriptors);
            if (!stopOrderResult.Success)
            {
                throw new InvalidOperationException($"Failed to resolve dependencies: {stopOrderResult.ErrorMessage}");
            }
            var stopOrder = stopOrderResult.OrderedComponents.Reverse();

            // Stop components in reverse order
            foreach (var descriptor in stopOrder)
            {
                var component = componentList.First(c => c.Id == descriptor.Id);

                try
                {
                    await component.StopAsync(cancellationToken);
                    result.SuccessfulComponents.Add(component.Id);
                    _logger.LogInformation("Stopped component: {ComponentId}", component.Id);
                }
                catch (Exception ex)
                {
                    result.FailedComponents.Add(component.Id);
                    result.Errors.Add(new ComponentStopError
                    {
                        ComponentId = component.Id,
                        Exception = ex,
                        Message = $"Failed to stop component {component.Id}: {ex.Message}"
                    });

                    _logger.LogWarning(ex, "Failed to stop component: {ComponentId}", component.Id);
                    // Continue stopping other components even on failure
                }
            }

            result.Success = result.FailedComponents.Count == 0;

            _logger.LogInformation(
                "Component stop completed: {Successful} successful, {Failed} failed",
                result.SuccessfulComponents.Count, result.FailedComponents.Count);

            return result;
        }
    }

    /// <summary>
    /// Options for component initialization.
    /// </summary>
    public class ComponentInitializationOptions
    {
        public bool ContinueOnError { get; set; } = false;
        public TimeSpan? Timeout { get; set; }
        public bool ValidateDependencies { get; set; } = true;
    }

    /// <summary>
    /// Result of component initialization.
    /// </summary>
    public class ComponentInitializationResult
    {
        public bool Success { get; set; }
        public List<string> SuccessfulComponents { get; } = new();
        public List<string> FailedComponents { get; } = new();
        public List<ComponentInitializationError> Errors { get; } = new();
        public ComponentInitializationOptions Options { get; set; } = new();
    }

    /// <summary>
    /// Error during component initialization.
    /// </summary>
    public class ComponentInitializationError
    {
        public string? ComponentId { get; set; }
        public string Message { get; set; } = "";
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Result of component start.
    /// </summary>
    public class ComponentStartResult
    {
        public bool Success { get; set; }
        public List<string> SuccessfulComponents { get; } = new();
        public List<string> FailedComponents { get; } = new();
        public List<ComponentStartError> Errors { get; } = new();
        public ComponentStartOptions Options { get; set; } = new();
    }

    /// <summary>
    /// Error during component start.
    /// </summary>
    public class ComponentStartError
    {
        public string? ComponentId { get; set; }
        public string Message { get; set; } = "";
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Options for component start.
    /// </summary>
    public class ComponentStartOptions
    {
        public bool ContinueOnError { get; set; } = false;
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Result of component stop.
    /// </summary>
    public class ComponentStopResult
    {
        public bool Success { get; set; }
        public List<string> SuccessfulComponents { get; } = new();
        public List<string> FailedComponents { get; } = new();
        public List<ComponentStopError> Errors { get; } = new();
    }

    /// <summary>
    /// Error during component stop.
    /// </summary>
    public class ComponentStopError
    {
        public string? ComponentId { get; set; }
        public string Message { get; set; } = "";
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Exception thrown when component initialization fails.
    /// </summary>
    public class ComponentInitializationException : Exception
    {
        public ComponentInitializationException(string message) : base(message) { }
        public ComponentInitializationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when component start fails.
    /// </summary>
    public class ComponentStartException : Exception
    {
        public ComponentStartException(string message) : base(message) { }
        public ComponentStartException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}