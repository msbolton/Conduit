using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Transports.Core;
using Microsoft.Extensions.Logging;

namespace Conduit.Gateway
{
    /// <summary>
    /// Registry for managing transport implementations and their lifecycle.
    /// </summary>
    public class TransportRegistry : IDisposable
    {
        private readonly ConcurrentDictionary<TransportType, ITransport> _transports;
        private readonly ConcurrentDictionary<string, ITransport> _transportsByName;
        private readonly ConcurrentDictionary<TransportType, TransportRegistration> _registrations;
        private readonly ILogger<TransportRegistry>? _logger;
        private readonly ReaderWriterLockSlim _lock;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TransportRegistry class.
        /// </summary>
        /// <param name="logger">Optional logger instance</param>
        public TransportRegistry(ILogger<TransportRegistry>? logger = null)
        {
            _transports = new ConcurrentDictionary<TransportType, ITransport>();
            _transportsByName = new ConcurrentDictionary<string, ITransport>();
            _registrations = new ConcurrentDictionary<TransportType, TransportRegistration>();
            _logger = logger;
            _lock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// Gets the number of registered transports.
        /// </summary>
        public int Count => _transports.Count;

        /// <summary>
        /// Registers a transport implementation.
        /// </summary>
        /// <param name="transport">The transport to register</param>
        /// <param name="mode">The transport mode</param>
        /// <param name="description">Optional description</param>
        /// <exception cref="ArgumentNullException">Thrown when transport is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when transport type is already registered</exception>
        public void RegisterTransport(ITransport transport, TransportMode mode, string? description = null)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));

            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                if (_transports.ContainsKey(transport.Type))
                    throw new InvalidOperationException($"Transport type {transport.Type} is already registered");

                var registration = new TransportRegistration
                {
                    Transport = transport,
                    Mode = mode,
                    Description = description,
                    RegisteredTime = DateTime.UtcNow
                };

                _transports.TryAdd(transport.Type, transport);
                _transportsByName.TryAdd(transport.Name, transport);
                _registrations.TryAdd(transport.Type, registration);

                _logger?.LogInformation("Registered transport: {TransportType} ({TransportName}) - Mode: {Mode}",
                    transport.Type, transport.Name, mode);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Unregisters a transport implementation.
        /// </summary>
        /// <param name="transportType">The transport type to unregister</param>
        /// <returns>True if the transport was unregistered, false if it wasn't found</returns>
        public bool UnregisterTransport(TransportType transportType)
        {
            ThrowIfDisposed();

            _lock.EnterWriteLock();
            try
            {
                if (_transports.TryRemove(transportType, out var transport))
                {
                    _transportsByName.TryRemove(transport.Name, out _);
                    _registrations.TryRemove(transportType, out _);

                    _logger?.LogInformation("Unregistered transport: {TransportType} ({TransportName})",
                        transportType, transport.Name);

                    return true;
                }

                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets a transport by type.
        /// </summary>
        /// <param name="transportType">The transport type</param>
        /// <returns>The transport if found, null otherwise</returns>
        public ITransport? GetTransport(TransportType transportType)
        {
            return _transports.TryGetValue(transportType, out var transport) ? transport : null;
        }

        /// <summary>
        /// Gets a transport by name.
        /// </summary>
        /// <param name="transportName">The transport name</param>
        /// <returns>The transport if found, null otherwise</returns>
        public ITransport? GetTransport(string transportName)
        {
            if (string.IsNullOrEmpty(transportName))
                return null;

            return _transportsByName.TryGetValue(transportName, out var transport) ? transport : null;
        }

        /// <summary>
        /// Gets the registration information for a transport.
        /// </summary>
        /// <param name="transportType">The transport type</param>
        /// <returns>The registration information if found, null otherwise</returns>
        public TransportRegistration? GetRegistration(TransportType transportType)
        {
            return _registrations.TryGetValue(transportType, out var registration) ? registration : null;
        }

        /// <summary>
        /// Gets all registered transports.
        /// </summary>
        /// <returns>List of all registered transports</returns>
        public List<ITransport> GetAllTransports()
        {
            _lock.EnterReadLock();
            try
            {
                return _transports.Values.ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all registered transports of a specific mode.
        /// </summary>
        /// <param name="mode">The transport mode to filter by</param>
        /// <returns>List of transports with the specified mode</returns>
        public List<ITransport> GetTransportsByMode(TransportMode mode)
        {
            _lock.EnterReadLock();
            try
            {
                return _registrations.Values
                    .Where(r => r.Mode == mode || r.Mode == TransportMode.Proxy)
                    .Select(r => r.Transport)
                    .ToList();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all server transports (transports that accept connections).
        /// </summary>
        /// <returns>List of server transports</returns>
        public List<ITransport> GetServerTransports()
        {
            return GetTransportsByMode(TransportMode.Server);
        }

        /// <summary>
        /// Gets all client transports (transports that initiate connections).
        /// </summary>
        /// <returns>List of client transports</returns>
        public List<ITransport> GetClientTransports()
        {
            return GetTransportsByMode(TransportMode.Client);
        }

        /// <summary>
        /// Checks if a transport type is registered.
        /// </summary>
        /// <param name="transportType">The transport type to check</param>
        /// <returns>True if the transport is registered, false otherwise</returns>
        public bool IsRegistered(TransportType transportType)
        {
            return _transports.ContainsKey(transportType);
        }

        /// <summary>
        /// Starts all registered transports.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when all transports are started</returns>
        public async Task StartAllTransportsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var transports = GetAllTransports();
            var startTasks = new List<Task>();

            foreach (var transport in transports)
            {
                startTasks.Add(StartTransportAsync(transport, cancellationToken));
            }

            await Task.WhenAll(startTasks);
            _logger?.LogInformation("Started {Count} transports", transports.Count);
        }

        /// <summary>
        /// Stops all registered transports.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when all transports are stopped</returns>
        public async Task StopAllTransportsAsync(CancellationToken cancellationToken = default)
        {
            var transports = GetAllTransports();
            var stopTasks = new List<Task>();

            foreach (var transport in transports)
            {
                stopTasks.Add(StopTransportAsync(transport, cancellationToken));
            }

            await Task.WhenAll(stopTasks);
            _logger?.LogInformation("Stopped {Count} transports", transports.Count);
        }

        /// <summary>
        /// Gets health information for all registered transports.
        /// </summary>
        /// <returns>Transport health information</returns>
        public Dictionary<Transports.Core.TransportType, TransportHealth> GetTransportHealth()
        {
            _lock.EnterReadLock();
            try
            {
                var health = new Dictionary<Transports.Core.TransportType, TransportHealth>();

                foreach (var kvp in _registrations)
                {
                    var transport = kvp.Value.Transport;
                    var statistics = transport.GetStatistics();

                    health[kvp.Key] = new TransportHealth
                    {
                        TransportType = transport.Type,
                        TransportName = transport.Name,
                        IsConnected = transport.IsConnected,
                        Statistics = statistics,
                        Mode = kvp.Value.Mode,
                        Description = kvp.Value.Description,
                        RegisteredTime = kvp.Value.RegisteredTime
                    };
                }

                return health;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Starts a specific transport.
        /// </summary>
        private async Task StartTransportAsync(ITransport transport, CancellationToken cancellationToken)
        {
            try
            {
                if (!transport.IsConnected)
                {
                    await transport.ConnectAsync(cancellationToken);
                    _logger?.LogDebug("Started transport: {TransportType} ({TransportName})",
                        transport.Type, transport.Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start transport: {TransportType} ({TransportName})",
                    transport.Type, transport.Name);
                throw;
            }
        }

        /// <summary>
        /// Stops a specific transport.
        /// </summary>
        private async Task StopTransportAsync(ITransport transport, CancellationToken cancellationToken)
        {
            try
            {
                if (transport.IsConnected)
                {
                    await transport.DisconnectAsync(cancellationToken);
                    _logger?.LogDebug("Stopped transport: {TransportType} ({TransportName})",
                        transport.Type, transport.Name);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping transport: {TransportType} ({TransportName})",
                    transport.Type, transport.Name);
            }
        }

        /// <summary>
        /// Throws an exception if the registry has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransportRegistry));
        }

        /// <summary>
        /// Disposes the transport registry and all registered transports.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Stop all transports
            try
            {
                StopAllTransportsAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping transports during disposal");
            }

            // Dispose all transports
            foreach (var transport in _transports.Values)
            {
                try
                {
                    transport.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing transport: {TransportType}",
                        transport.Type);
                }
            }

            _lock.Dispose();
            _logger?.LogInformation("Transport registry disposed");
        }
    }

    /// <summary>
    /// Registration information for a transport.
    /// </summary>
    public class TransportRegistration
    {
        /// <summary>
        /// Gets or sets the transport instance.
        /// </summary>
        public ITransport Transport { get; set; } = null!;

        /// <summary>
        /// Gets or sets the transport mode.
        /// </summary>
        public TransportMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the transport description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets when the transport was registered.
        /// </summary>
        public DateTime RegisteredTime { get; set; }
    }

    /// <summary>
    /// Health information for a transport.
    /// </summary>
    public class TransportHealth
    {
        /// <summary>
        /// Gets or sets the transport type.
        /// </summary>
        public Transports.Core.TransportType TransportType { get; set; }

        /// <summary>
        /// Gets or sets the transport name.
        /// </summary>
        public string TransportName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the transport is connected.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Gets or sets the transport statistics.
        /// </summary>
        public TransportStatistics? Statistics { get; set; }

        /// <summary>
        /// Gets or sets the transport mode.
        /// </summary>
        public TransportMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the transport description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets when the transport was registered.
        /// </summary>
        public DateTime RegisteredTime { get; set; }
    }
}